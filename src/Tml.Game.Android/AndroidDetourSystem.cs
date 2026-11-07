using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using MonoMod.Core.Platforms;
using MonoMod.Core.Platforms.Memory;
using MonoMod.Utils;

namespace Tml.Mobile.Android;

// Bionic/Android implementation for the pinned MonoMod 1.3.4 public ISystem API.
// Keep libc selectors, desktop exception-helper binaries and /tmp out of this path.
internal sealed class AndroidDetourSystem : ISystem
{
    internal readonly record struct Mapping(long Start, long End, int Protection, long Offset, string Path);
    internal static IEnumerable<Mapping> Maps()
    {
        foreach (string line in File.ReadLines("/proc/self/maps"))
        {
            string[] fields = line.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
            string[] bounds = fields[0].Split('-');
            int protection = (fields[1][0] == 'r' ? 1 : 0) | (fields[1][1] == 'w' ? 2 : 0) | (fields[1][2] == 'x' ? 4 : 0);
            yield return new(long.Parse(bounds[0], NumberStyles.HexNumber), long.Parse(bounds[1], NumberStyles.HexNumber),
                protection, long.Parse(fields[2], NumberStyles.HexNumber), fields.Length == 6 ? fields[5] : "");
        }
    }

    public OSKind Target => OSKind.Android;
    public SystemFeature Features => SystemFeature.RWXPages | SystemFeature.RXPages;
    public Abi? DefaultAbi { get; }
    public IMemoryAllocator MemoryAllocator { get; } = new AndroidAllocator();
    public INativeExceptionHelper? NativeExceptionHelper => null;

    internal AndroidDetourSystem()
    {
        bool arm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        if (!arm && RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("The Android detour host requires ARM64 or x86_64");
        Type classifierType = typeof(PlatformTriple).Assembly.GetType("MonoMod.Core.Platforms.Systems.SystemVABI", true)!;
        var classifier = classifierType.GetMethod(arm ? "ClassifyARM64" : "ClassifyAMD64", BindingFlags.Public | BindingFlags.Static)!
            .CreateDelegate<Classifier>();
        DefaultAbi = new Abi(arm
            ? new[] { SpecialArgumentKind.ThisPointer, SpecialArgumentKind.UserArguments }
            : new[] { SpecialArgumentKind.ReturnBuffer, SpecialArgumentKind.ThisPointer, SpecialArgumentKind.UserArguments }, classifier, !arm);
    }

    public IEnumerable<LoadedModule> EnumerateLoadedModules() => Maps().Where(m => m.Path.StartsWith('/'))
        .GroupBy(m => (m.Path, Base: m.Start - m.Offset))
        .Select(g => new LoadedModule((ulong)g.Key.Base, g.Key.Path, (ulong)(g.Max(m => m.End) - g.Key.Base)));
    public IEnumerable<string?> EnumerateLoadedModuleFiles() => EnumerateLoadedModules().Select(m => m.FileName);
    public nint GetSizeOfReadableMemory(nint start, nint guess)
    {
        if (guess <= 0) return 0;
        long cursor = start;
        long wanted = checked(cursor + (long)guess);
        foreach (var map in Maps())
        {
            if (map.End <= cursor) continue;
            if (map.Start > cursor || (map.Protection & 1) == 0) break;
            cursor = map.End;
            if (cursor >= wanted) break;
        }
        return (nint)Math.Min(Math.Max(0, cursor - (long)start), (long)guess);
    }

    public unsafe void PatchData(PatchTargetKind kind, nint target, ReadOnlySpan<byte> data, Span<byte> backup)
    {
        if (data.IsEmpty) return;
        if (GetSizeOfReadableMemory(target, data.Length) < data.Length)
            throw new InvalidOperationException("Detour patch crosses unreadable memory");
        long pageSize = Environment.SystemPageSize;
        long first = (long)target & -pageSize;
        long end = ((long)target + data.Length + pageSize - 1) & -pageSize;
        var maps = Maps().Where(m => m.Start < end && m.End > first).ToArray();
        var changed = new List<Mapping>();
        try
        {
            foreach (var map in maps)
            {
                long low = Math.Max(first, map.Start), high = Math.Min(end, map.End);
                if (Mprotect((nint)low, (nuint)(high - low), map.Protection | 2) != 0)
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "Android detour mprotect failed");
                changed.Add(new(low, high, map.Protection, 0, ""));
            }
            var destination = new Span<byte>((void*)target, data.Length);
            destination.TryCopyTo(backup);
            data.CopyTo(destination);
            Flush(target, (nuint)data.Length);
        }
        finally
        {
            foreach (var map in changed)
                if (Mprotect((nint)map.Start, (nuint)(map.End - map.Start), map.Protection) != 0)
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "Android detour protection restore failed");
        }
    }

    public nint GetNativeJitHookConfig(int runtimeMajMin) => throw new PlatformNotSupportedException("CoreCLR JIT interception is not used by this Mono host");
    [DllImport("libc.so", EntryPoint = "mmap", SetLastError = true)]
    private static extern nint Mmap(nint address, nuint length, int protection, int flags, int fd, long offset);
    [DllImport("libc.so", EntryPoint = "munmap", SetLastError = true)]
    private static extern int Munmap(nint address, nuint length);
    [DllImport("libc.so", EntryPoint = "mprotect", SetLastError = true)]
    private static extern int Mprotect(nint address, nuint length, int protection);
    [DllImport("tmlhost", EntryPoint = "tml_host_flush_instructions")]
    private static extern void Flush(nint address, nuint length);

    private sealed class AndroidAllocator() : PagedMemoryAllocator(Environment.SystemPageSize)
    {
        private bool Finish(nint address, AllocationRequest request, [MaybeNullWhen(false)] out IAllocatedMemory allocated)
        {
            var page = new Page(this, address, (uint)PageSize, request.Executable);
            InsertAllocatedPage(page);
            if (page.TryAllocate((uint)request.Size, (uint)request.Alignment, out PageAllocation? result))
            { allocated = result; return true; }
            RegisterForCleanup(page);
            allocated = null; return false;
        }
        protected override bool TryAllocateNewPage(AllocationRequest request, [MaybeNullWhen(false)] out IAllocatedMemory allocated)
        {
            nint address = Mmap(0, (nuint)PageSize, request.Executable ? 7 : 3, 0x22, -1, 0);
            if (address == -1) { allocated = null; return false; }
            return Finish(address, request, out allocated);
        }
        protected override bool TryAllocateNewPage(PositionedAllocationRequest request, nint targetPage,
            nint lowPageBound, nint highPageBound, [MaybeNullWhen(false)] out IAllocatedMemory allocated)
        {
            // Hints never overwrite existing mappings. Reject/unmap results outside
            // the requested branch range; MonoMod may use a longer branch instead.
            for (int index = 0; index < 128; index++)
            {
                long offset = (index / 2 + 1) * (long)PageSize * 16;
                long hint = (long)targetPage + ((index & 1) == 0 ? offset : -offset);
                if (hint < (long)lowPageBound || hint >= (long)highPageBound) continue;
                nint address = Mmap((nint)hint, (nuint)PageSize, request.Base.Executable ? 7 : 3, 0x22, -1, 0);
                if (address == -1) continue;
                if (address >= lowPageBound && address < highPageBound) return Finish(address, request.Base, out allocated);
                Munmap(address, (nuint)PageSize);
            }
            allocated = null; return false;
        }
        protected override bool TryFreePage(Page page, [NotNullWhen(false)] out string? errorMsg)
        {
            if (Munmap(page.BaseAddr, page.Size) == 0) { errorMsg = null; return true; }
            errorMsg = new Win32Exception(Marshal.GetLastPInvokeError()).Message; return false;
        }
    }
}

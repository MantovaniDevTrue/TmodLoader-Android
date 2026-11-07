using Android.App;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace Tml.Mobile.Android;

internal static class NativeFileProbe
{
    private delegate IntPtr ReadFile(string path, out IntPtr length);

    internal static void Run(Activity owner)
    {
        string directory = Path.Combine(owner.CacheDir!.AbsolutePath, "native-file-probe");
        Directory.CreateDirectory(Path.Combine(directory, "Content"));
        string previous = Environment.CurrentDirectory;
        const string relative = "Content/Sound Bank espaço.xsb";
        byte[] expected = [(byte)'S', (byte)'D', (byte)'B', (byte)'K', 1, 2, 3, 4];
        try
        {
            Environment.CurrentDirectory = directory;
            File.WriteAllBytes(relative, expected);
            // Reproduce the original mismatch without handing the null to FACT.
            IntPtr original = SDL3.SDL.SDL_LoadFile(relative, out _);
            if (original != IntPtr.Zero)
            {
                SDL3.SDL.SDL_free(original);
                throw new InvalidOperationException("Expected Android SDL relative-path mismatch was not reproduced");
            }
            GameEntry.Log("NATIVE_RELATIVE_PATH_MISMATCH_REPRODUCED");
            Type io = typeof(Microsoft.Xna.Framework.Game).Assembly.GetType("Microsoft.Xna.Framework.MobileFileIOEXT", true)!;
            var read = io.GetMethod("ReadFile", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<ReadFile>();
            var open = io.GetMethod("OpenStreamingFile", BindingFlags.Static | BindingFlags.NonPublic)!
                .CreateDelegate<Func<string, IntPtr>>();
            foreach (string path in new[] { relative, Path.GetFullPath(relative) })
            {
                IntPtr buffer = read(path, out IntPtr length);
                try
                {
                    byte[] actual = new byte[expected.Length];
                    if (length.ToInt64() != expected.Length) throw new IOException("Unexpected native file length");
                    Marshal.Copy(buffer, actual, 0, actual.Length);
                    if (!actual.SequenceEqual(expected)) throw new IOException("Native file bytes differ");
                }
                finally { SDL3.SDL.SDL_free(buffer); }
            }
            IntPtr stream = open(relative);
            IntPtr destination = Marshal.AllocHGlobal(expected.Length);
            try
            {
                var info = Marshal.PtrToStructure<FAudio.FAudioIOStream>(stream);
                if (info.data == IntPtr.Zero) throw new IOException("FAudio opened a null SDL stream");
                var reader = Marshal.GetDelegateForFunctionPointer<FAudio.FAudio_readfunc>(info.read);
                if (reader(info.data, destination, (IntPtr)1, (IntPtr)expected.Length).ToInt64() != expected.Length)
                    throw new IOException("FAudio streaming read was incomplete");
                byte[] actual = new byte[expected.Length];
                Marshal.Copy(destination, actual, 0, actual.Length);
                if (!actual.SequenceEqual(expected)) throw new IOException("FAudio streaming bytes differ");
            }
            finally { Marshal.FreeHGlobal(destination); FAudio.FAudio_close(stream); }
            try { read("missing-bank.xsb", out _); throw new InvalidOperationException("Missing SDL file did not throw"); }
            catch (IOException) { }
            try { open("missing-bank.xwb"); throw new InvalidOperationException("Missing stream did not throw"); }
            catch (FileNotFoundException) { }
            // File.Exists still succeeds when the native reader lacks read access.
            // Exercise the allocated FAudio wrapper with a null SDL stream too.
            using var denied = new Java.IO.File(Path.GetFullPath(relative));
            if (!denied.SetReadable(false, false)) throw new IOException("Could not prepare unreadable fixture");
            try
            {
                try { read(relative, out _); throw new InvalidOperationException("Denied SDL file did not throw"); }
                catch (IOException) { }
                try { open(relative); throw new InvalidOperationException("Denied FAudio file did not throw"); }
                catch (IOException) { }
            }
            finally { denied.SetReadable(true, true); }
            GameEntry.Log("NATIVE_FILE_IO_OK relative+absolute+spaces+utf8+streaming+missing+denied");
        }
        finally
        {
            Environment.CurrentDirectory = previous;
            Directory.Delete(directory, true);
        }
    }
}

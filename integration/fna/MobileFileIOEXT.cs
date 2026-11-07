using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework
{
    // Android SDL resolves relative paths against app storage, not the process
    // working directory. Resolve at both native file boundaries, including FACT
    // streaming, so managed File.Exists and native reads address the same file.
    internal static class MobileFileIOEXT
    {
        internal static IntPtr ReadFile(string path, out IntPtr size)
        {
            path = Path.GetFullPath(path);
            UIntPtr length;
            IntPtr buffer = SDL3.SDL.SDL_LoadFile(path, out length);
            size = (IntPtr) checked((long) length.ToUInt64());
            if (buffer == IntPtr.Zero)
            {
                throw new IOException("SDL could not read '" + path + "': " + SDL3.SDL.SDL_GetError());
            }
            return buffer;
        }

        internal static IntPtr OpenStreamingFile(string path)
        {
            path = Path.GetFullPath(path);
            if (!File.Exists(path)) throw new FileNotFoundException("Audio wave bank not found", path);
            IntPtr stream = FAudio.FAudio_fopen(path);
            if (stream == IntPtr.Zero) throw new IOException("FAudio could not open '" + path + "'");
            if (Marshal.PtrToStructure<FAudio.FAudioIOStream>(stream).data == IntPtr.Zero)
            {
                string error = SDL3.SDL.SDL_GetError();
                FAudio.FAudio_close(stream);
                throw new IOException("FAudio could not read '" + path + "': " + error);
            }
            return stream;
        }
    }
}

using System;
using System.Buffers;
using System.IO;
using System.Threading;

namespace Microsoft.Xna.Framework.Content
{
    internal sealed class PooledContentStream : MemoryStream
    {
        private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Create(1024 * 1024, 2);
        private byte[] rented;
        private PooledContentStream(byte[] data, int length) : base(data, 0, length, true, true) { rented = data; }
        internal static MemoryStream Rent(int length)
        {
            if (length < 0) throw new InvalidDataException("Negative XNB buffer size");
            return new PooledContentStream(Pool.Rent(Math.Max(1, length)), length);
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            byte[] data = Interlocked.Exchange(ref rented, null);
            if (data != null) Pool.Return(data);
        }
    }
}

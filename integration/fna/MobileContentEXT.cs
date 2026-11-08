using System.Threading;

namespace Microsoft.Xna.Framework.Content
{
    public static class MobileContentEXT
    {
        internal static long creations, reuses;
        public static long LzxDecoderCreations => Interlocked.Read(ref creations);
        public static long LzxDecoderReuses => Interlocked.Read(ref reuses);
    }

    internal static class LzxDecoderPool
    {
        private static readonly object Gate = new object();
        private static readonly LzxDecoder[] Items = new LzxDecoder[2];
        private static int count;

        internal static LzxDecoder Rent()
        {
            LzxDecoder decoder;
            lock (Gate)
            {
                if (count == 0)
                {
                    Interlocked.Increment(ref MobileContentEXT.creations);
                    return new LzxDecoder(16);
                }
                decoder = Items[--count];
                Items[count] = null;
                Interlocked.Increment(ref MobileContentEXT.reuses);
            }
            decoder.ResetEXT();
            return decoder;
        }

        internal static void Return(LzxDecoder decoder)
        {
            lock (Gate) if (count < Items.Length) Items[count++] = decoder;
        }
    }
}

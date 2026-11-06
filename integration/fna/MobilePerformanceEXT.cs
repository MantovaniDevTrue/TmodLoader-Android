using System;
using System.Diagnostics;
using Tml.Mobile;

namespace Microsoft.Xna.Framework
{
    public static class MobilePerformanceEXT
    {
        private static FrameTimingWindow destination, active;
        private static long frameStart, allocatedStart, updates, draw, present, presentationStamp;
        private static int passes, gen0, gen1, gen2;

        // Configure on the game thread. Null disables collection and avoids clock/GC reads.
        public static FrameTimingWindow Destination
        {
            get => destination;
            set { destination = value; active = null; }
        }

        public static void BeginFrame()
        {
            active = destination;
            if (active == null) return;
            frameStart = Stopwatch.GetTimestamp();
            allocatedStart = GC.GetAllocatedBytesForCurrentThread();
            gen0 = GC.CollectionCount(0); gen1 = GC.CollectionCount(1); gen2 = GC.CollectionCount(2);
            updates = draw = present = presentationStamp = 0;
            passes = 0;
        }

        public static long BeginSection() => active == null ? 0 : Stopwatch.GetTimestamp();

        public static void EndUpdate(long started)
        {
            if (active == null) return;
            updates += Stopwatch.GetTimestamp() - started;
            passes++;
        }

        public static void EndDraw(long started)
        {
            if (active != null) draw += Stopwatch.GetTimestamp() - started;
        }

        public static void EndPresent(long started)
        {
            if (active == null) return;
            presentationStamp = Stopwatch.GetTimestamp();
            present += presentationStamp - started;
        }

        public static void EndFrame()
        {
            if (active == null) return;
            active.Record(new FrameTimingSample(
                Math.Max(1, Stopwatch.GetTimestamp() - frameStart), updates, draw, present,
                passes, presentationStamp, GC.GetAllocatedBytesForCurrentThread() - allocatedStart,
                GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2));
            active = null;
        }
    }
}

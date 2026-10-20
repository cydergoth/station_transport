using System.Collections.Generic;
using System.Diagnostics;

namespace StationTransport
{
    /// <summary>Recent route activity and timings, shown in the debug panel.</summary>
    internal static class TradeLog
    {
        private const int MaxLines = 40;

        public static readonly List<string> Lines = new List<string>();

        public static readonly Timing Tick = new Timing();
        public static readonly Timing Stop = new Timing();

        /// <summary>Times the Stations Thread held the lock and a stop had to wait a frame.</summary>
        public static int LockMisses;

        public static void Add(string line)
        {
            Lines.Add(line);
            if (Lines.Count > MaxLines)
            {
                Lines.RemoveAt(0);
            }
            if (Plugin.LogTrades.Value)
            {
                Plugin.Log.LogInfo(line);
            }
        }

        public static void Clear()
        {
            Lines.Clear();
            Tick.Reset();
            Stop.Reset();
            LockMisses = 0;
        }

        /// <summary>Running average and maximum of a piece of work, in milliseconds.</summary>
        public class Timing
        {
            private readonly Stopwatch watch = new Stopwatch();
            private double total;

            public int Count { get; private set; }
            public double MaxMs { get; private set; }
            public double AverageMs => Count == 0 ? 0 : total / Count;

            public void Start()
            {
                watch.Restart();
            }

            public void End()
            {
                watch.Stop();
                double ms = watch.Elapsed.TotalMilliseconds;
                total += ms;
                Count++;
                if (ms > MaxMs)
                {
                    MaxMs = ms;
                }
            }

            public void Reset()
            {
                total = 0;
                Count = 0;
                MaxMs = 0;
            }

            public override string ToString()
            {
                return $"avg {AverageMs:0.000} ms, max {MaxMs:0.000} ms over {Count}";
            }
        }
    }
}

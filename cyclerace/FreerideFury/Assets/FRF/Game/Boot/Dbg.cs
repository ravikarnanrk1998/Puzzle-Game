using System.Threading;
using UnityEngine;

namespace FRF
{
    /// <summary>
    /// Breadcrumbs plus a freeze watchdog for test runs: if the main thread stops ticking
    /// for a few seconds, a background thread logs the last breadcrumb it passed.
    /// </summary>
    public static class Dbg
    {
        static volatile string _last = "";
        static volatile int _frame;
        static Thread _watch;

        public static void Mark(string where) => _last = where;

        public static void Tick() => _frame++;

        public static void StartWatchdog()
        {
            if (_watch != null) return;
            _watch = new Thread(() =>
            {
                int seen = -1, still = 0;
                bool reported = false;
                while (true)
                {
                    Thread.Sleep(1000);
                    int f = _frame;
                    if (f != seen)
                    {
                        seen = f;
                        still = 0;
                        reported = false;
                        continue;
                    }
                    if (++still >= 6 && !reported)
                    {
                        reported = true;
                        Debug.LogError("[FRF] FREEZE: main thread stuck at '" + _last + "' (frame " + f + ")");
                    }
                }
            }) { IsBackground = true, Name = "FRF watchdog" };
            _watch.Start();
        }
    }
}

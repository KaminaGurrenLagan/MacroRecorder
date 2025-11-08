using System.Diagnostics;
using MacroRecorderPro.Interfaces;

namespace MacroRecorderPro.Core
{
    // Реализация IPrecisionTimer (DIP)
    public class PrecisionTimer : IPrecisionTimer
    {
        private readonly Stopwatch stopwatch = new Stopwatch();

        public long ElapsedTicks => stopwatch.ElapsedTicks;

        public void Start()
        {
            if (!stopwatch.IsRunning)
                stopwatch.Start();
        }

        public void Stop()
        {
            if (stopwatch.IsRunning)
                stopwatch.Stop();
        }

        public void Reset()
        {
            stopwatch.Reset();
        }
    }
}
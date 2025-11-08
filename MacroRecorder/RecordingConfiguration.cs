using MacroRecorderPro.Interfaces;

namespace MacroRecorderPro.Core
{
    // Strategy Pattern для конфигурации записи (SRP + OCP)
    public class RecordingConfiguration : IRecordingConfiguration
    {
        public bool RecordMouseMoves { get; set; } = true;
        public bool HighPrecision { get; set; } = false;

        public int MoveThreshold => HighPrecision ? 3 : 8;
        public long MoveIntervalTicks => HighPrecision ? 200000 : 500000;
    }
}
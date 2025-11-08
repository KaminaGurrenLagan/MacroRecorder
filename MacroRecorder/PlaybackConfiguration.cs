using MacroRecorderPro.Interfaces;

namespace MacroRecorderPro.Core
{
    // Strategy Pattern для конфигурации воспроизведения (SRP + OCP)
    public class PlaybackConfiguration : IPlaybackConfiguration
    {
        public int LoopCount { get; set; } = 1;
        public double SpeedMultiplier { get; set; } = 1.0;
    }
}
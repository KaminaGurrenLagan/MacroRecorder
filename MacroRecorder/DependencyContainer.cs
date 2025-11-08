using MacroRecorderPro.Interfaces;

namespace MacroRecorderPro.Core
{
    // Simple DI Container (Dependency Injection)
    // Следует принципу DIP (Dependency Inversion Principle)
    public class DependencyContainer
    {
        // Singleton instances
        private IPrecisionTimer timer;
        private IMacroRepository repository;
        private RecordingConfiguration recordingConfig;
        private PlaybackConfiguration playbackConfig;
        private IActionExecutor executor;
        private IMacroRecorder recorder;
        private IMacroPlayer player;
        private IMacroStorage storage;
        private KeyboardHook keyboardHook;
        private MouseHook mouseHook;
        private MacroCoordinator coordinator;

        public IPrecisionTimer GetTimer()
        {
            return timer ??= new PrecisionTimer();
        }

        public IMacroRepository GetRepository()
        {
            return repository ??= new MacroRepository();
        }

        public RecordingConfiguration GetRecordingConfiguration()
        {
            return recordingConfig ??= new RecordingConfiguration();
        }

        public PlaybackConfiguration GetPlaybackConfiguration()
        {
            return playbackConfig ??= new PlaybackConfiguration();
        }

        public IActionExecutor GetExecutor()
        {
            return executor ??= new ActionExecutor();
        }

        public IMacroRecorder GetRecorder()
        {
            if (recorder == null)
            {
                var precisionTimer = GetTimer();
                precisionTimer.Start(); // Запускаем таймер
                recorder = new MacroRecorder(
                    GetRepository(),
                    precisionTimer
                );
            }
            return recorder;
        }

        public IMacroPlayer GetPlayer()
        {
            return player ??= new MacroPlayer(
                GetRepository(),
                GetExecutor(),
                GetPlaybackConfiguration()
            );
        }

        public IMacroStorage GetStorage()
        {
            return storage ??= new MacroStorage(GetRepository());
        }

        public KeyboardHook GetKeyboardHook()
        {
            return keyboardHook ??= new KeyboardHook();
        }

        public MouseHook GetMouseHook()
        {
            return mouseHook ??= new MouseHook();
        }

        public MacroCoordinator GetCoordinator()
        {
            return coordinator ??= new MacroCoordinator(
                GetKeyboardHook(),
                GetMouseHook(),
                GetRecorder(),
                GetPlayer()
            );
        }
    }
}
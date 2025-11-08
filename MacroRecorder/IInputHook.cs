using System;
using MacroRecorderPro.Native;

namespace MacroRecorderPro.Interfaces
{
    // Интерфейс для хуков ввода (ISP - Interface Segregation Principle)
    public interface IInputHook : IDisposable
    {
        void Install();
        void Uninstall();
    }

    // Интерфейс для записи макросов (SRP - Single Responsibility Principle)
    public interface IMacroRecorder
    {
        bool IsRecording { get; }
        int ActionCount { get; }

        void StartRecording();
        void StopRecording();
        void Clear();

        // Методы для записи действий
        void RecordMouseAction(MSLLHOOKSTRUCT data, int message);
        void RecordKeyboardAction(int key, bool isKeyDown);

        event EventHandler RecordingStarted;
        event EventHandler RecordingStopped;
        event EventHandler ActionsChanged;
    }

    // Интерфейс для воспроизведения макросов (SRP)
    public interface IMacroPlayer
    {
        bool IsPlaying { get; }

        void Play();
        void Stop();

        event EventHandler PlaybackStarted;
        event EventHandler PlaybackStopped;
        event EventHandler<int> ActionExecuted;
    }

    // Интерфейс для хранения макросов (SRP)
    public interface IMacroStorage
    {
        void Save(string filePath);
        void Load(string filePath);
    }

    // Интерфейс для выполнения действий (SRP)
    public interface IActionExecutor
    {
        void Execute(Models.MacroAction action);
    }

    // Интерфейс для репозитория действий (Repository Pattern)
    public interface IMacroRepository
    {
        void Add(Models.MacroAction action);
        void Clear();
        System.Collections.Generic.List<Models.MacroAction> GetAll();
        void SetAll(System.Collections.Generic.List<Models.MacroAction> actions);
        int Count { get; }
    }

    // Интерфейс для конфигурации записи (Strategy Pattern)
    public interface IRecordingConfiguration
    {
        bool RecordMouseMoves { get; set; }
        bool HighPrecision { get; set; }
    }

    // Интерфейс для конфигурации воспроизведения (Strategy Pattern)
    public interface IPlaybackConfiguration
    {
        int LoopCount { get; set; }
        double SpeedMultiplier { get; set; }
    }

    // Интерфейс для точного таймера (DIP - Dependency Inversion Principle)
    public interface IPrecisionTimer
    {
        long ElapsedTicks { get; }
        void Start();
        void Stop();
        void Reset();
    }
}
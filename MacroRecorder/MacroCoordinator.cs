using System;
using MacroRecorderPro.Interfaces;
using MacroRecorderPro.Native;

namespace MacroRecorderPro.Core
{
    // Facade Pattern + Mediator Pattern
    // Координирует взаимодействие между хуками, рекордером и плеером
    public class MacroCoordinator : IDisposable
    {
        private readonly KeyboardHook keyboardHook;
        private readonly MouseHook mouseHook;
        private readonly IMacroRecorder recorder;
       // private readonly MacroRecorder recorder;

        private readonly IMacroPlayer player;


        private bool ignoreNextClick = false;

        public event EventHandler<string> StatusChanged;

        public MacroCoordinator(
            KeyboardHook keyboardHook,
            MouseHook mouseHook,
            IMacroRecorder recorder,
            IMacroPlayer player)
        {
            this.keyboardHook = keyboardHook ?? throw new ArgumentNullException(nameof(keyboardHook));
            this.mouseHook = mouseHook ?? throw new ArgumentNullException(nameof(mouseHook));
            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            this.player = player ?? throw new ArgumentNullException(nameof(player));

            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            keyboardHook.KeyboardEvent += OnKeyboardEvent;
            mouseHook.MouseEvent += OnMouseEvent;

            recorder.RecordingStarted += (s, e) => StatusChanged?.Invoke(this, "Recording started");
            recorder.RecordingStopped += (s, e) => StatusChanged?.Invoke(this, "Recording stopped");

            player.PlaybackStarted += (s, e) =>
            {
                ignoreNextClick = true;
                StatusChanged?.Invoke(this, "Playback started");
            };

            player.PlaybackStopped += (s, e) =>
            {
                ignoreNextClick = false;
                StatusChanged?.Invoke(this, "Playback stopped");
            };
        }



        public void Initialize()
        {
            keyboardHook.Install();
            mouseHook.Install();
        }

        private void OnKeyboardEvent(object sender, KeyboardHookEventArgs e)
        {
            // Обработка горячих клавиш
            if (HandleHotkeys(e))
                return;

            // Запись событий клавиатуры
            if (recorder.IsRecording && !player.IsPlaying)
            {
                recorder.RecordKeyboardAction(e.Key, e.IsKeyDown);
            }
        }

        private bool HandleHotkeys(KeyboardHookEventArgs e)
        {
            // Shift+Tab для остановки воспроизведения
            if (player.IsPlaying &&
                e.IsKeyDown &&
                e.Key == VirtualKeyConstants.VK_TAB &&
                IsShiftPressed())
            {
                player.Stop();
                e.Handled = true;
                return true;
            }

            // F9 для управления записью
            if (e.Key == VirtualKeyConstants.VK_F9 &&
                e.IsKeyDown &&
                !player.IsPlaying)
            {
                ToggleRecording();
                e.Handled = true;
                return true;
            }

            return false;
        }

        private void OnMouseEvent(object sender, MouseHookEventArgs e)
        {
            // Игнорирование первого клика при воспроизведении
            if (ShouldIgnoreClick(e))
            {
                ignoreNextClick = false;
                e.Handled = true;
                return;
            }

            // Запись событий мыши
            if (recorder.IsRecording && !player.IsPlaying)
            {
                recorder.RecordMouseAction(e.Data, e.Message);
            }
        }

        private bool ShouldIgnoreClick(MouseHookEventArgs e)
        {
            if (!ignoreNextClick)
                return false;

            return e.Message == WindowsMessageConstants.WM_LBUTTONDOWN ||
                   e.Message == WindowsMessageConstants.WM_RBUTTONDOWN ||
                   e.Message == WindowsMessageConstants.WM_MBUTTONDOWN;
        }

        private void ToggleRecording()
        {
            if (recorder.IsRecording)
                recorder.StopRecording();
            else
                recorder.StartRecording();
        }

        private bool IsShiftPressed()
        {
            return (NativeMethods.GetAsyncKeyState(VirtualKeyConstants.VK_SHIFT) & 0x8000) != 0;
        }

        public void Dispose()
        {
            keyboardHook?.Dispose();
            mouseHook?.Dispose();
        }
    }
}
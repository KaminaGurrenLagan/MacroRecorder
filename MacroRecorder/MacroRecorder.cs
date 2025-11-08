using System;
using MacroRecorderPro.Interfaces;
using MacroRecorderPro.Models;
using MacroRecorderPro.Native;

namespace MacroRecorderPro.Core
{
    public class MacroRecorder : IMacroRecorder
    {
        private readonly IMacroRepository repository;
        private readonly IPrecisionTimer timer;
        private bool isRecording;

        public bool IsRecording => isRecording;
        public int ActionCount => repository.Count;

        public event EventHandler RecordingStarted;
        public event EventHandler RecordingStopped;
        public event EventHandler ActionsChanged;

        public MacroRecorder(IMacroRepository repository, IPrecisionTimer timer)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
        }

        public void StartRecording()
        {
            repository.Clear();
            timer.Reset();
            timer.Start();
            isRecording = true;
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }

        public void StopRecording()
        {
            isRecording = false;
            timer.Stop();
            RecordingStopped?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            repository.Clear();
            ActionsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RecordKeyboardAction(int key, bool isKeyDown)
        {
            if (!isRecording) return;

            var action = new MacroAction
            {
                Type = ActionType.Keyboard,
                Key = key,
                Down = isKeyDown,
                TimeTicks = timer.ElapsedTicks
            };

            repository.Add(action);
            ActionsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RecordMouseAction(MSLLHOOKSTRUCT data, int message)
        {
            if (!isRecording) return;

            var action = new MacroAction
            {
                Type = ActionType.Mouse,
                X = data.pt.x,
                Y = data.pt.y,
                Button = MapMouseButton(message),
                Down = IsButtonDown(message),
                WheelDelta = GetWheelDelta(message, data.mouseData),
                TimeTicks = timer.ElapsedTicks
            };

            repository.Add(action);
            ActionsChanged?.Invoke(this, EventArgs.Empty);
        }

        private static MouseButton MapMouseButton(int message)
        {
            switch (message)
            {
                case WindowsMessageConstants.WM_LBUTTONDOWN:
                case WindowsMessageConstants.WM_LBUTTONUP:
                    return MouseButton.Left;
                case WindowsMessageConstants.WM_RBUTTONDOWN:
                case WindowsMessageConstants.WM_RBUTTONUP:
                    return MouseButton.Right;
                case WindowsMessageConstants.WM_MBUTTONDOWN:
                case WindowsMessageConstants.WM_MBUTTONUP:
                    return MouseButton.Middle;
                case WindowsMessageConstants.WM_MOUSEWHEEL:
                    return MouseButton.Wheel;
                case WindowsMessageConstants.WM_MOUSEMOVE:
                    return MouseButton.Move;
                default:
                    return MouseButton.None;
            }
        }

        private static bool IsButtonDown(int message)
        {
            return message == WindowsMessageConstants.WM_LBUTTONDOWN ||
                   message == WindowsMessageConstants.WM_RBUTTONDOWN ||
                   message == WindowsMessageConstants.WM_MBUTTONDOWN;
        }

        private static int GetWheelDelta(int message, uint mouseData)
        {
            if (message == WindowsMessageConstants.WM_MOUSEWHEEL)
                return (short)((mouseData >> 16) & 0xffff);
            return 0;
        }
    }
}

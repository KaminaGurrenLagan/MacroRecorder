using System;
using System.Windows.Forms;
using MacroRecorderPro.Interfaces;
using MacroRecorderPro.Models;
using MacroRecorderPro.Native;

namespace MacroRecorderPro.Core
{
    // SRP - отвечает только за выполнение действий
    public class ActionExecutor : IActionExecutor
    {
        public void Execute(MacroAction action)
        {
            if (action.Type == ActionType.Keyboard)
            {
                ExecuteKeyboardAction(action);
            }
            else if (action.Type == ActionType.Mouse)
            {
                ExecuteMouseAction(action);
            }
        }

        private void ExecuteKeyboardAction(MacroAction action)
        {
            INPUT input = new INPUT { type = InputConstants.INPUT_KEYBOARD };
            input.U.ki.wVk = (ushort)action.Key;
            input.U.ki.dwFlags = action.Down ? 0u : InputConstants.KEYEVENTF_KEYUP;
            input.U.ki.dwExtraInfo = NativeMethods.GetMessageExtraInfo();

            NativeMethods.SendInput(1, new[] { input }, INPUT.Size);
        }

        private void ExecuteMouseAction(MacroAction action)
        {
            var (absX, absY) = ConvertToAbsoluteCoordinates(action.X, action.Y);

            INPUT input = new INPUT { type = InputConstants.INPUT_MOUSE };
            input.U.mi.dx = absX;
            input.U.mi.dy = absY;
            input.U.mi.dwFlags = InputConstants.MOUSEEVENTF_ABSOLUTE | InputConstants.MOUSEEVENTF_MOVE;
            input.U.mi.dwExtraInfo = NativeMethods.GetMessageExtraInfo();

            if (action.Button == MouseButton.Wheel)
            {
                input.U.mi.dwFlags |= InputConstants.MOUSEEVENTF_WHEEL;
                input.U.mi.mouseData = (uint)action.WheelDelta;
            }
            else if (action.Button != MouseButton.Move && action.Button != MouseButton.None)
            {
                input.U.mi.dwFlags |= GetMouseButtonFlag(action.Button, action.Down);
            }

            NativeMethods.SendInput(1, new[] { input }, INPUT.Size);
        }

        private (int absX, int absY) ConvertToAbsoluteCoordinates(int x, int y)
        {
            int screenW = NativeMethods.GetSystemMetrics(0);
            int screenH = NativeMethods.GetSystemMetrics(1);

            if (screenW == 0) screenW = Screen.PrimaryScreen.Bounds.Width;
            if (screenH == 0) screenH = Screen.PrimaryScreen.Bounds.Height;

            int absX = (int)((x * 65535.0) / screenW);
            int absY = (int)((y * 65535.0) / screenH);

            absX = Math.Max(0, Math.Min(65535, absX));
            absY = Math.Max(0, Math.Min(65535, absY));

            return (absX, absY);
        }

        private uint GetMouseButtonFlag(MouseButton button, bool down)
        {
            return button switch
            {
                MouseButton.Left => down ? InputConstants.MOUSEEVENTF_LEFTDOWN : InputConstants.MOUSEEVENTF_LEFTUP,
                MouseButton.Right => down ? InputConstants.MOUSEEVENTF_RIGHTDOWN : InputConstants.MOUSEEVENTF_RIGHTUP,
                MouseButton.Middle => down ? InputConstants.MOUSEEVENTF_MIDDLEDOWN : InputConstants.MOUSEEVENTF_MIDDLEUP,
                _ => 0u
            };
        }
    }
}
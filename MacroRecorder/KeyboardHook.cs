using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MacroRecorderPro.Interfaces;
using MacroRecorderPro.Native;

namespace MacroRecorderPro.Core
{
    // ISP - отдельный класс для клавиатурных хуков (SRP)
    public class KeyboardHook : IInputHook
    {
        private IntPtr hookHandle;
        private GCHandle procHandle;
        private readonly NativeMethods.LowLevelKeyboardProc hookProc;

        public event EventHandler<KeyboardHookEventArgs> KeyboardEvent;

        public KeyboardHook()
        {
            hookProc = HookCallback;
            procHandle = GCHandle.Alloc(hookProc);
        }

        public void Install()
        {
            if (hookHandle != IntPtr.Zero)
                return;

            using (var process = Process.GetCurrentProcess())
            using (var module = process.MainModule)
            {
                var moduleHandle = NativeMethods.GetModuleHandle(module.ModuleName);
                hookHandle = NativeMethods.SetWindowsHookEx(
                    HookConstants.WH_KEYBOARD_LL,
                    hookProc,
                    moduleHandle,
                    0);
            }
        }

        public void Uninstall()
        {
            if (hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0)
                return NativeMethods.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

            int key = Marshal.ReadInt32(lParam);
            bool isKeyDown = wParam == (IntPtr)WindowsMessageConstants.WM_KEYDOWN;

            var args = new KeyboardHookEventArgs(key, isKeyDown);
            KeyboardEvent?.Invoke(this, args);

            return args.Handled
                ? (IntPtr)1
                : NativeMethods.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        public void Dispose()
        {
            Uninstall();

            if (procHandle.IsAllocated)
                procHandle.Free();
        }
    }

    public class KeyboardHookEventArgs : EventArgs
    {
        public int Key { get; }
        public bool IsKeyDown { get; }
        public bool Handled { get; set; }

        public KeyboardHookEventArgs(int key, bool isKeyDown)
        {
            Key = key;
            IsKeyDown = isKeyDown;
        }
    }
}
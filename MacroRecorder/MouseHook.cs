using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MacroRecorderPro.Interfaces;
using MacroRecorderPro.Native;

namespace MacroRecorderPro.Core
{
    // ISP - отдельный класс для мышиных хуков (SRP)
    public class MouseHook : IInputHook
    {
        private IntPtr hookHandle;
        private GCHandle procHandle;
        private readonly NativeMethods.LowLevelMouseProc hookProc;

        public event EventHandler<MouseHookEventArgs> MouseEvent;

        public MouseHook()
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
                    HookConstants.WH_MOUSE_LL,
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

            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int message = (int)wParam;

            var args = new MouseHookEventArgs(data, message);
            MouseEvent?.Invoke(this, args);

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

    public class MouseHookEventArgs : EventArgs
    {
        public MSLLHOOKSTRUCT Data { get; }
        public int Message { get; }
        public bool Handled { get; set; }

        public MouseHookEventArgs(MSLLHOOKSTRUCT data, int message)
        {
            Data = data;
            Message = message;
        }
    }
}
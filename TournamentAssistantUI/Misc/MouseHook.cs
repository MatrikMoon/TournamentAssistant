using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace TournamentAssistantUI.Misc
{
    class MouseHook
    {
        public static event Action<Point> MouseMoved;
        public static event Action MouseDown;
        public static event Action MouseUp;
        public static bool DisableMouseDown { get; set; }
        public static bool DisableMouseUp { get; set; }

        internal delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        
        private static LowLevelMouseProc HookDelegateReference = HookCallback;

        private enum MouseMessages
        {
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_MOUSEMOVE = 0x0200,
            WM_MOUSEWHEEL = 0x020A,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int WH_MOUSE_LL = 14;

        internal static IntPtr _hookID = IntPtr.Zero;

        internal static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && MouseMessages.WM_MOUSEMOVE == (MouseMessages)wParam)
            {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                MouseMoved?.Invoke(new Point(hookStruct.pt.x, hookStruct.pt.y));
            }
            else if (nCode >= 0 && MouseMessages.WM_LBUTTONUP == (MouseMessages)wParam)
            {
                MouseUp?.Invoke();
                if (DisableMouseUp) return (IntPtr)(-1);
            }
            else if (nCode >= 0 && MouseMessages.WM_LBUTTONDOWN == (MouseMessages)wParam)
            {
                MouseDown?.Invoke();
                if (DisableMouseDown) return (IntPtr)(-1);
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public static void StartHook()
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _hookID = SetWindowsHookEx(WH_MOUSE_LL, HookDelegateReference, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public static void StopHook()
        {
            if ((int)_hookID > 0) UnhookWindowsHookEx(_hookID);
        }
    }
}

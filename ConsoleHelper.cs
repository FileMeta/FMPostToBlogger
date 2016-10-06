using System;
using System.Runtime.InteropServices;


namespace Win32Interop
{
    static class ConsoleHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleProcessList(
            uint[] ProcessList,
            uint ProcessCount
            );

        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public static bool IsSoleConsoleOwner
        {
            get
            {
                uint[] procIds = new uint[4];
                uint count = GetConsoleProcessList(procIds, (uint)procIds.Length);
                return count <= 1;
            }
        }

        public static void BringConsoleToFront()
        {
            SetForegroundWindow(GetConsoleWindow());
        }

    }
}

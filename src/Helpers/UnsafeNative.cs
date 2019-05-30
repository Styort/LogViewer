using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LogViewer.Helpers
{
    internal static class UnsafeNative
    {
        public const int WM_COPYDATA = 0x004A;

        public static string GetMessage(int message, IntPtr lParam)
        {
            if (message == UnsafeNative.WM_COPYDATA)
            {
                try
                {
                    var data = Marshal.PtrToStructure<UnsafeNative.CopyDataStruct>(lParam);
                    var result = string.Copy(data.lpData);
                    return result;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        public static void SendMessage(IntPtr hwnd, string message)
        {
            var messageBytes = Encoding.Unicode.GetBytes(message); /* ANSII encoding */
            var data = new UnsafeNative.CopyDataStruct
            {
                dwData = IntPtr.Zero,
                lpData = message,
                cbData = messageBytes.Length + 1 /* +1 because of \0 string termination */
            };

            if (UnsafeNative.SendMessage(hwnd, WM_COPYDATA, IntPtr.Zero, ref data) != 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, ref CopyDataStruct lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct CopyDataStruct
        {
            public IntPtr dwData;
            public int cbData;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpData;
        }
    }
}

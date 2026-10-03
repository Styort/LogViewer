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

        /// <summary>
        /// Separates command-line arguments forwarded to the running instance. A space split paths such as
        /// <c>C:\Users\John Smith\app.log</c>; a newline cannot occur in a Windows path.
        /// </summary>
        private const char ArgumentSeparator = '\n';

        public static string JoinArguments(IEnumerable<string> args)
        {
            return string.Join(ArgumentSeparator.ToString(), args ?? Enumerable.Empty<string>());
        }

        public static string[] SplitArguments(string message)
        {
            return (message ?? string.Empty).Split(new[] { ArgumentSeparator }, StringSplitOptions.RemoveEmptyEntries);
        }

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
            var data = new UnsafeNative.CopyDataStruct
            {
                dwData = IntPtr.Zero,
                lpData = message,
                // lpData is marshalled as UTF-16: the size must include the two-byte terminator, otherwise the
                // receiver reads past the copied buffer.
                cbData = Encoding.Unicode.GetByteCount(message) + sizeof(char)
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

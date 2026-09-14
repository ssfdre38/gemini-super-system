using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace GeminiSuperDesktop {
    class Program {
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll")]
        static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDesktopWindowsProc lpfn, IntPtr lParam);
        delegate bool EnumDesktopWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        static void Main(string[] args) {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length == 0) {
                Console.WriteLine("{\"error\": \"Usage: desktop_helper [list | capture | sendkeys | click]\"}");
                return;
            }

            string cmd = args[0].ToLowerInvariant();
            if (cmd == "list") {
                ListWindows();
            } else if (cmd == "capture" && args.Length >= 3) {
                CaptureWindow(args[1], args[2]);
            } else if (cmd == "sendkeys" && args.Length >= 3) {
                SendKeysToWindow(args[1], args[2]);
            } else if (cmd == "click" && args.Length >= 4) {
                int x = int.Parse(args[2]);
                int y = int.Parse(args[3]);
                ClickWindow(args[1], x, y);
            } else {
                Console.WriteLine("{\"error\": \"Invalid arguments\"}");
            }
        }

        static string EscapeJson(string s) {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char c in s) {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\t') sb.Append("\\t");
                else if (c < 32) sb.AppendFormat("\\u{0:x4}", (int)c);
                else sb.Append(c);
            }
            return sb.ToString();
        }

        static void ListWindows() {
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x0100 | 0x0001);
            if (hDesk == IntPtr.Zero) {
                Console.WriteLine("[]");
                return;
            }

            var list = new List<string>();
            EnumDesktopWindows(hDesk, (hWnd, lParam) => {
                if (IsWindowVisible(hWnd)) {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(title) && title != "Program Manager") {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);
                        RECT r;
                        GetWindowRect(hWnd, out r);
                        int w = r.Right - r.Left;
                        int h = r.Bottom - r.Top;
                        if (w > 50 && h > 50) {
                            string escaped = EscapeJson(title);
                            list.Add(string.Format("{{\"handle\": \"{0}\", \"pid\": {1}, \"title\": \"{2}\", \"x\": {3}, \"y\": {4}, \"width\": {5}, \"height\": {6}}}",
                                hWnd, pid, escaped, r.Left, r.Top, w, h));
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            Console.WriteLine("[" + string.Join(",", list.ToArray()) + "]");
        }

        static void CaptureWindow(string titleFilter, string outputPath) {
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x0100 | 0x0001);
            IntPtr targetHwnd = IntPtr.Zero;
            string actualTitle = "";

            EnumDesktopWindows(hDesk, (hWnd, lParam) => {
                if (IsWindowVisible(hWnd)) {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();
                    if (title.IndexOf(titleFilter, StringComparison.OrdinalIgnoreCase) >= 0) {
                        targetHwnd = hWnd;
                        actualTitle = title;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (targetHwnd == IntPtr.Zero) {
                Console.WriteLine("{{\"success\": false, \"error\": \"Window not found matching: {0}\"}}", titleFilter);
                return;
            }

            RECT r;
            GetWindowRect(targetHwnd, out r);
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;

            if (w <= 0 || h <= 0) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window has zero size or is minimized.\"}");
                return;
            }

            try {
                using (Bitmap bmp = new Bitmap(w, h)) {
                    using (Graphics g = Graphics.FromImage(bmp)) {
                        IntPtr hdc = g.GetHdc();
                        try {
                            bool res = PrintWindow(targetHwnd, hdc, 2);
                            if (!res) PrintWindow(targetHwnd, hdc, 0);
                        } finally {
                            g.ReleaseHdc(hdc);
                        }
                    }
                    string dir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                        Directory.CreateDirectory(dir);
                    }
                    bmp.Save(outputPath, ImageFormat.Png);
                }

                string escTitle = actualTitle.Replace("\\", "\\\\").Replace("\"", "\\\"");
                string escPath = outputPath.Replace("\\", "/");
                Console.WriteLine(string.Format("{{\"success\": true, \"title\": \"{0}\", \"width\": {1}, \"height\": {2}, \"path\": \"{3}\"}}",
                    escTitle, w, h, escPath));
            } catch (Exception ex) {
                Console.WriteLine("{{\"success\": false, \"error\": \"{0}\"}}", ex.Message.Replace("\"", "'"));
            }
        }

        static void SendKeysToWindow(string titleFilter, string keys) {
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x0100 | 0x0001);
            IntPtr targetHwnd = IntPtr.Zero;

            EnumDesktopWindows(hDesk, (hWnd, lParam) => {
                if (IsWindowVisible(hWnd)) {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();
                    if (title.IndexOf(titleFilter, StringComparison.OrdinalIgnoreCase) >= 0) {
                        targetHwnd = hWnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (targetHwnd == IntPtr.Zero) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            ShowWindowAsync(targetHwnd, 9); // SW_RESTORE
            SetForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(100);
            SendKeys.SendWait(keys);
            Console.WriteLine("{\"success\": true}");
        }

        static void ClickWindow(string titleFilter, int relX, int relY) {
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x0100 | 0x0001);
            IntPtr targetHwnd = IntPtr.Zero;

            EnumDesktopWindows(hDesk, (hWnd, lParam) => {
                if (IsWindowVisible(hWnd)) {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();
                    if (title.IndexOf(titleFilter, StringComparison.OrdinalIgnoreCase) >= 0) {
                        targetHwnd = hWnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (targetHwnd == IntPtr.Zero) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            RECT r;
            GetWindowRect(targetHwnd, out r);
            int absX = r.Left + relX;
            int absY = r.Top + relY;

            ShowWindowAsync(targetHwnd, 9);
            SetForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(50);
            SetCursorPos(absX, absY);
            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)absX, (uint)absY, 0, 0);
            System.Threading.Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, (uint)absX, (uint)absY, 0, 0);

            Console.WriteLine("{{\"success\": true, \"x\": {0}, \"y\": {1}}}", absX, absY);
        }
    }
}

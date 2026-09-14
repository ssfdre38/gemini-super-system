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

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT {
            public uint type;
            public MOUSEKEYBDHARDWAREINPUT mkhi;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct MOUSEKEYBDHARDWAREINPUT {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        const uint INPUT_MOUSE = 0;
        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_UNICODE = 0x0004;

        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        const uint MOUSEEVENTF_WHEEL = 0x0800;

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
                Console.WriteLine("{\"error\": \"Usage: desktop_helper [list | capture | sendkeys | type | click | doubleclick | rightclick | drag | scroll | hotkey]\"}");
                return;
            }

            string cmd = args[0].ToLowerInvariant();
            if (cmd == "list") {
                ListWindows();
            } else if (cmd == "capture" && args.Length >= 3) {
                CaptureWindow(args[1], args[2]);
            } else if (cmd == "sendkeys" && args.Length >= 3) {
                SendKeysToWindow(args[1], args[2]);
            } else if (cmd == "type" && args.Length >= 3) {
                TypeTextToWindow(args[1], args[2]);
            } else if (cmd == "click" && args.Length >= 4) {
                int x = int.Parse(args[2]);
                int y = int.Parse(args[3]);
                string btn = args.Length >= 5 ? args[4] : "left";
                ClickWindow(args[1], x, y, btn);
            } else if (cmd == "doubleclick" && args.Length >= 4) {
                int x = int.Parse(args[2]);
                int y = int.Parse(args[3]);
                ClickWindow(args[1], x, y, "double");
            } else if (cmd == "rightclick" && args.Length >= 4) {
                int x = int.Parse(args[2]);
                int y = int.Parse(args[3]);
                ClickWindow(args[1], x, y, "right");
            } else if (cmd == "drag" && args.Length >= 6) {
                int fx = int.Parse(args[2]);
                int fy = int.Parse(args[3]);
                int tx = int.Parse(args[4]);
                int ty = int.Parse(args[5]);
                DragInWindow(args[1], fx, fy, tx, ty);
            } else if (cmd == "scroll" && args.Length >= 3) {
                int delta = int.Parse(args[2]);
                int x = args.Length >= 4 ? int.Parse(args[3]) : -1;
                int y = args.Length >= 5 ? int.Parse(args[4]) : -1;
                ScrollInWindow(args[1], delta, x, y);
            } else if (cmd == "hotkey" && args.Length >= 3) {
                HotkeyWindow(args[1], args[2]);
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

        static bool FindWindow(IntPtr hDesk, string query, out IntPtr targetHwnd, out string actualTitle) {
            targetHwnd = IntPtr.Zero;
            actualTitle = "";

            long handleNum = 0;
            bool isHandle = long.TryParse(query, out handleNum);

            var candidates = new List<KeyValuePair<IntPtr, string>>();
            EnumDesktopWindows(hDesk, (hWnd, lParam) => {
                if (IsWindowVisible(hWnd)) {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(title) && title != "Program Manager") {
                        candidates.Add(new KeyValuePair<IntPtr, string>(hWnd, title));
                    }
                }
                return true;
            }, IntPtr.Zero);

            // 1. Direct HWND check
            if (isHandle) {
                foreach (var pair in candidates) {
                    if (pair.Key.ToInt64() == handleNum) {
                        targetHwnd = pair.Key;
                        actualTitle = pair.Value;
                        return true;
                    }
                }
            }

            // 2. Exact match (case-insensitive)
            foreach (var pair in candidates) {
                if (string.Equals(pair.Value, query, StringComparison.OrdinalIgnoreCase)) {
                    targetHwnd = pair.Key;
                    actualTitle = pair.Value;
                    return true;
                }
            }

            // 3. Starts with match
            foreach (var pair in candidates) {
                if (pair.Value.StartsWith(query, StringComparison.OrdinalIgnoreCase)) {
                    targetHwnd = pair.Key;
                    actualTitle = pair.Value;
                    return true;
                }
            }

            // 4. Substring match
            foreach (var pair in candidates) {
                if (pair.Value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) {
                    targetHwnd = pair.Key;
                    actualTitle = pair.Value;
                    return true;
                }
            }

            return false;
        }

        static void CaptureWindow(string titleFilter, string outputPath) {
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x0100 | 0x0001);
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
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
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            ShowWindowAsync(targetHwnd, 9); // SW_RESTORE
            SetForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(100);
            SendKeys.SendWait(keys);
            Console.WriteLine("{\"success\": true}");
        }

        static void TypeTextToWindow(string titleFilter, string text) {
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x0100 | 0x0001);
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            ShowWindowAsync(targetHwnd, 9);
            SetForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(80);

            foreach (char c in text) {
                INPUT[] inputs = new INPUT[2];
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].mkhi.ki.wVk = 0;
                inputs[0].mkhi.ki.wScan = (ushort)c;
                inputs[0].mkhi.ki.dwFlags = KEYEVENTF_UNICODE;

                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].mkhi.ki.wVk = 0;
                inputs[1].mkhi.ki.wScan = (ushort)c;
                inputs[1].mkhi.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;

                SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
                System.Threading.Thread.Sleep(2);
            }

            Console.WriteLine(string.Format("{{\"success\": true, \"typed\": {0}, \"title\": \"{1}\"}}",
                text.Length, EscapeJson(actualTitle)));
        }

        static void ClickWindow(string titleFilter, int relX, int relY, string button) {
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x0100 | 0x0001);
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
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

            string b = (button ?? "left").ToLowerInvariant();
            if (b == "right") {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, (uint)absX, (uint)absY, 0, 0);
                System.Threading.Thread.Sleep(50);
                mouse_event(MOUSEEVENTF_RIGHTUP, (uint)absX, (uint)absY, 0, 0);
            } else if (b == "middle") {
                mouse_event(MOUSEEVENTF_MIDDLEDOWN, (uint)absX, (uint)absY, 0, 0);
                System.Threading.Thread.Sleep(50);
                mouse_event(MOUSEEVENTF_MIDDLEUP, (uint)absX, (uint)absY, 0, 0);
            } else if (b == "double") {
                mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)absX, (uint)absY, 0, 0);
                mouse_event(MOUSEEVENTF_LEFTUP, (uint)absX, (uint)absY, 0, 0);
                System.Threading.Thread.Sleep(80);
                mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)absX, (uint)absY, 0, 0);
                mouse_event(MOUSEEVENTF_LEFTUP, (uint)absX, (uint)absY, 0, 0);
            } else {
                mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)absX, (uint)absY, 0, 0);
                System.Threading.Thread.Sleep(50);
                mouse_event(MOUSEEVENTF_LEFTUP, (uint)absX, (uint)absY, 0, 0);
            }

            Console.WriteLine(string.Format("{{\"success\": true, \"button\": \"{0}\", \"x\": {1}, \"y\": {2}, \"title\": \"{3}\"}}",
                b, absX, absY, EscapeJson(actualTitle)));
        }

        static void DragInWindow(string titleFilter, int fromX, int fromY, int toX, int toY) {
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x0100 | 0x0001);
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            RECT r;
            GetWindowRect(targetHwnd, out r);
            int startAbsX = r.Left + fromX;
            int startAbsY = r.Top + fromY;
            int endAbsX = r.Left + toX;
            int endAbsY = r.Top + toY;

            ShowWindowAsync(targetHwnd, 9);
            SetForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(50);

            SetCursorPos(startAbsX, startAbsY);
            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)startAbsX, (uint)startAbsY, 0, 0);
            System.Threading.Thread.Sleep(100);

            int steps = 10;
            for (int i = 1; i <= steps; i++) {
                int curX = startAbsX + (endAbsX - startAbsX) * i / steps;
                int curY = startAbsY + (endAbsY - startAbsY) * i / steps;
                SetCursorPos(curX, curY);
                System.Threading.Thread.Sleep(10);
            }

            mouse_event(MOUSEEVENTF_LEFTUP, (uint)endAbsX, (uint)endAbsY, 0, 0);
            Console.WriteLine(string.Format("{{\"success\": true, \"from\": [{0},{1}], \"to\": [{2},{3}], \"title\": \"{4}\"}}",
                startAbsX, startAbsY, endAbsX, endAbsY, EscapeJson(actualTitle)));
        }

        static void ScrollInWindow(string titleFilter, int delta, int relX, int relY) {
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x0100 | 0x0001);
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            RECT r;
            GetWindowRect(targetHwnd, out r);
            int absX = (relX > 0) ? (r.Left + relX) : (r.Left + (r.Right - r.Left) / 2);
            int absY = (relY > 0) ? (r.Top + relY) : (r.Top + (r.Bottom - r.Top) / 2);

            ShowWindowAsync(targetHwnd, 9);
            SetForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(50);
            SetCursorPos(absX, absY);
            mouse_event(MOUSEEVENTF_WHEEL, (uint)absX, (uint)absY, (uint)delta, 0);
            Console.WriteLine(string.Format("{{\"success\": true, \"scrolled\": {0}, \"title\": \"{1}\"}}",
                delta, EscapeJson(actualTitle)));
        }

        static void HotkeyWindow(string titleFilter, string combo) {
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x0100 | 0x0001);
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            ShowWindowAsync(targetHwnd, 9);
            SetForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(80);

            string norm = combo.ToLowerInvariant().Trim();
            string sendStr = "";
            if (norm == "ctrl+s") sendStr = "^s";
            else if (norm == "ctrl+a") sendStr = "^a";
            else if (norm == "ctrl+c") sendStr = "^c";
            else if (norm == "ctrl+v") sendStr = "^v";
            else if (norm == "ctrl+z") sendStr = "^z";
            else if (norm == "ctrl+y") sendStr = "^y";
            else if (norm == "ctrl+f") sendStr = "^f";
            else if (norm == "enter") sendStr = "{ENTER}";
            else if (norm == "esc" || norm == "escape") sendStr = "{ESC}";
            else if (norm == "tab") sendStr = "{TAB}";
            else if (norm == "backspace") sendStr = "{BACKSPACE}";
            else sendStr = combo;

            SendKeys.SendWait(sendStr);
            Console.WriteLine(string.Format("{{\"success\": true, \"hotkey\": \"{0}\", \"title\": \"{1}\"}}",
                combo, EscapeJson(actualTitle)));
        }
    }
}

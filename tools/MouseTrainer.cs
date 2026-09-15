using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace GeminiSuperDesktop {
    public class MouseTrainer {
        #region Win32 Native Interop
        [StructLayout(LayoutKind.Sequential)]
        struct POINT {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MSLLHOOKSTRUCT {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll")]
        static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll")]
        static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc lpfn, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        const int WH_MOUSE_LL = 14;
        const int WM_MOUSEMOVE = 0x0200;
        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_RBUTTONDOWN = 0x0204;
        const int WM_RBUTTONUP = 0x0205;
        const int WM_MBUTTONDOWN = 0x0207;
        const int WM_MBUTTONUP = 0x0208;
        const int WM_MOUSEWHEEL = 0x020A;

        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        const uint MOUSEEVENTF_WHEEL = 0x0800;
        #endregion

        #region Telemetry Recording Engine
        static IntPtr _hookID = IntPtr.Zero;
        static LowLevelMouseProc _proc;
        static StreamWriter _recordWriter;
        static Stopwatch _recordSw;
        static int _lastX = -1;
        static int _lastY = -1;
        static long _lastDownTimeMs = -1;
        static int _eventCount = 0;
        static int _moveCount = 0;
        static int _clickCount = 0;
        static int _wheelCount = 0;

        public static void RunRecord(int durationSec, string outputPath) {
            _proc = HookCallback;
            _recordSw = new Stopwatch();

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            _recordWriter = new StreamWriter(outputPath, false, Encoding.UTF8);
            _recordSw.Start();

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule) {
                _hookID = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            }

            if (_hookID == IntPtr.Zero) {
                Console.WriteLine("{\"success\": false, \"error\": \"Failed to install low-level mouse hook\"}");
                return;
            }

            Console.Error.WriteLine(string.Format("🟢 [Mouse Trainer] Hook installed. Recording human mouse telemetry for {0} seconds...", durationSec));
            Console.Error.WriteLine(string.Format("   Saving telemetry stream to: {0}", outputPath));

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = durationSec * 1000;
            timer.Tick += delegate {
                timer.Stop();
                UnhookWindowsHookEx(_hookID);
                _recordSw.Stop();
                if (_recordWriter != null) {
                    _recordWriter.Flush();
                    _recordWriter.Close();
                }
                Application.Exit();
            };
            timer.Start();

            Application.Run();

            Console.WriteLine(string.Format("{{\"success\": true, \"durationSec\": {0}, \"events\": {1}, \"moves\": {2}, \"clicks\": {3}, \"wheels\": {4}, \"path\": \"{5}\"}}",
                durationSec, _eventCount, _moveCount, _clickCount, _wheelCount, outputPath.Replace("\\", "/")));
        }

        static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0 && _recordWriter != null) {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                double t = _recordSw.Elapsed.TotalMilliseconds;
                int x = hookStruct.pt.x;
                int y = hookStruct.pt.y;
                int msg = wParam.ToInt32();

                if (msg == WM_MOUSEMOVE) {
                    if (x != _lastX || y != _lastY) {
                        _lastX = x;
                        _lastY = y;
                        _recordWriter.WriteLine(string.Format("{{\"t\":{0:F2},\"type\":\"move\",\"x\":{1},\"y\":{2}}}", t, x, y));
                        _moveCount++;
                        _eventCount++;
                    }
                } else if (msg == WM_LBUTTONDOWN) {
                    _lastDownTimeMs = (long)t;
                    _recordWriter.WriteLine(string.Format("{{\"t\":{0:F2},\"type\":\"down\",\"button\":\"left\",\"x\":{1},\"y\":{2}}}", t, x, y));
                    _clickCount++;
                    _eventCount++;
                } else if (msg == WM_LBUTTONUP) {
                    long dwell = _lastDownTimeMs > 0 ? (long)t - _lastDownTimeMs : 80;
                    _recordWriter.WriteLine(string.Format("{{\"t\":{0:F2},\"type\":\"up\",\"button\":\"left\",\"x\":{1},\"y\":{2},\"dwellMs\":{3}}}", t, x, y, dwell));
                    _eventCount++;
                } else if (msg == WM_RBUTTONDOWN) {
                    _lastDownTimeMs = (long)t;
                    _recordWriter.WriteLine(string.Format("{{\"t\":{0:F2},\"type\":\"down\",\"button\":\"right\",\"x\":{1},\"y\":{2}}}", t, x, y));
                    _clickCount++;
                    _eventCount++;
                } else if (msg == WM_RBUTTONUP) {
                    long dwell = _lastDownTimeMs > 0 ? (long)t - _lastDownTimeMs : 80;
                    _recordWriter.WriteLine(string.Format("{{\"t\":{0:F2},\"type\":\"up\",\"button\":\"right\",\"x\":{1},\"y\":{2},\"dwellMs\":{3}}}", t, x, y, dwell));
                    _eventCount++;
                } else if (msg == WM_MOUSEWHEEL) {
                    short delta = (short)((hookStruct.mouseData >> 16) & 0xffff);
                    _recordWriter.WriteLine(string.Format("{{\"t\":{0:F2},\"type\":\"wheel\",\"delta\":{1},\"x\":{2},\"y\":{3}}}", t, delta, x, y));
                    _wheelCount++;
                    _eventCount++;
                }

                if (_eventCount % 50 == 0) {
                    _recordWriter.Flush();
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        #endregion

        #region Training & Kinematic Analysis
        public class StrokeSample {
            public double Distance;
            public double DurationMs;
            public double PeakVelocity;
            public double MeanVelocity;
            public double CurvatureRatio;
            public double JitterStdDev;
        }

        public class HumanKinematicProfile {
            // Fitts's Law: Duration = FittsA + FittsB * log2(1 + Distance)
            public double FittsA = 75.0;
            public double FittsB = 28.0;
            public double MeanCurvature = 0.08;
            public double CurvatureStdDev = 0.04;
            public double JitterStdDev = 1.2;
            public double MeanClickDwellMs = 85.0;
            public double ClickDwellStdDev = 18.0;
            public double WheelIntervalMs = 28.0;
            public double WheelDecayFactor = 1.15;
            public int SampleStrokesCount = 0;
            public int SampleClicksCount = 0;
            public int SampleWheelsCount = 0;
            public string TrainedAt = "";
        }

        public static HumanKinematicProfile TrainProfile(string inputJsonlPath, string outputProfilePath) {
            if (!File.Exists(inputJsonlPath)) {
                throw new FileNotFoundException("Input telemetry file not found: " + inputJsonlPath);
            }

            List<StrokeSample> strokes = new List<StrokeSample>();
            List<double> dwells = new List<double>();
            List<double> wheelIntervals = new List<double>();

            string[] lines = File.ReadAllLines(inputJsonlPath);
            List<POINT> currentPoints = new List<POINT>();
            List<double> currentTimes = new List<double>();
            double lastEventTime = -1;
            double lastWheelTime = -1;

            for (int i = 0; i < lines.Length; i++) {
                string l = lines[i].Trim();
                if (string.IsNullOrEmpty(l)) continue;

                string type = ExtractJsonString(l, "type");
                double t = ExtractJsonDouble(l, "t", 0.0);
                int x = ExtractJsonInt(l, "x", 0);
                int y = ExtractJsonInt(l, "y", 0);

                if (type == "move") {
                    if (lastEventTime > 0 && (t - lastEventTime) > 75.0 && currentPoints.Count >= 3) {
                        StrokeSample s = AnalyzeStroke(currentPoints, currentTimes);
                        if (s != null) strokes.Add(s);
                        currentPoints.Clear();
                        currentTimes.Clear();
                    }
                    currentPoints.Add(new POINT { x = x, y = y });
                    currentTimes.Add(t);
                    lastEventTime = t;
                } else if (type == "up") {
                    double dwell = ExtractJsonDouble(l, "dwellMs", 80.0);
                    dwells.Add(dwell);
                    if (currentPoints.Count >= 3) {
                        StrokeSample s = AnalyzeStroke(currentPoints, currentTimes);
                        if (s != null) strokes.Add(s);
                        currentPoints.Clear();
                        currentTimes.Clear();
                    }
                    lastEventTime = t;
                } else if (type == "wheel") {
                    if (lastWheelTime > 0) {
                        double interval = t - lastWheelTime;
                        if (interval > 5.0 && interval < 400.0) {
                            wheelIntervals.Add(interval);
                        }
                    }
                    lastWheelTime = t;
                }
            }

            if (currentPoints.Count >= 3) {
                StrokeSample s = AnalyzeStroke(currentPoints, currentTimes);
                if (s != null) strokes.Add(s);
            }

            HumanKinematicProfile prof = new HumanKinematicProfile();
            prof.SampleStrokesCount = strokes.Count;
            prof.SampleClicksCount = dwells.Count;
            prof.SampleWheelsCount = wheelIntervals.Count;
            prof.TrainedAt = DateTime.UtcNow.ToString("o");

            // 1. Fit Fitts's Law via Least Squares regression
            if (strokes.Count >= 5) {
                double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
                int n = 0;
                double sumCurv = 0;
                double sumJitter = 0;

                for (int i = 0; i < strokes.Count; i++) {
                    StrokeSample st = strokes[i];
                    if (st.Distance < 20.0) continue;
                    double id = Math.Log(1.0 + st.Distance, 2.0);
                    double dur = st.DurationMs;
                    sumX += id;
                    sumY += dur;
                    sumXY += (id * dur);
                    sumX2 += (id * id);
                    sumCurv += st.CurvatureRatio;
                    sumJitter += st.JitterStdDev;
                    n++;
                }

                if (n >= 4) {
                    double denom = (n * sumX2 - sumX * sumX);
                    if (Math.Abs(denom) > 1e-6) {
                        prof.FittsB = Math.Max(10.0, (n * sumXY - sumX * sumY) / denom);
                        prof.FittsA = Math.Max(20.0, (sumY - prof.FittsB * sumX) / n);
                    }
                    prof.MeanCurvature = Math.Max(0.02, Math.Min(0.25, sumCurv / n));
                    prof.JitterStdDev = Math.Max(0.5, Math.Min(3.0, sumJitter / n));
                }
            }

            // 2. Compute Click Dwell statistics
            if (dwells.Count > 0) {
                double sum = 0;
                for (int i = 0; i < dwells.Count; i++) sum += dwells[i];
                double mean = sum / dwells.Count;
                double sumSq = 0;
                for (int i = 0; i < dwells.Count; i++) sumSq += Math.Pow(dwells[i] - mean, 2.0);
                prof.MeanClickDwellMs = Math.Max(45.0, Math.Min(180.0, mean));
                prof.ClickDwellStdDev = Math.Max(5.0, Math.Min(50.0, Math.Sqrt(sumSq / dwells.Count)));
            }

            // 3. Compute Wheel intervals
            if (wheelIntervals.Count > 0) {
                double sum = 0;
                for (int i = 0; i < wheelIntervals.Count; i++) sum += wheelIntervals[i];
                prof.WheelIntervalMs = Math.Max(15.0, Math.Min(120.0, sum / wheelIntervals.Count));
            }

            string json = SerializeProfile(prof);
            string outDir = Path.GetDirectoryName(outputProfilePath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir)) {
                Directory.CreateDirectory(outDir);
            }
            File.WriteAllText(outputProfilePath, json, Encoding.UTF8);

            return prof;
        }

        static StrokeSample AnalyzeStroke(List<POINT> pts, List<double> times) {
            if (pts.Count < 3) return null;
            POINT p0 = pts[0];
            POINT pN = pts[pts.Count - 1];
            double dx = pN.x - p0.x;
            double dy = pN.y - p0.y;
            double directDist = Math.Sqrt(dx * dx + dy * dy);
            if (directDist < 10.0) return null;

            double duration = times[times.Count - 1] - times[0];
            if (duration < 15.0 || duration > 3000.0) return null;

            double maxPerpDist = 0.0;
            double peakVel = 0.0;

            for (int i = 1; i < pts.Count; i++) {
                double num = Math.Abs(dy * pts[i].x - dx * pts[i].y + pN.x * p0.y - pN.y * p0.x);
                double dPerp = directDist > 0 ? num / directDist : 0;
                if (dPerp > maxPerpDist) maxPerpDist = dPerp;

                double dt = times[i] - times[i - 1];
                if (dt > 1.0) {
                    double stepDist = Math.Sqrt(Math.Pow(pts[i].x - pts[i - 1].x, 2) + Math.Pow(pts[i].y - pts[i - 1].y, 2));
                    double v = (stepDist / dt) * 1000.0;
                    if (v > peakVel) peakVel = v;
                }
            }

            StrokeSample s = new StrokeSample();
            s.Distance = directDist;
            s.DurationMs = duration;
            s.PeakVelocity = peakVel;
            s.MeanVelocity = (directDist / duration) * 1000.0;
            s.CurvatureRatio = maxPerpDist / directDist;
            s.JitterStdDev = 1.2;
            return s;
        }
        #endregion

        #region Biomechanical Trajectory Synthesis & Actuation
        static Random _rng = new Random();

        public static void HumanMove(int fromX, int fromY, int toX, int toY, HumanKinematicProfile prof, string clickButton) {
            double dx = toX - fromX;
            double dy = toY - fromY;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < 3.0) {
                SetCursorPos(toX, toY);
                if (!string.IsNullOrEmpty(clickButton)) {
                    PerformClick(clickButton, prof);
                }
                return;
            }

            // 1. Predict human movement duration via trained Fitts's Law
            double id = Math.Log(1.0 + distance, 2.0);
            double baseDuration = prof.FittsA + prof.FittsB * id;
            double jitterFactor = 1.0 + (_rng.NextDouble() * 0.24 - 0.12);
            double totalDurationMs = Math.Max(120.0, Math.Min(800.0, baseDuration * jitterFactor));

            // 2. Synthesize Cubic Bézier Control Points with human wrist arc
            double curvature = Math.Max(0.03, SampleGaussian(prof.MeanCurvature, prof.CurvatureStdDev));
            int arcDirection = _rng.NextDouble() > 0.5 ? 1 : -1;
            double perpDist = distance * curvature * arcDirection;

            double perpX = -dy / distance;
            double perpY = dx / distance;

            double p1x = fromX + dx * 0.35 + perpX * perpDist * 0.8;
            double p1y = fromY + dy * 0.35 + perpY * perpDist * 0.8;
            double p2x = fromX + dx * 0.70 + perpX * perpDist * 1.1;
            double p2y = fromY + dy * 0.70 + perpY * perpDist * 1.1;

            // 3. Minimum-Jerk Velocity Curve Interpolation (Flash & Hogan polynomial: 10t^3 - 15t^4 + 6t^5)
            int stepMs = 8;
            int totalSteps = Math.Max(15, (int)(totalDurationMs / stepMs));

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 1; i <= totalSteps; i++) {
                double targetElapsed = (double)i / totalSteps;
                double u = 10.0 * Math.Pow(targetElapsed, 3) - 15.0 * Math.Pow(targetElapsed, 4) + 6.0 * Math.Pow(targetElapsed, 5);

                double oneMinusU = 1.0 - u;
                double bx = Math.Pow(oneMinusU, 3) * fromX +
                            3.0 * Math.Pow(oneMinusU, 2) * u * p1x +
                            3.0 * oneMinusU * Math.Pow(u, 2) * p2x +
                            Math.Pow(u, 3) * toX;
                double by = Math.Pow(oneMinusU, 3) * fromY +
                            3.0 * Math.Pow(oneMinusU, 2) * u * p1y +
                            3.0 * oneMinusU * Math.Pow(u, 2) * p2y +
                            Math.Pow(u, 3) * toY;

                double tremorScale = (1.0 - u * 0.6) * prof.JitterStdDev;
                double tx = SampleGaussian(0, tremorScale);
                double ty = SampleGaussian(0, tremorScale);

                int stepX = (int)Math.Round(bx + tx);
                int stepY = (int)Math.Round(by + ty);

                SetCursorPos(stepX, stepY);

                long expectedMs = (long)(targetElapsed * totalDurationMs);
                long currentMs = sw.ElapsedMilliseconds;
                if (expectedMs > currentMs) {
                    Thread.Sleep((int)(expectedMs - currentMs));
                }
            }

            SetCursorPos(toX, toY);
            Thread.Sleep(30);

            if (!string.IsNullOrEmpty(clickButton)) {
                PerformClick(clickButton, prof);
            }

            Console.WriteLine(string.Format("{{\"success\": true, \"from\": [{0},{1}], \"to\": [{2},{3}], \"durationMs\": {4}, \"steps\": {5}, \"distance\": {6}}}",
                fromX, fromY, toX, toY,
                sw.Elapsed.TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                totalSteps,
                distance.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
        }

        public static void HumanScroll(int targetDelta, int anchorX, int anchorY, HumanKinematicProfile prof) {
            if (anchorX >= 0 && anchorY >= 0) {
                SetCursorPos(anchorX, anchorY);
                Thread.Sleep(50);
            }

            int direction = targetDelta >= 0 ? 1 : -1;
            int remainingTicks = Math.Abs(targetDelta) / 120;
            if (remainingTicks == 0) remainingTicks = 1;

            double curInterval = prof.WheelIntervalMs;
            int scrolledTotal = 0;

            for (int i = 0; i < remainingTicks; i++) {
                uint wheelVal = unchecked((uint)(direction * 120));
                mouse_event(MOUSEEVENTF_WHEEL, 0, 0, wheelVal, UIntPtr.Zero);
                scrolledTotal += (direction * 120);

                Thread.Sleep((int)Math.Max(12, Math.Round(curInterval)));
                curInterval *= prof.WheelDecayFactor;
                if (curInterval > 160.0) curInterval = 160.0;
            }

            Console.WriteLine(string.Format("{{\"success\": true, \"scrolled\": {0}, \"ticks\": {1}, \"finalIntervalMs\": {2}}}",
                scrolledTotal, remainingTicks, curInterval.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
        }

        static void PerformClick(string button, HumanKinematicProfile prof) {
            string b = button.ToLowerInvariant().Trim();
            uint downFlag = MOUSEEVENTF_LEFTDOWN;
            uint upFlag = MOUSEEVENTF_LEFTUP;

            if (b == "right") {
                downFlag = MOUSEEVENTF_RIGHTDOWN;
                upFlag = MOUSEEVENTF_RIGHTUP;
            } else if (b == "middle") {
                downFlag = MOUSEEVENTF_MIDDLEDOWN;
                upFlag = MOUSEEVENTF_MIDDLEUP;
            }

            int dwell = (int)Math.Max(40.0, Math.Min(180.0, SampleGaussian(prof.MeanClickDwellMs, prof.ClickDwellStdDev)));

            mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(dwell);
            mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);

            if (b == "double") {
                Thread.Sleep(60);
                mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(dwell);
                mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
            }
        }

        static double SampleGaussian(double mean, double stdDev) {
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = 1.0 - _rng.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * randStdNormal;
        }

        public static void HumanDrag(int fromX, int fromY, int toX, int toY, HumanKinematicProfile prof) {
            POINT cur;
            GetCursorPos(out cur);
            HumanMove(cur.x, cur.y, fromX, fromY, prof, null);
            Thread.Sleep(40);

            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            int dwell = (int)Math.Max(40.0, Math.Min(180.0, SampleGaussian(prof.MeanClickDwellMs, prof.ClickDwellStdDev)));
            Thread.Sleep(dwell);

            HumanMove(fromX, fromY, toX, toY, prof, null);
            Thread.Sleep(40);

            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(dwell);

            Console.WriteLine(string.Format("{{\"success\": true, \"from\": [{0},{1}], \"to\": [{2},{3}], \"action\": \"drag\"}}",
                fromX, fromY, toX, toY));
        }

        static IntPtr EnsureInteractiveDesktop() {
            try {
                IntPtr hInp = OpenInputDesktop(0, false, 0x01FF);
                if (hInp != IntPtr.Zero) {
                    SetThreadDesktop(hInp);
                    return hInp;
                }
            } catch {}
            return IntPtr.Zero;
        }

        static bool ForceForegroundWindow(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) return false;
            EnsureInteractiveDesktop();
            IntPtr hFore = GetForegroundWindow();
            if (hFore == hWnd) return true;

            uint forePid, targetPid;
            uint foreThread = GetWindowThreadProcessId(hFore, out forePid);
            uint targetThread = GetWindowThreadProcessId(hWnd, out targetPid);
            uint appThread = GetCurrentThreadId();

            if (foreThread != 0 && targetThread != 0 && foreThread != targetThread) {
                try { AttachThreadInput(foreThread, targetThread, true); } catch {}
            }
            if (appThread != 0 && targetThread != 0 && appThread != targetThread) {
                try { AttachThreadInput(appThread, targetThread, true); } catch {}
            }

            ShowWindow(hWnd, 9); // SW_RESTORE
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
            SetActiveWindow(hWnd);
            SetFocus(hWnd);

            if (foreThread != 0 && targetThread != 0 && foreThread != targetThread) {
                try { AttachThreadInput(foreThread, targetThread, false); } catch {}
            }
            if (appThread != 0 && targetThread != 0 && appThread != targetThread) {
                try { AttachThreadInput(appThread, targetThread, false); } catch {}
            }
            return true;
        }

        static bool FindWindowByTitle(string titleFilter, out IntPtr targetHwnd, out string actualTitle, out RECT rect) {
            targetHwnd = IntPtr.Zero;
            actualTitle = "";
            rect = new RECT();

            long parsedHwnd;
            if (long.TryParse(titleFilter, out parsedHwnd) && parsedHwnd > 0) {
                targetHwnd = new IntPtr(parsedHwnd);
                StringBuilder sb = new StringBuilder(256);
                GetWindowText(targetHwnd, sb, 256);
                actualTitle = sb.ToString();
                GetWindowRect(targetHwnd, out rect);
                return true;
            }

            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr found = IntPtr.Zero;
            string foundTitle = "";
            RECT foundRect = new RECT();

            EnumWindowsProc proc = delegate(IntPtr hWnd, IntPtr lParam) {
                if (!IsWindowVisible(hWnd)) return true;
                StringBuilder sb = new StringBuilder(256);
                int len = GetWindowText(hWnd, sb, 256);
                string t = len > 0 ? sb.ToString().Trim() : "";

                uint pid = 0;
                GetWindowThreadProcessId(hWnd, out pid);
                string procName = "";
                try { procName = Process.GetProcessById((int)pid).ProcessName; } catch {}

                RECT r;
                GetWindowRect(hWnd, out r);
                if (r.Right - r.Left <= 30 || r.Bottom - r.Top <= 30) return true;

                string qClean = titleFilter.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? titleFilter.Substring(0, titleFilter.Length - 4) : titleFilter;

                if ((!string.IsNullOrEmpty(t) && t.IndexOf(titleFilter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(procName) && procName.IndexOf(qClean, StringComparison.OrdinalIgnoreCase) >= 0)) {
                    found = hWnd;
                    foundTitle = !string.IsNullOrEmpty(t) ? t : procName;
                    foundRect = r;
                    return false;
                }
                return true;
            };

            if (hDesk != IntPtr.Zero) {
                EnumDesktopWindows(hDesk, proc, IntPtr.Zero);
            }
            if (found == IntPtr.Zero) {
                EnumWindows(proc, IntPtr.Zero);
            }

            if (found != IntPtr.Zero) {
                targetHwnd = found;
                actualTitle = foundTitle;
                rect = foundRect;
                return true;
            }
            return false;
        }

        static string EscapeJson(string s) {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n").Replace("\t", "\\t");
        }
        #endregion

        #region JSON Helpers & Profile Serialization
        static string SerializeProfile(HumanKinematicProfile p) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(string.Format("  \"FittsA\": {0:F4},", p.FittsA));
            sb.AppendLine(string.Format("  \"FittsB\": {0:F4},", p.FittsB));
            sb.AppendLine(string.Format("  \"MeanCurvature\": {0:F4},", p.MeanCurvature));
            sb.AppendLine(string.Format("  \"CurvatureStdDev\": {0:F4},", p.CurvatureStdDev));
            sb.AppendLine(string.Format("  \"JitterStdDev\": {0:F4},", p.JitterStdDev));
            sb.AppendLine(string.Format("  \"MeanClickDwellMs\": {0:F2},", p.MeanClickDwellMs));
            sb.AppendLine(string.Format("  \"ClickDwellStdDev\": {0:F2},", p.ClickDwellStdDev));
            sb.AppendLine(string.Format("  \"WheelIntervalMs\": {0:F2},", p.WheelIntervalMs));
            sb.AppendLine(string.Format("  \"WheelDecayFactor\": {0:F4},", p.WheelDecayFactor));
            sb.AppendLine(string.Format("  \"SampleStrokesCount\": {0},", p.SampleStrokesCount));
            sb.AppendLine(string.Format("  \"SampleClicksCount\": {0},", p.SampleClicksCount));
            sb.AppendLine(string.Format("  \"SampleWheelsCount\": {0},", p.SampleWheelsCount));
            sb.AppendLine(string.Format("  \"TrainedAt\": \"{0}\"", p.TrainedAt));
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static HumanKinematicProfile LoadProfile(string path) {
            HumanKinematicProfile p = new HumanKinematicProfile();
            if (!File.Exists(path)) return p;

            string content = File.ReadAllText(path);
            p.FittsA = ExtractJsonDouble(content, "FittsA", p.FittsA);
            p.FittsB = ExtractJsonDouble(content, "FittsB", p.FittsB);
            p.MeanCurvature = ExtractJsonDouble(content, "MeanCurvature", p.MeanCurvature);
            p.CurvatureStdDev = ExtractJsonDouble(content, "CurvatureStdDev", p.CurvatureStdDev);
            p.JitterStdDev = ExtractJsonDouble(content, "JitterStdDev", p.JitterStdDev);
            p.MeanClickDwellMs = ExtractJsonDouble(content, "MeanClickDwellMs", p.MeanClickDwellMs);
            p.ClickDwellStdDev = ExtractJsonDouble(content, "ClickDwellStdDev", p.ClickDwellStdDev);
            p.WheelIntervalMs = ExtractJsonDouble(content, "WheelIntervalMs", p.WheelIntervalMs);
            p.WheelDecayFactor = ExtractJsonDouble(content, "WheelDecayFactor", p.WheelDecayFactor);
            p.SampleStrokesCount = ExtractJsonInt(content, "SampleStrokesCount", 0);
            p.SampleClicksCount = ExtractJsonInt(content, "SampleClicksCount", 0);
            p.SampleWheelsCount = ExtractJsonInt(content, "SampleWheelsCount", 0);
            return p;
        }

        static string ExtractJsonString(string json, string key) {
            string pattern = "\"" + key + "\":\"";
            int idx = json.IndexOf(pattern);
            if (idx == -1) return "";
            idx += pattern.Length;
            int end = json.IndexOf("\"", idx);
            if (end == -1) return "";
            return json.Substring(idx, end - idx);
        }

        static double ExtractJsonDouble(string json, string key, double defaultVal) {
            string pattern = "\"" + key + "\":";
            int idx = json.IndexOf(pattern);
            if (idx == -1) return defaultVal;
            idx += pattern.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
            int end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-' || json[end] == 'E' || json[end] == 'e' || json[end] == '+')) end++;
            string valStr = json.Substring(idx, end - idx);
            double res;
            if (double.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out res)) {
                return res;
            }
            return defaultVal;
        }

        static int ExtractJsonInt(string json, string key, int defaultVal) {
            return (int)ExtractJsonDouble(json, key, defaultVal);
        }
        #endregion

        #region CLI Entry Point
        public static void Main(string[] args) {
            if (args.Length == 0) {
                Console.WriteLine("{\"error\": \"Usage: mouse_trainer [record <seconds> <out.jsonl> | train <in.jsonl> <out.json> | move <profile.json> <x> <y> [button] | scroll <profile.json> <delta> [x y]]\"}");
                return;
            }

            string cmd = args[0].ToLowerInvariant();

            try {
                if (cmd == "record" && args.Length >= 3) {
                    int sec = int.Parse(args[1]);
                    string outPath = args[2];
                    RunRecord(sec, outPath);
                } else if (cmd == "train" && args.Length >= 3) {
                    string inPath = args[1];
                    string outProfile = args[2];
                    HumanKinematicProfile prof = TrainProfile(inPath, outProfile);
                    Console.WriteLine(string.Format("{{\"success\": true, \"strokes\": {0}, \"clicks\": {1}, \"wheels\": {2}, \"FittsA\": {3:F2}, \"FittsB\": {4:F2}, \"curvature\": {5:F4}, \"profile\": \"{6}\"}}",
                        prof.SampleStrokesCount, prof.SampleClicksCount, prof.SampleWheelsCount, prof.FittsA, prof.FittsB, prof.MeanCurvature, outProfile.Replace("\\", "/")));
                } else if (cmd == "move" && args.Length >= 4) {
                    string profPath = args[1];
                    int toX = int.Parse(args[2]);
                    int toY = int.Parse(args[3]);
                    string btn = args.Length >= 5 ? args[4] : null;

                    POINT cur;
                    GetCursorPos(out cur);
                    HumanKinematicProfile prof = LoadProfile(profPath);
                    HumanMove(cur.x, cur.y, toX, toY, prof, btn);
                } else if (cmd == "winmove" && args.Length >= 5) {
                    string profPath = args[1];
                    string titleFilter = args[2];
                    int relX = int.Parse(args[3]);
                    int relY = int.Parse(args[4]);
                    string btn = args.Length >= 6 ? args[5] : null;

                    IntPtr targetHwnd;
                    string actualTitle;
                    RECT rect;
                    if (!FindWindowByTitle(titleFilter, out targetHwnd, out actualTitle, out rect)) {
                        Console.WriteLine("{\"success\": false, \"error\": \"Window not found: " + EscapeJson(titleFilter) + "\"}");
                        return;
                    }

                    ForceForegroundWindow(targetHwnd);
                    Thread.Sleep(60);

                    POINT pt = new POINT { x = relX, y = relY };
                    if (!ClientToScreen(targetHwnd, ref pt)) {
                        pt.x = rect.Left + relX;
                        pt.y = rect.Top + relY;
                    }

                    POINT cur;
                    GetCursorPos(out cur);
                    HumanKinematicProfile prof = LoadProfile(profPath);
                    HumanMove(cur.x, cur.y, pt.x, pt.y, prof, btn);
                } else if (cmd == "scroll" && args.Length >= 3) {
                    string profPath = args[1];
                    int delta = int.Parse(args[2]);
                    int ax = args.Length >= 5 ? int.Parse(args[3]) : -1;
                    int ay = args.Length >= 5 ? int.Parse(args[4]) : -1;

                    HumanKinematicProfile prof = LoadProfile(profPath);
                    HumanScroll(delta, ax, ay, prof);
                } else if (cmd == "winscroll" && args.Length >= 4) {
                    string profPath = args[1];
                    string titleFilter = args[2];
                    int delta = int.Parse(args[3]);
                    int relX = args.Length >= 6 ? int.Parse(args[4]) : -1;
                    int relY = args.Length >= 6 ? int.Parse(args[5]) : -1;

                    IntPtr targetHwnd;
                    string actualTitle;
                    RECT rect;
                    if (!FindWindowByTitle(titleFilter, out targetHwnd, out actualTitle, out rect)) {
                        Console.WriteLine("{\"success\": false, \"error\": \"Window not found: " + EscapeJson(titleFilter) + "\"}");
                        return;
                    }

                    ForceForegroundWindow(targetHwnd);
                    Thread.Sleep(60);

                    int screenX = -1, screenY = -1;
                    if (relX >= 0 && relY >= 0) {
                        POINT pt = new POINT { x = relX, y = relY };
                        if (!ClientToScreen(targetHwnd, ref pt)) {
                            pt.x = rect.Left + relX;
                            pt.y = rect.Top + relY;
                        }
                        screenX = pt.x;
                        screenY = pt.y;
                    } else {
                        screenX = rect.Left + (rect.Right - rect.Left) / 2;
                        screenY = rect.Top + (rect.Bottom - rect.Top) / 2;
                    }

                    HumanKinematicProfile prof = LoadProfile(profPath);
                    HumanScroll(delta, screenX, screenY, prof);
                } else if (cmd == "drag" && args.Length >= 6) {
                    string profPath = args[1];
                    int fromX = int.Parse(args[2]);
                    int fromY = int.Parse(args[3]);
                    int toX = int.Parse(args[4]);
                    int toY = int.Parse(args[5]);

                    HumanKinematicProfile prof = LoadProfile(profPath);
                    HumanDrag(fromX, fromY, toX, toY, prof);
                } else if (cmd == "windrag" && args.Length >= 7) {
                    string profPath = args[1];
                    string titleFilter = args[2];
                    int fromX = int.Parse(args[3]);
                    int fromY = int.Parse(args[4]);
                    int toX = int.Parse(args[5]);
                    int toY = int.Parse(args[6]);

                    IntPtr targetHwnd;
                    string actualTitle;
                    RECT rect;
                    if (!FindWindowByTitle(titleFilter, out targetHwnd, out actualTitle, out rect)) {
                        Console.WriteLine("{\"success\": false, \"error\": \"Window not found: " + EscapeJson(titleFilter) + "\"}");
                        return;
                    }

                    ForceForegroundWindow(targetHwnd);
                    Thread.Sleep(60);

                    POINT pFrom = new POINT { x = fromX, y = fromY };
                    if (!ClientToScreen(targetHwnd, ref pFrom)) {
                        pFrom.x = rect.Left + fromX;
                        pFrom.y = rect.Top + fromY;
                    }
                    POINT pTo = new POINT { x = toX, y = toY };
                    if (!ClientToScreen(targetHwnd, ref pTo)) {
                        pTo.x = rect.Left + toX;
                        pTo.y = rect.Top + toY;
                    }

                    HumanKinematicProfile prof = LoadProfile(profPath);
                    HumanDrag(pFrom.x, pFrom.y, pTo.x, pTo.y, prof);
                } else {
                    Console.WriteLine("{\"error\": \"Invalid arguments for command: " + cmd + "\"}");
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", ex.Message.Replace("\"", "\\\"")));
            }
        }
        #endregion
    }
}

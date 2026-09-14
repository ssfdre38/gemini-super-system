using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Automation;

namespace GeminiSuperDesktop {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    class WindowMeta {
        public IntPtr Handle;
        public uint Pid;
        public string ProcessName;
        public string ClassName;
        public string Title;
        public RECT Rect;
        public bool IsForeground;
        public bool IsMinimized;
        public bool IsMaximized;
        public bool IsHung;
        public bool IsElevated;
    }

    class DxgiCaptureEngine {
        static readonly Guid IID_IDXGIFactory1 = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
        static readonly Guid IID_IDXGIOutput1 = new Guid("00cddea8-939b-4b83-a340-a685226666cc");
        static readonly Guid IID_ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int D3D11CreateDevice(
            IntPtr pAdapter, int driverType, IntPtr Software, uint flags,
            IntPtr pFeatureLevels, uint FeatureLevels, uint SDKVersion,
            out IntPtr ppDevice, out int pFeatureLevel, out IntPtr ppImmediateContext);

        [DllImport("dxgi.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

        [StructLayout(LayoutKind.Sequential)]
        struct D3D11_TEXTURE2D_DESC {
            public uint Width;
            public uint Height;
            public uint MipLevels;
            public uint ArraySize;
            public int Format;
            public uint SampleDesc_Count;
            public uint SampleDesc_Quality;
            public int Usage;
            public uint BindFlags;
            public uint CPUAccessFlags;
            public uint MiscFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct D3D11_MAPPED_SUBRESOURCE {
            public IntPtr pData;
            public uint RowPitch;
            public uint DepthPitch;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int EnumAdapters1Delegate(IntPtr thisPtr, uint index, out IntPtr ppAdapter);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int EnumOutputsDelegate(IntPtr thisPtr, uint index, out IntPtr ppOutput);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DuplicateOutputDelegate(IntPtr thisPtr, IntPtr pDevice, out IntPtr ppOutputDuplication);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int QueryInterfaceDelegate(IntPtr thisPtr, ref Guid riid, out IntPtr ppvObject);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate uint ReleaseDelegate(IntPtr thisPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int AcquireNextFrameDelegate(IntPtr thisPtr, uint timeoutMs, [Out] byte[] pFrameInfo, out IntPtr ppResource);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ReleaseFrameDelegate(IntPtr thisPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void GetTexture2DDescDelegate(IntPtr thisPtr, ref D3D11_TEXTURE2D_DESC pDesc);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreateTexture2DDelegate(IntPtr thisPtr, ref D3D11_TEXTURE2D_DESC pDesc, IntPtr pInitialData, out IntPtr ppTexture2D);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void CopyResourceDelegate(IntPtr thisPtr, IntPtr pDstResource, IntPtr pSrcResource);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int MapDelegate(IntPtr thisPtr, IntPtr pResource, uint subresource, int mapType, uint mapFlags, out D3D11_MAPPED_SUBRESOURCE pMappedResource);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void UnmapDelegate(IntPtr thisPtr, IntPtr pResource, uint subresource);

        static T GetVtable<T>(IntPtr pComObject, int slot) where T : class {
            IntPtr vtable = Marshal.ReadIntPtr(pComObject);
            IntPtr funcPtr = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T)) as T;
        }

        public static bool CaptureRegion(int cropX, int cropY, int cropW, int cropH, string destPath) {
            IntPtr pFactory = IntPtr.Zero;
            IntPtr pAdapter = IntPtr.Zero;
            IntPtr pDevice = IntPtr.Zero;
            IntPtr pContext = IntPtr.Zero;
            IntPtr pOutput = IntPtr.Zero;
            IntPtr pOutput1 = IntPtr.Zero;
            IntPtr pDuplication = IntPtr.Zero;
            IntPtr pResource = IntPtr.Zero;
            IntPtr pDesktopTexture = IntPtr.Zero;
            IntPtr pStagingTexture = IntPtr.Zero;

            try {
                Guid factoryGuid = IID_IDXGIFactory1;
                int hr = CreateDXGIFactory1(ref factoryGuid, out pFactory);
                if (hr != 0) return false;

                var enumAdapters = GetVtable<EnumAdapters1Delegate>(pFactory, 7);
                hr = enumAdapters(pFactory, 0, out pAdapter);
                if (hr != 0) return false;

                int fl;
                hr = D3D11CreateDevice(pAdapter, 0, IntPtr.Zero, 0, IntPtr.Zero, 0, 7, out pDevice, out fl, out pContext);
                if (hr != 0) return false;

                var enumOutputs = GetVtable<EnumOutputsDelegate>(pAdapter, 7);
                hr = enumOutputs(pAdapter, 0, out pOutput);
                if (hr != 0) return false;

                var qi = GetVtable<QueryInterfaceDelegate>(pOutput, 0);
                Guid output1Guid = IID_IDXGIOutput1;
                hr = qi(pOutput, ref output1Guid, out pOutput1);
                if (hr != 0) return false;

                var duplicateOutput = GetVtable<DuplicateOutputDelegate>(pOutput1, 22);
                hr = duplicateOutput(pOutput1, pDevice, out pDuplication);
                if (hr != 0) return false;

                var acquireNextFrame = GetVtable<AcquireNextFrameDelegate>(pDuplication, 8);
                var releaseFrame = GetVtable<ReleaseFrameDelegate>(pDuplication, 14);
                byte[] frameInfo = new byte[64];

                for (int attempt = 0; attempt < 8; attempt++) {
                    hr = acquireNextFrame(pDuplication, 150, frameInfo, out pResource);
                    long lastPresent = BitConverter.ToInt64(frameInfo, 0);
                    uint accum = BitConverter.ToUInt32(frameInfo, 16);

                    if (hr == 0 && (accum > 0 || lastPresent > 0)) {
                        break;
                    }

                    if (hr == 0) {
                        if (pResource != IntPtr.Zero) {
                            GetVtable<ReleaseDelegate>(pResource, 2)(pResource);
                            pResource = IntPtr.Zero;
                        }
                        releaseFrame(pDuplication);
                    }

                    Program.POINT pt;
                    Program.GetCursorPos(out pt);
                    Program.SetCursorPos(pt.x + 1, pt.y);
                    Thread.Sleep(15);
                    Program.SetCursorPos(pt.x, pt.y);
                    Thread.Sleep(20);
                }

                if (hr != 0 || pResource == IntPtr.Zero) return false;

                var resQi = GetVtable<QueryInterfaceDelegate>(pResource, 0);
                Guid tex2dGuid = IID_ID3D11Texture2D;
                hr = resQi(pResource, ref tex2dGuid, out pDesktopTexture);
                if (hr != 0) return false;

                var getDesc = GetVtable<GetTexture2DDescDelegate>(pDesktopTexture, 10);
                D3D11_TEXTURE2D_DESC texDesc = new D3D11_TEXTURE2D_DESC();
                getDesc(pDesktopTexture, ref texDesc);

                D3D11_TEXTURE2D_DESC stagingDesc = texDesc;
                stagingDesc.MipLevels = 1;
                stagingDesc.ArraySize = 1;
                stagingDesc.SampleDesc_Count = 1;
                stagingDesc.SampleDesc_Quality = 0;
                stagingDesc.Usage = 3; // D3D11_USAGE_STAGING
                stagingDesc.BindFlags = 0;
                stagingDesc.CPUAccessFlags = 0x20000; // D3D11_CPU_ACCESS_READ
                stagingDesc.MiscFlags = 0;

                var createTexture2D = GetVtable<CreateTexture2DDelegate>(pDevice, 5);
                hr = createTexture2D(pDevice, ref stagingDesc, IntPtr.Zero, out pStagingTexture);
                if (hr != 0) return false;

                var copyResource = GetVtable<CopyResourceDelegate>(pContext, 47);
                copyResource(pContext, pStagingTexture, pDesktopTexture);

                var map = GetVtable<MapDelegate>(pContext, 14);
                D3D11_MAPPED_SUBRESOURCE mapped = new D3D11_MAPPED_SUBRESOURCE();
                hr = map(pContext, pStagingTexture, 0, 1 /* D3D11_MAP_READ */, 0, out mapped);
                if (hr != 0) return false;

                int texW = (int)texDesc.Width;
                int texH = (int)texDesc.Height;

                string outDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir)) {
                    Directory.CreateDirectory(outDir);
                }

                using (var fullBmp = new Bitmap(texW, texH, (int)mapped.RowPitch, PixelFormat.Format32bppRgb, mapped.pData)) {
                    bool needCrop = (cropW > 0 && cropH > 0) && (cropW < texW || cropH < texH || cropX > 0 || cropY > 0);
                    if (needCrop) {
                        int startX = Math.Max(0, cropX);
                        int startY = Math.Max(0, cropY);
                        int endX = Math.Min(texW, cropX + cropW);
                        int endY = Math.Min(texH, cropY + cropH);
                        int actualW = Math.Max(1, endX - startX);
                        int actualH = Math.Max(1, endY - startY);

                        Rectangle cropRect = new Rectangle(startX, startY, actualW, actualH);
                        using (Bitmap cropped = fullBmp.Clone(cropRect, PixelFormat.Format32bppRgb)) {
                            cropped.Save(destPath, ImageFormat.Png);
                        }
                    } else {
                        using (Bitmap finalBmp = new Bitmap(fullBmp)) {
                            finalBmp.Save(destPath, ImageFormat.Png);
                        }
                    }
                }

                var unmap = GetVtable<UnmapDelegate>(pContext, 15);
                unmap(pContext, pStagingTexture, 0);

                return true;
            } catch {
                return false;
            } finally {
                if (pStagingTexture != IntPtr.Zero) {
                    try { GetVtable<ReleaseDelegate>(pStagingTexture, 2)(pStagingTexture); } catch {}
                }
                if (pDesktopTexture != IntPtr.Zero) {
                    try { GetVtable<ReleaseDelegate>(pDesktopTexture, 2)(pDesktopTexture); } catch {}
                }
                if (pResource != IntPtr.Zero) {
                    try { GetVtable<ReleaseDelegate>(pResource, 2)(pResource); } catch {}
                }
                if (pDuplication != IntPtr.Zero) {
                    try {
                        GetVtable<ReleaseFrameDelegate>(pDuplication, 14)(pDuplication);
                    } catch {}
                    try { GetVtable<ReleaseDelegate>(pDuplication, 2)(pDuplication); } catch {}
                }
                if (pOutput1 != IntPtr.Zero) {
                    try { GetVtable<ReleaseDelegate>(pOutput1, 2)(pOutput1); } catch {}
                }
                if (pOutput != IntPtr.Zero) {
                    try { GetVtable<ReleaseDelegate>(pOutput, 2)(pOutput); } catch {}
                }
                if (pAdapter != IntPtr.Zero) {
                    try { GetVtable<ReleaseDelegate>(pAdapter, 2)(pAdapter); } catch {}
                }
                if (pContext != IntPtr.Zero) {
                    try { GetVtable<ReleaseDelegate>(pContext, 2)(pContext); } catch {}
                }
                if (pDevice != IntPtr.Zero) {
                    try { GetVtable<ReleaseDelegate>(pDevice, 2)(pDevice); } catch {}
                }
                if (pFactory != IntPtr.Zero) {
                    try { GetVtable<ReleaseDelegate>(pFactory, 2)(pFactory); } catch {}
                }
            }
        }
    }

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
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true)]
        static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr OpenWindowStation(string lpszWinSta, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetProcessWindowStation(IntPtr hWinSta);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool IsHungAppWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        static extern void SwitchToThisWindow(IntPtr hWnd, bool fUnknown);

        [DllImport("user32.dll")]
        static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll")]
        static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);
        delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool BlockInput(bool fBlockIt);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        const int VK_SHIFT = 0x10;
        const int VK_CONTROL = 0x11;
        const int VK_MENU = 0x12;
        const int VK_LWIN = 0x5B;
        const int VK_RWIN = 0x5C;

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

        const uint MOUSEEVENTF_MOVE = 0x0001;
        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        const uint MOUSEEVENTF_WHEEL = 0x0800;
        const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        public class AtomicInputLock : IDisposable {
            private bool locked = false;

            public AtomicInputLock(bool enable = true) {
                if (enable) {
                    try {
                        locked = BlockInput(true);
                    } catch {}
                }
            }

            public void Dispose() {
                if (locked) {
                    try {
                        BlockInput(false);
                    } catch {}
                    locked = false;
                }
            }
        }

        public static List<ushort> ReleaseActiveModifiers() {
            int[] checkKeys = new int[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5 };
            var released = new List<ushort>();
            foreach (int vk in checkKeys) {
                try {
                    if ((GetAsyncKeyState(vk) & 0x8000) != 0) {
                        INPUT[] inUp = new INPUT[1];
                        inUp[0].type = INPUT_KEYBOARD;
                        inUp[0].mkhi.ki.wVk = (ushort)vk;
                        inUp[0].mkhi.ki.dwFlags = KEYEVENTF_KEYUP;
                        SendInput(1, inUp, Marshal.SizeOf(typeof(INPUT)));
                        released.Add((ushort)vk);
                    }
                } catch {}
            }
            if (released.Count > 0) {
                Thread.Sleep(10);
            }
            return released;
        }


        static string EscapeJson(string s) {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char c in s) {
                if (c == '\\') sb.Append(@"\\");
                else if (c == '"') sb.Append(@"""");
                else if (c == '\r') sb.Append(@"\r");
                else if (c == '\n') sb.Append(@"\n");
                else if (c == '\t') sb.Append(@"\t");
                else if (c < 32) sb.AppendFormat(@"\u{0:x4}", (int)c);
                else sb.Append(c);
            }
            return sb.ToString();
        }

        static void SetClipboardTextSTA(string text) {
            try {
                Thread t = new Thread(() => {
                    try {
                        Clipboard.SetText(text);
                    } catch {}
                });
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                t.Join(1500);
            } catch {}
        }

        public static IntPtr EnsureInteractiveDesktop() {
            try {
                IntPtr hInp = OpenInputDesktop(0, false, 0x01FF);
                if (hInp != IntPtr.Zero) {
                    SetThreadDesktop(hInp);
                    return hInp;
                }
            } catch {}

            try {
                IntPtr hWinsta = OpenWindowStation("winsta0", false, 0x037F);
                if (hWinsta != IntPtr.Zero) {
                    SetProcessWindowStation(hWinsta);
                }
                IntPtr hDesk = OpenDesktop("Default", 0, false, 0x01FF);
                if (hDesk != IntPtr.Zero) {
                    SetThreadDesktop(hDesk);
                    return hDesk;
                }
            } catch {}
            return IntPtr.Zero;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
        const uint SPIF_SENDCHANGE = 0x0002;
        const uint SPIF_UPDATEINIFILE = 0x0001;

        public static bool IsCurrentProcessElevated() {
            IntPtr hToken = IntPtr.Zero;
            try {
                if (OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0008, out hToken)) {
                    IntPtr pElevation = Marshal.AllocHGlobal(sizeof(int));
                    uint retLen;
                    if (GetTokenInformation(hToken, 20, pElevation, (uint)sizeof(int), out retLen)) {
                        int isElevated = Marshal.ReadInt32(pElevation);
                        Marshal.FreeHGlobal(pElevation);
                        return isElevated != 0;
                    }
                    Marshal.FreeHGlobal(pElevation);
                }
            } catch {}
            finally {
                if (hToken != IntPtr.Zero) CloseHandle(hToken);
            }
            return false;
        }

        public static bool IsProcessElevated(uint pid) {
            IntPtr hProc = IntPtr.Zero;
            IntPtr hToken = IntPtr.Zero;
            try {
                hProc = Process.GetProcessById((int)pid).Handle;
                if (OpenProcessToken(hProc, 0x0008, out hToken)) {
                    IntPtr pElevation = Marshal.AllocHGlobal(sizeof(int));
                    uint retLen;
                    if (GetTokenInformation(hToken, 20, pElevation, (uint)sizeof(int), out retLen)) {
                        int isElevated = Marshal.ReadInt32(pElevation);
                        Marshal.FreeHGlobal(pElevation);
                        return isElevated != 0;
                    }
                    Marshal.FreeHGlobal(pElevation);
                }
            } catch {}
            finally {
                if (hToken != IntPtr.Zero) CloseHandle(hToken);
            }
            return false;
        }

        public static void UnlockForegroundLockTimeout() {
            try {
                SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, 0);
            } catch {}
        }

        static void Main(string[] args) {
            try { Console.OutputEncoding = Encoding.UTF8; } catch {}
            if (args.Length > 0 && (args[0].Equals("elevation", StringComparison.OrdinalIgnoreCase) || args[0].Equals("uipi", StringComparison.OrdinalIgnoreCase))) {
                Console.WriteLine(string.Format("{{\"isElevated\": {0}, \"uiAccess\": false, \"dpiAware\": true}}", IsCurrentProcessElevated() ? "true" : "false"));
                return;
            }

            EnsureInteractiveDesktop();
            UnlockForegroundLockTimeout();
            try {
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) {
                    SetProcessDPIAware();
                }
            } catch {
                try { SetProcessDPIAware(); } catch {}
            }

            if (args.Length == 0) {
                Console.WriteLine("{\"error\": \"Usage: desktop_helper [active | list | info | focus | type | paste | click_and_type | click | doubleclick | rightclick | drag | scroll | hotkey | capture | listchildren | scan | elements | findelement | clickelement]\"}");
                return;
            }

            string cmd = args[0].ToLowerInvariant();
            if (cmd == "elevation" || cmd == "uipi") {
                Console.WriteLine(string.Format("{{\"isElevated\": {0}, \"uiAccess\": false, \"dpiAware\": true}}", IsCurrentProcessElevated() ? "true" : "false"));
                return;
            }
            if (cmd == "active" || cmd == "foreground") {
                GetActiveWindow();
            } else if (cmd == "list") {
                ListWindows();
            } else if (cmd == "info" && args.Length >= 2) {
                GetWindowInfoCmd(args[1]);
            } else if (cmd == "focus" && args.Length >= 2) {
                FocusWindowCmd(args[1]);
            } else if (cmd == "elements" && args.Length >= 2) {
                ListUIElements(args[1]);
            } else if (cmd == "findelement" && args.Length >= 3) {
                FindUIElementCmd(args[1], args[2]);
            } else if (cmd == "clickelement" && args.Length >= 3) {
                string btn = args.Length >= 4 ? args[3] : "left";
                ClickUIElementCmd(args[1], args[2], btn);
            } else if (cmd == "type" && args.Length >= 3) {
                int delay = args.Length >= 4 ? int.Parse(args[3]) : 2;
                TypeTextToWindow(args[1], args[2], delay);
            } else if (cmd == "paste" && args.Length >= 3) {
                PasteTextToWindow(args[1], args[2]);
            } else if (cmd == "click_and_type" && args.Length >= 5) {
                int x = int.Parse(args[2]);
                int y = int.Parse(args[3]);
                int delay = args.Length >= 6 ? int.Parse(args[5]) : 2;
                ClickAndTypeWindow(args[1], x, y, args[4], delay);
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
            } else if (cmd == "capture" && args.Length >= 3) {
                CaptureWindow(args[1], args[2]);
            } else if (cmd == "listchildren" && args.Length >= 2) {
                ListChildWindows(args[1]);
            } else if (cmd == "scan" && args.Length >= 2) {
                int rT = args.Length >= 3 ? int.Parse(args[2]) : 180;
                int gT = args.Length >= 4 ? int.Parse(args[3]) : 180;
                int bT = args.Length >= 5 ? int.Parse(args[4]) : 180;
                ScanImageWhitePixels(args[1], rT, gT, bT);
            } else if (cmd == "metrics") {
                POINT pt;
                GetCursorPos(out pt);
                Console.WriteLine(string.Format("{{\"cx\": {0}, \"cy\": {1}, \"vx\": {2}, \"vy\": {3}, \"vw\": {4}, \"vh\": {5}, \"cursorX\": {6}, \"cursorY\": {7}}}",
                    GetSystemMetrics(0), GetSystemMetrics(1),
                    GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79),
                    pt.x, pt.y));
            } else if (cmd == "probe" && args.Length >= 4) {
                string img = args[1];
                int px = int.Parse(args[2]);
                int py = int.Parse(args[3]);
                using (var bmp = new Bitmap(img)) {
                    if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height) {
                        Color c = bmp.GetPixel(px, py);
                        Console.WriteLine(string.Format("{{\"x\": {0}, \"y\": {1}, \"r\": {2}, \"g\": {3}, \"b\": {4}, \"hex\": \"#{2:X2}{3:X2}{4:X2}\"}}",
                            px, py, c.R, c.G, c.B));
                    } else {
                        Console.WriteLine("{\"error\": \"Coords out of bounds\"}");
                    }
                }
            } else {
                Console.WriteLine("{\"error\": \"Invalid arguments\"}");
            }
        }

        static List<WindowMeta> CollectDesktopWindows(IntPtr hDesk) {
            IntPtr hFore = GetForegroundWindow();
            var list = new List<WindowMeta>();
            var elevCache = new Dictionary<uint, bool>();

            EnumDesktopWindows(hDesk, (hWnd, lParam) => {
                if (IsWindowVisible(hWnd)) {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(title) && title != "Program Manager") {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);
                        string procName = "";
                        try { procName = Process.GetProcessById((int)pid).ProcessName; } catch {}
                        var sbClass = new StringBuilder(128);
                        GetClassName(hWnd, sbClass, sbClass.Capacity);
                        RECT r;
                        GetWindowRect(hWnd, out r);
                        int w = r.Right - r.Left;
                        int h = r.Bottom - r.Top;
                        if (w > 30 && h > 30) {
                            bool isElevated = false;
                            if (!elevCache.TryGetValue(pid, out isElevated)) {
                                isElevated = IsProcessElevated(pid);
                                elevCache[pid] = isElevated;
                            }

                            list.Add(new WindowMeta {
                                Handle = hWnd,
                                Pid = pid,
                                ProcessName = procName,
                                ClassName = sbClass.ToString().Trim(),
                                Title = title,
                                Rect = r,
                                IsForeground = (hWnd == hFore),
                                IsMinimized = IsIconic(hWnd),
                                IsMaximized = IsZoomed(hWnd),
                                IsHung = IsHungAppWindow(hWnd),
                                IsElevated = isElevated
                            });
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }

        static void GetActiveWindow() {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero && hDesk != IntPtr.Zero) {
                var windows = CollectDesktopWindows(hDesk);
                foreach (var w in windows) {
                    if (!w.IsMinimized && (w.Rect.Right - w.Rect.Left) > 100 && (w.Rect.Bottom - w.Rect.Top) > 100 && w.Title != "PopupHost") {
                        hWnd = w.Handle;
                        break;
                    }
                }
            }
            if (hWnd == IntPtr.Zero) {
                Console.WriteLine("{\"success\": false, \"error\": \"No foreground window detected\"}");
                return;
            }

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString().Trim();

            var sbClass = new StringBuilder(128);
            GetClassName(hWnd, sbClass, sbClass.Capacity);
            string className = sbClass.ToString().Trim();

            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            string procName = "";
            try { procName = Process.GetProcessById((int)pid).ProcessName; } catch {}

            RECT r;
            GetWindowRect(hWnd, out r);
            bool isMin = IsIconic(hWnd);
            bool isMax = IsZoomed(hWnd);
            bool isHung = IsHungAppWindow(hWnd);
            bool isElevated = IsProcessElevated(pid);

            Console.WriteLine(string.Format("{{\"success\": true, \"handle\": \"{0}\", \"pid\": {1}, \"process\": \"{2}\", \"class\": \"{3}\", \"title\": \"{4}\", \"x\": {5}, \"y\": {6}, \"width\": {7}, \"height\": {8}, \"isMinimized\": {9}, \"isMaximized\": {10}, \"isHung\": {11}, \"isElevated\": {12}}}",
                hWnd, pid, EscapeJson(procName), EscapeJson(className), EscapeJson(title),
                r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                isMin ? "true" : "false", isMax ? "true" : "false", isHung ? "true" : "false",
                isElevated ? "true" : "false"));
        }

        static void ListWindows() {
            IntPtr hDesk = EnsureInteractiveDesktop();
            if (hDesk == IntPtr.Zero) {
                Console.WriteLine("[]");
                return;
            }

            var windows = CollectDesktopWindows(hDesk);
            var list = new List<string>();
            foreach (var w in windows) {
                int width = w.Rect.Right - w.Rect.Left;
                int height = w.Rect.Bottom - w.Rect.Top;
                list.Add(string.Format("{{\"handle\": \"{0}\", \"pid\": {1}, \"process\": \"{2}\", \"class\": \"{3}\", \"title\": \"{4}\", \"x\": {5}, \"y\": {6}, \"width\": {7}, \"height\": {8}, \"isForeground\": {9}, \"isMinimized\": {10}, \"isMaximized\": {11}, \"isElevated\": {12}}}",
                    w.Handle, w.Pid, EscapeJson(w.ProcessName), EscapeJson(w.ClassName), EscapeJson(w.Title),
                    w.Rect.Left, w.Rect.Top, width, height,
                    w.IsForeground ? "true" : "false", w.IsMinimized ? "true" : "false", w.IsMaximized ? "true" : "false",
                    w.IsElevated ? "true" : "false"));
            }

            Console.WriteLine("[" + string.Join(",", list.ToArray()) + "]");
        }

        static bool FindWindow(IntPtr hDesk, string query, out IntPtr targetHwnd, out string actualTitle) {
            targetHwnd = IntPtr.Zero;
            actualTitle = "";

            long handleNum = 0;
            bool isHandle = long.TryParse(query, out handleNum);

            var candidates = CollectDesktopWindows(hDesk);

            if (isHandle) {
                foreach (var w in candidates) {
                    if (w.Handle.ToInt64() == handleNum) {
                        targetHwnd = w.Handle;
                        actualTitle = w.Title;
                        return true;
                    }
                }
            }

            foreach (var w in candidates) {
                if (string.Equals(w.Title, query, StringComparison.OrdinalIgnoreCase)) {
                    targetHwnd = w.Handle;
                    actualTitle = w.Title;
                    return true;
                }
            }

            string qClean = query.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? query.Substring(0, query.Length - 4) : query;
            foreach (var w in candidates) {
                if (string.Equals(w.ProcessName, qClean, StringComparison.OrdinalIgnoreCase)) {
                    targetHwnd = w.Handle;
                    actualTitle = w.Title;
                    return true;
                }
            }

            foreach (var w in candidates) {
                if (w.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase)) {
                    targetHwnd = w.Handle;
                    actualTitle = w.Title;
                    return true;
                }
            }

            foreach (var w in candidates) {
                if (w.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) {
                    targetHwnd = w.Handle;
                    actualTitle = w.Title;
                    return true;
                }
            }

            foreach (var w in candidates) {
                if (w.ProcessName.IndexOf(qClean, StringComparison.OrdinalIgnoreCase) >= 0) {
                    targetHwnd = w.Handle;
                    actualTitle = w.Title;
                    return true;
                }
            }

            foreach (var w in candidates) {
                if (w.ClassName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) {
                    targetHwnd = w.Handle;
                    actualTitle = w.Title;
                    return true;
                }
            }

            return false;
        }

        static void GetWindowInfoCmd(string query) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, query, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            var sbClass = new StringBuilder(128);
            GetClassName(targetHwnd, sbClass, sbClass.Capacity);

            uint pid;
            GetWindowThreadProcessId(targetHwnd, out pid);
            string procName = "";
            try { procName = Process.GetProcessById((int)pid).ProcessName; } catch {}

            RECT r;
            GetWindowRect(targetHwnd, out r);
            IntPtr hFore = GetForegroundWindow();
            bool isElevated = IsProcessElevated(pid);

            Console.WriteLine(string.Format("{{\"success\": true, \"handle\": \"{0}\", \"pid\": {1}, \"process\": \"{2}\", \"class\": \"{3}\", \"title\": \"{4}\", \"x\": {5}, \"y\": {6}, \"width\": {7}, \"height\": {8}, \"isForeground\": {9}, \"isMinimized\": {10}, \"isMaximized\": {11}, \"isHung\": {12}, \"isElevated\": {13}}}",
                targetHwnd, pid, EscapeJson(procName), EscapeJson(sbClass.ToString().Trim()), EscapeJson(actualTitle),
                r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                (targetHwnd == hFore) ? "true" : "false",
                IsIconic(targetHwnd) ? "true" : "false",
                IsZoomed(targetHwnd) ? "true" : "false",
                IsHungAppWindow(targetHwnd) ? "true" : "false",
                isElevated ? "true" : "false"));
        }

        static bool ForceForegroundWindow(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) return false;
            EnsureInteractiveDesktop();
            IntPtr hFore = GetForegroundWindow();
            if (hFore == hWnd) return true;

            try { AllowSetForegroundWindow(-1); } catch {}

            uint forePid, targetPid;
            uint foreThread = GetWindowThreadProcessId(hFore, out forePid);
            uint targetThread = GetWindowThreadProcessId(hWnd, out targetPid);
            uint appThread = GetCurrentThreadId();

            if (foreThread != 0 && targetThread != 0 && foreThread != targetThread) {
                AttachThreadInput(foreThread, targetThread, true);
            }
            if (foreThread != 0 && foreThread != appThread) AttachThreadInput(appThread, foreThread, true);
            if (targetThread != 0 && targetThread != appThread) AttachThreadInput(appThread, targetThread, true);

            SwitchToThisWindow(hWnd, true);
            keybd_event(0x12, 0, 0, 0); // Alt down

            if (IsIconic(hWnd)) {
                ShowWindow(hWnd, 9); // SW_RESTORE
            } else {
                ShowWindow(hWnd, 5); // SW_SHOW
            }

            SetForegroundWindow(hWnd);
            BringWindowToTop(hWnd);
            SetFocus(hWnd);

            keybd_event(0x12, 0, 2, 0); // Alt up

            if (targetThread != 0 && targetThread != appThread) AttachThreadInput(appThread, targetThread, false);
            if (foreThread != 0 && foreThread != appThread) AttachThreadInput(appThread, foreThread, false);
            if (foreThread != 0 && targetThread != 0 && foreThread != targetThread) {
                AttachThreadInput(foreThread, targetThread, false);
            }

            for (int i = 0; i < 8; i++) {
                if (GetForegroundWindow() == hWnd) return true;
                System.Threading.Thread.Sleep(25);
            }

            return GetForegroundWindow() == hWnd;
        }

        static void FocusWindowCmd(string titleFilter) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            bool focused = ForceForegroundWindow(targetHwnd);
            IntPtr finalFore = GetForegroundWindow();
            Console.WriteLine(string.Format("{{\"success\": {0}, \"title\": \"{1}\", \"handle\": \"{2}\", \"currentFore\": \"{3}\"}}",
                focused ? "true" : "false", EscapeJson(actualTitle), targetHwnd, finalFore));
        }

        static void ListChildWindows(string titleFilter) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("[]");
                return;
            }

            var children = new List<string>();
            EnumChildWindows(targetHwnd, (hChild, lParam) => {
                var sbClass = new StringBuilder(128);
                GetClassName(hChild, sbClass, sbClass.Capacity);
                var sbText = new StringBuilder(256);
                GetWindowText(hChild, sbText, sbText.Capacity);
                RECT r;
                GetWindowRect(hChild, out r);
                int w = r.Right - r.Left;
                int h = r.Bottom - r.Top;

                if (w > 0 && h > 0) {
                    children.Add(string.Format("{{\"handle\": \"{0}\", \"class\": \"{1}\", \"text\": \"{2}\", \"x\": {3}, \"y\": {4}, \"width\": {5}, \"height\": {6}}}",
                        hChild, EscapeJson(sbClass.ToString().Trim()), EscapeJson(sbText.ToString().Trim()), r.Left, r.Top, w, h));
                }
                return true;
            }, IntPtr.Zero);

            Console.WriteLine("[" + string.Join(",", children.ToArray()) + "]");
        }

        static void SendTextDirect(string text, int delayMs) {
            ReleaseActiveModifiers();
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (c == '\r') continue;

                if (c == '\n') {
                    INPUT[] enterInputs = new INPUT[2];
                    enterInputs[0].type = INPUT_KEYBOARD;
                    enterInputs[0].mkhi.ki.wVk = 0x0D; // VK_RETURN
                    enterInputs[0].mkhi.ki.dwFlags = 0;

                    enterInputs[1].type = INPUT_KEYBOARD;
                    enterInputs[1].mkhi.ki.wVk = 0x0D;
                    enterInputs[1].mkhi.ki.dwFlags = KEYEVENTF_KEYUP;

                    SendInput(2, enterInputs, Marshal.SizeOf(typeof(INPUT)));
                    System.Threading.Thread.Sleep(Math.Max(delayMs, 10));
                    continue;
                }

                if (c == '\t') {
                    INPUT[] tabInputs = new INPUT[2];
                    tabInputs[0].type = INPUT_KEYBOARD;
                    tabInputs[0].mkhi.ki.wVk = 0x09; // VK_TAB
                    tabInputs[0].mkhi.ki.dwFlags = 0;

                    tabInputs[1].type = INPUT_KEYBOARD;
                    tabInputs[1].mkhi.ki.wVk = 0x09;
                    tabInputs[1].mkhi.ki.dwFlags = KEYEVENTF_KEYUP;

                    SendInput(2, tabInputs, Marshal.SizeOf(typeof(INPUT)));
                    System.Threading.Thread.Sleep(Math.Max(delayMs, 10));
                    continue;
                }

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
                if (delayMs > 0) System.Threading.Thread.Sleep(delayMs);
            }
        }

        static void TypeTextToWindow(string titleFilter, string text, int delayMs) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            if (GetForegroundWindow() != targetHwnd) {
                ForceForegroundWindow(targetHwnd);
                System.Threading.Thread.Sleep(80);
            }

            SendTextDirect(text, delayMs);

            Console.WriteLine(string.Format("{{\"success\": true, \"typed\": {0}, \"title\": \"{1}\"}}",
                text.Length, EscapeJson(actualTitle)));
        }

        static void PasteTextToWindow(string titleFilter, string text) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            SetClipboardTextSTA(text);
            if (GetForegroundWindow() != targetHwnd) {
                ForceForegroundWindow(targetHwnd);
                System.Threading.Thread.Sleep(80);
            }

            ReleaseActiveModifiers();
            keybd_event(0x11, 0, 0, 0); // Ctrl down
            System.Threading.Thread.Sleep(25);
            keybd_event(0x56, 0, 0, 0); // V down
            System.Threading.Thread.Sleep(45);
            keybd_event(0x56, 0, 2, 0); // V up
            System.Threading.Thread.Sleep(25);
            keybd_event(0x11, 0, 2, 0); // Ctrl up

            Console.WriteLine(string.Format("{{\"success\": true, \"pasted\": {0}, \"title\": \"{1}\"}}",
                text.Length, EscapeJson(actualTitle)));
        }

        static void ClickAndTypeWindow(string titleFilter, int relX, int relY, string text, int delayMs) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            ClickWindow(titleFilter, relX, relY, "left");
            System.Threading.Thread.Sleep(120);

            SendTextDirect(text, delayMs);

            Console.WriteLine(string.Format("{{\"success\": true, \"typed\": {0}, \"title\": \"{1}\"}}",
                text.Length, EscapeJson(actualTitle)));
        }

        static void ClickWindow(string titleFilter, int relX, int relY, string button) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            POINT pt = new POINT { x = relX, y = relY };
            if (!ClientToScreen(targetHwnd, ref pt)) {
                RECT r;
                GetWindowRect(targetHwnd, out r);
                pt.x = r.Left + relX;
                pt.y = r.Top + relY;
            }
            int absX = pt.x;
            int absY = pt.y;

            ForceForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(80);

            int screenW = Math.Max(1, GetSystemMetrics(0));
            int screenH = Math.Max(1, GetSystemMetrics(1));
            int normX = (int)Math.Round((absX * 65535.0) / (screenW - 1));
            int normY = (int)Math.Round((absY * 65535.0) / (screenH - 1));

            string b = (button ?? "left").ToLowerInvariant();
            uint downFlag = MOUSEEVENTF_LEFTDOWN;
            uint upFlag = MOUSEEVENTF_LEFTUP;
            if (b == "right") { downFlag = MOUSEEVENTF_RIGHTDOWN; upFlag = MOUSEEVENTF_RIGHTUP; }
            else if (b == "middle") { downFlag = MOUSEEVENTF_MIDDLEDOWN; upFlag = MOUSEEVENTF_MIDDLEUP; }

            using (new AtomicInputLock()) {
                SetCursorPos(absX, absY);
                System.Threading.Thread.Sleep(30);

                mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                System.Threading.Thread.Sleep(30);
                mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);

                if (b == "double") {
                    System.Threading.Thread.Sleep(50);
                    mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                    System.Threading.Thread.Sleep(30);
                    mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                }
            }

            Console.WriteLine(string.Format("{{\"success\": true, \"button\": \"{0}\", \"x\": {1}, \"y\": {2}, \"title\": \"{3}\"}}",
                b, absX, absY, EscapeJson(actualTitle)));
        }

        static void DragInWindow(string titleFilter, int fromX, int fromY, int toX, int toY) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            POINT ptStart = new POINT { x = fromX, y = fromY };
            if (!ClientToScreen(targetHwnd, ref ptStart)) {
                RECT r;
                GetWindowRect(targetHwnd, out r);
                ptStart.x = r.Left + fromX;
                ptStart.y = r.Top + fromY;
            }
            POINT ptEnd = new POINT { x = toX, y = toY };
            if (!ClientToScreen(targetHwnd, ref ptEnd)) {
                RECT r;
                GetWindowRect(targetHwnd, out r);
                ptEnd.x = r.Left + toX;
                ptEnd.y = r.Top + toY;
            }
            int startAbsX = ptStart.x;
            int startAbsY = ptStart.y;
            int endAbsX = ptEnd.x;
            int endAbsY = ptEnd.y;

            ForceForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(80);

            int screenW = Math.Max(1, GetSystemMetrics(0));
            int screenH = Math.Max(1, GetSystemMetrics(1));

            int normStartX = (int)Math.Round((startAbsX * 65535.0) / (screenW - 1));
            int normStartY = (int)Math.Round((startAbsY * 65535.0) / (screenH - 1));

            using (new AtomicInputLock()) {
                INPUT[] down = new INPUT[2];
                down[0].type = INPUT_MOUSE;
                down[0].mkhi.mi.dx = normStartX;
                down[0].mkhi.mi.dy = normStartY;
                down[0].mkhi.mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE;

                down[1].type = INPUT_MOUSE;
                down[1].mkhi.mi.dx = normStartX;
                down[1].mkhi.mi.dy = normStartY;
                down[1].mkhi.mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTDOWN;
                SendInput(2, down, Marshal.SizeOf(typeof(INPUT)));
                System.Threading.Thread.Sleep(50);

                int steps = 15;
                for (int i = 1; i <= steps; i++) {
                    int curX = startAbsX + (endAbsX - startAbsX) * i / steps;
                    int curY = startAbsY + (endAbsY - startAbsY) * i / steps;
                    int nX = (int)Math.Round((curX * 65535.0) / (screenW - 1));
                    int nY = (int)Math.Round((curY * 65535.0) / (screenH - 1));

                    INPUT[] step = new INPUT[1];
                    step[0].type = INPUT_MOUSE;
                    step[0].mkhi.mi.dx = nX;
                    step[0].mkhi.mi.dy = nY;
                    step[0].mkhi.mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE;
                    SendInput(1, step, Marshal.SizeOf(typeof(INPUT)));
                    System.Threading.Thread.Sleep(8);
                }

                SetCursorPos(endAbsX, endAbsY);
                System.Threading.Thread.Sleep(50);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            }

            int normEndX = (int)Math.Round((endAbsX * 65535.0) / (screenW - 1));
            int normEndY = (int)Math.Round((endAbsY * 65535.0) / (screenH - 1));

            INPUT[] up = new INPUT[1];
            up[0].type = INPUT_MOUSE;
            up[0].mkhi.mi.dx = normEndX;
            up[0].mkhi.mi.dy = normEndY;
            up[0].mkhi.mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTUP;
            SendInput(1, up, Marshal.SizeOf(typeof(INPUT)));

            Console.WriteLine(string.Format("{{\"success\": true, \"from\": [{0},{1}], \"to\": [{2},{3}], \"title\": \"{4}\"}}",
                startAbsX, startAbsY, endAbsX, endAbsY, EscapeJson(actualTitle)));
        }

        static void ScrollInWindow(string titleFilter, int delta, int relX, int relY) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            int absX, absY;
            if (relX >= 0 && relY >= 0) {
                POINT pt = new POINT { x = relX, y = relY };
                if (!ClientToScreen(targetHwnd, ref pt)) {
                    RECT r;
                    GetWindowRect(targetHwnd, out r);
                    pt.x = r.Left + relX;
                    pt.y = r.Top + relY;
                }
                absX = pt.x;
                absY = pt.y;
            } else {
                RECT r;
                GetWindowRect(targetHwnd, out r);
                absX = r.Left + (r.Right - r.Left) / 2;
                absY = r.Top + (r.Bottom - r.Top) / 2;
            }

            ForceForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(80);

            SetCursorPos(absX, absY);
            System.Threading.Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, unchecked((uint)delta), UIntPtr.Zero);

            Console.WriteLine(string.Format("{{\"success\": true, \"scrolled\": {0}, \"title\": \"{1}\"}}",
                delta, EscapeJson(actualTitle)));
        }

        static ushort ResolveVk(string key) {
            string k = key.ToLowerInvariant().Trim();
            if (k == "ctrl" || k == "control") return 0x11;
            if (k == "shift") return 0x10;
            if (k == "alt" || k == "menu") return 0x12;
            if (k == "win" || k == "windows" || k == "cmd") return 0x5B;
            if (k == "enter" || k == "return") return 0x0D;
            if (k == "esc" || k == "escape") return 0x1B;
            if (k == "tab") return 0x09;
            if (k == "space") return 0x20;
            if (k == "backspace" || k == "bksp") return 0x08;
            if (k == "delete" || k == "del") return 0x2E;
            if (k == "insert" || k == "ins") return 0x2D;
            if (k == "home") return 0x24;
            if (k == "end") return 0x23;
            if (k == "pageup" || k == "pgup") return 0x21;
            if (k == "pagedown" || k == "pgdn") return 0x22;
            if (k == "up") return 0x26;
            if (k == "down") return 0x28;
            if (k == "left") return 0x25;
            if (k == "right") return 0x27;
            if (k.Length >= 2 && k[0] == 'f' && char.IsDigit(k[1])) {
                int fNum;
                if (int.TryParse(k.Substring(1), out fNum) && fNum >= 1 && fNum <= 12) {
                    return (ushort)(0x70 + (fNum - 1));
                }
            }
            if (k.Length == 1) {
                char ch = char.ToUpperInvariant(k[0]);
                if (ch >= 'A' && ch <= 'Z') return (ushort)ch;
                if (ch >= '0' && ch <= '9') return (ushort)ch;
            }
            return 0;
        }

        static void HotkeyWindow(string titleFilter, string combo) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            ForceForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(80);

            string[] parts = combo.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            var modVks = new List<ushort>();
            ushort mainVk = 0;

            foreach (var part in parts) {
                ushort vk = ResolveVk(part);
                if (vk == 0x11 || vk == 0x10 || vk == 0x12 || vk == 0x5B) {
                    modVks.Add(vk);
                } else if (vk != 0) {
                    mainVk = vk;
                }
            }

            if (mainVk != 0 || modVks.Count > 0) {
                // 1. Modifiers down
                foreach (var m in modVks) {
                    INPUT[] inMod = new INPUT[1];
                    inMod[0].type = INPUT_KEYBOARD;
                    inMod[0].mkhi.ki.wVk = m;
                    SendInput(1, inMod, Marshal.SizeOf(typeof(INPUT)));
                    System.Threading.Thread.Sleep(15);
                }

                // 2. Main key down
                if (mainVk != 0) {
                    INPUT[] inDown = new INPUT[1];
                    inDown[0].type = INPUT_KEYBOARD;
                    inDown[0].mkhi.ki.wVk = mainVk;
                    SendInput(1, inDown, Marshal.SizeOf(typeof(INPUT)));
                    System.Threading.Thread.Sleep(50); // realistic key hold

                    INPUT[] inUp = new INPUT[1];
                    inUp[0].type = INPUT_KEYBOARD;
                    inUp[0].mkhi.ki.wVk = mainVk;
                    inUp[0].mkhi.ki.dwFlags = KEYEVENTF_KEYUP;
                    SendInput(1, inUp, Marshal.SizeOf(typeof(INPUT)));
                    System.Threading.Thread.Sleep(20);
                }

                // 3. Modifiers up (reverse order)
                for (int i = modVks.Count - 1; i >= 0; i--) {
                    INPUT[] inModUp = new INPUT[1];
                    inModUp[0].type = INPUT_KEYBOARD;
                    inModUp[0].mkhi.ki.wVk = modVks[i];
                    inModUp[0].mkhi.ki.dwFlags = KEYEVENTF_KEYUP;
                    SendInput(1, inModUp, Marshal.SizeOf(typeof(INPUT)));
                    System.Threading.Thread.Sleep(15);
                }

                Console.WriteLine(string.Format("{{\"success\": true, \"hotkey\": \"{0}\", \"title\": \"{1}\"}}",
                    combo, EscapeJson(actualTitle)));
            } else {
                SendKeys.SendWait(combo);
                Console.WriteLine(string.Format("{{\"success\": true, \"hotkey\": \"{0}\", \"title\": \"{1}\"}}",
                    combo, EscapeJson(actualTitle)));
            }
        }

        static void CaptureWindow(string titleFilter, string destPath) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd = IntPtr.Zero;
            string actualTitle = "Desktop";
            bool fullDesktop = string.IsNullOrEmpty(titleFilter) ||
                               titleFilter.Equals("desktop", StringComparison.OrdinalIgnoreCase) ||
                               titleFilter.Equals("screen", StringComparison.OrdinalIgnoreCase);

            RECT r = new RECT();
            if (!fullDesktop) {
                if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                    return;
                }
                ForceForegroundWindow(targetHwnd);
                System.Threading.Thread.Sleep(60);
                GetWindowRect(targetHwnd, out r);
            } else {
                r.Left = 0;
                r.Top = 0;
                r.Right = GetSystemMetrics(78); // SM_CXVIRTUALSCREEN
                r.Bottom = GetSystemMetrics(79); // SM_CYVIRTUALSCREEN
                if (r.Right <= 0 || r.Bottom <= 0) {
                    r.Right = GetSystemMetrics(0);
                    r.Bottom = GetSystemMetrics(1);
                }
            }

            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            if (w <= 0 || h <= 0) {
                Console.WriteLine("{\"success\": false, \"error\": \"Invalid window geometry\"}");
                return;
            }

            string captureMethod = "dxgi_hardware_duplication";
            bool captured = false;

            // Tier 1: Hardware DirectX 11 Desktop Duplication (sub-2ms VRAM direct)
            try {
                captured = DxgiCaptureEngine.CaptureRegion(r.Left, r.Top, w, h, destPath);
            } catch (Exception ex) {
                Console.Error.WriteLine("DXGI Tier 1 fallback: " + ex.Message);
                captured = false;
            }

            // Tier 2 & Tier 3 Fallbacks if DXGI unavailable or unsupported in environment
            if (!captured) {
                try {
                    using (var bmp = new Bitmap(w, h)) {
                        using (var g = Graphics.FromImage(bmp)) {
                            bool capturedDirect = false;
                            try {
                                int screenX = GetSystemMetrics(76);
                                int screenY = GetSystemMetrics(77);
                                int screenW = GetSystemMetrics(78);
                                int screenH = GetSystemMetrics(79);
                                if (screenW <= 0 || screenH <= 0) {
                                    screenX = 0; screenY = 0;
                                    screenW = GetSystemMetrics(0);
                                    screenH = GetSystemMetrics(1);
                                }

                                int srcX = Math.Max(screenX, r.Left);
                                int srcY = Math.Max(screenY, r.Top);
                                int destX = Math.Max(0, srcX - r.Left);
                                int destY = Math.Max(0, srcY - r.Top);
                                int right = Math.Min(screenX + screenW, r.Right);
                                int bottom = Math.Min(screenY + screenH, r.Bottom);
                                int copyW = Math.Max(0, right - srcX);
                                int copyH = Math.Max(0, bottom - srcY);

                                if (copyW > 0 && copyH > 0) {
                                    g.CopyFromScreen(srcX, srcY, destX, destY, new Size(copyW, copyH), CopyPixelOperation.SourceCopy);
                                    capturedDirect = true;
                                    captureMethod = "direct_gdi";
                                }
                            } catch {}

                            if (!capturedDirect && targetHwnd != IntPtr.Zero) {
                                IntPtr hdc = g.GetHdc();
                                bool pwOk = PrintWindow(targetHwnd, hdc, 2); // PW_RENDERFULLCONTENT
                                g.ReleaseHdc(hdc);
                                if (pwOk) {
                                    captureMethod = "printwindow";
                                } else {
                                    g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
                                    captureMethod = "copy_screen_fallback";
                                }
                            }
                        }

                        string dir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                            Directory.CreateDirectory(dir);
                        }
                        bmp.Save(destPath, ImageFormat.Png);
                        captured = true;
                    }
                } catch (Exception ex) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
                    return;
                }
            }

            if (captured) {
                string escTitle = EscapeJson(actualTitle);
                string escPath = destPath.Replace("\\", "/");
                Console.WriteLine(string.Format("{{\"success\": true, \"title\": \"{0}\", \"width\": {1}, \"height\": {2}, \"method\": \"{3}\", \"fore\": \"{4}\", \"target\": \"{5}\", \"path\": \"{6}\"}}",
                    escTitle, w, h, captureMethod, GetForegroundWindow(), targetHwnd, escPath));
            } else {
                Console.WriteLine("{\"success\": false, \"error\": \"All 3 capture tiers failed\"}");
            }
        }

        static void ScanImageWhitePixels(string imagePath, int rThresh, int gThresh, int bThresh) {
            if (!File.Exists(imagePath)) {
                Console.WriteLine("{\"error\": \"Image file not found\"}");
                return;
            }
            using (var bmp = new Bitmap(imagePath)) {
                int minX = int.MaxValue, maxX = int.MinValue;
                int minY = int.MaxValue, maxY = int.MinValue;
                int count = 0;
                for (int y = 0; y < bmp.Height; y++) {
                    for (int x = 0; x < bmp.Width; x++) {
                        Color c = bmp.GetPixel(x, y);
                        if (c.R >= rThresh && c.G >= gThresh && c.B >= bThresh) {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            count++;
                        }
                    }
                }
                if (count > 0) {
                    Console.WriteLine(string.Format("{{\"count\": {0}, \"minX\": {1}, \"maxX\": {2}, \"minY\": {3}, \"maxY\": {4}, \"centerX\": {5}, \"centerY\": {6}}}",
                        count, minX, maxX, minY, maxY, (minX + maxX) / 2, (minY + maxY) / 2));
                } else {
                    Console.WriteLine("{\"count\": 0}");
                }
            }
        }

        static CacheRequest CreateStandardCacheRequest() {
            CacheRequest req = new CacheRequest();
            req.Add(AutomationElement.NameProperty);
            req.Add(AutomationElement.BoundingRectangleProperty);
            req.Add(AutomationElement.ControlTypeProperty);
            req.Add(AutomationElement.AutomationIdProperty);
            req.Add(AutomationElement.IsOffscreenProperty);
            req.TreeScope = TreeScope.Element | TreeScope.Descendants;
            return req;
        }

        static void ListUIElements(string titleFilter) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;
            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            try {
                AutomationElement root = AutomationElement.FromHandle(targetHwnd);
                if (root == null) {
                    Console.WriteLine("[]");
                    return;
                }

                RECT winRect;
                GetWindowRect(targetHwnd, out winRect);

                AutomationElementCollection elements;
                try {
                    CacheRequest cache = CreateStandardCacheRequest();
                    using (cache.Activate()) {
                        elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                    }
                } catch {
                    elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                }

                var list = new List<string>();
                int maxCount = Math.Min(elements.Count, 600);
                for (int i = 0; i < maxCount; i++) {
                    var el = elements[i];
                    try {
                        System.Windows.Rect r;
                        string name;
                        string type;
                        string autoId;
                        bool isOffscreen;

                        try {
                            r = el.Cached.BoundingRectangle;
                            name = el.Cached.Name ?? "";
                            type = el.Cached.ControlType != null ? el.Cached.ControlType.ProgrammaticName.Replace("ControlType.", "") : "";
                            autoId = el.Cached.AutomationId ?? "";
                            isOffscreen = el.Cached.IsOffscreen;
                        } catch {
                            r = el.Current.BoundingRectangle;
                            name = el.Current.Name ?? "";
                            type = el.Current.ControlType != null ? el.Current.ControlType.ProgrammaticName.Replace("ControlType.", "") : "";
                            autoId = el.Current.AutomationId ?? "";
                            isOffscreen = el.Current.IsOffscreen;
                        }

                        if (!isOffscreen && r.Width > 0 && r.Height > 0 && (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(autoId))) {
                            int relX = (int)r.X - winRect.Left;
                            int relY = (int)r.Y - winRect.Top;
                            int centerX = (int)r.X + (int)r.Width / 2;
                            int centerY = (int)r.Y + (int)r.Height / 2;
                            list.Add(string.Format("{{\"name\": \"{0}\", \"type\": \"{1}\", \"autoId\": \"{2}\", \"x\": {3}, \"y\": {4}, \"width\": {5}, \"height\": {6}, \"relX\": {7}, \"relY\": {8}, \"centerX\": {9}, \"centerY\": {10}}}",
                                EscapeJson(name), EscapeJson(type), EscapeJson(autoId),
                                (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height,
                                relX, relY, centerX, centerY));
                        }
                    } catch {}
                }
                Console.WriteLine("[" + string.Join(",", list.ToArray()) + "]");
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static bool FindMatchingElement(IntPtr targetHwnd, string query, out RECT r, out string matchedName, out string matchedType) {
            r = new RECT();
            matchedName = "";
            matchedType = "";
            try {
                AutomationElement root = AutomationElement.FromHandle(targetHwnd);
                if (root == null) return false;

                AutomationElementCollection elements;
                try {
                    CacheRequest cache = CreateStandardCacheRequest();
                    using (cache.Activate()) {
                        elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                    }
                } catch {
                    elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                }

                int count = elements.Count;
                // Exact match pass
                for (int i = 0; i < count; i++) {
                    var el = elements[i];
                    try {
                        bool isOffscreen = false;
                        System.Windows.Rect rect;
                        string name;
                        string autoId;
                        string ctrlType;

                        try {
                            isOffscreen = el.Cached.IsOffscreen;
                            rect = el.Cached.BoundingRectangle;
                            name = el.Cached.Name ?? "";
                            autoId = el.Cached.AutomationId ?? "";
                            ctrlType = el.Cached.ControlType != null ? el.Cached.ControlType.ProgrammaticName.Replace("ControlType.", "") : "";
                        } catch {
                            isOffscreen = el.Current.IsOffscreen;
                            rect = el.Current.BoundingRectangle;
                            name = el.Current.Name ?? "";
                            autoId = el.Current.AutomationId ?? "";
                            ctrlType = el.Current.ControlType != null ? el.Current.ControlType.ProgrammaticName.Replace("ControlType.", "") : "";
                        }

                        if (isOffscreen || rect.Width <= 0 || rect.Height <= 0) continue;
                        if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase) || string.Equals(autoId, query, StringComparison.OrdinalIgnoreCase)) {
                            r.Left = (int)rect.X;
                            r.Top = (int)rect.Y;
                            r.Right = (int)(rect.X + rect.Width);
                            r.Bottom = (int)(rect.Y + rect.Height);
                            matchedName = name;
                            matchedType = ctrlType;
                            return true;
                        }
                    } catch {}
                }

                // Substring match pass
                for (int i = 0; i < count; i++) {
                    var el = elements[i];
                    try {
                        bool isOffscreen = false;
                        System.Windows.Rect rect;
                        string name;
                        string autoId;
                        string ctrlType;

                        try {
                            isOffscreen = el.Cached.IsOffscreen;
                            rect = el.Cached.BoundingRectangle;
                            name = el.Cached.Name ?? "";
                            autoId = el.Cached.AutomationId ?? "";
                            ctrlType = el.Cached.ControlType != null ? el.Cached.ControlType.ProgrammaticName.Replace("ControlType.", "") : "";
                        } catch {
                            isOffscreen = el.Current.IsOffscreen;
                            rect = el.Current.BoundingRectangle;
                            name = el.Current.Name ?? "";
                            autoId = el.Current.AutomationId ?? "";
                            ctrlType = el.Current.ControlType != null ? el.Current.ControlType.ProgrammaticName.Replace("ControlType.", "") : "";
                        }

                        if (isOffscreen || rect.Width <= 0 || rect.Height <= 0) continue;
                        if (name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || autoId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) {
                            r.Left = (int)rect.X;
                            r.Top = (int)rect.Y;
                            r.Right = (int)(rect.X + rect.Width);
                            r.Bottom = (int)(rect.Y + rect.Height);
                            matchedName = name;
                            matchedType = ctrlType;
                            return true;
                        }
                    } catch {}
                }
            } catch {}
            return false;
        }

        static void FindUIElementCmd(string titleFilter, string query) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;
            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            RECT winRect;
            GetWindowRect(targetHwnd, out winRect);

            RECT elRect;
            string name, type;
            if (FindMatchingElement(targetHwnd, query, out elRect, out name, out type)) {
                int w = elRect.Right - elRect.Left;
                int h = elRect.Bottom - elRect.Top;
                int centerX = elRect.Left + w / 2;
                int centerY = elRect.Top + h / 2;
                POINT ptCenter = new POINT { x = centerX, y = centerY };
                ScreenToClient(targetHwnd, ref ptCenter);
                int relX = ptCenter.x;
                int relY = ptCenter.y;
                Console.WriteLine(string.Format("{{\"success\": true, \"name\": \"{0}\", \"type\": \"{1}\", \"x\": {2}, \"y\": {3}, \"width\": {4}, \"height\": {5}, \"relX\": {6}, \"relY\": {7}, \"centerX\": {8}, \"centerY\": {9}}}",
                    EscapeJson(name), EscapeJson(type), elRect.Left, elRect.Top, w, h, relX, relY, centerX, centerY));
            } else {
                Console.WriteLine("{\"success\": false, \"error\": \"Element not found\"}");
            }
        }

        static void ClickUIElementCmd(string titleFilter, string query, string button) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;
            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            RECT winRect;
            GetWindowRect(targetHwnd, out winRect);

            RECT elRect;
            string name, type;
            if (!FindMatchingElement(targetHwnd, query, out elRect, out name, out type)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Element not found\"}");
                return;
            }

            int w = elRect.Right - elRect.Left;
            int h = elRect.Bottom - elRect.Top;
            int centerX = elRect.Left + w / 2;
            int centerY = elRect.Top + h / 2;

            ForceForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(80);

            string b = (button ?? "left").ToLowerInvariant();
            uint downFlag = MOUSEEVENTF_LEFTDOWN;
            uint upFlag = MOUSEEVENTF_LEFTUP;
            if (b == "right") { downFlag = MOUSEEVENTF_RIGHTDOWN; upFlag = MOUSEEVENTF_RIGHTUP; }
            else if (b == "middle") { downFlag = MOUSEEVENTF_MIDDLEDOWN; upFlag = MOUSEEVENTF_MIDDLEUP; }

            using (new AtomicInputLock()) {
                SetCursorPos(centerX, centerY);
                System.Threading.Thread.Sleep(30);
                mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                System.Threading.Thread.Sleep(30);
                mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);

                if (b == "double") {
                    System.Threading.Thread.Sleep(50);
                    mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                    System.Threading.Thread.Sleep(30);
                    mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                }
            }

            Console.WriteLine(string.Format("{{\"success\": true, \"clicked\": \"{0}\", \"type\": \"{1}\", \"centerX\": {2}, \"centerY\": {3}, \"title\": \"{4}\"}}",
                EscapeJson(name), EscapeJson(type), centerX, centerY, EscapeJson(actualTitle)));
        }
    }
}

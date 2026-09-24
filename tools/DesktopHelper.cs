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
using System.Net;
using System.Net.NetworkInformation;

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
        public RECT FrameRect;
        public bool IsForeground;
        public bool IsMinimized;
        public bool IsMaximized;
        public bool IsHung;
        public bool IsElevated;
        public bool IsCloaked;
        public int ZOrder;
        public string MonitorDevice;
        public bool IsPrimaryMonitor;
        public bool IsTopmost;
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

        public static Bitmap CaptureBitmap(int cropX, int cropY, int cropW, int cropH) {
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
                if (hr != 0) return null;

                var enumAdapters = GetVtable<EnumAdapters1Delegate>(pFactory, 7);
                hr = enumAdapters(pFactory, 0, out pAdapter);
                if (hr != 0) return null;

                int fl;
                hr = D3D11CreateDevice(pAdapter, 0, IntPtr.Zero, 0, IntPtr.Zero, 0, 7, out pDevice, out fl, out pContext);
                if (hr != 0) return null;

                var enumOutputs = GetVtable<EnumOutputsDelegate>(pAdapter, 7);
                hr = enumOutputs(pAdapter, 0, out pOutput);
                if (hr != 0) return null;

                var qi = GetVtable<QueryInterfaceDelegate>(pOutput, 0);
                Guid output1Guid = IID_IDXGIOutput1;
                hr = qi(pOutput, ref output1Guid, out pOutput1);
                if (hr != 0) return null;

                var duplicateOutput = GetVtable<DuplicateOutputDelegate>(pOutput1, 22);
                hr = duplicateOutput(pOutput1, pDevice, out pDuplication);
                if (hr != 0) return null;

                var acquireNextFrame = GetVtable<AcquireNextFrameDelegate>(pDuplication, 8);
                var releaseFrame = GetVtable<ReleaseFrameDelegate>(pDuplication, 14);
                byte[] frameInfo = new byte[64];

                for (int attempt = 0; attempt < 3; attempt++) {
                    hr = acquireNextFrame(pDuplication, 40, frameInfo, out pResource);
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
                    Thread.Sleep(5);
                    Program.SetCursorPos(pt.x, pt.y);
                    Thread.Sleep(5);
                }

                if (hr != 0 || pResource == IntPtr.Zero) return null;

                var resQi = GetVtable<QueryInterfaceDelegate>(pResource, 0);
                Guid tex2dGuid = IID_ID3D11Texture2D;
                hr = resQi(pResource, ref tex2dGuid, out pDesktopTexture);
                if (hr != 0) return null;

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
                if (hr != 0) return null;

                var copyResource = GetVtable<CopyResourceDelegate>(pContext, 47);
                copyResource(pContext, pStagingTexture, pDesktopTexture);

                var map = GetVtable<MapDelegate>(pContext, 14);
                D3D11_MAPPED_SUBRESOURCE mapped = new D3D11_MAPPED_SUBRESOURCE();
                hr = map(pContext, pStagingTexture, 0, 1 /* D3D11_MAP_READ */, 0, out mapped);
                if (hr != 0) return null;

                int texW = (int)texDesc.Width;
                int texH = (int)texDesc.Height;

                Bitmap resultBmp = null;
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
                        resultBmp = fullBmp.Clone(cropRect, PixelFormat.Format32bppRgb);
                    } else {
                        resultBmp = new Bitmap(fullBmp);
                    }
                }

                var unmap = GetVtable<UnmapDelegate>(pContext, 15);
                unmap(pContext, pStagingTexture, 0);

                return resultBmp;
            } catch {
                return null;
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

        public static bool CaptureRegion(int cropX, int cropY, int cropW, int cropH, string destPath) {
            try {
                using (Bitmap bmp = CaptureBitmap(cropX, cropY, cropW, cropH)) {
                    if (bmp == null) return false;
                    string outDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir)) {
                        Directory.CreateDirectory(outDir);
                    }
                    bmp.Save(destPath, ImageFormat.Png);
                    return true;
                }
            } catch {
                return false;
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

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out RECT pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

        const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        const uint DWMWA_CLOAKED = 14;
        const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return GetWindowLongPtr32(hWnd, nIndex);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_MEMORY_COUNTERS_EX {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
            public UIntPtr PrivateUsage;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX counters, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetProcessTimes(IntPtr hProcess, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        [DllImport("user32.dll")]
        static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct DEVMODE {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        const int ENUM_CURRENT_SETTINGS = -1;

        [DllImport("user32.dll")]
        static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorComObject {}

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr deviceCollection);
            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
            int GetDevice(string pwstrId, out IMMDevice endpoint);
            int RegisterEndpointNotificationCallback(IntPtr pClient);
            int UnregisterEndpointNotificationCallback(IntPtr pClient);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            int GetState(out int pdwState);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume {
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out uint pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig]
            int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            [PreserveSig]
            int GetMasterVolumeLevelScalar(out float pfLevel);
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            [PreserveSig]
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
            [PreserveSig]
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorInfo(out CURSORINFO pci);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        [StructLayout(LayoutKind.Sequential)]
        public struct PERFORMANCE_INFORMATION {
            public uint cb;
            public UIntPtr CommitTotal;
            public UIntPtr CommitLimit;
            public UIntPtr CommitPeak;
            public UIntPtr PhysicalTotal;
            public UIntPtr PhysicalAvailable;
            public UIntPtr SystemCache;
            public UIntPtr KernelTotal;
            public UIntPtr KernelPaged;
            public UIntPtr KernelNonpaged;
            public UIntPtr PageSize;
            public uint HandleCount;
            public uint ProcessCount;
            public uint ThreadCount;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, uint cb);

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetProcessAffinityMask(IntPtr hProcess, UIntPtr dwProcessAffinityMask);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetProcessAffinityMask(IntPtr hProcess, out UIntPtr lpProcessAffinityMask, out UIntPtr lpSystemAffinityMask);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("powrprof.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint PowerReadFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid, IntPtr PowerSettingGuid, StringBuilder Buffer, ref uint BufferSize);

        [DllImport("kernel32.dll")]
        public static extern IntPtr LocalFree(IntPtr hMem);

        // IP Helper (iphlpapi.dll) Socket Table APIs
        public enum TCP_TABLE_CLASS {
            TCP_TABLE_BASIC_LISTENER = 0,
            TCP_TABLE_BASIC_CONNECTIONS = 1,
            TCP_TABLE_BASIC_ALL = 2,
            TCP_TABLE_OWNER_PID_LISTENER = 3,
            TCP_TABLE_OWNER_PID_CONNECTIONS = 4,
            TCP_TABLE_OWNER_PID_ALL = 5,
            TCP_TABLE_OWNER_MODULE_LISTENER = 6,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS = 7,
            TCP_TABLE_OWNER_MODULE_ALL = 8
        }

        public enum UDP_TABLE_CLASS {
            UDP_TABLE_BASIC,
            UDP_TABLE_OWNER_PID,
            UDP_TABLE_OWNER_MODULE
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPROW_OWNER_PID {
            public uint dwState;
            public uint dwLocalAddr;
            public uint dwLocalPort;
            public uint dwRemoteAddr;
            public uint dwRemotePort;
            public uint dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCP6ROW_OWNER_PID {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public uint dwLocalScopeId;
            public uint dwLocalPort;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucRemoteAddr;
            public uint dwRemoteScopeId;
            public uint dwRemotePort;
            public uint dwState;
            public uint dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_UDPROW_OWNER_PID {
            public uint dwLocalAddr;
            public uint dwLocalPort;
            public uint dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_UDP6ROW_OWNER_PID {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public uint dwLocalScopeId;
            public uint dwLocalPort;
            public uint dwOwningPid;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, TCP_TABLE_CLASS TableClass, uint Reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int pdwSize, bool bOrder, int ulAf, UDP_TABLE_CLASS TableClass, uint Reserved);

        // Power Setting API (powrprof.dll)
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

        // Job Object APIs (kernel32.dll)
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInformationClass, IntPtr lpJobObjectInformation, uint cbJobObjectInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool QueryInformationJobObject(IntPtr hJob, int JobObjectInformationClass, IntPtr lpJobObjectInformation, uint cbJobObjectInformationLength, out uint lpReturnLength);

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION {
            public uint ControlFlags;
            public uint CpuRate;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct USN_JOURNAL_DATA_V0 {
            public ulong UsnJournalID;
            public long FirstUsn;
            public long NextUsn;
            public long LowestValidUsn;
            public long MaxUsn;
            public ulong MaximumSize;
            public ulong AllocationDelta;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, int wtsInfoClass, out IntPtr ppBuffer, out int pBytesReturned);

        [DllImport("wtsapi32.dll")]
        static extern void WTSFreeMemory(IntPtr pMemory);

        [StructLayout(LayoutKind.Sequential)]
        struct WTS_CLIENT_DISPLAY {
            public uint HorizontalResolution;
            public uint VerticalResolution;
            public uint ColorDepth;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WTS_CLIENT_ADDRESS {
            public uint AddressFamily;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] Address;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OSVERSIONINFOEX {
            public int dwOSVersionInfoSize;
            public uint dwMajorVersion;
            public uint dwMinorVersion;
            public uint dwBuildNumber;
            public uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int RtlGetVersion(ref OSVERSIONINFOEX versionInfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        const int SW_HIDE = 0;
        const int SW_SHOWNORMAL = 1;
        const int SW_SHOWMINIMIZED = 2;
        const int SW_SHOWMAXIMIZED = 3;
        const int SW_SHOWNOACTIVATE = 4;
        const int SW_SHOW = 5;
        const int SW_RESTORE = 9;
        const int WPF_RESTORETOMAXIMIZED = 0x0002;

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
                else if (c == '"') sb.Append("\\\"");
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
                        Clipboard.SetDataObject(text, true, 5, 50);
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

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
        static extern bool SystemParametersInfoRect(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        const uint SPI_GETWORKAREA = 0x0030;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_SHOWWINDOW = 0x0040;

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

        static void RunSta(Action action) {
            Exception threadEx = null;
            Thread t = new Thread(() => {
                try { action(); }
                catch (Exception ex) { threadEx = ex; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(3000);
            if (threadEx != null) throw threadEx;
        }

        static void MoveWindowCmd(string titleFilter, int x, int y, int w, int h) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;
            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            ShowWindow(targetHwnd, SW_RESTORE);
            Thread.Sleep(40);
            bool ok = MoveWindow(targetHwnd, x, y, w, h, true);
            if (!ok) {
                ok = SetWindowPos(targetHwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }

            RECT r;
            GetWindowRect(targetHwnd, out r);
            Console.WriteLine(string.Format("{{\"success\": {0}, \"title\": \"{1}\", \"x\": {2}, \"y\": {3}, \"width\": {4}, \"height\": {5}}}",
                ok ? "true" : "false", EscapeJson(actualTitle), r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top));
        }

        static void GetWorkAreaCmd() {
            RECT r = new RECT();
            SystemParametersInfoRect(SPI_GETWORKAREA, 0, ref r, 0);
            int screenW = GetSystemMetrics(0);
            int screenH = GetSystemMetrics(1);
            int virtX = GetSystemMetrics(76);
            int virtY = GetSystemMetrics(77);
            int virtW = GetSystemMetrics(78);
            int virtH = GetSystemMetrics(79);

            var monitorList = new List<string>();
            try {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rcMon, IntPtr data) => {
                    MONITORINFOEX mi = new MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                    if (GetMonitorInfo(hMon, ref mi)) {
                        bool isPrimary = (mi.dwFlags & 1) != 0;
                        int mW = mi.rcMonitor.Right - mi.rcMonitor.Left;
                        int mH = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
                        int wW = mi.rcWork.Right - mi.rcWork.Left;
                        int wH = mi.rcWork.Bottom - mi.rcWork.Top;
                        monitorList.Add(string.Format("{{\"device\": \"{0}\", \"isPrimary\": {1}, \"bounds\": {{\"x\": {2}, \"y\": {3}, \"width\": {4}, \"height\": {5}}}, \"workArea\": {{\"x\": {6}, \"y\": {7}, \"width\": {8}, \"height\": {9}}}}}",
                            EscapeJson(mi.szDevice), isPrimary ? "true" : "false",
                            mi.rcMonitor.Left, mi.rcMonitor.Top, mW, mH,
                            mi.rcWork.Left, mi.rcWork.Top, wW, wH));
                    }
                    return true;
                }, IntPtr.Zero);
            } catch {}

            Console.WriteLine(string.Format("{{\"success\": true, \"screenW\": {0}, \"screenH\": {1}, \"workX\": {2}, \"workY\": {3}, \"workW\": {4}, \"workH\": {5}, \"virtX\": {6}, \"virtY\": {7}, \"virtW\": {8}, \"virtH\": {9}, \"monitors\": [{10}]}}",
                screenW, screenH, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                virtX, virtY, virtW, virtH,
                string.Join(",", monitorList.ToArray())));
        }

        static void GetVitalsCmd() {
            try {
                MEMORYSTATUSEX msex = new MEMORYSTATUSEX();
                msex.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                bool memOk = GlobalMemoryStatusEx(ref msex);

                SYSTEM_POWER_STATUS pwr = new SYSTEM_POWER_STATUS();
                bool pwrOk = GetSystemPowerStatus(out pwr);

                ulong uptimeMs = GetTickCount64();
                ulong uptimeSec = uptimeMs / 1000UL;

                uint colorization = 0;
                bool opaque = false;
                string accentHex = "#0078D7";
                try {
                    if (DwmGetColorizationColor(out colorization, out opaque) == 0) {
                        accentHex = string.Format("#{0:X6}", colorization & 0x00FFFFFF);
                    }
                } catch {}

                string acStr = "Unknown";
                if (pwr.ACLineStatus == 0) acStr = "Offline";
                else if (pwr.ACLineStatus == 1) acStr = "Online";

                string batStatus = "Unknown";
                if ((pwr.BatteryFlag & 128) != 0) batStatus = "NoBattery";
                else if ((pwr.BatteryFlag & 8) != 0) batStatus = "Charging";
                else if ((pwr.BatteryFlag & 4) != 0) batStatus = "Critical";
                else if ((pwr.BatteryFlag & 2) != 0) batStatus = "Low";
                else if ((pwr.BatteryFlag & 1) != 0) batStatus = "High";

                int batPct = (pwr.BatteryLifePercent <= 100) ? pwr.BatteryLifePercent : -1;
                long batTimeSec = (pwr.BatteryLifeTime != 0xFFFFFFFF) ? (long)pwr.BatteryLifeTime : -1L;

                ulong totalPhysMB = memOk ? (msex.ullTotalPhys / (1024UL * 1024UL)) : 0UL;
                ulong availPhysMB = memOk ? (msex.ullAvailPhys / (1024UL * 1024UL)) : 0UL;
                ulong usedPhysMB = (totalPhysMB > availPhysMB) ? (totalPhysMB - availPhysMB) : 0UL;
                ulong totalPageMB = memOk ? (msex.ullTotalPageFile / (1024UL * 1024UL)) : 0UL;
                ulong availPageMB = memOk ? (msex.ullAvailPageFile / (1024UL * 1024UL)) : 0UL;
                ulong totalVirtMB = memOk ? (msex.ullTotalVirtual / (1024UL * 1024UL)) : 0UL;
                ulong availVirtMB = memOk ? (msex.ullAvailVirtual / (1024UL * 1024UL)) : 0UL;

                string exactOsVersion = Environment.OSVersion.VersionString;
                try {
                    var osInfo = new OSVERSIONINFOEX();
                    osInfo.dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFOEX));
                    if (RtlGetVersion(ref osInfo) == 0) {
                        exactOsVersion = string.Format("Windows {0}.{1} (Build {2})", osInfo.dwMajorVersion, osInfo.dwMinorVersion, osInfo.dwBuildNumber);
                    }
                } catch {}

                bool isDarkMode = true;
                bool enableTransparency = true;
                try {
                    object appTheme = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0);
                    if (appTheme is int && ((int)appTheme) == 1) isDarkMode = false;
                    object trans = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1);
                    if (trans is int && ((int)trans) == 0) enableTransparency = false;
                } catch {}

                string json = string.Format("{{\"success\": true, \"memory\": {{\"loadPercent\": {0}, \"totalPhysicalMB\": {1}, \"availPhysicalMB\": {2}, \"usedPhysicalMB\": {3}, \"totalPageFileMB\": {4}, \"availPageFileMB\": {5}, \"totalVirtualMB\": {6}, \"availVirtualMB\": {7}}}, \"power\": {{\"acLineStatus\": {8}, \"acStatus\": \"{9}\", \"batteryFlag\": {10}, \"batteryStatus\": \"{11}\", \"batteryLifePercent\": {12}, \"batterySaver\": {13}, \"batteryLifeTimeSeconds\": {14}}}, \"system\": {{\"uptimeSeconds\": {15}, \"uptimeMs\": {16}, \"processorCount\": {17}, \"machineName\": \"{18}\", \"osVersion\": \"{19}\", \"exactOsVersion\": \"{20}\", \"darkMode\": {21}, \"transparency\": {22}, \"is64BitOS\": {23}, \"isElevated\": {24}, \"accentColor\": \"{25}\"}}}}",
                    memOk ? msex.dwMemoryLoad : 0, totalPhysMB, availPhysMB, usedPhysMB, totalPageMB, availPageMB, totalVirtMB, availVirtMB,
                    pwr.ACLineStatus, acStr, pwr.BatteryFlag, batStatus, batPct, (pwr.SystemStatusFlag == 1) ? "true" : "false", batTimeSec,
                    uptimeSec, uptimeMs, Environment.ProcessorCount, EscapeJson(Environment.MachineName), EscapeJson(Environment.OSVersion.VersionString),
                    EscapeJson(exactOsVersion), isDarkMode ? "true" : "false", enableTransparency ? "true" : "false",
                    Environment.Is64BitOperatingSystem ? "true" : "false", IsCurrentProcessElevated() ? "true" : "false", accentHex);

                Console.WriteLine(json);
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetCursorInfoCmd() {
            try {
                CURSORINFO ci = new CURSORINFO();
                ci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
                if (GetCursorInfo(out ci)) {
                    bool isVisible = (ci.flags & 1) != 0;
                    bool isSuppressed = (ci.flags & 2) != 0;
                    Console.WriteLine(string.Format("{{\"success\": true, \"x\": {0}, \"y\": {1}, \"isVisible\": {2}, \"isSuppressed\": {3}, \"hCursor\": \"0x{4:X}\"}}",
                        ci.ptScreenPos.x, ci.ptScreenPos.y, isVisible ? "true" : "false", isSuppressed ? "true" : "false", ci.hCursor.ToInt64()));
                } else {
                    POINT pt;
                    GetCursorPos(out pt);
                    Console.WriteLine(string.Format("{{\"success\": true, \"x\": {0}, \"y\": {1}, \"isVisible\": true, \"isSuppressed\": false, \"hCursor\": \"0x0\"}}",
                        pt.x, pt.y));
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetAudioVolumeCmd() {
            try {
                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice dev;
                int hr = enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out dev);
                if (hr != 0 || dev == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to get audio endpoint (0x{0:X})\"}}", hr));
                    return;
                }
                Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
                object obj;
                hr = dev.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out obj);
                if (hr != 0 || obj == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to activate audio volume (0x{0:X})\"}}", hr));
                    return;
                }
                var vol = (IAudioEndpointVolume)obj;
                float level;
                vol.GetMasterVolumeLevelScalar(out level);
                bool mute;
                vol.GetMute(out mute);
                int pct = (int)Math.Round(level * 100f);
                Console.WriteLine(string.Format("{{\"success\": true, \"volume\": {0}, \"scalar\": {1:F3}, \"isMuted\": {2}}}",
                    pct, level, mute ? "true" : "false"));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void SetAudioVolumeCmd(float percent) {
            try {
                if (percent < 0f) percent = 0f;
                if (percent > 100f) percent = 100f;
                float scalar = percent / 100.0f;

                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice dev;
                int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out dev);
                if (hr != 0 || dev == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to get audio endpoint (0x{0:X})\"}}", hr));
                    return;
                }
                Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
                object obj;
                hr = dev.Activate(ref iid, 1, IntPtr.Zero, out obj);
                if (hr != 0 || obj == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to activate audio volume (0x{0:X})\"}}", hr));
                    return;
                }
                var vol = (IAudioEndpointVolume)obj;
                Guid ctx = Guid.Empty;
                vol.SetMasterVolumeLevelScalar(scalar, ref ctx);
                bool mute;
                vol.GetMute(out mute);
                Console.WriteLine(string.Format("{{\"success\": true, \"volume\": {0}, \"scalar\": {1:F3}, \"isMuted\": {2}}}",
                    (int)Math.Round(percent), scalar, mute ? "true" : "false"));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void SetAudioMuteCmd(bool mute) {
            try {
                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice dev;
                int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out dev);
                if (hr != 0 || dev == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to get audio endpoint (0x{0:X})\"}}", hr));
                    return;
                }
                Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
                object obj;
                hr = dev.Activate(ref iid, 1, IntPtr.Zero, out obj);
                if (hr != 0 || obj == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to activate audio volume (0x{0:X})\"}}", hr));
                    return;
                }
                var vol = (IAudioEndpointVolume)obj;
                Guid ctx = Guid.Empty;
                vol.SetMute(mute, ref ctx);
                float level;
                vol.GetMasterVolumeLevelScalar(out level);
                int pct = (int)Math.Round(level * 100f);
                Console.WriteLine(string.Format("{{\"success\": true, \"isMuted\": {0}, \"volume\": {1}}}",
                    mute ? "true" : "false", pct));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void ToggleAudioMuteCmd() {
            try {
                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice dev;
                int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out dev);
                if (hr != 0 || dev == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to get audio endpoint (0x{0:X})\"}}", hr));
                    return;
                }
                Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
                object obj;
                hr = dev.Activate(ref iid, 1, IntPtr.Zero, out obj);
                if (hr != 0 || obj == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to activate audio volume (0x{0:X})\"}}", hr));
                    return;
                }
                var vol = (IAudioEndpointVolume)obj;
                bool currentMute;
                vol.GetMute(out currentMute);
                bool newMute = !currentMute;
                Guid ctx = Guid.Empty;
                vol.SetMute(newMute, ref ctx);
                float level;
                vol.GetMasterVolumeLevelScalar(out level);
                int pct = (int)Math.Round(level * 100f);
                Console.WriteLine(string.Format("{{\"success\": true, \"isMuted\": {0}, \"volume\": {1}}}",
                    newMute ? "true" : "false", pct));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetProcessVitalsCmd(string query) {
            try {
                Process targetProc = null;
                int pid = 0;
                if (int.TryParse(query, out pid)) {
                    try { targetProc = Process.GetProcessById(pid); } catch {}
                } else {
                    string cleanName = query.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? query.Substring(0, query.Length - 4) : query;
                    var procs = Process.GetProcessesByName(cleanName);
                    if (procs != null && procs.Length > 0) {
                        targetProc = procs[0];
                    } else {
                        var all = Process.GetProcesses();
                        foreach (var p in all) {
                            try {
                                if (p.ProcessName.IndexOf(cleanName, StringComparison.OrdinalIgnoreCase) >= 0) {
                                    targetProc = p;
                                    break;
                                }
                            } catch {}
                        }
                    }
                }

                if (targetProc == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Process not found: {0}\"}}", EscapeJson(query)));
                    return;
                }

                uint targetPid = (uint)targetProc.Id;
                string name = targetProc.ProcessName;
                bool responding = true;
                try { responding = targetProc.Responding; } catch {}
                int threads = 0;
                try { threads = targetProc.Threads.Count; } catch {}

                IntPtr hProc = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */ | 0x0400 /* PROCESS_VM_READ */, false, targetPid);
                if (hProc == IntPtr.Zero) {
                    try { hProc = targetProc.Handle; } catch {}
                }

                double wsMB = 0, peakWsMB = 0, privMB = 0, pagefileMB = 0;
                long kMs = 0, uMs = 0, totalCpuMs = 0;

                try {
                    if (hProc != IntPtr.Zero) {
                        PROCESS_MEMORY_COUNTERS_EX mem;
                        mem.cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS_EX));
                        if (GetProcessMemoryInfo(hProc, out mem, mem.cb)) {
                            wsMB = (ulong)mem.WorkingSetSize / (1024.0 * 1024.0);
                            peakWsMB = (ulong)mem.PeakWorkingSetSize / (1024.0 * 1024.0);
                            privMB = (ulong)mem.PrivateUsage / (1024.0 * 1024.0);
                            pagefileMB = (ulong)mem.PagefileUsage / (1024.0 * 1024.0);
                        }

                        long cTime, eTime, kTime, uTime;
                        if (GetProcessTimes(hProc, out cTime, out eTime, out kTime, out uTime)) {
                            kMs = kTime / 10000;
                            uMs = uTime / 10000;
                            totalCpuMs = kMs + uMs;
                        }
                    } else {
                        wsMB = targetProc.WorkingSet64 / (1024.0 * 1024.0);
                        peakWsMB = targetProc.PeakWorkingSet64 / (1024.0 * 1024.0);
                        privMB = targetProc.PrivateMemorySize64 / (1024.0 * 1024.0);
                        totalCpuMs = (long)targetProc.TotalProcessorTime.TotalMilliseconds;
                        uMs = (long)targetProc.UserProcessorTime.TotalMilliseconds;
                        kMs = (long)targetProc.PrivilegedProcessorTime.TotalMilliseconds;
                    }
                } finally {
                    if (hProc != IntPtr.Zero) {
                        try {
                            if (hProc != targetProc.Handle) CloseHandle(hProc);
                        } catch {
                            CloseHandle(hProc);
                        }
                    }
                }

                Console.WriteLine(string.Format("{{\"success\": true, \"pid\": {0}, \"process\": \"{1}\", \"workingSetMB\": {2:F2}, \"peakWorkingSetMB\": {3:F2}, \"privateBytesMB\": {4:F2}, \"pagefileMB\": {5:F2}, \"kernelTimeMs\": {6}, \"userTimeMs\": {7}, \"totalCpuTimeMs\": {8}, \"threads\": {9}, \"isResponding\": {10}}}",
                    targetPid, EscapeJson(name), wsMB, peakWsMB, privMB, pagefileMB,
                    kMs, uMs, totalCpuMs, threads, responding ? "true" : "false"));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetKernelVitalsCmd() {
            try {
                PERFORMANCE_INFORMATION pi = new PERFORMANCE_INFORMATION();
                pi.cb = (uint)Marshal.SizeOf(typeof(PERFORMANCE_INFORMATION));
                if (!GetPerformanceInfo(out pi, pi.cb)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"GetPerformanceInfo failed\"}");
                    return;
                }

                ulong pageSize = pi.PageSize.ToUInt64();
                ulong pagedMb = (pi.KernelPaged.ToUInt64() * pageSize) / (1024UL * 1024UL);
                ulong nonPagedMb = (pi.KernelNonpaged.ToUInt64() * pageSize) / (1024UL * 1024UL);
                ulong kernelTotalMb = (pi.KernelTotal.ToUInt64() * pageSize) / (1024UL * 1024UL);
                ulong cacheMb = (pi.SystemCache.ToUInt64() * pageSize) / (1024UL * 1024UL);

                ulong commitTotalMb = (pi.CommitTotal.ToUInt64() * pageSize) / (1024UL * 1024UL);
                ulong commitLimitMb = (pi.CommitLimit.ToUInt64() * pageSize) / (1024UL * 1024UL);
                ulong commitPeakMb = (pi.CommitPeak.ToUInt64() * pageSize) / (1024UL * 1024UL);

                ulong physTotalMb = (pi.PhysicalTotal.ToUInt64() * pageSize) / (1024UL * 1024UL);
                ulong physAvailMb = (pi.PhysicalAvailable.ToUInt64() * pageSize) / (1024UL * 1024UL);
                ulong physUsedMb = (physTotalMb > physAvailMb) ? (physTotalMb - physAvailMb) : 0UL;

                double commitPct = commitLimitMb > 0 ? Math.Round((double)commitTotalMb / commitLimitMb * 100.0, 1) : 0.0;
                double physPct = physTotalMb > 0 ? Math.Round((double)physUsedMb / physTotalMb * 100.0, 1) : 0.0;

                string sCommitPct = commitPct.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                string sPhysPct = physPct.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

                Console.WriteLine(string.Format("{{\"success\": true, \"pageSizeBytes\": {0}, \"memoryPools\": {{\"kernelPagedMB\": {1}, \"kernelNonpagedMB\": {2}, \"kernelTotalMB\": {3}, \"systemCacheMB\": {4}}}, \"commit\": {{\"commitTotalMB\": {5}, \"commitLimitMB\": {6}, \"commitPeakMB\": {7}, \"commitRatioPct\": {8}}}, \"physical\": {{\"physicalTotalMB\": {9}, \"physicalAvailMB\": {10}, \"physicalUsedMB\": {11}, \"physicalUsagePct\": {12}}}, \"handles\": {{\"totalHandleCount\": {13}, \"processCount\": {14}, \"threadCount\": {15}}}}}",
                    pageSize, pagedMb, nonPagedMb, kernelTotalMb, cacheMb,
                    commitTotalMb, commitLimitMb, commitPeakMb, sCommitPct,
                    physTotalMb, physAvailMb, physUsedMb, sPhysPct,
                    pi.HandleCount, pi.ProcessCount, pi.ThreadCount));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetPowerStatusCmd() {
            try {
                SYSTEM_POWER_STATUS pwr = new SYSTEM_POWER_STATUS();
                bool pwrOk = GetSystemPowerStatus(out pwr);

                string acStr = "Unknown";
                if (pwr.ACLineStatus == 0) acStr = "Offline";
                else if (pwr.ACLineStatus == 1) acStr = "Online";

                string batStatus = "Unknown";
                if ((pwr.BatteryFlag & 128) != 0) batStatus = "NoBattery";
                else if ((pwr.BatteryFlag & 8) != 0) batStatus = "Charging";
                else if ((pwr.BatteryFlag & 4) != 0) batStatus = "Critical";
                else if ((pwr.BatteryFlag & 2) != 0) batStatus = "Low";
                else if ((pwr.BatteryFlag & 1) != 0) batStatus = "High";

                int batPct = (pwr.BatteryLifePercent <= 100) ? pwr.BatteryLifePercent : -1;
                long batTimeSec = (pwr.BatteryLifeTime != 0xFFFFFFFF) ? (long)pwr.BatteryLifeTime : -1L;
                bool saver = (pwr.SystemStatusFlag == 1);

                string schemeGuidStr = "";
                string schemeName = "Unknown";
                IntPtr pGuid = IntPtr.Zero;
                try {
                    if (PowerGetActiveScheme(IntPtr.Zero, out pGuid) == 0 && pGuid != IntPtr.Zero) {
                        Guid g = (Guid)Marshal.PtrToStructure(pGuid, typeof(Guid));
                        schemeGuidStr = g.ToString();
                        StringBuilder sb = new StringBuilder(256);
                        uint bufSize = (uint)sb.Capacity * 2;
                        if (PowerReadFriendlyName(IntPtr.Zero, ref g, IntPtr.Zero, IntPtr.Zero, sb, ref bufSize) == 0) {
                            schemeName = sb.ToString();
                        }
                    }
                } catch {} finally {
                    if (pGuid != IntPtr.Zero) LocalFree(pGuid);
                }

                Console.WriteLine(string.Format("{{\"success\": true, \"acLineStatus\": \"{0}\", \"batteryStatus\": \"{1}\", \"batteryLifePercent\": {2}, \"batterySaver\": {3}, \"batteryLifeTimeSeconds\": {4}, \"activePowerScheme\": {{\"name\": \"{5}\", \"guid\": \"{6}\"}}}}",
                    acStr, batStatus, batPct, saver ? "true" : "false", batTimeSec, EscapeJson(schemeName), EscapeJson(schemeGuidStr)));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void TuneProcessCmd(string query, string priority, string affinityStr, bool trim) {
            try {
                Process targetProc = null;
                int pid = 0;
                if (int.TryParse(query, out pid)) {
                    try { targetProc = Process.GetProcessById(pid); } catch {}
                } else {
                    string cleanName = query.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? query.Substring(0, query.Length - 4) : query;
                    var procs = Process.GetProcessesByName(cleanName);
                    if (procs != null && procs.Length > 0) {
                        targetProc = procs[0];
                    } else {
                        var all = Process.GetProcesses();
                        foreach (var p in all) {
                            try {
                                if (p.ProcessName.IndexOf(cleanName, StringComparison.OrdinalIgnoreCase) >= 0) {
                                    targetProc = p;
                                    break;
                                }
                            } catch {}
                        }
                    }
                }

                if (targetProc == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Process not found: {0}\"}}", EscapeJson(query)));
                    return;
                }

                uint targetPid = (uint)targetProc.Id;
                string procName = targetProc.ProcessName;

                // Capture initial state
                string prevPriority = "Unknown";
                long prevAffinity = 0;
                double prevWsMB = (targetProc.WorkingSet64 / (1024.0 * 1024.0));
                try { prevPriority = targetProc.PriorityClass.ToString(); } catch {}
                try { prevAffinity = targetProc.ProcessorAffinity.ToInt64(); } catch {}

                bool priorityChanged = false;
                if (!string.IsNullOrEmpty(priority)) {
                    string pri = priority.ToLowerInvariant().Replace("_", "").Replace("-", "");
                    ProcessPriorityClass newPri = targetProc.PriorityClass;
                    bool priValid = true;
                    if (pri == "idle" || pri == "low") newPri = ProcessPriorityClass.Idle;
                    else if (pri == "belownormal") newPri = ProcessPriorityClass.BelowNormal;
                    else if (pri == "normal") newPri = ProcessPriorityClass.Normal;
                    else if (pri == "abovenormal") newPri = ProcessPriorityClass.AboveNormal;
                    else if (pri == "high") newPri = ProcessPriorityClass.High;
                    else if (pri == "realtime") newPri = ProcessPriorityClass.RealTime;
                    else priValid = false;

                    if (priValid) {
                        try {
                            targetProc.PriorityClass = newPri;
                            priorityChanged = true;
                        } catch (Exception exPri) {
                            Console.Error.WriteLine("Priority set warning: " + exPri.Message);
                        }
                    }
                }

                bool affinityChanged = false;
                if (!string.IsNullOrEmpty(affinityStr)) {
                    long mask = 0;
                    bool parsed = false;
                    string aTrim = affinityStr.Trim();
                    if (aTrim.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                        parsed = long.TryParse(aTrim.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out mask);
                    } else {
                        parsed = long.TryParse(aTrim, out mask);
                    }

                    if (parsed && mask > 0) {
                        try {
                            targetProc.ProcessorAffinity = new IntPtr(mask);
                            affinityChanged = true;
                        } catch (Exception exAff) {
                            Console.Error.WriteLine("Affinity set warning: " + exAff.Message);
                        }
                    }
                }

                bool trimmed = false;
                if (trim) {
                    try {
                        IntPtr hProc = OpenProcess(0x1F0FFF /* PROCESS_ALL_ACCESS */, false, targetPid);
                        if (hProc == IntPtr.Zero) hProc = targetProc.Handle;
                        if (hProc != IntPtr.Zero) {
                            trimmed = EmptyWorkingSet(hProc);
                            if (hProc != targetProc.Handle) CloseHandle(hProc);
                        }
                    } catch (Exception exTrim) {
                        Console.Error.WriteLine("Trim warning: " + exTrim.Message);
                    }
                }

                // Refresh current state
                targetProc.Refresh();
                string curPriority = prevPriority;
                long curAffinity = prevAffinity;
                double curWsMB = (targetProc.WorkingSet64 / (1024.0 * 1024.0));
                try { curPriority = targetProc.PriorityClass.ToString(); } catch {}
                try { curAffinity = targetProc.ProcessorAffinity.ToInt64(); } catch {}

                string sPrevWs = prevWsMB.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                string sCurWs = curWsMB.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                Console.WriteLine(string.Format("{{\"success\": true, \"pid\": {0}, \"processName\": \"{1}\", \"previous\": {{\"priority\": \"{2}\", \"affinityMask\": \"0x{3:X}\", \"workingSetMB\": {4}}}, \"current\": {{\"priority\": \"{5}\", \"affinityMask\": \"0x{6:X}\", \"workingSetMB\": {7}}}, \"priorityChanged\": {8}, \"affinityChanged\": {9}, \"trimmed\": {10}}}",
                    targetPid, EscapeJson(procName), prevPriority, prevAffinity, sPrevWs,
                    curPriority, curAffinity, sCurWs,
                    priorityChanged ? "true" : "false", affinityChanged ? "true" : "false", trimmed ? "true" : "false"));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetKernelInterruptsCmd() {
            try {
                using (var dpcCounter = new PerformanceCounter("Processor Information", "% DPC Time", "_Total"))
                using (var intCounter = new PerformanceCounter("Processor Information", "% Interrupt Time", "_Total"))
                using (var qCounter = new PerformanceCounter("System", "Processor Queue Length"))
                using (var csCounter = new PerformanceCounter("System", "Context Switches/sec"))
                using (var scCounter = new PerformanceCounter("System", "System Calls/sec")) {
                    dpcCounter.NextValue();
                    intCounter.NextValue();
                    csCounter.NextValue();
                    scCounter.NextValue();
                    float q = qCounter.NextValue();

                    Thread.Sleep(150);

                    float dpc = dpcCounter.NextValue();
                    float irq = intCounter.NextValue();
                    float cs = csCounter.NextValue();
                    float sc = scCounter.NextValue();

                    string sDpc = dpc.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    string sIrq = irq.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                    Console.WriteLine(string.Format("{{\"success\": true, \"dpcTimePct\": {0}, \"interruptTimePct\": {1}, \"processorQueueLength\": {2}, \"contextSwitchesPerSec\": {3}, \"systemCallsPerSec\": {4}}}",
                        sDpc, sIrq, (int)q, (long)cs, (long)sc));
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\", \"dpcTimePct\": 0, \"interruptTimePct\": 0, \"processorQueueLength\": 0, \"contextSwitchesPerSec\": 0, \"systemCallsPerSec\": 0}}", EscapeJson(ex.Message)));
            }
        }

        static string ResolveTcpState(uint state) {
            switch (state) {
                case 1: return "CLOSED";
                case 2: return "LISTENING";
                case 3: return "SYN_SENT";
                case 4: return "SYN_RECEIVED";
                case 5: return "ESTABLISHED";
                case 6: return "FIN_WAIT_1";
                case 7: return "FIN_WAIT_2";
                case 8: return "CLOSE_WAIT";
                case 9: return "CLOSING";
                case 10: return "LAST_ACK";
                case 11: return "TIME_WAIT";
                case 12: return "DELETE_TCB";
                default: return "UNKNOWN";
            }
        }

        static void GetSocketsCmd(int filterPort, string filterState, string filterProtocol, int limit = 0) {
            try {
                var list = new List<string>();
                var procNames = new Dictionary<uint, string>();
                string fState = (filterState ?? "").Trim().ToUpperInvariant();
                string fProto = (filterProtocol ?? "all").Trim().ToLowerInvariant();

                Func<uint, string> getProc = (pid) => {
                    if (procNames.ContainsKey(pid)) return procNames[pid];
                    string name = "Unknown";
                    try {
                        var p = Process.GetProcessById((int)pid);
                        name = p.ProcessName;
                    } catch {}
                    procNames[pid] = name;
                    return name;
                };

                int tableOffset = 4;
                int totalCount = 0;

                // 1. IPv4 TCP Sockets
                if (fProto == "all" || fProto == "tcp") {
                    int size = 0;
                    uint ret = GetExtendedTcpTable(IntPtr.Zero, ref size, true, 2 /* AF_INET */, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
                    IntPtr pTable = IntPtr.Zero;
                    try {
                        for (int retry = 0; retry < 5; retry++) {
                            if (pTable != IntPtr.Zero) { Marshal.FreeHGlobal(pTable); pTable = IntPtr.Zero; }
                            size += 8192;
                            pTable = Marshal.AllocHGlobal(size);
                            ret = GetExtendedTcpTable(pTable, ref size, true, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
                            if (ret == 0) break;
                        }

                        if (ret == 0 && pTable != IntPtr.Zero) {
                            int numEntries = Marshal.ReadInt32(pTable);
                            IntPtr pRow = (IntPtr)((long)pTable + tableOffset);
                            int rowSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));
                            for (int i = 0; i < numEntries; i++) {
                                MIB_TCPROW_OWNER_PID row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(pRow, typeof(MIB_TCPROW_OWNER_PID));
                                pRow = (IntPtr)((long)pRow + rowSize);

                                ushort localPort = (ushort)(((row.dwLocalPort & 0xFF) << 8) | ((row.dwLocalPort >> 8) & 0xFF));
                                ushort remotePort = (ushort)(((row.dwRemotePort & 0xFF) << 8) | ((row.dwRemotePort >> 8) & 0xFF));
                                string localAddr = new IPAddress(BitConverter.GetBytes(row.dwLocalAddr)).ToString();
                                string remoteAddr = new IPAddress(BitConverter.GetBytes(row.dwRemoteAddr)).ToString();
                                string state = ResolveTcpState(row.dwState);

                                if (filterPort > 0 && localPort != filterPort && remotePort != filterPort) continue;
                                if (!string.IsNullOrEmpty(fState) && state.IndexOf(fState, StringComparison.OrdinalIgnoreCase) < 0) continue;

                                totalCount++;
                                if (limit <= 0 || list.Count < limit) {
                                    string pName = getProc(row.dwOwningPid);
                                    list.Add(string.Format("{{\"protocol\": \"tcp\", \"ipVersion\": 4, \"localAddress\": \"{0}\", \"localPort\": {1}, \"remoteAddress\": \"{2}\", \"remotePort\": {3}, \"state\": \"{4}\", \"pid\": {5}, \"process\": \"{6}\"}}",
                                        EscapeJson(localAddr), localPort, EscapeJson(remoteAddr), remotePort, EscapeJson(state), row.dwOwningPid, EscapeJson(pName)));
                                }
                            }
                        }
                    } finally {
                        if (pTable != IntPtr.Zero) Marshal.FreeHGlobal(pTable);
                    }

                    // 2. IPv6 TCP Sockets
                    int size6 = 0;
                    uint ret6 = GetExtendedTcpTable(IntPtr.Zero, ref size6, true, 23 /* AF_INET6 */, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
                    IntPtr pTable6 = IntPtr.Zero;
                    try {
                        for (int retry = 0; retry < 5; retry++) {
                            if (pTable6 != IntPtr.Zero) { Marshal.FreeHGlobal(pTable6); pTable6 = IntPtr.Zero; }
                            size6 += 8192;
                            pTable6 = Marshal.AllocHGlobal(size6);
                            ret6 = GetExtendedTcpTable(pTable6, ref size6, true, 23, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
                            if (ret6 == 0) break;
                        }

                        if (ret6 == 0 && pTable6 != IntPtr.Zero) {
                            int numEntries = Marshal.ReadInt32(pTable6);
                            IntPtr pRow = (IntPtr)((long)pTable6 + tableOffset);
                            int rowSize = Marshal.SizeOf(typeof(MIB_TCP6ROW_OWNER_PID));
                            for (int i = 0; i < numEntries; i++) {
                                MIB_TCP6ROW_OWNER_PID row = (MIB_TCP6ROW_OWNER_PID)Marshal.PtrToStructure(pRow, typeof(MIB_TCP6ROW_OWNER_PID));
                                pRow = (IntPtr)((long)pRow + rowSize);

                                ushort localPort = (ushort)(((row.dwLocalPort & 0xFF) << 8) | ((row.dwLocalPort >> 8) & 0xFF));
                                ushort remotePort = (ushort)(((row.dwRemotePort & 0xFF) << 8) | ((row.dwRemotePort >> 8) & 0xFF));
                                string localAddr = new IPAddress(row.ucLocalAddr).ToString();
                                string remoteAddr = new IPAddress(row.ucRemoteAddr).ToString();
                                string state = ResolveTcpState(row.dwState);

                                if (filterPort > 0 && localPort != filterPort && remotePort != filterPort) continue;
                                if (!string.IsNullOrEmpty(fState) && state.IndexOf(fState, StringComparison.OrdinalIgnoreCase) < 0) continue;

                                totalCount++;
                                if (limit <= 0 || list.Count < limit) {
                                    string pName = getProc(row.dwOwningPid);
                                    list.Add(string.Format("{{\"protocol\": \"tcp\", \"ipVersion\": 6, \"localAddress\": \"{0}\", \"localPort\": {1}, \"remoteAddress\": \"{2}\", \"remotePort\": {3}, \"state\": \"{4}\", \"pid\": {5}, \"process\": \"{6}\"}}",
                                        EscapeJson(localAddr), localPort, EscapeJson(remoteAddr), remotePort, EscapeJson(state), row.dwOwningPid, EscapeJson(pName)));
                                }
                            }
                        }
                    } finally {
                        if (pTable6 != IntPtr.Zero) Marshal.FreeHGlobal(pTable6);
                    }
                }

                // 3. IPv4 UDP Sockets
                if (fProto == "all" || fProto == "udp") {
                    int sizeUdp = 0;
                    GetExtendedUdpTable(IntPtr.Zero, ref sizeUdp, true, 2 /* AF_INET */, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
                    if (sizeUdp > 0) {
                        IntPtr pTableUdp = Marshal.AllocHGlobal(sizeUdp);
                        try {
                            if (GetExtendedUdpTable(pTableUdp, ref sizeUdp, true, 2, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0) == 0) {
                                int numEntries = Marshal.ReadInt32(pTableUdp);
                                IntPtr pRow = (IntPtr)((long)pTableUdp + tableOffset);
                                int rowSize = Marshal.SizeOf(typeof(MIB_UDPROW_OWNER_PID));
                                for (int i = 0; i < numEntries; i++) {
                                    MIB_UDPROW_OWNER_PID row = (MIB_UDPROW_OWNER_PID)Marshal.PtrToStructure(pRow, typeof(MIB_UDPROW_OWNER_PID));
                                    pRow = (IntPtr)((long)pRow + rowSize);

                                    ushort localPort = (ushort)(((row.dwLocalPort & 0xFF) << 8) | ((row.dwLocalPort >> 8) & 0xFF));
                                    string localAddr = new IPAddress(BitConverter.GetBytes(row.dwLocalAddr)).ToString();

                                    if (filterPort > 0 && localPort != filterPort) continue;
                                    if (!string.IsNullOrEmpty(fState) && "UDP".IndexOf(fState, StringComparison.OrdinalIgnoreCase) < 0) continue;

                                    totalCount++;
                                    if (limit <= 0 || list.Count < limit) {
                                        string pName = getProc(row.dwOwningPid);
                                        list.Add(string.Format("{{\"protocol\": \"udp\", \"ipVersion\": 4, \"localAddress\": \"{0}\", \"localPort\": {1}, \"remoteAddress\": \"*\", \"remotePort\": 0, \"state\": \"UDP\", \"pid\": {2}, \"process\": \"{3}\"}}",
                                            EscapeJson(localAddr), localPort, row.dwOwningPid, EscapeJson(pName)));
                                    }
                                }
                            }
                        } finally {
                            Marshal.FreeHGlobal(pTableUdp);
                        }
                    }
                }

                Console.WriteLine(string.Format("{{\"success\": true, \"totalCount\": {0}, \"count\": {1}, \"sockets\": [{2}]}}",
                    totalCount, list.Count, string.Join(",", list.ToArray())));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void SetPowerSchemeCmd(string schemeOrGuid) {
            try {
                if (string.IsNullOrEmpty(schemeOrGuid)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Scheme name or GUID required\"}");
                    return;
                }

                Guid targetGuid;
                string s = schemeOrGuid.Trim().ToLowerInvariant();
                if (s == "balanced") {
                    targetGuid = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
                } else if (s == "high" || s == "high_performance" || s == "highperformance") {
                    targetGuid = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                } else if (s == "saver" || s == "power_saver" || s == "powersaver") {
                    targetGuid = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");
                } else if (s == "ultimate" || s == "ultimate_performance" || s == "ultimateperformance") {
                    targetGuid = new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");
                } else {
                    try {
                        targetGuid = new Guid(schemeOrGuid);
                    } catch {
                        Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Invalid scheme name or GUID: {0}\"}}", EscapeJson(schemeOrGuid)));
                        return;
                    }
                }

                string prevName = "Unknown";
                string prevGuidStr = "";
                IntPtr pPrev = IntPtr.Zero;
                try {
                    if (PowerGetActiveScheme(IntPtr.Zero, out pPrev) == 0 && pPrev != IntPtr.Zero) {
                        Guid pg = (Guid)Marshal.PtrToStructure(pPrev, typeof(Guid));
                        prevGuidStr = pg.ToString();
                        StringBuilder sbPrev = new StringBuilder(256);
                        uint bSz = (uint)sbPrev.Capacity * 2;
                        if (PowerReadFriendlyName(IntPtr.Zero, ref pg, IntPtr.Zero, IntPtr.Zero, sbPrev, ref bSz) == 0) {
                            prevName = sbPrev.ToString();
                        }
                    }
                } catch {} finally {
                    if (pPrev != IntPtr.Zero) LocalFree(pPrev);
                }

                uint res = PowerSetActiveScheme(IntPtr.Zero, ref targetGuid);
                if (res != 0) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"PowerSetActiveScheme failed with error code {0}\"}}", res));
                    return;
                }

                string currName = "Unknown";
                string currGuidStr = targetGuid.ToString();
                IntPtr pCurr = IntPtr.Zero;
                try {
                    if (PowerGetActiveScheme(IntPtr.Zero, out pCurr) == 0 && pCurr != IntPtr.Zero) {
                        Guid cg = (Guid)Marshal.PtrToStructure(pCurr, typeof(Guid));
                        currGuidStr = cg.ToString();
                        StringBuilder sbCurr = new StringBuilder(256);
                        uint bSz = (uint)sbCurr.Capacity * 2;
                        if (PowerReadFriendlyName(IntPtr.Zero, ref cg, IntPtr.Zero, IntPtr.Zero, sbCurr, ref bSz) == 0) {
                            currName = sbCurr.ToString();
                        }
                    }
                } catch {} finally {
                    if (pCurr != IntPtr.Zero) LocalFree(pCurr);
                }

                Console.WriteLine(string.Format("{{\"success\": true, \"previousScheme\": {{\"name\": \"{0}\", \"guid\": \"{1}\"}}, \"activePowerScheme\": {{\"name\": \"{2}\", \"guid\": \"{3}\"}}}}",
                    EscapeJson(prevName), EscapeJson(prevGuidStr), EscapeJson(currName), EscapeJson(currGuidStr)));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void JobSandboxCmd(string targetQuery, int cpuRatePct, long maxMemMb, bool killOnClose) {
            try {
                Process targetProc = null;
                int pid = 0;
                if (int.TryParse(targetQuery, out pid)) {
                    try { targetProc = Process.GetProcessById(pid); } catch {}
                } else {
                    string cleanName = targetQuery.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? targetQuery.Substring(0, targetQuery.Length - 4) : targetQuery;
                    var procs = Process.GetProcessesByName(cleanName);
                    if (procs != null && procs.Length > 0) targetProc = procs[0];
                    else {
                        var all = Process.GetProcesses();
                        foreach (var p in all) {
                            try {
                                if (p.ProcessName.IndexOf(cleanName, StringComparison.OrdinalIgnoreCase) >= 0) {
                                    targetProc = p;
                                    break;
                                }
                            } catch {}
                        }
                    }
                }

                if (targetProc == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Process not found: {0}\"}}", EscapeJson(targetQuery)));
                    return;
                }

                int targetPid = targetProc.Id;
                string procName = targetProc.ProcessName;
                string jobName = "GeminiJob_" + targetPid;

                IntPtr hJob = CreateJobObject(IntPtr.Zero, jobName);
                if (hJob == IntPtr.Zero) {
                    int err = Marshal.GetLastWin32Error();
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"CreateJobObject failed with error {0}\"}}", err));
                    return;
                }

                bool killSet = false;
                bool memSet = false;
                bool cpuSet = false;

                if (killOnClose || maxMemMb > 0) {
                    JOBOBJECT_EXTENDED_LIMIT_INFORMATION exLimits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                    uint flags = 0;
                    if (killOnClose) {
                        flags |= 0x2000;
                        killSet = true;
                    }
                    if (maxMemMb > 0) {
                        flags |= 0x0100;
                        exLimits.ProcessMemoryLimit = new UIntPtr((ulong)maxMemMb * 1024UL * 1024UL);
                        memSet = true;
                    }
                    exLimits.BasicLimitInformation.LimitFlags = flags;
                    int structSize = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                    IntPtr pInfo = Marshal.AllocHGlobal(structSize);
                    try {
                        Marshal.StructureToPtr(exLimits, pInfo, false);
                        if (SetInformationJobObject(hJob, 9 /* JobObjectExtendedLimitInformation */, pInfo, (uint)structSize)) {
                            // Limits applied
                        }
                    } finally {
                        Marshal.FreeHGlobal(pInfo);
                    }
                }

                if (cpuRatePct > 0) {
                    int clampPct = Math.Min(Math.Max(cpuRatePct, 1), 100);
                    JOBOBJECT_CPU_RATE_CONTROL_INFORMATION cpuInfo = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION();
                    cpuInfo.ControlFlags = 0x1 | 0x4; // ENABLE | HARD_CAP
                    cpuInfo.CpuRate = (uint)(clampPct * 100);
                    int cpuSize = Marshal.SizeOf(typeof(JOBOBJECT_CPU_RATE_CONTROL_INFORMATION));
                    IntPtr pCpu = Marshal.AllocHGlobal(cpuSize);
                    try {
                        Marshal.StructureToPtr(cpuInfo, pCpu, false);
                        if (SetInformationJobObject(hJob, 15 /* JobObjectCpuRateControlInformation */, pCpu, (uint)cpuSize)) {
                            cpuSet = true;
                        }
                    } finally {
                        Marshal.FreeHGlobal(pCpu);
                    }
                }

                bool assigned = AssignProcessToJobObject(hJob, targetProc.Handle);
                int assignErr = assigned ? 0 : Marshal.GetLastWin32Error();

                Console.WriteLine(string.Format("{{\"success\": {0}, \"pid\": {1}, \"process\": \"{2}\", \"jobName\": \"{3}\", \"assigned\": {4}, \"assignError\": {5}, \"limitsApplied\": {{\"cpuRatePct\": {6}, \"maxMemoryMB\": {7}, \"killOnJobClose\": {8}}}}}",
                    assigned ? "true" : "false", targetPid, EscapeJson(procName), EscapeJson(jobName),
                    assigned ? "true" : "false", assignErr,
                    cpuSet ? cpuRatePct : 0, memSet ? maxMemMb : 0, killSet ? "true" : "false"));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetUsnJournalCmd(string driveQuery) {
            try {
                string d = string.IsNullOrEmpty(driveQuery) ? "C" : driveQuery.Trim().ToUpperInvariant();
                if (d.EndsWith(":\\") || d.EndsWith(":/")) d = d.Substring(0, 1);
                else if (d.EndsWith(":")) d = d.Substring(0, 1);
                else if (d.StartsWith(@"\\.\")) d = d.Substring(4, 1);

                string rootPath = d + @":\";
                string devicePath = @"\\.\" + d + ":";

                DriveInfo di = null;
                try { di = new DriveInfo(rootPath); } catch {}
                string fsFormat = (di != null && di.IsReady) ? di.DriveFormat : "Unknown";

                const uint GENERIC_READ = 0x80000000;
                const uint FILE_SHARE_READ = 1;
                const uint FILE_SHARE_WRITE = 2;
                const uint OPEN_EXISTING = 3;
                const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
                const uint FSCTL_QUERY_USN_JOURNAL = 0x000900f4;

                IntPtr hVol = CreateFile(devicePath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
                if (hVol.ToInt64() == -1 || hVol == IntPtr.Zero) {
                    int err = Marshal.GetLastWin32Error();
                    Console.WriteLine(string.Format("{{\"success\": false, \"drive\": \"{0}:\", \"fileSystem\": \"{1}\", \"error\": \"Volume handle failed (Access denied or not NTFS). Win32 Error: {2}\", \"elevationRequired\": {3}}}",
                        d, EscapeJson(fsFormat), err, err == 5 ? "true" : "false"));
                    return;
                }

                try {
                    int outSize = Marshal.SizeOf(typeof(USN_JOURNAL_DATA_V0));
                    IntPtr pOut = Marshal.AllocHGlobal(outSize);
                    try {
                        uint bytesReturned = 0;
                        bool ok = DeviceIoControl(hVol, FSCTL_QUERY_USN_JOURNAL, IntPtr.Zero, 0, pOut, (uint)outSize, out bytesReturned, IntPtr.Zero);
                        if (!ok) {
                            int err = Marshal.GetLastWin32Error();
                            Console.WriteLine(string.Format("{{\"success\": false, \"drive\": \"{0}:\", \"fileSystem\": \"{1}\", \"error\": \"FSCTL_QUERY_USN_JOURNAL failed with error {2}\"}}",
                                d, EscapeJson(fsFormat), err));
                            return;
                        }

                        USN_JOURNAL_DATA_V0 ujd = (USN_JOURNAL_DATA_V0)Marshal.PtrToStructure(pOut, typeof(USN_JOURNAL_DATA_V0));
                        double maxMb = Math.Round((double)ujd.MaximumSize / (1024.0 * 1024.0), 2);
                        double deltaMb = Math.Round((double)ujd.AllocationDelta / (1024.0 * 1024.0), 2);

                        string sMaxMb = maxMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                        string sDeltaMb = deltaMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                        Console.WriteLine(string.Format("{{\"success\": true, \"drive\": \"{0}:\", \"fileSystem\": \"{1}\", \"status\": \"Active\", \"journalId\": \"0x{2:X}\", \"firstUsn\": {3}, \"nextUsn\": {4}, \"lowestValidUsn\": {5}, \"maxUsn\": {6}, \"maximumSizeMB\": {7}, \"allocationDeltaMB\": {8}}}",
                            d, EscapeJson(fsFormat), ujd.UsnJournalID, ujd.FirstUsn, ujd.NextUsn, ujd.LowestValidUsn, ujd.MaxUsn, sMaxMb, sDeltaMb));
                    } finally {
                        Marshal.FreeHGlobal(pOut);
                    }
                } finally {
                    CloseHandle(hVol);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetPresenceCmd() {
            try {
                LASTINPUTINFO lii = new LASTINPUTINFO();
                lii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
                uint idleMs = 0;
                if (GetLastInputInfo(ref lii)) {
                    uint tick = (uint)Environment.TickCount;
                    idleMs = (tick >= lii.dwTime) ? (tick - lii.dwTime) : 0;
                }
                double idleSec = Math.Round(idleMs / 1000.0, 2);
                bool isIdle = (idleSec >= 300.0);

                const int SM_REMOTESESSION = 0x1000;
                bool isRemote = (GetSystemMetrics(SM_REMOTESESSION) != 0);
                int sessionId = Process.GetCurrentProcess().SessionId;

                string clientName = "";
                string clientDisplay = "";
                string clientAddress = "";

                if (isRemote) {
                    IntPtr pBuf = IntPtr.Zero;
                    int bytes = 0;
                    try {
                        if (WTSQuerySessionInformation(IntPtr.Zero, -1, 10, out pBuf, out bytes) && pBuf != IntPtr.Zero) {
                            clientName = Marshal.PtrToStringAnsi(pBuf) ?? "";
                        }
                    } catch {} finally {
                        if (pBuf != IntPtr.Zero) WTSFreeMemory(pBuf);
                        pBuf = IntPtr.Zero;
                    }

                    try {
                        if (WTSQuerySessionInformation(IntPtr.Zero, -1, 15, out pBuf, out bytes) && pBuf != IntPtr.Zero) {
                            var d = (WTS_CLIENT_DISPLAY)Marshal.PtrToStructure(pBuf, typeof(WTS_CLIENT_DISPLAY));
                            if (d.HorizontalResolution > 0 && d.VerticalResolution > 0) {
                                clientDisplay = string.Format("{0}x{1} @ {2}bpp", d.HorizontalResolution, d.VerticalResolution, d.ColorDepth);
                            }
                        }
                    } catch {} finally {
                        if (pBuf != IntPtr.Zero) WTSFreeMemory(pBuf);
                        pBuf = IntPtr.Zero;
                    }

                    try {
                        if (WTSQuerySessionInformation(IntPtr.Zero, -1, 14, out pBuf, out bytes) && pBuf != IntPtr.Zero) {
                            var a = (WTS_CLIENT_ADDRESS)Marshal.PtrToStructure(pBuf, typeof(WTS_CLIENT_ADDRESS));
                            if (a.AddressFamily == 2) {
                                clientAddress = string.Format("{0}.{1}.{2}.{3}", a.Address[2], a.Address[3], a.Address[4], a.Address[5]);
                            }
                        }
                    } catch {} finally {
                        if (pBuf != IntPtr.Zero) WTSFreeMemory(pBuf);
                        pBuf = IntPtr.Zero;
                    }
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"idleMs\": {0}, \"idleSeconds\": {1:F2}, \"isIdle\": {2}, \"isRemoteSession\": {3}, \"sessionId\": {4}, \"sessionType\": \"{5}\", \"clientName\": \"{6}\", \"clientDisplay\": \"{7}\", \"clientAddress\": \"{8}\", \"note\": \"{9}\"}}",
                    idleMs,
                    idleSec,
                    isIdle ? "true" : "false",
                    isRemote ? "true" : "false",
                    sessionId,
                    isRemote ? "RDP" : "Console",
                    EscapeJson(clientName),
                    EscapeJson(clientDisplay),
                    EscapeJson(clientAddress),
                    isRemote
                        ? "RDP session active: input tracking measures events forwarded across network; high latency, client minimization, or session disconnect can freeze or jump reported idle time."
                        : "Direct interactive console session active."
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetStorageVitalsCmd() {
            try {
                var drives = DriveInfo.GetDrives();
                var list = new List<string>();
                long totalAllBytes = 0;
                long freeAllBytes = 0;

                foreach (var d in drives) {
                    try {
                        if (!d.IsReady) continue;
                        totalAllBytes += d.TotalSize;
                        freeAllBytes += d.TotalFreeSpace;
                        long usedBytes = d.TotalSize - d.TotalFreeSpace;
                        double pctUsed = d.TotalSize > 0 ? Math.Round((double)usedBytes / d.TotalSize * 100.0, 1) : 0.0;

                        double totalGB = Math.Round(d.TotalSize / (1024.0 * 1024.0 * 1024.0), 2);
                        double freeGB = Math.Round(d.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0), 2);
                        double usedGB = Math.Round(usedBytes / (1024.0 * 1024.0 * 1024.0), 2);
                        bool isRedirected = (d.DriveType == DriveType.Network) || d.Name.IndexOf("tsclient", StringComparison.OrdinalIgnoreCase) >= 0;

                        list.Add(string.Format(
                            "{{\"name\": \"{0}\", \"label\": \"{1}\", \"type\": \"{2}\", \"fileSystem\": \"{3}\", \"totalGB\": {4:F2}, \"freeGB\": {5:F2}, \"usedGB\": {6:F2}, \"percentUsed\": {7:F1}, \"isRdpRedirected\": {8}, \"isReady\": true}}",
                            EscapeJson(d.Name),
                            EscapeJson(d.VolumeLabel),
                            EscapeJson(d.DriveType.ToString()),
                            EscapeJson(d.DriveFormat),
                            totalGB,
                            freeGB,
                            usedGB,
                            pctUsed,
                            isRedirected ? "true" : "false"
                        ));
                    } catch {}
                }

                double totalStorageGB = Math.Round(totalAllBytes / (1024.0 * 1024.0 * 1024.0), 2);
                double freeStorageGB = Math.Round(freeAllBytes / (1024.0 * 1024.0 * 1024.0), 2);

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"totalStorageGB\": {0:F2}, \"freeStorageGB\": {1:F2}, \"driveCount\": {2}, \"drives\": [{3}]}}",
                    totalStorageGB, freeStorageGB, list.Count, string.Join(", ", list.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetNetworkVitalsCmd() {
            try {
                bool isNetAvailable = NetworkInterface.GetIsNetworkAvailable();
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                var list = new List<string>();

                foreach (var ni in interfaces) {
                    try {
                        if (ni.OperationalStatus != OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback) {
                            continue;
                        }

                        var ipProps = ni.GetIPProperties();
                        var ips = new List<string>();
                        foreach (var unicast in ipProps.UnicastAddresses) {
                            if (unicast.Address != null) {
                                ips.Add(string.Format("\"{0}\"", unicast.Address.ToString()));
                            }
                        }

                        var gateways = new List<string>();
                        foreach (var gw in ipProps.GatewayAddresses) {
                            if (gw.Address != null) {
                                gateways.Add(string.Format("\"{0}\"", gw.Address.ToString()));
                            }
                        }

                        var stats = ni.GetIPv4Statistics();
                        long rxBytes = stats != null ? stats.BytesReceived : 0;
                        long txBytes = stats != null ? stats.BytesSent : 0;
                        double speedMbps = Math.Round(ni.Speed / 1000000.0, 1);

                        list.Add(string.Format(
                            "{{\"name\": \"{0}\", \"description\": \"{1}\", \"type\": \"{2}\", \"status\": \"{3}\", \"speedMbps\": {4:F1}, \"bytesReceived\": {5}, \"bytesSent\": {6}, \"addresses\": [{7}], \"gateways\": [{8}]}}",
                            EscapeJson(ni.Name),
                            EscapeJson(ni.Description),
                            EscapeJson(ni.NetworkInterfaceType.ToString()),
                            EscapeJson(ni.OperationalStatus.ToString()),
                            speedMbps,
                            rxBytes,
                            txBytes,
                            string.Join(", ", ips.ToArray()),
                            string.Join(", ", gateways.ToArray())
                        ));
                    } catch {}
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"isNetworkAvailable\": {0}, \"adapterCount\": {1}, \"adapters\": [{2}]}}",
                    isNetAvailable ? "true" : "false",
                    list.Count,
                    string.Join(", ", list.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetDisplayTopologyCmd() {
            try {
                const int SM_REMOTESESSION = 0x1000;
                bool isRemote = (GetSystemMetrics(SM_REMOTESESSION) != 0);
                int virtX = GetSystemMetrics(76);
                int virtY = GetSystemMetrics(77);
                int virtW = GetSystemMetrics(78);
                int virtH = GetSystemMetrics(79);
                int monCount = GetSystemMetrics(80);
                if (monCount == 0) monCount = Screen.AllScreens.Length;

                var monList = new List<string>();
                for (int i = 0; i < Screen.AllScreens.Length; i++) {
                    var s = Screen.AllScreens[i];
                    DEVMODE dm = new DEVMODE();
                    dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                    int refreshHz = 60;
                    if (EnumDisplaySettings(s.DeviceName, ENUM_CURRENT_SETTINGS, ref dm)) {
                        refreshHz = dm.dmDisplayFrequency;
                    }

                    monList.Add(string.Format(
                        "{{\"index\": {0}, \"device\": \"{1}\", \"isPrimary\": {2}, \"bounds\": {{\"x\": {3}, \"y\": {4}, \"width\": {5}, \"height\": {6}}}, \"workingArea\": {{\"x\": {7}, \"y\": {8}, \"width\": {9}, \"height\": {10}}}, \"refreshRateHz\": {11}, \"bitsPerPixel\": {12}}}",
                        i,
                        EscapeJson(s.DeviceName),
                        s.Primary ? "true" : "false",
                        s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height,
                        s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height,
                        refreshHz,
                        s.BitsPerPixel
                    ));
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"isRemoteSession\": {0}, \"sessionType\": \"{1}\", \"monitorCount\": {2}, \"virtualScreen\": {{\"x\": {3}, \"y\": {4}, \"width\": {5}, \"height\": {6}}}, \"monitors\": [{7}], \"note\": \"{8}\"}}",
                    isRemote ? "true" : "false",
                    isRemote ? "RDP" : "Console",
                    monCount,
                    virtX, virtY, virtW > 0 ? virtW : GetSystemMetrics(0), virtH > 0 ? virtH : GetSystemMetrics(1),
                    string.Join(", ", monList.ToArray()),
                    isRemote ? "RDP virtual display adapter active: virtual display topology conforms to remote client geometry." : "Native physical displays active."
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void FlashWindowCmd(string query, int count) {
            try {
                IntPtr hWnd = IntPtr.Zero;
                if (string.IsNullOrEmpty(query) || query.Equals("active", StringComparison.OrdinalIgnoreCase) || query.Equals("foreground", StringComparison.OrdinalIgnoreCase)) {
                    hWnd = GetForegroundWindow();
                } else {
                    IntPtr hDesk = OpenInputDesktop(0, false, 0x01FF);
                    if (hDesk == IntPtr.Zero) hDesk = OpenDesktop("Default", 0, false, 0x01FF);
                    var windows = CollectDesktopWindows(hDesk, true);
                    foreach (var w in windows) {
                        if ((w.Title != null && w.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (w.ProcessName != null && w.ProcessName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)) {
                            hWnd = w.Handle;
                            break;
                        }
                    }
                }

                if (hWnd == IntPtr.Zero) {
                    hWnd = GetShellWindow();
                    if (hWnd == IntPtr.Zero) hWnd = GetDesktopWindow();
                }

                if (hWnd == IntPtr.Zero) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"No matching window found for '{0}'\"}}", EscapeJson(query)));
                    return;
                }

                FLASHWINFO fwi = new FLASHWINFO();
                fwi.cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO));
                fwi.hwnd = hWnd;
                fwi.dwFlags = 3 | 12; // FLASHW_ALL | FLASHW_TIMERNOFG
                fwi.uCount = (uint)Math.Max(1, count);
                fwi.dwTimeout = 0;

                FlashWindowEx(ref fwi);
                Console.WriteLine(string.Format("{{\"success\": true, \"handle\": {0}, \"count\": {1}}}",
                    hWnd.ToInt64(), count));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void ClipboardGetCmd() {
            try {
                string text = "";
                bool hasText = false;
                bool hasImage = false;
                bool hasFiles = false;

                RunSta(() => {
                    hasText = Clipboard.ContainsText();
                    hasImage = Clipboard.ContainsImage();
                    hasFiles = Clipboard.ContainsFileDropList();
                    if (hasText) {
                        text = Clipboard.GetText();
                    }
                });

                Console.WriteLine(string.Format("{{\"success\": true, \"hasText\": {0}, \"hasImage\": {1}, \"hasFiles\": {2}, \"charCount\": {3}, \"text\": \"{4}\"}}",
                    hasText ? "true" : "false", hasImage ? "true" : "false", hasFiles ? "true" : "false",
                    text.Length, EscapeJson(text)));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void ClipboardSetCmd(string text) {
            try {
                RunSta(() => {
                    Clipboard.SetDataObject(text, true, 5, 50);
                });
                Console.WriteLine(string.Format("{{\"success\": true, \"charCount\": {0}}}", text.Length));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void ClipboardSaveImageCmd(string outputPath) {
            try {
                int w = 0, h = 0;
                bool saved = false;
                RunSta(() => {
                    if (Clipboard.ContainsImage()) {
                        using (Image img = Clipboard.GetImage()) {
                            if (img != null) {
                                string dir = Path.GetDirectoryName(outputPath);
                                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                                    Directory.CreateDirectory(dir);
                                }
                                img.Save(outputPath, ImageFormat.Png);
                                w = img.Width;
                                h = img.Height;
                                saved = true;
                            }
                        }
                    }
                });

                if (saved) {
                    Console.WriteLine(string.Format("{{\"success\": true, \"path\": \"{0}\", \"width\": {1}, \"height\": {2}}}",
                        outputPath.Replace("\\", "/"), w, h));
                } else {
                    Console.WriteLine("{\"success\": false, \"error\": \"No image on clipboard\"}");
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void ClipboardLoadImageCmd(string inputPath) {
            try {
                if (!File.Exists(inputPath)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Input image file not found\"}");
                    return;
                }
                int w = 0, h = 0;
                RunSta(() => {
                    using (Image img = Image.FromFile(inputPath)) {
                        Clipboard.SetDataObject(img, true, 5, 50);
                        w = img.Width;
                        h = img.Height;
                    }
                });
                Console.WriteLine(string.Format("{{\"success\": true, \"path\": \"{0}\", \"width\": {1}, \"height\": {2}}}",
                    inputPath.Replace("\\", "/"), w, h));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void ClipboardClearCmd() {
            try {
                RunSta(() => {
                    Clipboard.Clear();
                });
                Console.WriteLine("{\"success\": true}");
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
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
                Console.WriteLine("{\"error\": \"Usage: desktop_helper [active | list | info | focus | maximize | minimize | restore | type | paste | click_and_type | click | doubleclick | rightclick | drag | scroll | hotkey | capture | deltacapture | listchildren | scan | elements | findelement | clickelement]\"}");
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
                bool includeCloaked = (args.Length > 1 && (args[1].Equals("--all", StringComparison.OrdinalIgnoreCase) || args[1].Equals("all", StringComparison.OrdinalIgnoreCase)));
                ListWindows(includeCloaked);
            } else if (cmd == "info" && args.Length >= 2) {
                GetWindowInfoCmd(args[1]);
            } else if (cmd == "focus" && args.Length >= 2) {
                FocusWindowCmd(args[1]);
            } else if (cmd == "maximize" && args.Length >= 2) {
                MaximizeWindowCmd(args[1]);
            } else if (cmd == "minimize" && args.Length >= 2) {
                MinimizeWindowCmd(args[1]);
            } else if (cmd == "restore" && args.Length >= 2) {
                RestoreWindowCmd(args[1]);
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
                int maxDim = args.Length >= 4 ? int.Parse(args[3]) : 0;
                CaptureWindow(args[1], args[2], maxDim);
            } else if (cmd == "deltacapture" && args.Length >= 3) {
                int maxDim = args.Length >= 4 ? int.Parse(args[3]) : 768;
                double diffThresh = args.Length >= 5 ? double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 0.01;
                DeltaCaptureWindow(args[1], args[2], maxDim, diffThresh);
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
            } else if (cmd == "move" && args.Length >= 6) {
                int x = int.Parse(args[2]);
                int y = int.Parse(args[3]);
                int w = int.Parse(args[4]);
                int h = int.Parse(args[5]);
                MoveWindowCmd(args[1], x, y, w, h);
            } else if (cmd == "workarea") {
                GetWorkAreaCmd();
            } else if (cmd == "clip_get") {
                ClipboardGetCmd();
            } else if (cmd == "clip_set" && args.Length >= 2) {
                ClipboardSetCmd(args[1]);
            } else if (cmd == "clip_save_image" && args.Length >= 2) {
                ClipboardSaveImageCmd(args[1]);
            } else if (cmd == "clip_load_image" && args.Length >= 2) {
                ClipboardLoadImageCmd(args[1]);
            } else if (cmd == "clip_clear") {
                ClipboardClearCmd();
            } else if (cmd == "vitals" || cmd == "sysinfo" || cmd == "hardware") {
                GetVitalsCmd();
            } else if (cmd == "cursor") {
                GetCursorInfoCmd();
            } else if (cmd == "volume" || cmd == "audio_volume" || cmd == "get_volume") {
                GetAudioVolumeCmd();
            } else if ((cmd == "set_volume" || cmd == "setvolume") && args.Length >= 2) {
                float val = 0f;
                float.TryParse(args[1], out val);
                SetAudioVolumeCmd(val);
            } else if (cmd == "mute") {
                SetAudioMuteCmd(true);
            } else if (cmd == "unmute") {
                SetAudioMuteCmd(false);
            } else if (cmd == "toggle_mute" || cmd == "togglemute") {
                ToggleAudioMuteCmd();
            } else if ((cmd == "process_vitals" || cmd == "proc_vitals" || cmd == "procvitals") && args.Length >= 2) {
                GetProcessVitalsCmd(args[1]);
            } else if (cmd == "presence" || cmd == "idle" || cmd == "user_presence") {
                GetPresenceCmd();
            } else if (cmd == "storage" || cmd == "drives" || cmd == "disks") {
                GetStorageVitalsCmd();
            } else if (cmd == "network" || cmd == "net" || cmd == "adapters") {
                GetNetworkVitalsCmd();
            } else if (cmd == "display_topology" || cmd == "monitors" || cmd == "displays") {
                GetDisplayTopologyCmd();
            } else if (cmd == "kernel_vitals" || cmd == "kernelvitals" || cmd == "pool_vitals") {
                GetKernelVitalsCmd();
            } else if (cmd == "power_status" || cmd == "powerstatus" || cmd == "power_scheme") {
                GetPowerStatusCmd();
            } else if ((cmd == "process_tune" || cmd == "tune_process" || cmd == "proctune") && args.Length >= 2) {
                string target = args[1];
                string priority = args.Length >= 3 ? args[2] : null;
                string affinity = args.Length >= 4 ? args[3] : null;
                bool trim = args.Length >= 5 ? (args[4].Equals("trim", StringComparison.OrdinalIgnoreCase) || args[4].Equals("true", StringComparison.OrdinalIgnoreCase)) : false;
                TuneProcessCmd(target, priority, affinity, trim);
            } else if (cmd == "kernel_interrupts" || cmd == "interrupts" || cmd == "dpc") {
                GetKernelInterruptsCmd();
            } else if (cmd == "sockets" || cmd == "ports" || cmd == "socket_table" || cmd == "tcp_table") {
                int portFilter = 0;
                if (args.Length >= 2) int.TryParse(args[1], out portFilter);
                string stateFilter = args.Length >= 3 ? args[2] : "";
                string protoFilter = args.Length >= 4 ? args[3] : "all";
                int limit = 0;
                if (args.Length >= 5) int.TryParse(args[4], out limit);
                GetSocketsCmd(portFilter, stateFilter, protoFilter, limit);
            } else if (cmd == "set_power_scheme" || cmd == "set_powerscheme" || cmd == "setpower" || cmd == "power_set") {
                string scheme = args.Length >= 2 ? args[1] : "";
                SetPowerSchemeCmd(scheme);
            } else if ((cmd == "job_sandbox" || cmd == "jobsandbox" || cmd == "job_apply" || cmd == "sandbox_process") && args.Length >= 2) {
                string target = args[1];
                int cpuRate = args.Length >= 3 ? int.Parse(args[2]) : 0;
                long maxMem = args.Length >= 4 ? long.Parse(args[3]) : 0;
                bool killOnClose = args.Length >= 5 ? (args[4].Equals("true", StringComparison.OrdinalIgnoreCase) || args[4].Equals("kill", StringComparison.OrdinalIgnoreCase)) : false;
                JobSandboxCmd(target, cpuRate, maxMem, killOnClose);
            } else if (cmd == "usn_journal" || cmd == "usn" || cmd == "mft_journal") {
                string drive = args.Length >= 2 ? args[1] : "C";
                GetUsnJournalCmd(drive);
            } else if (cmd == "flash" || cmd == "flash_window") {
                string target = args.Length >= 2 ? args[1] : "active";
                int count = args.Length >= 3 ? int.Parse(args[2]) : 3;
                FlashWindowCmd(target, count);
            } else {
                Console.WriteLine("{\"error\": \"Invalid arguments\"}");
            }
        }

        static List<WindowMeta> CollectDesktopWindows(IntPtr hDesk, bool includeCloaked = false) {
            IntPtr hFore = GetForegroundWindow();
            var list = new List<WindowMeta>();
            var elevCache = new Dictionary<uint, bool>();
            var monCache = new Dictionary<IntPtr, Tuple<string, bool>>();
            int currentZOrder = 0;

            EnumDesktopWindows(hDesk, (hWnd, lParam) => {
                if (IsWindowVisible(hWnd)) {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(title) && title != "Program Manager") {
                        int cloaked = 0;
                        try {
                            DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out cloaked, sizeof(int));
                        } catch {}
                        bool isCloaked = (cloaked != 0);
                        if (!includeCloaked && isCloaked) {
                            return true;
                        }

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

                            RECT frameRect = r;
                            try {
                                RECT extRect;
                                if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out extRect, Marshal.SizeOf(typeof(RECT))) == 0) {
                                    frameRect = extRect;
                                }
                            } catch {}

                            IntPtr hMon = IntPtr.Zero;
                            try { hMon = MonitorFromWindow(hWnd, 2 /* MONITOR_DEFAULTTONEAREST */); } catch {}
                            string monDevice = "";
                            bool isPrimaryMon = false;
                            if (hMon != IntPtr.Zero) {
                                Tuple<string, bool> mInfo;
                                if (!monCache.TryGetValue(hMon, out mInfo)) {
                                    MONITORINFOEX mi = new MONITORINFOEX();
                                    mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                                    if (GetMonitorInfo(hMon, ref mi)) {
                                        mInfo = Tuple.Create(mi.szDevice, (mi.dwFlags & 1) != 0);
                                    } else {
                                        mInfo = Tuple.Create("", false);
                                    }
                                    monCache[hMon] = mInfo;
                                }
                                monDevice = mInfo.Item1;
                                isPrimaryMon = mInfo.Item2;
                            }

                            long exStyle = 0;
                            try { exStyle = GetWindowLongPtr(hWnd, -20 /* GWL_EXSTYLE */).ToInt64(); } catch {}
                            bool isTopmost = (exStyle & 0x00000008) != 0;

                            list.Add(new WindowMeta {
                                Handle = hWnd,
                                Pid = pid,
                                ProcessName = procName,
                                ClassName = sbClass.ToString().Trim(),
                                Title = title,
                                Rect = r,
                                FrameRect = frameRect,
                                IsForeground = (hWnd == hFore),
                                IsMinimized = IsIconic(hWnd),
                                IsMaximized = IsZoomed(hWnd),
                                IsHung = IsHungAppWindow(hWnd),
                                IsElevated = isElevated,
                                IsCloaked = isCloaked,
                                ZOrder = currentZOrder++,
                                MonitorDevice = monDevice,
                                IsPrimaryMonitor = isPrimaryMon,
                                IsTopmost = isTopmost
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
                var windows = CollectDesktopWindows(hDesk, false);
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
            RECT frameRect = r;
            try {
                RECT extRect;
                if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out extRect, Marshal.SizeOf(typeof(RECT))) == 0) {
                    frameRect = extRect;
                }
            } catch {}

            int cloaked = 0;
            try { DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out cloaked, sizeof(int)); } catch {}

            bool isMin = IsIconic(hWnd);
            bool isMax = IsZoomed(hWnd);
            bool isHung = IsHungAppWindow(hWnd);
            bool isElevated = IsProcessElevated(pid);

            IntPtr hMon = IntPtr.Zero;
            try { hMon = MonitorFromWindow(hWnd, 2); } catch {}
            string monDevice = "";
            bool isPrimaryMon = false;
            if (hMon != IntPtr.Zero) {
                MONITORINFOEX mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (GetMonitorInfo(hMon, ref mi)) {
                    monDevice = mi.szDevice;
                    isPrimaryMon = (mi.dwFlags & 1) != 0;
                }
            }
            long exStyle = 0;
            try { exStyle = GetWindowLongPtr(hWnd, -20).ToInt64(); } catch {}
            bool isTopmost = (exStyle & 0x00000008) != 0;

            Console.WriteLine(string.Format("{{\"success\": true, \"handle\": \"{0}\", \"pid\": {1}, \"process\": \"{2}\", \"class\": \"{3}\", \"title\": \"{4}\", \"x\": {5}, \"y\": {6}, \"width\": {7}, \"height\": {8}, \"frameX\": {9}, \"frameY\": {10}, \"frameWidth\": {11}, \"frameHeight\": {12}, \"isMinimized\": {13}, \"isMaximized\": {14}, \"isHung\": {15}, \"isElevated\": {16}, \"isCloaked\": {17}, \"isTopmost\": {18}, \"monitor\": \"{19}\", \"isPrimaryMonitor\": {20}}}",
                hWnd, pid, EscapeJson(procName), EscapeJson(className), EscapeJson(title),
                r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                frameRect.Left, frameRect.Top, frameRect.Right - frameRect.Left, frameRect.Bottom - frameRect.Top,
                isMin ? "true" : "false", isMax ? "true" : "false", isHung ? "true" : "false",
                isElevated ? "true" : "false", (cloaked != 0) ? "true" : "false",
                isTopmost ? "true" : "false", EscapeJson(monDevice), isPrimaryMon ? "true" : "false"));
        }

        static void ListWindows(bool includeCloaked = false) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            if (hDesk == IntPtr.Zero) {
                Console.WriteLine("[]");
                return;
            }

            var windows = CollectDesktopWindows(hDesk, includeCloaked);
            var list = new List<string>();
            foreach (var w in windows) {
                int width = w.Rect.Right - w.Rect.Left;
                int height = w.Rect.Bottom - w.Rect.Top;
                int fWidth = w.FrameRect.Right - w.FrameRect.Left;
                int fHeight = w.FrameRect.Bottom - w.FrameRect.Top;
                list.Add(string.Format("{{\"handle\": \"{0}\", \"pid\": {1}, \"process\": \"{2}\", \"class\": \"{3}\", \"title\": \"{4}\", \"x\": {5}, \"y\": {6}, \"width\": {7}, \"height\": {8}, \"frameX\": {9}, \"frameY\": {10}, \"frameWidth\": {11}, \"frameHeight\": {12}, \"isForeground\": {13}, \"isMinimized\": {14}, \"isMaximized\": {15}, \"isElevated\": {16}, \"isCloaked\": {17}, \"zOrder\": {18}, \"isTopmost\": {19}, \"monitor\": \"{20}\", \"isPrimaryMonitor\": {21}}}",
                    w.Handle, w.Pid, EscapeJson(w.ProcessName), EscapeJson(w.ClassName), EscapeJson(w.Title),
                    w.Rect.Left, w.Rect.Top, width, height,
                    w.FrameRect.Left, w.FrameRect.Top, fWidth, fHeight,
                    w.IsForeground ? "true" : "false", w.IsMinimized ? "true" : "false", w.IsMaximized ? "true" : "false",
                    w.IsElevated ? "true" : "false", w.IsCloaked ? "true" : "false",
                    w.ZOrder, w.IsTopmost ? "true" : "false", EscapeJson(w.MonitorDevice), w.IsPrimaryMonitor ? "true" : "false"));
            }

            Console.WriteLine("[" + string.Join(",", list.ToArray()) + "]");
        }

        static bool FindWindow(IntPtr hDesk, string query, out IntPtr targetHwnd, out string actualTitle) {
            targetHwnd = IntPtr.Zero;
            actualTitle = "";

            long handleNum = 0;
            bool isHandle = long.TryParse(query, out handleNum);

            var candidates = CollectDesktopWindows(hDesk, true);

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
            RECT frameRect = r;
            try {
                RECT extRect;
                if (DwmGetWindowAttribute(targetHwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out extRect, Marshal.SizeOf(typeof(RECT))) == 0) {
                    frameRect = extRect;
                }
            } catch {}

            int cloaked = 0;
            try { DwmGetWindowAttribute(targetHwnd, DWMWA_CLOAKED, out cloaked, sizeof(int)); } catch {}

            IntPtr hFore = GetForegroundWindow();
            bool isElevated = IsProcessElevated(pid);

            Console.WriteLine(string.Format("{{\"success\": true, \"handle\": \"{0}\", \"pid\": {1}, \"process\": \"{2}\", \"class\": \"{3}\", \"title\": \"{4}\", \"x\": {5}, \"y\": {6}, \"width\": {7}, \"height\": {8}, \"frameX\": {9}, \"frameY\": {10}, \"frameWidth\": {11}, \"frameHeight\": {12}, \"isForeground\": {13}, \"isMinimized\": {14}, \"isMaximized\": {15}, \"isHung\": {16}, \"isElevated\": {17}, \"isCloaked\": {18}}}",
                targetHwnd, pid, EscapeJson(procName), EscapeJson(sbClass.ToString().Trim()), EscapeJson(actualTitle),
                r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                frameRect.Left, frameRect.Top, frameRect.Right - frameRect.Left, frameRect.Bottom - frameRect.Top,
                (targetHwnd == hFore) ? "true" : "false",
                IsIconic(targetHwnd) ? "true" : "false",
                IsZoomed(targetHwnd) ? "true" : "false",
                IsHungAppWindow(targetHwnd) ? "true" : "false",
                isElevated ? "true" : "false", (cloaked != 0) ? "true" : "false"));
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

            bool wasMaximized = IsZoomed(hWnd);
            WINDOWPLACEMENT wp = new WINDOWPLACEMENT();
            wp.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
            if (GetWindowPlacement(hWnd, ref wp)) {
                if (wp.showCmd == SW_SHOWMAXIMIZED || (wp.flags & WPF_RESTORETOMAXIMIZED) != 0) {
                    wasMaximized = true;
                }
            }

            keybd_event(0x12, 0, 0, 0); // Alt down

            if (wasMaximized) {
                // Window is or was maximized - maintain maximized state, NEVER call SW_RESTORE
                ShowWindow(hWnd, SW_SHOWMAXIMIZED);
            } else if (IsIconic(hWnd)) {
                ShowWindow(hWnd, SW_RESTORE);
            } else {
                ShowWindow(hWnd, SW_SHOW);
            }

            if (!wasMaximized) {
                try { SwitchToThisWindow(hWnd, false); } catch {}
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

        static void MaximizeWindowCmd(string titleFilter) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            ShowWindow(targetHwnd, SW_SHOWMAXIMIZED);
            ForceForegroundWindow(targetHwnd);
            Console.WriteLine(string.Format("{{\"success\": true, \"title\": \"{0}\", \"handle\": \"{1}\", \"isMaximized\": {2}}}",
                EscapeJson(actualTitle), targetHwnd, IsZoomed(targetHwnd) ? "true" : "false"));
        }

        static void MinimizeWindowCmd(string titleFilter) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            ShowWindow(targetHwnd, SW_SHOWMINIMIZED);
            Console.WriteLine(string.Format("{{\"success\": true, \"title\": \"{0}\", \"handle\": \"{1}\", \"isMinimized\": {2}}}",
                EscapeJson(actualTitle), targetHwnd, IsIconic(targetHwnd) ? "true" : "false"));
        }

        static void RestoreWindowCmd(string titleFilter) {
            IntPtr hDesk = EnsureInteractiveDesktop();
            IntPtr targetHwnd;
            string actualTitle;

            if (!FindWindow(hDesk, titleFilter, out targetHwnd, out actualTitle)) {
                Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                return;
            }

            ShowWindow(targetHwnd, SW_RESTORE);
            ForceForegroundWindow(targetHwnd);
            Console.WriteLine(string.Format("{{\"success\": true, \"title\": \"{0}\", \"handle\": \"{1}\", \"isMaximized\": {2}}}",
                EscapeJson(actualTitle), targetHwnd, IsZoomed(targetHwnd) ? "true" : "false"));
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

        static Bitmap AcquireWindowBitmap(IntPtr targetHwnd, RECT r, out string captureMethod) {
            captureMethod = "dxgi_hardware_duplication";
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            if (w <= 0 || h <= 0) return null;

            // Tier 1: Hardware DirectX 11 Desktop Duplication (sub-2ms VRAM direct)
            try {
                Bitmap dxBmp = DxgiCaptureEngine.CaptureBitmap(r.Left, r.Top, w, h);
                if (dxBmp != null) {
                    return dxBmp;
                }
            } catch (Exception ex) {
                Console.Error.WriteLine("DXGI Tier 1 fallback: " + ex.Message);
            }

            // Tier 2 & Tier 3 Fallbacks: Direct GDI or PrintWindow
            try {
                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
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
                return bmp;
            } catch (Exception ex) {
                Console.Error.WriteLine("GDI Fallback error: " + ex.Message);
                return null;
            }
        }

        static byte[] ComputePerceptualHash(Bitmap src) {
            byte[] hash = new byte[256];
            using (Bitmap thumb = new Bitmap(16, 16, PixelFormat.Format24bppRgb)) {
                using (Graphics g = Graphics.FromImage(thumb)) {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                    g.DrawImage(src, 0, 0, 16, 16);
                }
                int idx = 0;
                for (int y = 0; y < 16; y++) {
                    for (int x = 0; x < 16; x++) {
                        Color c = thumb.GetPixel(x, y);
                        byte lum = (byte)((c.R * 299 + c.G * 587 + c.B * 114) / 1000);
                        hash[idx++] = lum;
                    }
                }
            }
            return hash;
        }

        static double ComputeHashDiff(byte[] h1, byte[] h2) {
            if (h1 == null || h2 == null || h1.Length != h2.Length) return 1.0;
            long totalDiff = 0;
            for (int i = 0; i < h1.Length; i++) {
                totalDiff += Math.Abs((int)h1[i] - (int)h2[i]);
            }
            return (double)totalDiff / (h1.Length * 255.0);
        }

        static Bitmap ScaleBitmapPreserveAspect(Bitmap src, int maxDim) {
            if (maxDim <= 0 || (src.Width <= maxDim && src.Height <= maxDim)) {
                return new Bitmap(src);
            }
            int targetW, targetH;
            if (src.Width >= src.Height) {
                targetW = maxDim;
                targetH = Math.Max(1, (int)Math.Round((double)src.Height * maxDim / src.Width));
            } else {
                targetH = maxDim;
                targetW = Math.Max(1, (int)Math.Round((double)src.Width * maxDim / src.Height));
            }
            Bitmap scaled = new Bitmap(targetW, targetH, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(scaled)) {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(src, 0, 0, targetW, targetH);
            }
            return scaled;
        }

        static string SanitizeOpticsKey(string input) {
            if (string.IsNullOrEmpty(input)) return "screen";
            StringBuilder sb = new StringBuilder();
            foreach (char c in input) {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
                else sb.Append('_');
            }
            return sb.ToString().ToLowerInvariant();
        }

        static void CaptureWindow(string titleFilter, string destPath, int maxDim) {
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

            string captureMethod;
            using (Bitmap rawBmp = AcquireWindowBitmap(targetHwnd, r, out captureMethod)) {
                if (rawBmp == null) {
                    Console.WriteLine("{\"success\": false, \"error\": \"All 3 capture tiers failed\"}");
                    return;
                }

                using (Bitmap finalBmp = maxDim > 0 ? ScaleBitmapPreserveAspect(rawBmp, maxDim) : new Bitmap(rawBmp)) {
                    string dir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                        Directory.CreateDirectory(dir);
                    }
                    finalBmp.Save(destPath, ImageFormat.Png);

                    string escTitle = EscapeJson(actualTitle);
                    string escPath = destPath.Replace("\\", "/");
                    Console.WriteLine(string.Format("{{\"success\": true, \"title\": \"{0}\", \"width\": {1}, \"height\": {2}, \"nativeWidth\": {3}, \"nativeHeight\": {4}, \"method\": \"{5}\", \"fore\": \"{6}\", \"target\": \"{7}\", \"path\": \"{8}\"}}",
                        escTitle, finalBmp.Width, finalBmp.Height, w, h, captureMethod, GetForegroundWindow(), targetHwnd, escPath));
                }
            }
        }

        static void DeltaCaptureWindow(string titleFilter, string destPath, int maxDim, double diffThreshold) {
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

            string captureMethod;
            using (Bitmap rawBmp = AcquireWindowBitmap(targetHwnd, r, out captureMethod)) {
                if (rawBmp == null) {
                    Console.WriteLine("{\"success\": false, \"error\": \"All 3 capture tiers failed\"}");
                    return;
                }

                byte[] newHash = ComputePerceptualHash(rawBmp);
                string hashFile = Path.Combine(Path.GetTempPath(), "gemini_optics_" + SanitizeOpticsKey(titleFilter) + ".hash");
                double diff = 1.0;
                bool hasOld = false;

                if (File.Exists(hashFile)) {
                    try {
                        byte[] oldHash = File.ReadAllBytes(hashFile);
                        if (oldHash.Length == newHash.Length) {
                            diff = ComputeHashDiff(newHash, oldHash);
                            hasOld = true;
                        }
                    } catch {}
                }

                if (hasOld && diff < diffThreshold) {
                    string escT = EscapeJson(actualTitle);
                    string escP = destPath.Replace("\\", "/");
                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"changed\": false, \"diff\": {0}, \"estimatedTokens\": 0, \"title\": \"{1}\", \"width\": {2}, \"height\": {3}, \"method\": \"{4}\", \"path\": \"{5}\"}}",
                        diff.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                        escT, w, h, captureMethod, escP));
                    return;
                }

                try {
                    File.WriteAllBytes(hashFile, newHash);
                } catch {}

                int targetMax = maxDim > 0 ? maxDim : 768;
                using (Bitmap scaledBmp = ScaleBitmapPreserveAspect(rawBmp, targetMax)) {
                    string dir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                        Directory.CreateDirectory(dir);
                    }
                    scaledBmp.Save(destPath, ImageFormat.Png);

                    string escTitle = EscapeJson(actualTitle);
                    string escPath = destPath.Replace("\\", "/");
                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"changed\": true, \"diff\": {0}, \"estimatedTokens\": 258, \"title\": \"{1}\", \"width\": {2}, \"height\": {3}, \"nativeWidth\": {4}, \"nativeHeight\": {5}, \"method\": \"{6}\", \"fore\": \"{7}\", \"target\": \"{8}\", \"path\": \"{9}\"}}",
                        diff.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                        escTitle, scaledBmp.Width, scaledBmp.Height, w, h, captureMethod, GetForegroundWindow(), targetHwnd, escPath));
                }
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

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
using System.Management;
using System.ServiceProcess;
using System.Diagnostics.Eventing.Reader;
using Microsoft.Win32;
using System.IO.Pipes;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography.X509Certificates;

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

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVINFO_DATA {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_CLASSINSTALL_HEADER {
            public uint cbSize;
            public uint InstallFunction;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_PROPCHANGE_PARAMS {
            public SP_CLASSINSTALL_HEADER ClassInstallHeader;
            public uint StateChange;
            public uint Scope;
            public uint HwProfile;
        }

        const uint DIGCF_PRESENT = 0x00000002;
        const uint DIGCF_ALLCLASSES = 0x00000004;

        const uint SPDRP_DEVICEDESC = 0x00000000;
        const uint SPDRP_HARDWAREID = 0x00000001;
        const uint SPDRP_CLASS = 0x00000007;
        const uint SPDRP_CLASSGUID = 0x00000008;
        const uint SPDRP_DRIVER = 0x00000009;
        const uint SPDRP_MFG = 0x0000000B;
        const uint SPDRP_FRIENDLYNAME = 0x0000000C;

        const uint DIF_PROPERTYCHANGE = 0x00000012;
        const uint DICS_ENABLE = 0x00000001;
        const uint DICS_DISABLE = 0x00000002;
        const uint DICS_PROPCHANGE = 0x00000003;
        const uint DICS_FLAG_GLOBAL = 0x00000001;

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(IntPtr ClassGuid, string Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiGetDeviceRegistryProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, out uint PropertyRegDataType, byte[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiGetDeviceInstanceId(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, StringBuilder DeviceInstanceId, uint DeviceInstanceIdSize, out uint RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiSetClassInstallParams(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref SP_PROPCHANGE_PARAMS ClassInstallParams, uint ClassInstallParamsSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiCallClassInstaller(uint InstallFunction, IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        static extern int CM_Get_DevNode_Status(out uint pulStatus, out uint pulProblemNumber, uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        static extern int CM_Reenumerate_DevNode(uint dnDevInst, uint ulFlags);

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

        [DllImport("ole32.dll")]
        static extern int PropVariantClear(ref PROPVARIANT pvar);

        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Beep(uint dwFreq, uint dwDuration);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool MessageBeep(uint uType);

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorComObject {}

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection deviceCollection);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
            int GetDevice(string pwstrId, out IMMDevice endpoint);
            int RegisterEndpointNotificationCallback(IntPtr pClient);
            int UnregisterEndpointNotificationCallback(IntPtr pClient);
        }

        [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection {
            [PreserveSig] int GetCount(out uint pcDevices);
            [PreserveSig] int Item(uint nDevice, out IMMDevice ppDevice);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROPERTYKEY {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct PROPVARIANT {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pwszVal;
            [FieldOffset(8)] public int iVal;
            [FieldOffset(8)] public uint uiVal;
        }

        [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore {
            [PreserveSig] int GetCount(out uint cProps);
            [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
            [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
            [PreserveSig] int Commit();
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice {
            [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            [PreserveSig] int GetState(out int pdwState);
        }

        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionManager2 {
            [PreserveSig] int GetAudioSessionControl(ref Guid AudioSessionGuid, uint StreamFlags, out IntPtr SessionControl);
            [PreserveSig] int GetSimpleAudioVolume(ref Guid AudioSessionGuid, uint StreamFlags, out ISimpleAudioVolume AudioVolume);
            [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
            [PreserveSig] int RegisterSessionNotification(IntPtr NewSessionNotification);
            [PreserveSig] int UnregisterSessionNotification(IntPtr NewSessionNotification);
            [PreserveSig] int RegisterDuckNotification(string sessionID, IntPtr duckNotification);
            [PreserveSig] int UnregisterDuckNotification(IntPtr duckNotification);
        }

        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionEnumerator {
            [PreserveSig] int GetCount(out int SessionCount);
            [PreserveSig] int GetSession(int SessionIndex, out IAudioSessionControl Session);
        }

        [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionControl {
            [PreserveSig] int GetState(out int pRetVal);
            [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
            [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
            [PreserveSig] int GetGroupingParam(out Guid pRetVal);
            [PreserveSig] int SetGroupingParam(ref Guid Override, ref Guid EventContext);
            [PreserveSig] int RegisterAudioSessionNotification(IntPtr NewNotifications);
            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr NewNotifications);
        }

        [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionControl2 : IAudioSessionControl {
            [PreserveSig] new int GetState(out int pRetVal);
            [PreserveSig] new int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            [PreserveSig] new int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
            [PreserveSig] new int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            [PreserveSig] new int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
            [PreserveSig] new int GetGroupingParam(out Guid pRetVal);
            [PreserveSig] new int SetGroupingParam(ref Guid Override, ref Guid EventContext);
            [PreserveSig] new int RegisterAudioSessionNotification(IntPtr NewNotifications);
            [PreserveSig] new int UnregisterAudioSessionNotification(IntPtr NewNotifications);
            [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            [PreserveSig] int GetProcessId(out uint pRetVal);
            [PreserveSig] int IsSystemSoundsSession();
            [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
        }

        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISimpleAudioVolume {
            [PreserveSig] int SetMasterVolume(float fLevel, ref Guid EventContext);
            [PreserveSig] int GetMasterVolume(out float pfLevel);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid EventContext);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }

        [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioMeterInformation {
            [PreserveSig] int GetPeakValue(out float pfPeak);
            [PreserveSig] int GetMeteringChannelCount(out uint pnChannelCount);
            [PreserveSig] int GetChannelsPeakValues(uint u32ChannelCount, [In, Out] float[] afPeakValues);
            [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
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
        public struct WAVEFORMATEX {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioClient {
            [PreserveSig] int Initialize(int shareMode, uint streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, ref Guid audioSessionGuid);
            [PreserveSig] int GetBufferSize(out uint numBufferFrames);
            [PreserveSig] int GetStreamLatency(out long hnsLatency);
            [PreserveSig] int GetCurrentPadding(out uint numPaddingFrames);
            [PreserveSig] int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
            [PreserveSig] int GetMixFormat(out IntPtr ppDeviceFormat);
            [PreserveSig] int GetDevicePeriod(out long hnsDefaultDevicePeriod, out long hnsMinimumDevicePeriod);
            [PreserveSig] int Start();
            [PreserveSig] int Stop();
            [PreserveSig] int Reset();
            [PreserveSig] int SetEventHandle(IntPtr eventHandle);
            [PreserveSig] int GetService(ref Guid interfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        }

        [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioCaptureClient {
            [PreserveSig] int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
            [PreserveSig] int ReleaseBuffer(uint numFramesRead);
            [PreserveSig] int GetNextPacketSize(out uint pNumFramesInNextPacket);
        }

        [ComImport]
        [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IVirtualDesktopManager {
            [PreserveSig]
            int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

            [PreserveSig]
            int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

            [PreserveSig]
            int MoveWindowToDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.LPStruct)] Guid desktopId);
        }

        [ComImport]
        [Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
        public class VirtualDesktopManagerComObject {}

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESSOR_POWER_INFORMATION {
            public uint Number;
            public uint MaxMhz;
            public uint CurrentMhz;
            public uint MhzLimit;
            public uint MaxIdleState;
            public uint CurrentIdleState;
        }

        [DllImport("powrprof.dll")]
        public static extern int CallNtPowerInformation(
            int InformationLevel,
            IntPtr lpInputBuffer,
            uint nInputBufferSize,
            IntPtr lpOutputBuffer,
            uint nOutputBufferSize
        );

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

        static void GetFirewallStatusCmd() {
            try {
                Type t = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                if (t == null) {
                    Console.WriteLine("{\"success\": false, \"error\": \"HNetCfg.FwPolicy2 COM ProgID not registered\"}");
                    return;
                }
                dynamic fw = Activator.CreateInstance(t);

                // Profiles: NET_FW_PROFILE2_DOMAIN = 1, NET_FW_PROFILE2_PRIVATE = 2, NET_FW_PROFILE2_PUBLIC = 4
                bool domainEnabled = fw.FirewallEnabled(1);
                bool privateEnabled = fw.FirewallEnabled(2);
                bool publicEnabled = fw.FirewallEnabled(4);

                // Default Actions: NET_FW_ACTION_BLOCK = 0, NET_FW_ACTION_ALLOW = 1
                int domainIn = fw.DefaultInboundAction(1);
                int domainOut = fw.DefaultOutboundAction(1);
                int privateIn = fw.DefaultInboundAction(2);
                int privateOut = fw.DefaultOutboundAction(2);
                int publicIn = fw.DefaultInboundAction(4);
                int publicOut = fw.DefaultOutboundAction(4);

                int rulesCount = fw.Rules.Count;

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"rulesCount\": {0}, \"profiles\": {{" +
                    "\"domain\": {{\"enabled\": {1}, \"defaultInbound\": \"{2}\", \"defaultOutbound\": \"{3}\"}}, " +
                    "\"private\": {{\"enabled\": {4}, \"defaultInbound\": \"{5}\", \"defaultOutbound\": \"{6}\"}}, " +
                    "\"public\": {{\"enabled\": {7}, \"defaultInbound\": \"{8}\", \"defaultOutbound\": \"{9}\"}}" +
                    "}}}}",
                    rulesCount,
                    domainEnabled ? "true" : "false", domainIn == 1 ? "allow" : "block", domainOut == 1 ? "allow" : "block",
                    privateEnabled ? "true" : "false", privateIn == 1 ? "allow" : "block", privateOut == 1 ? "allow" : "block",
                    publicEnabled ? "true" : "false", publicIn == 1 ? "allow" : "block", publicOut == 1 ? "allow" : "block"
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetFirewallRulesCmd(string directionFilter, string actionFilter, string protoFilter, int portFilter, string searchFilter, int limit) {
            try {
                if (limit <= 0) limit = 50;
                string dirF = (directionFilter ?? "").Trim().ToLowerInvariant();
                string actF = (actionFilter ?? "").Trim().ToLowerInvariant();
                string proF = (protoFilter ?? "").Trim().ToLowerInvariant();
                string searchF = (searchFilter ?? "").Trim().ToLowerInvariant();

                Type t = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                if (t == null) {
                    Console.WriteLine("{\"success\": false, \"error\": \"HNetCfg.FwPolicy2 COM ProgID not registered\"}");
                    return;
                }
                dynamic fw = Activator.CreateInstance(t);

                var list = new List<string>();
                int totalMatched = 0;

                foreach (dynamic r in fw.Rules) {
                    try {
                        string name = r.Name ?? "";
                        string desc = r.Description ?? "";
                        int dir = r.Direction; // 1 = Inbound, 2 = Outbound
                        int act = r.Action;    // 1 = Allow, 0 = Block
                        int proto = r.Protocol; // 6 = TCP, 17 = UDP, 256 = Any
                        string localPorts = r.LocalPorts ?? "";
                        string appName = r.ApplicationName ?? "";
                        string serviceName = r.ServiceName ?? "";
                        bool enabled = r.Enabled;
                        int profiles = r.Profiles;

                        string dirStr = dir == 1 ? "inbound" : "outbound";
                        string actStr = act == 1 ? "allow" : "block";
                        string proStr = proto == 6 ? "tcp" : (proto == 17 ? "udp" : (proto == 256 ? "any" : proto.ToString()));

                        if (!string.IsNullOrEmpty(dirF) && dirF != "all" && dirStr != dirF) continue;
                        if (!string.IsNullOrEmpty(actF) && actF != "all" && actStr != actF) continue;
                        if (!string.IsNullOrEmpty(proF) && proF != "any" && proStr != proF && proStr != "any") continue;

                        if (portFilter > 0) {
                            if (string.IsNullOrEmpty(localPorts)) continue;
                            string pStr = portFilter.ToString();
                            bool portMatch = false;
                            string[] parts = localPorts.Split(',');
                            foreach (var p in parts) {
                                string trimP = p.Trim();
                                if (trimP == pStr) { portMatch = true; break; }
                                if (trimP.Contains("-")) {
                                    string[] rng = trimP.Split('-');
                                    int lo, hi;
                                    if (int.TryParse(rng[0], out lo) && int.TryParse(rng[1], out hi)) {
                                        if (portFilter >= lo && portFilter <= hi) { portMatch = true; break; }
                                    }
                                }
                            }
                            if (!portMatch) continue;
                        }

                        if (!string.IsNullOrEmpty(searchF)) {
                            bool matched = name.ToLowerInvariant().Contains(searchF) ||
                                           desc.ToLowerInvariant().Contains(searchF) ||
                                           appName.ToLowerInvariant().Contains(searchF) ||
                                           serviceName.ToLowerInvariant().Contains(searchF);
                            if (!matched) continue;
                        }

                        totalMatched++;
                        if (list.Count < limit) {
                            list.Add(string.Format(
                                "{{\"name\": \"{0}\", \"description\": \"{1}\", \"direction\": \"{2}\", \"action\": \"{3}\", \"protocol\": \"{4}\", \"localPorts\": \"{5}\", \"appName\": \"{6}\", \"serviceName\": \"{7}\", \"enabled\": {8}, \"profiles\": {9}}}",
                                EscapeJson(name), EscapeJson(desc), dirStr, actStr, proStr, EscapeJson(localPorts),
                                EscapeJson(appName), EscapeJson(serviceName), enabled ? "true" : "false", profiles
                            ));
                        }
                    } catch {}
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"count\": {0}, \"totalMatched\": {1}, \"rules\": [{2}]}}",
                    list.Count, totalMatched, string.Join(", ", list.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void ManageFirewallRuleCmd(string action, string name, string description, string direction, string protocol, string localPorts, string appPath, string ruleAction, string profiles) {
            try {
                if (string.IsNullOrEmpty(name)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Rule name is required\"}");
                    return;
                }

                string act = (action ?? "add").Trim().ToLowerInvariant();
                Type t = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                if (t == null) {
                    Console.WriteLine("{\"success\": false, \"error\": \"HNetCfg.FwPolicy2 COM ProgID not registered\"}");
                    return;
                }
                dynamic fw = Activator.CreateInstance(t);

                if (act == "delete" || act == "remove") {
                    try {
                        fw.Rules.Remove(name);
                        Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"delete\", \"name\": \"{0}\", \"deleted\": true}}", EscapeJson(name)));
                    } catch (Exception ex) {
                        Console.WriteLine(string.Format("{{\"success\": false, \"action\": \"delete\", \"name\": \"{0}\", \"error\": \"{1}\"}}", EscapeJson(name), EscapeJson(ex.Message)));
                    }
                    return;
                }

                if (act == "enable" || act == "disable") {
                    try {
                        dynamic rule = fw.Rules.Item(name);
                        rule.Enabled = (act == "enable");
                        Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"{0}\", \"name\": \"{1}\", \"enabled\": {2}}}",
                            act, EscapeJson(name), act == "enable" ? "true" : "false"));
                    } catch (Exception ex) {
                        Console.WriteLine(string.Format("{{\"success\": false, \"action\": \"{0}\", \"name\": \"{1}\", \"error\": \"{2}\"}}", act, EscapeJson(name), EscapeJson(ex.Message)));
                    }
                    return;
                }

                if (act == "add") {
                    Type ruleType = Type.GetTypeFromProgID("HNetCfg.FWRule");
                    if (ruleType == null) {
                        Console.WriteLine("{\"success\": false, \"error\": \"HNetCfg.FWRule COM ProgID not registered\"}");
                        return;
                    }
                    dynamic newRule = Activator.CreateInstance(ruleType);
                    newRule.Name = name;
                    if (!string.IsNullOrEmpty(description)) newRule.Description = description;
                    if (!string.IsNullOrEmpty(appPath)) newRule.ApplicationName = appPath;

                    string dir = (direction ?? "inbound").Trim().ToLowerInvariant();
                    newRule.Direction = dir == "outbound" ? 2 : 1;

                    string pro = (protocol ?? "tcp").Trim().ToLowerInvariant();
                    if (pro == "udp") newRule.Protocol = 17;
                    else if (pro == "any") newRule.Protocol = 256;
                    else newRule.Protocol = 6; // TCP default

                    if (!string.IsNullOrEmpty(localPorts)) newRule.LocalPorts = localPorts;

                    string rAct = (ruleAction ?? "allow").Trim().ToLowerInvariant();
                    newRule.Action = rAct == "block" ? 0 : 1;

                    string prof = (profiles ?? "all").Trim().ToLowerInvariant();
                    if (prof == "domain") newRule.Profiles = 1;
                    else if (prof == "private") newRule.Profiles = 2;
                    else if (prof == "public") newRule.Profiles = 4;
                    else newRule.Profiles = 7; // NET_FW_PROFILE2_ALL

                    newRule.Enabled = true;

                    // Remove existing rule with same name if present
                    try { fw.Rules.Remove(name); } catch {}

                    fw.Rules.Add(newRule);

                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"action\": \"add\", \"name\": \"{0}\", \"direction\": \"{1}\", \"protocol\": \"{2}\", \"localPorts\": \"{3}\", \"actionType\": \"{4}\", \"enabled\": true}}",
                        EscapeJson(name), dir, pro, EscapeJson(localPorts ?? ""), rAct
                    ));
                    return;
                }

                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Unknown firewall action '{0}'\"}}", EscapeJson(act)));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static string GetTaskStateName(int state) {
            switch (state) {
                case 1: return "disabled";
                case 2: return "queued";
                case 3: return "ready";
                case 4: return "running";
                default: return "unknown";
            }
        }

        static string FormatTaskDate(DateTime dt) {
            if (dt.Year <= 1900 || dt.Year == 1999) return "null";
            return "\"" + dt.ToString("o") + "\"";
        }

        static string GetActionTypeName(int type) {
            switch (type) {
                case 0: return "exec";
                case 5: return "com_handler";
                case 6: return "email";
                case 7: return "message";
                default: return "type_" + type;
            }
        }

        static string GetTriggerTypeName(int type) {
            switch (type) {
                case 0: return "event";
                case 1: return "time";
                case 2: return "daily";
                case 3: return "weekly";
                case 4: return "monthly";
                case 5: return "monthly_dow";
                case 6: return "idle";
                case 7: return "registration";
                case 8: return "boot";
                case 9: return "logon";
                case 11: return "session_change";
                default: return "type_" + type;
            }
        }

        static void TaskSchedulerListCmd(string folderPath, bool recursive, string stateFilter, string searchFilter, int limit) {
            try {
                if (string.IsNullOrEmpty(folderPath)) folderPath = "\\";
                if (limit <= 0) limit = 50;
                string sFilter = (stateFilter ?? "all").Trim().ToLowerInvariant();
                string qFilter = (searchFilter ?? "").Trim().ToLowerInvariant();

                Type t = Type.GetTypeFromProgID("Schedule.Service");
                if (t == null) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Schedule.Service COM ProgID not registered\"}");
                    return;
                }
                dynamic ts = Activator.CreateInstance(t);
                ts.Connect();

                dynamic rootFolder = ts.GetFolder(folderPath);
                var list = new List<string>();
                int totalMatched = 0;

                EnumerateFolderTasks(rootFolder, recursive, 0, 10, list, sFilter, qFilter, limit, ref totalMatched);

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"folder\": \"{0}\", \"recursive\": {1}, \"count\": {2}, \"totalMatched\": {3}, \"tasks\": [{4}]}}",
                    EscapeJson(folderPath), recursive ? "true" : "false", list.Count, totalMatched, string.Join(", ", list.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void EnumerateFolderTasks(dynamic folder, bool recursive, int depth, int maxDepth, List<string> list, string stateFilter, string searchFilter, int limit, ref int totalMatched) {
            try {
                dynamic tasks = folder.GetTasks(1);
                foreach (dynamic task in tasks) {
                    try {
                        string name = task.Name ?? "";
                        string path = task.Path ?? "";
                        int state = 0;
                        try { state = task.State; } catch {}
                        string stateStr = GetTaskStateName(state);
                        bool enabled = true;
                        try { enabled = task.Enabled; } catch {}

                        if (!string.IsNullOrEmpty(stateFilter) && stateFilter != "all" && stateStr != stateFilter) {
                            continue;
                        }

                        DateTime lastRun = DateTime.MinValue;
                        try { lastRun = task.LastRunTime; } catch {}
                        int lastResult = 0;
                        try { lastResult = task.LastTaskResult; } catch {}
                        DateTime nextRun = DateTime.MinValue;
                        try { nextRun = task.NextRunTime; } catch {}

                        string actionSummary = "";
                        try {
                            dynamic def = task.Definition;
                            dynamic actions = def.Actions;
                            if (actions != null) {
                                foreach (dynamic act in actions) {
                                    int aType = act.Type;
                                    if (aType == 0) {
                                        actionSummary = act.Path ?? "";
                                        string aArgs = "";
                                        try { aArgs = act.Arguments ?? ""; } catch {}
                                        if (!string.IsNullOrEmpty(aArgs)) actionSummary += " " + aArgs;
                                    } else if (aType == 5) {
                                        actionSummary = "COM: " + (act.ClassId ?? "");
                                    } else {
                                        actionSummary = GetActionTypeName(aType);
                                    }
                                    break;
                                }
                            }
                        } catch {}

                        if (!string.IsNullOrEmpty(searchFilter)) {
                            bool m = name.ToLowerInvariant().Contains(searchFilter) ||
                                     path.ToLowerInvariant().Contains(searchFilter) ||
                                     actionSummary.ToLowerInvariant().Contains(searchFilter);
                            if (!m) continue;
                        }

                        totalMatched++;
                        if (list.Count < limit) {
                            list.Add(string.Format(
                                "{{\"name\": \"{0}\", \"path\": \"{1}\", \"state\": \"{2}\", \"enabled\": {3}, \"lastRunTime\": {4}, \"lastTaskResult\": {5}, \"nextRunTime\": {6}, \"actionSummary\": \"{7}\", \"folder\": \"{8}\"}}",
                                EscapeJson(name), EscapeJson(path), stateStr, enabled ? "true" : "false",
                                FormatTaskDate(lastRun), lastResult, FormatTaskDate(nextRun),
                                EscapeJson(actionSummary), EscapeJson(folder.Path ?? "")
                            ));
                        }
                    } catch {}
                }
            } catch {}

            if (recursive && depth < maxDepth) {
                try {
                    dynamic subs = folder.GetFolders(0);
                    foreach (dynamic sf in subs) {
                        EnumerateFolderTasks(sf, recursive, depth + 1, maxDepth, list, stateFilter, searchFilter, limit, ref totalMatched);
                    }
                } catch {}
            }
        }

        static void TaskSchedulerInfoCmd(string taskPath) {
            try {
                if (string.IsNullOrEmpty(taskPath)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"taskPath is required\"}");
                    return;
                }

                Type t = Type.GetTypeFromProgID("Schedule.Service");
                if (t == null) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Schedule.Service COM ProgID not registered\"}");
                    return;
                }
                dynamic ts = Activator.CreateInstance(t);
                ts.Connect();

                string normPath = (taskPath ?? "").Trim().Replace('/', '\\');
                if (!normPath.StartsWith("\\") && normPath.Contains("\\")) {
                    normPath = "\\" + normPath;
                }
                int lastSlash = normPath.LastIndexOf('\\');
                string folderPath = "\\";
                string taskName = normPath;
                if (lastSlash > 0) {
                    folderPath = normPath.Substring(0, lastSlash);
                    taskName = normPath.Substring(lastSlash + 1);
                } else if (lastSlash == 0) {
                    folderPath = "\\";
                    taskName = normPath.Substring(1);
                }

                dynamic folder = ts.GetFolder(folderPath);
                dynamic task = folder.GetTask(taskName);

                string name = task.Name ?? "";
                string path = task.Path ?? "";
                int state = 0;
                try { state = task.State; } catch {}
                string stateStr = GetTaskStateName(state);
                bool enabled = true;
                try { enabled = task.Enabled; } catch {}

                DateTime lastRun = DateTime.MinValue;
                try { lastRun = task.LastRunTime; } catch {}
                int lastResult = 0;
                try { lastResult = task.LastTaskResult; } catch {}
                DateTime nextRun = DateTime.MinValue;
                try { nextRun = task.NextRunTime; } catch {}
                int missedRuns = 0;
                try { missedRuns = task.NumberOfMissedRuns; } catch {}

                dynamic def = task.Definition;
                dynamic reg = def.RegistrationInfo;
                string author = ""; try { author = reg.Author ?? ""; } catch {}
                string desc = ""; try { desc = reg.Description ?? ""; } catch {}
                string date = ""; try { date = reg.Date ?? ""; } catch {}
                string version = ""; try { version = reg.Version ?? ""; } catch {}
                string uri = ""; try { uri = reg.URI ?? ""; } catch {}

                dynamic princ = def.Principal;
                string userId = ""; try { userId = princ.UserId ?? ""; } catch {}
                int logonType = 0; try { logonType = princ.LogonType; } catch {}
                int runLevel = 0; try { runLevel = princ.RunLevel; } catch {}
                string runLevelStr = (runLevel == 1) ? "highest_available" : "least_privilege";

                dynamic sett = def.Settings;
                bool allowDemand = false; try { allowDemand = sett.AllowDemandStart; } catch {}
                bool disallowBatteries = false; try { disallowBatteries = sett.DisallowStartIfOnBatteries; } catch {}
                bool stopBatteries = false; try { stopBatteries = sett.StopIfGoingOnBatteries; } catch {}
                bool hidden = false; try { hidden = sett.Hidden; } catch {}
                string execLimit = ""; try { execLimit = sett.ExecutionTimeLimit ?? ""; } catch {}
                int restartCount = 0; try { restartCount = sett.RestartCount; } catch {}

                var actionsList = new List<string>();
                try {
                    foreach (dynamic act in def.Actions) {
                        int aType = act.Type;
                        string actTypeStr = GetActionTypeName(aType);
                        string aPath = ""; try { aPath = act.Path ?? ""; } catch {}
                        string aArgs = ""; try { aArgs = act.Arguments ?? ""; } catch {}
                        string aDir = ""; try { aDir = act.WorkingDirectory ?? ""; } catch {}
                        string aClsid = ""; try { aClsid = act.ClassId ?? ""; } catch {}

                        actionsList.Add(string.Format(
                            "{{\"type\": \"{0}\", \"path\": \"{1}\", \"arguments\": \"{2}\", \"workingDirectory\": \"{3}\", \"classId\": \"{4}\"}}",
                            actTypeStr, EscapeJson(aPath), EscapeJson(aArgs), EscapeJson(aDir), EscapeJson(aClsid)
                        ));
                    }
                } catch {}

                var triggersList = new List<string>();
                try {
                    foreach (dynamic tr in def.Triggers) {
                        int trType = tr.Type;
                        string trTypeStr = GetTriggerTypeName(trType);
                        bool trEnabled = true; try { trEnabled = tr.Enabled; } catch {}
                        string startBoundary = ""; try { startBoundary = tr.StartBoundary ?? ""; } catch {}
                        string endBoundary = ""; try { endBoundary = tr.EndBoundary ?? ""; } catch {}

                        triggersList.Add(string.Format(
                            "{{\"type\": \"{0}\", \"enabled\": {1}, \"startBoundary\": \"{2}\", \"endBoundary\": \"{3}\"}}",
                            trTypeStr, trEnabled ? "true" : "false", EscapeJson(startBoundary), EscapeJson(endBoundary)
                        ));
                    }
                } catch {}

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"name\": \"{0}\", \"path\": \"{1}\", \"folder\": \"{2}\", \"state\": \"{3}\", \"enabled\": {4}, " +
                    "\"lastRunTime\": {5}, \"lastTaskResult\": {6}, \"nextRunTime\": {7}, \"numberOfMissedRuns\": {8}, " +
                    "\"registration\": {{\"author\": \"{9}\", \"description\": \"{10}\", \"date\": \"{11}\", \"version\": \"{12}\", \"uri\": \"{13}\"}}, " +
                    "\"principal\": {{\"userId\": \"{14}\", \"logonType\": {15}, \"runLevel\": \"{16}\"}}, " +
                    "\"settings\": {{\"allowDemandStart\": {17}, \"disallowStartIfOnBatteries\": {18}, \"stopIfGoingOnBatteries\": {19}, \"hidden\": {20}, \"executionTimeLimit\": \"{21}\", \"restartCount\": {22}}}, " +
                    "\"actions\": [{23}], \"triggers\": [{24}]}}",
                    EscapeJson(name), EscapeJson(path), EscapeJson(folderPath), stateStr, enabled ? "true" : "false",
                    FormatTaskDate(lastRun), lastResult, FormatTaskDate(nextRun), missedRuns,
                    EscapeJson(author), EscapeJson(desc), EscapeJson(date), EscapeJson(version), EscapeJson(uri),
                    EscapeJson(userId), logonType, runLevelStr,
                    allowDemand ? "true" : "false", disallowBatteries ? "true" : "false", stopBatteries ? "true" : "false", hidden ? "true" : "false", EscapeJson(execLimit), restartCount,
                    string.Join(", ", actionsList.ToArray()), string.Join(", ", triggersList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void TaskSchedulerActionCmd(string action, string taskPath) {
            try {
                if (string.IsNullOrEmpty(action)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"action is required (run, stop, enable, disable, delete)\"}");
                    return;
                }
                if (string.IsNullOrEmpty(taskPath)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"taskPath is required\"}");
                    return;
                }

                string act = action.Trim().ToLowerInvariant();
                Type t = Type.GetTypeFromProgID("Schedule.Service");
                if (t == null) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Schedule.Service COM ProgID not registered\"}");
                    return;
                }
                dynamic ts = Activator.CreateInstance(t);
                ts.Connect();

                string normPath = (taskPath ?? "").Trim().Replace('/', '\\');
                if (!normPath.StartsWith("\\") && normPath.Contains("\\")) {
                    normPath = "\\" + normPath;
                }
                int lastSlash = normPath.LastIndexOf('\\');
                string folderPath = "\\";
                string taskName = normPath;
                if (lastSlash > 0) {
                    folderPath = normPath.Substring(0, lastSlash);
                    taskName = normPath.Substring(lastSlash + 1);
                } else if (lastSlash == 0) {
                    folderPath = "\\";
                    taskName = normPath.Substring(1);
                }

                dynamic folder = ts.GetFolder(folderPath);

                if (act == "delete" || act == "remove") {
                    folder.DeleteTask(taskName, 0);
                    Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"delete\", \"path\": \"{0}\", \"deleted\": true}}", EscapeJson(normPath)));
                    return;
                }

                dynamic task = folder.GetTask(taskName);

                if (act == "run" || act == "start") {
                    dynamic runningTask = task.Run(null);
                    string instanceId = "";
                    try { instanceId = runningTask.InstanceGuid ?? ""; } catch {}
                    Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"run\", \"path\": \"{0}\", \"instanceId\": \"{1}\"}}", EscapeJson(normPath), EscapeJson(instanceId)));
                    return;
                }

                if (act == "stop" || act == "terminate") {
                    task.Stop(0);
                    Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"stop\", \"path\": \"{0}\", \"stopped\": true}}", EscapeJson(normPath)));
                    return;
                }

                if (act == "enable") {
                    task.Enabled = true;
                    Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"enable\", \"path\": \"{0}\", \"enabled\": true}}", EscapeJson(normPath)));
                    return;
                }

                if (act == "disable") {
                    task.Enabled = false;
                    Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"disable\", \"path\": \"{0}\", \"enabled\": false}}", EscapeJson(normPath)));
                    return;
                }

                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Unsupported action: {0}\"}}", EscapeJson(act)));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static StoreName ParseStoreName(string s) {
            string lower = (s ?? "my").Trim().ToLowerInvariant();
            if (lower == "root") return StoreName.Root;
            if (lower == "ca" || lower == "certificateauthority" || lower == "intermediate") return StoreName.CertificateAuthority;
            if (lower == "authroot") return StoreName.AuthRoot;
            if (lower == "trustedpublisher") return StoreName.TrustedPublisher;
            if (lower == "addressbook" || lower == "people") return StoreName.AddressBook;
            return StoreName.My;
        }

        static StoreLocation ParseStoreLocation(string s) {
            string lower = (s ?? "localmachine").Trim().ToLowerInvariant();
            if (lower == "currentuser" || lower == "user") return StoreLocation.CurrentUser;
            return StoreLocation.LocalMachine;
        }

        static X509Certificate2 FindCertificateByThumbprint(string thumbprint, string storeNameStr, string storeLocationStr, out string foundStore, out string foundLocation) {
            foundStore = "";
            foundLocation = "";
            if (string.IsNullOrEmpty(thumbprint)) return null;
            string cleanThumb = (thumbprint ?? "").Replace(" ", "").Replace(":", "").Trim('"', '\'').ToUpperInvariant();

            if (!string.IsNullOrEmpty(storeNameStr) && !string.IsNullOrEmpty(storeLocationStr)) {
                StoreName sn = ParseStoreName(storeNameStr);
                StoreLocation sl = ParseStoreLocation(storeLocationStr);
                X509Store store = new X509Store(sn, sl);
                try {
                    store.Open(OpenFlags.ReadOnly);
                    foreach (var c in store.Certificates) {
                        if (c.Thumbprint.ToUpperInvariant() == cleanThumb) {
                            foundStore = store.Name;
                            foundLocation = store.Location.ToString();
                            return c;
                        }
                    }
                } catch {} finally {
                    store.Close();
                }
                return null;
            }

            StoreLocation[] locs = new StoreLocation[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser };
            StoreName[] names = new StoreName[] { StoreName.My, StoreName.Root, StoreName.CertificateAuthority, StoreName.AuthRoot, StoreName.TrustedPublisher };

            foreach (var l in locs) {
                foreach (var n in names) {
                    X509Store s = new X509Store(n, l);
                    try {
                        s.Open(OpenFlags.ReadOnly);
                        foreach (var c in s.Certificates) {
                            if (c.Thumbprint.ToUpperInvariant() == cleanThumb) {
                                foundStore = s.Name;
                                foundLocation = s.Location.ToString();
                                return c;
                            }
                        }
                    } catch {} finally {
                        s.Close();
                    }
                }
            }
            return null;
        }

        static void CertificateStoreListCmd(string storeNameStr, string storeLocationStr, string searchFilter, int expiringDays, bool hasKeyOnly, int limit) {
            try {
                if (limit <= 0) limit = 50;
                StoreName sn = ParseStoreName(storeNameStr);
                StoreLocation sl = ParseStoreLocation(storeLocationStr);
                string qFilter = (searchFilter ?? "").Trim().Trim('"', '\'').ToLowerInvariant();

                X509Store store = new X509Store(sn, sl);
                store.Open(OpenFlags.ReadOnly);

                var list = new List<string>();
                int totalMatched = 0;
                DateTime nowUtc = DateTime.UtcNow;

                try {
                    foreach (X509Certificate2 c in store.Certificates) {
                        try {
                            string subject = c.Subject ?? "";
                            string issuer = c.Issuer ?? "";
                            string thumb = c.Thumbprint ?? "";
                            bool hasKey = c.HasPrivateKey;

                            if (hasKeyOnly && !hasKey) continue;

                            DateTime notBefore = c.NotBefore;
                            DateTime notAfter = c.NotAfter;
                            bool isExpired = notAfter < nowUtc;
                            int daysUntil = (int)Math.Round((notAfter - nowUtc).TotalDays);

                            if (expiringDays > 0) {
                                if (isExpired || daysUntil > expiringDays) continue;
                            }

                            if (!string.IsNullOrEmpty(qFilter)) {
                                bool m = subject.ToLowerInvariant().Contains(qFilter) ||
                                         issuer.ToLowerInvariant().Contains(qFilter) ||
                                         thumb.ToLowerInvariant().Contains(qFilter);
                                if (!m) continue;
                            }

                            totalMatched++;
                            if (list.Count < limit) {
                                string keyAlgo = "";
                                try {
                                    keyAlgo = c.PublicKey.Oid.FriendlyName ?? c.PublicKey.Oid.Value;
                                    if (c.PublicKey.Key != null) {
                                        keyAlgo += " (" + c.PublicKey.Key.KeySize + " bits)";
                                    }
                                } catch {}

                                string sigAlgo = "";
                                try { sigAlgo = c.SignatureAlgorithm.FriendlyName ?? c.SignatureAlgorithm.Value ?? ""; } catch {}

                                list.Add(string.Format(
                                    "{{\"subject\": \"{0}\", \"issuer\": \"{1}\", \"thumbprint\": \"{2}\", " +
                                    "\"notBefore\": \"{3:o}\", \"notAfter\": \"{4:o}\", \"isExpired\": {5}, \"daysUntilExpiration\": {6}, " +
                                    "\"hasPrivateKey\": {7}, \"keyAlgorithm\": \"{8}\", \"signatureAlgorithm\": \"{9}\"}}",
                                    EscapeJson(subject), EscapeJson(issuer), EscapeJson(thumb),
                                    notBefore, notAfter, isExpired ? "true" : "false", daysUntil,
                                    hasKey ? "true" : "false", EscapeJson(keyAlgo), EscapeJson(sigAlgo)
                                ));
                            }
                        } catch {}
                    }
                } finally {
                    store.Close();
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"storeName\": \"{0}\", \"storeLocation\": \"{1}\", \"count\": {2}, \"totalMatched\": {3}, \"certificates\": [{4}]}}",
                    sn.ToString(), sl.ToString(), list.Count, totalMatched, string.Join(", ", list.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void CertificateInfoCmd(string thumbprint, string storeNameStr, string storeLocationStr) {
            try {
                string foundStore, foundLocation;
                X509Certificate2 c = FindCertificateByThumbprint(thumbprint, storeNameStr, storeLocationStr, out foundStore, out foundLocation);
                if (c == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Certificate not found with thumbprint: {0}\"}}", EscapeJson(thumbprint ?? "")));
                    return;
                }

                string subject = c.Subject ?? "";
                string issuer = c.Issuer ?? "";
                string thumb = c.Thumbprint ?? "";
                string serial = c.SerialNumber ?? "";
                int version = c.Version;
                DateTime notBefore = c.NotBefore;
                DateTime notAfter = c.NotAfter;
                bool isExpired = notAfter < DateTime.UtcNow;
                int daysUntil = (int)Math.Round((notAfter - DateTime.UtcNow).TotalDays);
                bool hasKey = c.HasPrivateKey;

                string keyAlgo = "";
                int keySize = 0;
                try {
                    keyAlgo = c.PublicKey.Oid.FriendlyName ?? c.PublicKey.Oid.Value ?? "";
                    if (c.PublicKey.Key != null) keySize = c.PublicKey.Key.KeySize;
                } catch {}

                string sigAlgo = "";
                try { sigAlgo = c.SignatureAlgorithm.FriendlyName ?? c.SignatureAlgorithm.Value ?? ""; } catch {}

                var ekus = new List<string>();
                string san = "";
                foreach (var ext in c.Extensions) {
                    try {
                        var ekuExt = ext as X509EnhancedKeyUsageExtension;
                        if (ekuExt != null) {
                            foreach (var oid in ekuExt.EnhancedKeyUsages) {
                                ekus.Add(string.Format("\"{0} ({1})\"", EscapeJson(oid.FriendlyName ?? ""), EscapeJson(oid.Value ?? "")));
                            }
                        }
                        if (ext.Oid.Value == "2.5.29.17") { // SAN
                            san = ext.Format(false) ?? "";
                        }
                    } catch {}
                }

                bool chainValid = false;
                int chainCount = 0;
                var chainErrors = new List<string>();
                try {
                    X509Chain chain = new X509Chain();
                    chainValid = chain.Build(c);
                    chainCount = chain.ChainElements.Count;
                    foreach (var s in chain.ChainStatus) {
                        chainErrors.Add(string.Format("\"{0}: {1}\"", EscapeJson(s.Status.ToString()), EscapeJson(s.StatusInformation ?? "")));
                    }
                } catch {}

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"subject\": \"{0}\", \"issuer\": \"{1}\", \"thumbprint\": \"{2}\", \"serialNumber\": \"{3}\", " +
                    "\"version\": {4}, \"storeName\": \"{5}\", \"storeLocation\": \"{6}\", \"notBefore\": \"{7:o}\", \"notAfter\": \"{8:o}\", " +
                    "\"isExpired\": {9}, \"daysUntilExpiration\": {10}, \"hasPrivateKey\": {11}, " +
                    "\"publicKey\": {{\"algorithm\": \"{12}\", \"keySize\": {13}}}, \"signatureAlgorithm\": \"{14}\", " +
                    "\"enhancedKeyUsages\": [{15}], \"subjectAlternativeNames\": \"{16}\", " +
                    "\"chain\": {{\"isValid\": {17}, \"elementsCount\": {18}, \"chainStatus\": [{19}]}}}}",
                    EscapeJson(subject), EscapeJson(issuer), EscapeJson(thumb), EscapeJson(serial),
                    version, EscapeJson(foundStore), EscapeJson(foundLocation), notBefore, notAfter,
                    isExpired ? "true" : "false", daysUntil, hasKey ? "true" : "false",
                    EscapeJson(keyAlgo), keySize, EscapeJson(sigAlgo),
                    string.Join(", ", ekus.ToArray()), EscapeJson(san),
                    chainValid ? "true" : "false", chainCount, string.Join(", ", chainErrors.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void CertificateExportCmd(string thumbprint, string format, string storeNameStr, string storeLocationStr) {
            try {
                string foundStore, foundLocation;
                X509Certificate2 c = FindCertificateByThumbprint(thumbprint, storeNameStr, storeLocationStr, out foundStore, out foundLocation);
                if (c == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Certificate not found with thumbprint: {0}\"}}", EscapeJson(thumbprint ?? "")));
                    return;
                }

                string fmt = (format ?? "pem").Trim().ToLowerInvariant();
                byte[] raw = c.Export(X509ContentType.Cert);
                string b64 = Convert.ToBase64String(raw);

                if (fmt == "base64") {
                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"thumbprint\": \"{0}\", \"subject\": \"{1}\", \"format\": \"base64\", \"data\": \"{2}\"}}",
                        EscapeJson(c.Thumbprint ?? ""), EscapeJson(c.Subject ?? ""), b64
                    ));
                    return;
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("-----BEGIN CERTIFICATE-----");
                for (int i = 0; i < b64.Length; i += 64) {
                    sb.AppendLine(b64.Substring(i, Math.Min(64, b64.Length - i)));
                }
                sb.Append("-----END CERTIFICATE-----");
                string pem = sb.ToString();

                var chainList = new List<string>();
                if (fmt == "chain") {
                    try {
                        X509Chain chain = new X509Chain();
                        chain.Build(c);
                        foreach (var el in chain.ChainElements) {
                            byte[] elRaw = el.Certificate.Export(X509ContentType.Cert);
                            string elB64 = Convert.ToBase64String(elRaw);
                            StringBuilder elSb = new StringBuilder();
                            elSb.AppendLine("-----BEGIN CERTIFICATE-----");
                            for (int i = 0; i < elB64.Length; i += 64) {
                                elSb.AppendLine(elB64.Substring(i, Math.Min(64, elB64.Length - i)));
                            }
                            elSb.Append("-----END CERTIFICATE-----");
                            chainList.Add(string.Format("\"{0}\"", EscapeJson(elSb.ToString())));
                        }
                    } catch {}
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"thumbprint\": \"{0}\", \"subject\": \"{1}\", \"format\": \"{2}\", \"pem\": \"{3}\", \"chain\": [{4}]}}",
                    EscapeJson(c.Thumbprint ?? ""), EscapeJson(c.Subject ?? ""), fmt, EscapeJson(pem), string.Join(", ", chainList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RM_UNIQUE_PROCESS {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        const int CCH_RM_MAX_APP_NAME = 255;
        const int CCH_RM_MAX_SVC_NAME = 63;
        const int CCH_RM_SESSION_KEY = 32;

        enum RM_APP_TYPE {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct RM_PROCESS_INFO {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;
            public RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmJoinSession(out uint pSessionHandle, string strSessionKey);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmRegisterResources(uint dwSessionHandle,
            uint nFiles, string[] rgsFileNames,
            uint nApplications, RM_UNIQUE_PROCESS[] rgApplications,
            uint nServices, string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmGetList(uint dwSessionHandle,
            out uint pnProcInfoNeeded,
            ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
            out uint lpdwRebootReasons);

        [DllImport("rstrtmgr.dll")]
        static extern int RmEndSession(uint dwSessionHandle);

        [DllImport("rstrtmgr.dll")]
        static extern int RmShutdown(uint dwSessionHandle, uint ActionFlags, IntPtr fnStatus);

        [DllImport("rstrtmgr.dll")]
        static extern int RmRestart(uint dwSessionHandle, uint dwRestartFlags, IntPtr fnStatus);

        static string GetRmAppTypeName(RM_APP_TYPE t) {
            switch (t) {
                case RM_APP_TYPE.RmMainWindow: return "MainWindow";
                case RM_APP_TYPE.RmOtherWindow: return "OtherWindow";
                case RM_APP_TYPE.RmService: return "Service";
                case RM_APP_TYPE.RmExplorer: return "Explorer";
                case RM_APP_TYPE.RmConsole: return "Console";
                case RM_APP_TYPE.RmCritical: return "Critical";
                default: return "Unknown";
            }
        }

        static void RestartManagerFindLocksCmd(string filesCsv) {
            try {
                char[] splitters = new char[] { '|', ';', ',' };
                string[] rawParts = (filesCsv ?? "").Split(splitters, StringSplitOptions.RemoveEmptyEntries);
                List<string> fileList = new List<string>();
                foreach (var p in rawParts) {
                    string trimmed = p.Trim().Trim('"', '\'');
                    if (!string.IsNullOrEmpty(trimmed)) {
                        try { fileList.Add(Path.GetFullPath(trimmed)); } catch { fileList.Add(trimmed); }
                    }
                }
                if (fileList.Count == 0) {
                    Console.WriteLine("{\"success\": false, \"error\": \"No file paths provided\"}");
                    return;
                }

                uint handle;
                StringBuilder sbKey = new StringBuilder(CCH_RM_SESSION_KEY + 1);
                int res = RmStartSession(out handle, 0, sbKey);
                if (res != 0) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"RmStartSession failed with code {0}\"}}", res));
                    return;
                }

                try {
                    string[] filesArr = fileList.ToArray();
                    res = RmRegisterResources(handle, (uint)filesArr.Length, filesArr, 0, null, 0, null);
                    if (res != 0) {
                        Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"RmRegisterResources failed with code {0}\"}}", res));
                        return;
                    }

                    uint pnProcInfoNeeded = 0;
                    uint pnProcInfo = 0;
                    uint rebootReasons = 0;
                    int getRes = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null, out rebootReasons);

                    var procList = new List<string>();
                    if (pnProcInfoNeeded > 0) {
                        RM_PROCESS_INFO[] apps = new RM_PROCESS_INFO[pnProcInfoNeeded];
                        pnProcInfo = pnProcInfoNeeded;
                        getRes = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, apps, out rebootReasons);
                        if (getRes == 0) {
                            for (int i = 0; i < pnProcInfo; i++) {
                                int pid = apps[i].Process.dwProcessId;
                                string appName = apps[i].strAppName ?? "";
                                string svcName = apps[i].strServiceShortName ?? "";
                                string appTypeStr = GetRmAppTypeName(apps[i].ApplicationType);
                                uint appStatus = apps[i].AppStatus;
                                uint tsSessionId = apps[i].TSSessionId;
                                bool isRestartable = apps[i].bRestartable;

                                string exePath = "";
                                string winTitle = "";
                                try {
                                    Process proc = Process.GetProcessById(pid);
                                    if (string.IsNullOrEmpty(appName)) appName = proc.ProcessName;
                                    try { exePath = proc.MainModule.FileName ?? ""; } catch {}
                                    try { winTitle = proc.MainWindowTitle ?? ""; } catch {}
                                } catch {}

                                procList.Add(string.Format(
                                    "{{\"processId\": {0}, \"appName\": \"{1}\", \"serviceShortName\": \"{2}\", " +
                                    "\"applicationType\": \"{3}\", \"appStatus\": {4}, \"terminalServicesSessionId\": {5}, " +
                                    "\"isRestartable\": {6}, \"executablePath\": \"{7}\", \"windowTitle\": \"{8}\"}}",
                                    pid, EscapeJson(appName), EscapeJson(svcName),
                                    EscapeJson(appTypeStr), appStatus, tsSessionId,
                                    isRestartable ? "true" : "false",
                                    EscapeJson(exePath), EscapeJson(winTitle)
                                ));
                            }
                        }
                    }

                    var fileJsonList = new List<string>();
                    foreach (var f in fileList) fileJsonList.Add(string.Format("\"{0}\"", EscapeJson(f)));

                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"files\": [{0}], \"lockCount\": {1}, \"rebootReasons\": {2}, \"processes\": [{3}]}}",
                        string.Join(", ", fileJsonList.ToArray()),
                        procList.Count,
                        rebootReasons,
                        string.Join(", ", procList.ToArray())
                    ));
                } finally {
                    RmEndSession(handle);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void RestartManagerShutdownCmd(string filesCsv, bool force) {
            try {
                char[] splitters = new char[] { '|', ';', ',' };
                string[] rawParts = (filesCsv ?? "").Split(splitters, StringSplitOptions.RemoveEmptyEntries);
                List<string> fileList = new List<string>();
                foreach (var p in rawParts) {
                    string trimmed = p.Trim().Trim('"', '\'');
                    if (!string.IsNullOrEmpty(trimmed)) {
                        try { fileList.Add(Path.GetFullPath(trimmed)); } catch { fileList.Add(trimmed); }
                    }
                }
                if (fileList.Count == 0) {
                    Console.WriteLine("{\"success\": false, \"error\": \"No file paths provided\"}");
                    return;
                }

                uint handle;
                StringBuilder sbKey = new StringBuilder(CCH_RM_SESSION_KEY + 1);
                int res = RmStartSession(out handle, 0, sbKey);
                if (res != 0) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"RmStartSession failed with code {0}\"}}", res));
                    return;
                }

                string sessionKey = sbKey.ToString();
                string[] filesArr = fileList.ToArray();
                res = RmRegisterResources(handle, (uint)filesArr.Length, filesArr, 0, null, 0, null);
                if (res != 0) {
                    RmEndSession(handle);
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"RmRegisterResources failed with code {0}\"}}", res));
                    return;
                }

                uint pnProcInfoNeeded = 0;
                uint pnProcInfo = 0;
                uint rebootReasons = 0;
                RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null, out rebootReasons);

                var procList = new List<string>();
                if (pnProcInfoNeeded > 0) {
                    RM_PROCESS_INFO[] apps = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    pnProcInfo = pnProcInfoNeeded;
                    if (RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, apps, out rebootReasons) == 0) {
                        for (int i = 0; i < pnProcInfo; i++) {
                            int pid = apps[i].Process.dwProcessId;
                            string appName = apps[i].strAppName ?? "";
                            string svcName = apps[i].strServiceShortName ?? "";
                            string appTypeStr = GetRmAppTypeName(apps[i].ApplicationType);
                            procList.Add(string.Format("{{\"processId\": {0}, \"appName\": \"{1}\", \"serviceShortName\": \"{2}\", \"applicationType\": \"{3}\"}}",
                                pid, EscapeJson(appName), EscapeJson(svcName), EscapeJson(appTypeStr)));
                        }
                    }
                }

                uint actionFlags = force ? 1u : 0u; // RmForceShutdown = 0x1, RmNormalShutdown = 0x0
                int shutRes = RmShutdown(handle, actionFlags, IntPtr.Zero);

                var fileJsonList = new List<string>();
                foreach (var f in fileList) fileJsonList.Add(string.Format("\"{0}\"", EscapeJson(f)));

                Console.WriteLine(string.Format(
                    "{{\"success\": {0}, \"sessionKey\": \"{1}\", \"files\": [{2}], \"forced\": {3}, \"affectedCount\": {4}, \"shutdownCode\": {5}, \"affectedProcesses\": [{6}]}}",
                    shutRes == 0 ? "true" : "false",
                    EscapeJson(sessionKey),
                    string.Join(", ", fileJsonList.ToArray()),
                    force ? "true" : "false",
                    procList.Count,
                    shutRes,
                    string.Join(", ", procList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void RestartManagerSessionCmd(string filesCsv, bool force) {
            uint handle = 0;
            try {
                char[] splitters = new char[] { '|', ';', ',' };
                string[] rawParts = (filesCsv ?? "").Split(splitters, StringSplitOptions.RemoveEmptyEntries);
                List<string> fileList = new List<string>();
                foreach (var p in rawParts) {
                    string trimmed = p.Trim().Trim('"', '\'');
                    if (!string.IsNullOrEmpty(trimmed)) {
                        try { fileList.Add(Path.GetFullPath(trimmed)); } catch { fileList.Add(trimmed); }
                    }
                }
                if (fileList.Count == 0) {
                    Console.WriteLine("{\"success\": false, \"error\": \"No file paths provided\"}");
                    return;
                }

                StringBuilder sbKey = new StringBuilder(CCH_RM_SESSION_KEY + 1);
                int res = RmStartSession(out handle, 0, sbKey);
                if (res != 0) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"RmStartSession failed with code {0}\"}}", res));
                    return;
                }

                string sessionKey = sbKey.ToString();
                string[] filesArr = fileList.ToArray();
                res = RmRegisterResources(handle, (uint)filesArr.Length, filesArr, 0, null, 0, null);
                if (res != 0) {
                    RmEndSession(handle);
                    handle = 0;
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"RmRegisterResources failed with code {0}\"}}", res));
                    return;
                }

                uint pnProcInfoNeeded = 0;
                uint pnProcInfo = 0;
                uint rebootReasons = 0;
                RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null, out rebootReasons);

                var procList = new List<string>();
                if (pnProcInfoNeeded > 0) {
                    RM_PROCESS_INFO[] apps = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    pnProcInfo = pnProcInfoNeeded;
                    if (RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, apps, out rebootReasons) == 0) {
                        for (int i = 0; i < pnProcInfo; i++) {
                            int pid = apps[i].Process.dwProcessId;
                            string appName = apps[i].strAppName ?? "";
                            string svcName = apps[i].strServiceShortName ?? "";
                            string appTypeStr = GetRmAppTypeName(apps[i].ApplicationType);
                            procList.Add(string.Format("{{\"processId\": {0}, \"appName\": \"{1}\", \"serviceShortName\": \"{2}\", \"applicationType\": \"{3}\"}}",
                                pid, EscapeJson(appName), EscapeJson(svcName), EscapeJson(appTypeStr)));
                        }
                    }
                }

                uint actionFlags = force ? 1u : 0u; // RmForceShutdown = 0x1, RmNormalShutdown = 0x0
                int shutRes = RmShutdown(handle, actionFlags, IntPtr.Zero);

                var fileJsonList = new List<string>();
                foreach (var f in fileList) fileJsonList.Add(string.Format("\"{0}\"", EscapeJson(f)));

                Console.WriteLine(string.Format(
                    "{{\"success\": {0}, \"sessionKey\": \"{1}\", \"files\": [{2}], \"forced\": {3}, \"affectedCount\": {4}, \"shutdownCode\": {5}, \"affectedProcesses\": [{6}]}}",
                    shutRes == 0 ? "true" : "false",
                    EscapeJson(sessionKey),
                    string.Join(", ", fileJsonList.ToArray()),
                    force ? "true" : "false",
                    procList.Count,
                    shutRes,
                    string.Join(", ", procList.ToArray())
                ));
                Console.Out.Flush();

                if (shutRes != 0) {
                    RmEndSession(handle);
                    handle = 0;
                    return;
                }

                // Interactive / Piped Conductor Session:
                // Watchdog thread terminates after 5 minutes (300 seconds) if no input received
                uint watchdogHandle = handle;
                System.Threading.Thread watchdog = new System.Threading.Thread(() => {
                    System.Threading.Thread.Sleep(300000);
                    if (watchdogHandle != 0) {
                        try { RmEndSession(watchdogHandle); } catch {}
                        Environment.Exit(0);
                    }
                });
                watchdog.IsBackground = true;
                watchdog.Start();

                string line;
                while ((line = Console.ReadLine()) != null) {
                    line = line.Trim().ToLowerInvariant();
                    if (line == "restart") {
                        int restartRes = RmRestart(handle, 0, IntPtr.Zero);
                        RmEndSession(handle);
                        handle = 0;
                        watchdogHandle = 0;
                        Console.WriteLine(string.Format(
                            "{{\"success\": {0}, \"sessionKey\": \"{1}\", \"restartCode\": {2}, \"restarted\": {3}}}",
                            restartRes == 0 ? "true" : "false",
                            EscapeJson(sessionKey),
                            restartRes,
                            restartRes == 0 ? "true" : "false"
                        ));
                        Console.Out.Flush();
                        return;
                    } else if (line == "end" || line == "close" || line == "abort" || line == "quit") {
                        RmEndSession(handle);
                        handle = 0;
                        watchdogHandle = 0;
                        Console.WriteLine(string.Format("{{\"success\": true, \"sessionKey\": \"{0}\", \"closed\": true}}", EscapeJson(sessionKey)));
                        Console.Out.Flush();
                        return;
                    }
                }

                // If stdin closed (EOF), clean up session
                if (handle != 0) {
                    RmEndSession(handle);
                    handle = 0;
                }
            } catch (Exception ex) {
                if (handle != 0) {
                    try { RmEndSession(handle); } catch {}
                }
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void RestartManagerRestartCmd(string sessionKey) {
            try {
                string cleanKey = (sessionKey ?? "").Trim().Trim('"', '\'');
                if (string.IsNullOrEmpty(cleanKey)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"sessionKey is required\"}");
                    return;
                }

                uint handle;
                int res = RmJoinSession(out handle, cleanKey);
                if (res != 0) {
                    string hint = res == 1219 ? " (Session expired, closed, or secondary installer restriction)" : "";
                    Console.WriteLine(string.Format("{{\"success\": false, \"sessionKey\": \"{0}\", \"error\": \"RmJoinSession failed with code {1}{2}\"}}", EscapeJson(cleanKey), res, hint));
                    return;
                }

                int restartRes = RmRestart(handle, 0, IntPtr.Zero);
                RmEndSession(handle);

                Console.WriteLine(string.Format(
                    "{{\"success\": {0}, \"sessionKey\": \"{1}\", \"restartCode\": {2}, \"restarted\": {3}}}",
                    restartRes == 0 ? "true" : "false",
                    EscapeJson(cleanKey),
                    restartRes,
                    restartRes == 0 ? "true" : "false"
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static string FormatWmiValue(object val) {
            if (val == null) return "null";
            if (val is bool) return ((bool)val) ? "true" : "false";
            if (val is byte || val is sbyte || val is short || val is ushort || val is int || val is uint || val is long || val is ulong)
                return val.ToString();
            if (val is float || val is double || val is decimal) {
                double d = Convert.ToDouble(val);
                return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            if (val is Array) {
                var arr = (Array)val;
                var items = new List<string>();
                foreach (var item in arr) items.Add(FormatWmiValue(item));
                return "[" + string.Join(", ", items.ToArray()) + "]";
            }
            return "\"" + EscapeJson(val.ToString().Trim()) + "\"";
        }

        static string GetWmiString(ManagementObject mo, string prop) {
            try {
                if (mo == null) return "";
                object val = mo[prop];
                return val != null ? val.ToString().Trim() : "";
            } catch {
                return "";
            }
        }

        static void WmiQueryCmd(string wqlQuery, string ns, int limit) {
            try {
                string query = (wqlQuery ?? "").Trim();
                if (string.IsNullOrEmpty(query)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Query parameter is required\"}");
                    return;
                }
                string targetNamespace = string.IsNullOrEmpty(ns) ? "root\\cimv2" : ns.Trim();
                int maxRecords = limit > 0 ? Math.Min(limit, 1000) : 100;

                ManagementScope scope = new ManagementScope(targetNamespace);
                scope.Connect();

                SelectQuery sQuery = new SelectQuery(query);
                var records = new List<string>();

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, sQuery))
                using (ManagementObjectCollection collection = searcher.Get()) {
                    int count = 0;
                    foreach (ManagementObject mo in collection) {
                        if (count >= maxRecords) break;
                        var propList = new List<string>();
                        foreach (PropertyData prop in mo.Properties) {
                            try {
                                propList.Add(string.Format("\"{0}\": {1}", EscapeJson(prop.Name), FormatWmiValue(prop.Value)));
                            } catch {}
                        }
                        records.Add("{" + string.Join(", ", propList.ToArray()) + "}");
                        count++;
                    }
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"namespace\": \"{0}\", \"query\": \"{1}\", \"count\": {2}, \"records\": [{3}]}}",
                    EscapeJson(targetNamespace),
                    EscapeJson(query),
                    records.Count,
                    string.Join(", ", records.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void WmiHardwareSpecCmd() {
            try {
                ManagementScope scope = new ManagementScope("root\\cimv2");
                scope.Connect();

                // 1. Motherboard / Baseboard
                var baseboardList = new List<string>();
                try {
                    using (var s = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Manufacturer, Product, SerialNumber, Version, Status FROM Win32_BaseBoard")))
                    using (var col = s.Get()) {
                        foreach (ManagementObject mo in col) {
                            baseboardList.Add(string.Format(
                                "{{\"manufacturer\": \"{0}\", \"product\": \"{1}\", \"serialNumber\": \"{2}\", \"version\": \"{3}\", \"status\": \"{4}\"}}",
                                EscapeJson(GetWmiString(mo, "Manufacturer")),
                                EscapeJson(GetWmiString(mo, "Product")),
                                EscapeJson(GetWmiString(mo, "SerialNumber")),
                                EscapeJson(GetWmiString(mo, "Version")),
                                EscapeJson(GetWmiString(mo, "Status"))
                            ));
                        }
                    }
                } catch {}

                // 2. BIOS
                var biosList = new List<string>();
                try {
                    using (var s = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber, Version FROM Win32_BIOS")))
                    using (var col = s.Get()) {
                        foreach (ManagementObject mo in col) {
                            biosList.Add(string.Format(
                                "{{\"manufacturer\": \"{0}\", \"smbiosVersion\": \"{1}\", \"releaseDate\": \"{2}\", \"serialNumber\": \"{3}\", \"version\": \"{4}\"}}",
                                EscapeJson(GetWmiString(mo, "Manufacturer")),
                                EscapeJson(GetWmiString(mo, "SMBIOSBIOSVersion")),
                                EscapeJson(GetWmiString(mo, "ReleaseDate")),
                                EscapeJson(GetWmiString(mo, "SerialNumber")),
                                EscapeJson(GetWmiString(mo, "Version"))
                            ));
                        }
                    }
                } catch {}

                // 3. Processors
                var cpuList = new List<string>();
                try {
                    using (var s = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L2CacheSize, L3CacheSize, Architecture, SocketDesignation FROM Win32_Processor")))
                    using (var col = s.Get()) {
                        foreach (ManagementObject mo in col) {
                            cpuList.Add(string.Format(
                                "{{\"name\": \"{0}\", \"manufacturer\": \"{1}\", \"cores\": {2}, \"logicalProcessors\": {3}, \"maxClockSpeedMHz\": {4}, \"l2CacheKB\": {5}, \"l3CacheKB\": {6}, \"socket\": \"{7}\"}}",
                                EscapeJson(GetWmiString(mo, "Name")),
                                EscapeJson(GetWmiString(mo, "Manufacturer")),
                                mo["NumberOfCores"] != null ? mo["NumberOfCores"].ToString() : "0",
                                mo["NumberOfLogicalProcessors"] != null ? mo["NumberOfLogicalProcessors"].ToString() : "0",
                                mo["MaxClockSpeed"] != null ? mo["MaxClockSpeed"].ToString() : "0",
                                mo["L2CacheSize"] != null ? mo["L2CacheSize"].ToString() : "0",
                                mo["L3CacheSize"] != null ? mo["L3CacheSize"].ToString() : "0",
                                EscapeJson(GetWmiString(mo, "SocketDesignation"))
                            ));
                        }
                    }
                } catch {}

                // 4. Memory Modules (Physical RAM DIMMs)
                var memList = new List<string>();
                double totalRamBytes = 0;
                try {
                    using (var s = new ManagementObjectSearcher(scope, new SelectQuery("SELECT BankLabel, DeviceLocator, Capacity, Speed, ConfiguredClockSpeed, Manufacturer, PartNumber, FormFactor FROM Win32_PhysicalMemory")))
                    using (var col = s.Get()) {
                        foreach (ManagementObject mo in col) {
                            ulong cap = 0;
                            if (mo["Capacity"] != null) ulong.TryParse(mo["Capacity"].ToString(), out cap);
                            totalRamBytes += cap;
                            double capGB = Math.Round((double)cap / (1024.0 * 1024.0 * 1024.0), 2);

                            memList.Add(string.Format(
                                "{{\"bank\": \"{0}\", \"slot\": \"{1}\", \"capacityGB\": {2:F2}, \"speedMHz\": {3}, \"configuredSpeedMHz\": {4}, \"manufacturer\": \"{5}\", \"partNumber\": \"{6}\"}}",
                                EscapeJson(GetWmiString(mo, "BankLabel")),
                                EscapeJson(GetWmiString(mo, "DeviceLocator")),
                                capGB,
                                mo["Speed"] != null ? mo["Speed"].ToString() : "0",
                                mo["ConfiguredClockSpeed"] != null ? mo["ConfiguredClockSpeed"].ToString() : "0",
                                EscapeJson(GetWmiString(mo, "Manufacturer")),
                                EscapeJson(GetWmiString(mo, "PartNumber"))
                            ));
                        }
                    }
                } catch {}

                // 5. Video Controllers (GPUs)
                var gpuList = new List<string>();
                try {
                    using (var s = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Name, AdapterRAM, DriverVersion, VideoProcessor, CurrentHorizontalResolution, CurrentVerticalResolution, VideoModeDescription FROM Win32_VideoController")))
                    using (var col = s.Get()) {
                        foreach (ManagementObject mo in col) {
                            ulong vramBytes = 0;
                            if (mo["AdapterRAM"] != null) ulong.TryParse(mo["AdapterRAM"].ToString(), out vramBytes);
                            double vramMB = Math.Round((double)vramBytes / (1024.0 * 1024.0), 1);

                            gpuList.Add(string.Format(
                                "{{\"name\": \"{0}\", \"vramMB\": {1:F1}, \"driverVersion\": \"{2}\", \"processor\": \"{3}\", \"resolution\": \"{4}x{5}\", \"mode\": \"{6}\"}}",
                                EscapeJson(GetWmiString(mo, "Name")),
                                vramMB,
                                EscapeJson(GetWmiString(mo, "DriverVersion")),
                                EscapeJson(GetWmiString(mo, "VideoProcessor")),
                                mo["CurrentHorizontalResolution"] != null ? mo["CurrentHorizontalResolution"].ToString() : "0",
                                mo["CurrentVerticalResolution"] != null ? mo["CurrentVerticalResolution"].ToString() : "0",
                                EscapeJson(GetWmiString(mo, "VideoModeDescription"))
                            ));
                        }
                    }
                } catch {}

                double totalRamGB = Math.Round(totalRamBytes / (1024.0 * 1024.0 * 1024.0), 2);

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"totalRamGB\": {0:F2}, \"dimmCount\": {1}, \"baseboard\": [{2}], \"bios\": [{3}], \"processors\": [{4}], \"memoryModules\": [{5}], \"videoControllers\": [{6}]}}",
                    totalRamGB,
                    memList.Count,
                    string.Join(", ", baseboardList.ToArray()),
                    string.Join(", ", biosList.ToArray()),
                    string.Join(", ", cpuList.ToArray()),
                    string.Join(", ", memList.ToArray()),
                    string.Join(", ", gpuList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void WmiOsHealthCmd() {
            try {
                ManagementScope scope = new ManagementScope("root\\cimv2");
                scope.Connect();

                // 1. Operating System
                var osList = new List<string>();
                try {
                    using (var s = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Caption, Version, BuildNumber, OSArchitecture, InstallDate, LastBootUpTime, TotalVisibleMemorySize, FreePhysicalMemory, TotalVirtualMemorySize, FreeVirtualMemory, NumberOfProcesses FROM Win32_OperatingSystem")))
                    using (var col = s.Get()) {
                        foreach (ManagementObject mo in col) {
                            ulong totVisKB = 0, freePhysKB = 0, totVirtKB = 0, freeVirtKB = 0;
                            if (mo["TotalVisibleMemorySize"] != null) ulong.TryParse(mo["TotalVisibleMemorySize"].ToString(), out totVisKB);
                            if (mo["FreePhysicalMemory"] != null) ulong.TryParse(mo["FreePhysicalMemory"].ToString(), out freePhysKB);
                            if (mo["TotalVirtualMemorySize"] != null) ulong.TryParse(mo["TotalVirtualMemorySize"].ToString(), out totVirtKB);
                            if (mo["FreeVirtualMemory"] != null) ulong.TryParse(mo["FreeVirtualMemory"].ToString(), out freeVirtKB);

                            double totVisMB = Math.Round((double)totVisKB / 1024.0, 1);
                            double freePhysMB = Math.Round((double)freePhysKB / 1024.0, 1);
                            double totVirtMB = Math.Round((double)totVirtKB / 1024.0, 1);
                            double freeVirtMB = Math.Round((double)freeVirtKB / 1024.0, 1);

                            osList.Add(string.Format(
                                "{{\"caption\": \"{0}\", \"version\": \"{1}\", \"buildNumber\": \"{2}\", \"architecture\": \"{3}\", \"installDate\": \"{4}\", \"lastBootUpTime\": \"{5}\", \"totalVisibleMemoryMB\": {6:F1}, \"freePhysicalMemoryMB\": {7:F1}, \"totalVirtualMemoryMB\": {8:F1}, \"freeVirtualMemoryMB\": {9:F1}, \"processCount\": {10}}}",
                                EscapeJson(GetWmiString(mo, "Caption")),
                                EscapeJson(GetWmiString(mo, "Version")),
                                EscapeJson(GetWmiString(mo, "BuildNumber")),
                                EscapeJson(GetWmiString(mo, "OSArchitecture")),
                                EscapeJson(GetWmiString(mo, "InstallDate")),
                                EscapeJson(GetWmiString(mo, "LastBootUpTime")),
                                totVisMB, freePhysMB, totVirtMB, freeVirtMB,
                                mo["NumberOfProcesses"] != null ? mo["NumberOfProcesses"].ToString() : "0"
                            ));
                        }
                    }
                } catch {}

                // 2. Page File Usage
                var pageFileList = new List<string>();
                try {
                    using (var s = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Name, AllocatedBaseSize, CurrentUsage, PeakUsage FROM Win32_PageFileUsage")))
                    using (var col = s.Get()) {
                        foreach (ManagementObject mo in col) {
                            uint allocMB = 0, curMB = 0, peakMB = 0;
                            if (mo["AllocatedBaseSize"] != null) uint.TryParse(mo["AllocatedBaseSize"].ToString(), out allocMB);
                            if (mo["CurrentUsage"] != null) uint.TryParse(mo["CurrentUsage"].ToString(), out curMB);
                            if (mo["PeakUsage"] != null) uint.TryParse(mo["PeakUsage"].ToString(), out peakMB);
                            double pct = allocMB > 0 ? Math.Round((double)curMB / allocMB * 100.0, 1) : 0.0;

                            pageFileList.Add(string.Format(
                                "{{\"name\": \"{0}\", \"allocatedMB\": {1}, \"currentUsageMB\": {2}, \"peakUsageMB\": {3}, \"percentUsed\": {4}}}",
                                EscapeJson(GetWmiString(mo, "Name")),
                                allocMB, curMB, peakMB,
                                pct.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                            ));
                        }
                    }
                } catch {}

                // 3. Startup Commands
                var startupList = new List<string>();
                try {
                    using (var s = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Name, Command, Location, User FROM Win32_StartupCommand")))
                    using (var col = s.Get()) {
                        int count = 0;
                        foreach (ManagementObject mo in col) {
                            if (count++ >= 50) break;
                            startupList.Add(string.Format(
                                "{{\"name\": \"{0}\", \"command\": \"{1}\", \"location\": \"{2}\", \"user\": \"{3}\"}}",
                                EscapeJson(GetWmiString(mo, "Name")),
                                EscapeJson(GetWmiString(mo, "Command")),
                                EscapeJson(GetWmiString(mo, "Location")),
                                EscapeJson(GetWmiString(mo, "User"))
                            ));
                        }
                    }
                } catch {}

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"operatingSystem\": [{0}], \"pageFiles\": [{1}], \"startupItems\": [{2}]}}",
                    string.Join(", ", osList.ToArray()),
                    string.Join(", ", pageFileList.ToArray()),
                    string.Join(", ", startupList.ToArray())
                ));
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

        [DllImport("ole32.dll")]
        static extern int OleFlushClipboard();

        static void ClipboardSetCmd(string text) {
            Exception lastEx = null;
            for (int attempt = 0; attempt < 5; attempt++) {
                try {
                    RunSta(() => {
                        Clipboard.Clear();
                        Clipboard.SetDataObject(text, true, 10, 100);
                        Application.DoEvents();
                        try { OleFlushClipboard(); } catch {}
                    });
                    Console.WriteLine(string.Format("{{\"success\": true, \"charCount\": {0}}}", text.Length));
                    return;
                } catch (Exception ex) {
                    lastEx = ex;
                    Thread.Sleep(80);
                }
            }
            Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(lastEx != null ? lastEx.Message : "Clipboard set failed")));
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
                    try { OleFlushClipboard(); } catch {}
                });
                Console.WriteLine("{\"success\": true}");
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AudioListenCmd(int durationMs) {
            try {
                if (durationMs < 50) durationMs = 50;
                if (durationMs > 10000) durationMs = 10000;

                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice dev;
                int hr = enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out dev);
                if (hr != 0 || dev == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"isHeadless\": true, \"error\": \"Failed to get default audio endpoint (0x{0:X})\"}}", hr));
                    return;
                }

                Guid iidClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
                object objClient;
                hr = dev.Activate(ref iidClient, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out objClient);
                if (hr != 0 || objClient == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to activate IAudioClient (0x{0:X})\"}}", hr));
                    return;
                }

                var client = (IAudioClient)objClient;
                IntPtr pMixFormat;
                hr = client.GetMixFormat(out pMixFormat);
                if (hr != 0 || pMixFormat == IntPtr.Zero) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to get mix format (0x{0:X})\"}}", hr));
                    return;
                }

                var fmt = (WAVEFORMATEX)Marshal.PtrToStructure(pMixFormat, typeof(WAVEFORMATEX));
                const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
                Guid session = Guid.Empty;
                hr = client.Initialize(0 /* AUDCLNT_SHAREMODE_SHARED */, AUDCLNT_STREAMFLAGS_LOOPBACK, 10000000 /* 1 sec */, 0, pMixFormat, ref session);
                if (hr != 0) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Initialize loopback audio failed (0x{0:X})\"}}", hr));
                    return;
                }

                Guid iidCapture = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
                object objCapture;
                hr = client.GetService(ref iidCapture, out objCapture);
                if (hr != 0 || objCapture == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"GetService IAudioCaptureClient failed (0x{0:X})\"}}", hr));
                    return;
                }

                var capture = (IAudioCaptureClient)objCapture;
                client.Start();

                double peak = 0.0;
                double sumSquares = 0.0;
                long totalSamples = 0;

                int elapsed = 0;
                while (elapsed < durationMs) {
                    Thread.Sleep(20);
                    elapsed += 20;

                    uint packetSize = 0;
                    capture.GetNextPacketSize(out packetSize);
                    while (packetSize > 0) {
                        IntPtr pData;
                        uint numFrames;
                        uint flags;
                        ulong devPos, qpcPos;
                        hr = capture.GetBuffer(out pData, out numFrames, out flags, out devPos, out qpcPos);
                        if (hr == 0 && numFrames > 0) {
                            if ((flags & 2 /* AUDCLNT_BUFFERFLAGS_SILENT */) == 0 && pData != IntPtr.Zero) {
                                int samplesToRead = (int)(numFrames * fmt.nChannels);
                                if (fmt.wBitsPerSample == 32) {
                                    float[] floatBuf = new float[samplesToRead];
                                    Marshal.Copy(pData, floatBuf, 0, samplesToRead);
                                    for (int i = 0; i < samplesToRead; i++) {
                                        double val = Math.Abs(floatBuf[i]);
                                        if (val > peak) peak = val;
                                        sumSquares += val * val;
                                        totalSamples++;
                                    }
                                } else if (fmt.wBitsPerSample == 16) {
                                    short[] shortBuf = new short[samplesToRead];
                                    Marshal.Copy(pData, shortBuf, 0, samplesToRead);
                                    for (int i = 0; i < samplesToRead; i++) {
                                        double val = Math.Abs((double)shortBuf[i] / 32768.0);
                                        if (val > peak) peak = val;
                                        sumSquares += val * val;
                                        totalSamples++;
                                    }
                                }
                            }
                            capture.ReleaseBuffer(numFrames);
                        }
                        capture.GetNextPacketSize(out packetSize);
                    }
                }

                client.Stop();

                double rms = totalSamples > 0 ? Math.Sqrt(sumSquares / totalSamples) : 0.0;
                double peakDb = peak > 0.00001 ? Math.Round(20.0 * Math.Log10(peak), 1) : -96.0;
                double rmsDb = rms > 0.00001 ? Math.Round(20.0 * Math.Log10(rms), 1) : -96.0;
                bool isPlaying = peakDb > -50.0;

                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"success\": true, \"peakDecibels\": {0:F1}, \"rmsDecibels\": {1:F1}, \"isPlaying\": {2}, \"sampleRate\": {3}, \"channels\": {4}, \"bitsPerSample\": {5}, \"samplesCaptured\": {6}, \"durationMs\": {7}}}",
                    peakDb, rmsDb, isPlaying ? "true" : "false", fmt.nSamplesPerSec, fmt.nChannels, fmt.wBitsPerSample, totalSamples, durationMs));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AudioRecordCmd(string outputPath, int durationSeconds) {
            try {
                if (durationSeconds < 1) durationSeconds = 1;
                if (durationSeconds > 30) durationSeconds = 30;

                string fullPath = Path.GetFullPath(outputPath);
                string dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }

                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice dev;
                int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out dev);
                if (hr != 0 || dev == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"isHeadless\": true, \"error\": \"Failed to get audio endpoint (0x{0:X})\"}}", hr));
                    return;
                }

                Guid iidClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
                object objClient;
                hr = dev.Activate(ref iidClient, 1, IntPtr.Zero, out objClient);
                if (hr != 0 || objClient == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to activate IAudioClient (0x{0:X})\"}}", hr));
                    return;
                }

                var client = (IAudioClient)objClient;
                IntPtr pMixFormat;
                hr = client.GetMixFormat(out pMixFormat);
                if (hr != 0 || pMixFormat == IntPtr.Zero) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to get mix format (0x{0:X})\"}}", hr));
                    return;
                }

                var fmt = (WAVEFORMATEX)Marshal.PtrToStructure(pMixFormat, typeof(WAVEFORMATEX));
                const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
                Guid session = Guid.Empty;
                hr = client.Initialize(0, AUDCLNT_STREAMFLAGS_LOOPBACK, 10000000, 0, pMixFormat, ref session);
                if (hr != 0) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Initialize loopback failed (0x{0:X})\"}}", hr));
                    return;
                }

                Guid iidCapture = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
                object objCapture;
                hr = client.GetService(ref iidCapture, out objCapture);
                if (hr != 0 || objCapture == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"GetService IAudioCaptureClient failed (0x{0:X})\"}}", hr));
                    return;
                }

                var capture = (IAudioCaptureClient)objCapture;
                client.Start();

                var pcmStream = new MemoryStream();
                var writer = new BinaryWriter(pcmStream);

                double peak = 0.0;
                double sumSquares = 0.0;
                long totalSamples = 0;

                int durationMs = durationSeconds * 1000;
                int elapsed = 0;
                while (elapsed < durationMs) {
                    Thread.Sleep(20);
                    elapsed += 20;

                    uint packetSize = 0;
                    capture.GetNextPacketSize(out packetSize);
                    while (packetSize > 0) {
                        IntPtr pData;
                        uint numFrames;
                        uint flags;
                        ulong devPos, qpcPos;
                        hr = capture.GetBuffer(out pData, out numFrames, out flags, out devPos, out qpcPos);
                        if (hr == 0 && numFrames > 0) {
                            int samplesToRead = (int)(numFrames * fmt.nChannels);
                            if ((flags & 2) != 0 || pData == IntPtr.Zero) {
                                for (int i = 0; i < samplesToRead; i++) {
                                    writer.Write((short)0);
                                    totalSamples++;
                                }
                            } else {
                                if (fmt.wBitsPerSample == 32) {
                                    float[] floatBuf = new float[samplesToRead];
                                    Marshal.Copy(pData, floatBuf, 0, samplesToRead);
                                    for (int i = 0; i < samplesToRead; i++) {
                                        float f = floatBuf[i];
                                        double val = Math.Abs(f);
                                        if (val > peak) peak = val;
                                        sumSquares += val * val;
                                        totalSamples++;
                                        short s = (short)Math.Max(-32768, Math.Min(32767, (int)(f * 32767.0f)));
                                        writer.Write(s);
                                    }
                                } else if (fmt.wBitsPerSample == 16) {
                                    short[] shortBuf = new short[samplesToRead];
                                    Marshal.Copy(pData, shortBuf, 0, samplesToRead);
                                    for (int i = 0; i < samplesToRead; i++) {
                                        short s = shortBuf[i];
                                        double val = Math.Abs((double)s / 32768.0);
                                        if (val > peak) peak = val;
                                        sumSquares += val * val;
                                        totalSamples++;
                                        writer.Write(s);
                                    }
                                }
                            }
                            capture.ReleaseBuffer(numFrames);
                        }
                        capture.GetNextPacketSize(out packetSize);
                    }
                }

                client.Stop();

                byte[] pcmData = pcmStream.ToArray();
                uint dataSize = (uint)pcmData.Length;
                uint subchunk1Size = 16;
                ushort audioFormat = 1; // PCM
                ushort channels = fmt.nChannels;
                uint sampleRate = fmt.nSamplesPerSec;
                ushort bitsPerSample = 16;
                uint byteRate = sampleRate * channels * (uint)(bitsPerSample / 8);
                ushort blockAlign = (ushort)(channels * (bitsPerSample / 8));
                uint chunkSize = 36 + dataSize;

                using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var bw = new BinaryWriter(fs)) {
                    // RIFF header
                    bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(chunkSize);
                    bw.Write(Encoding.ASCII.GetBytes("WAVE"));

                    // fmt subchunk
                    bw.Write(Encoding.ASCII.GetBytes("fmt "));
                    bw.Write(subchunk1Size);
                    bw.Write(audioFormat);
                    bw.Write(channels);
                    bw.Write(sampleRate);
                    bw.Write(byteRate);
                    bw.Write(blockAlign);
                    bw.Write(bitsPerSample);

                    // data subchunk
                    bw.Write(Encoding.ASCII.GetBytes("data"));
                    bw.Write(dataSize);
                    bw.Write(pcmData);
                }

                double rms = totalSamples > 0 ? Math.Sqrt(sumSquares / totalSamples) : 0.0;
                double peakDb = peak > 0.00001 ? Math.Round(20.0 * Math.Log10(peak), 1) : -96.0;
                double rmsDb = rms > 0.00001 ? Math.Round(20.0 * Math.Log10(rms), 1) : -96.0;

                FileInfo fi = new FileInfo(fullPath);
                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"success\": true, \"path\": \"{0}\", \"durationSeconds\": {1}, \"fileSize\": {2}, \"sampleRate\": {3}, \"channels\": {4}, \"bitsPerSample\": 16, \"peakDecibels\": {5:F1}, \"rmsDecibels\": {6:F1}, \"samplesRecorded\": {7}}}",
                    fullPath.Replace("\\", "/"), durationSeconds, fi.Length, sampleRate, channels, peakDb, rmsDb, totalSamples));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static string GetDeviceFriendlyName(IMMDevice dev) {
            if (dev == null) return "Unknown";
            try {
                IPropertyStore store;
                if (dev.OpenPropertyStore(0 /* STGM_READ */, out store) == 0 && store != null) {
                    var pkey = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
                    PROPVARIANT pv;
                    if (store.GetValue(ref pkey, out pv) == 0 && pv.vt == 31 /* VT_LPWSTR */) {
                        string name = Marshal.PtrToStringUni(pv.pwszVal);
                        PropVariantClear(ref pv);
                        if (!string.IsNullOrEmpty(name)) return name;
                    }
                }
            } catch {}
            return "Generic Audio Device";
        }

        static void AudioDevicesCmd() {
            try {
                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                string defRenderId = "";
                IMMDevice defRender;
                if (enumerator.GetDefaultAudioEndpoint(0, 1, out defRender) == 0 && defRender != null) {
                    defRender.GetId(out defRenderId);
                }

                string defCaptureId = "";
                IMMDevice defCapture;
                if (enumerator.GetDefaultAudioEndpoint(1, 1, out defCapture) == 0 && defCapture != null) {
                    defCapture.GetId(out defCaptureId);
                }

                var sb = new StringBuilder();
                sb.Append("{\"success\": true, \"devices\": [");
                bool first = true;
                int renderCount = 0;
                int captureCount = 0;

                IMMDeviceCollection renderCol;
                int hr = enumerator.EnumAudioEndpoints(0 /* eRender */, 1 /* DEVICE_STATE_ACTIVE */, out renderCol);
                if (hr == 0 && renderCol != null) {
                    uint count;
                    renderCol.GetCount(out count);
                    for (uint i = 0; i < count; i++) {
                        IMMDevice dev;
                        if (renderCol.Item(i, out dev) == 0 && dev != null) {
                            string id = "";
                            dev.GetId(out id);
                            string name = GetDeviceFriendlyName(dev);
                            int state = 1;
                            dev.GetState(out state);
                            bool isDefault = !string.IsNullOrEmpty(defRenderId) && id == defRenderId;

                            if (!first) sb.Append(",");
                            first = false;
                            sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                "{{\"id\": \"{0}\", \"name\": \"{1}\", \"type\": \"render\", \"state\": {2}, \"isDefault\": {3}}}",
                                EscapeJson(id), EscapeJson(name), state, isDefault ? "true" : "false"));
                            renderCount++;
                        }
                    }
                }

                IMMDeviceCollection captureCol;
                hr = enumerator.EnumAudioEndpoints(1 /* eCapture */, 1 /* DEVICE_STATE_ACTIVE */, out captureCol);
                if (hr == 0 && captureCol != null) {
                    uint count;
                    captureCol.GetCount(out count);
                    for (uint i = 0; i < count; i++) {
                        IMMDevice dev;
                        if (captureCol.Item(i, out dev) == 0 && dev != null) {
                            string id = "";
                            dev.GetId(out id);
                            string name = GetDeviceFriendlyName(dev);
                            int state = 1;
                            dev.GetState(out state);
                            bool isDefault = !string.IsNullOrEmpty(defCaptureId) && id == defCaptureId;

                            if (!first) sb.Append(",");
                            first = false;
                            sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                "{{\"id\": \"{0}\", \"name\": \"{1}\", \"type\": \"capture\", \"state\": {2}, \"isDefault\": {3}}}",
                                EscapeJson(id), EscapeJson(name), state, isDefault ? "true" : "false"));
                            captureCount++;
                        }
                    }
                }

                sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "], \"renderCount\": {0}, \"captureCount\": {1}, \"defaultRender\": \"{2}\", \"defaultCapture\": \"{3}\", \"isHeadless\": {4}}}",
                    renderCount, captureCount, EscapeJson(defRenderId), EscapeJson(defCaptureId), (renderCount == 0 && captureCount == 0) ? "true" : "false"));
                Console.WriteLine(sb.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"isHeadless\": true, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AudioMicListenCmd(int durationMs) {
            try {
                if (durationMs < 50) durationMs = 50;
                if (durationMs > 10000) durationMs = 10000;

                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice dev = null;
                int hr = enumerator.GetDefaultAudioEndpoint(1 /* eCapture */, 1 /* eMultimedia */, out dev);
                if (hr != 0 || dev == null) {
                    hr = enumerator.GetDefaultAudioEndpoint(1 /* eCapture */, 0 /* eConsole */, out dev);
                }
                if (hr != 0 || dev == null) {
                    IMMDeviceCollection col;
                    if (enumerator.EnumAudioEndpoints(1, 1, out col) == 0 && col != null) {
                        uint c;
                        col.GetCount(out c);
                        if (c > 0) col.Item(0, out dev);
                    }
                }

                if (dev == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"isHeadless\": true, \"error\": \"No audio capture endpoint available (0x{0:X8})\"}}", hr));
                    return;
                }

                Guid iidClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
                object objClient;
                hr = dev.Activate(ref iidClient, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out objClient);
                if (hr != 0 || objClient == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to activate IAudioClient (0x{0:X})\"}}", hr));
                    return;
                }

                var client = (IAudioClient)objClient;
                IntPtr pMixFormat;
                hr = client.GetMixFormat(out pMixFormat);
                if (hr != 0 || pMixFormat == IntPtr.Zero) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to get mix format (0x{0:X})\"}}", hr));
                    return;
                }

                var fmt = (WAVEFORMATEX)Marshal.PtrToStructure(pMixFormat, typeof(WAVEFORMATEX));
                Guid session = Guid.Empty;
                hr = client.Initialize(0 /* AUDCLNT_SHAREMODE_SHARED */, 0 /* normal mic capture */, 10000000 /* 1 sec */, 0, pMixFormat, ref session);
                if (hr != 0) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Initialize microphone capture failed (0x{0:X})\"}}", hr));
                    return;
                }

                Guid iidCapture = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
                object objCapture;
                hr = client.GetService(ref iidCapture, out objCapture);
                if (hr != 0 || objCapture == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"GetService IAudioCaptureClient failed (0x{0:X})\"}}", hr));
                    return;
                }

                var capture = (IAudioCaptureClient)objCapture;
                client.Start();

                double peak = 0.0;
                double sumSquares = 0.0;
                long totalSamples = 0;

                int elapsed = 0;
                while (elapsed < durationMs) {
                    Thread.Sleep(20);
                    elapsed += 20;

                    uint packetSize = 0;
                    capture.GetNextPacketSize(out packetSize);
                    while (packetSize > 0) {
                        IntPtr pData;
                        uint numFrames;
                        uint flags;
                        ulong devPos, qpcPos;
                        hr = capture.GetBuffer(out pData, out numFrames, out flags, out devPos, out qpcPos);
                        if (hr == 0 && numFrames > 0) {
                            if ((flags & 2 /* AUDCLNT_BUFFERFLAGS_SILENT */) == 0 && pData != IntPtr.Zero) {
                                int samplesToRead = (int)(numFrames * fmt.nChannels);
                                if (fmt.wBitsPerSample == 32) {
                                    float[] floatBuf = new float[samplesToRead];
                                    Marshal.Copy(pData, floatBuf, 0, samplesToRead);
                                    for (int i = 0; i < samplesToRead; i++) {
                                        double val = Math.Abs(floatBuf[i]);
                                        if (val > peak) peak = val;
                                        sumSquares += val * val;
                                        totalSamples++;
                                    }
                                } else if (fmt.wBitsPerSample == 16) {
                                    short[] shortBuf = new short[samplesToRead];
                                    Marshal.Copy(pData, shortBuf, 0, samplesToRead);
                                    for (int i = 0; i < samplesToRead; i++) {
                                        double val = Math.Abs((double)shortBuf[i] / 32768.0);
                                        if (val > peak) peak = val;
                                        sumSquares += val * val;
                                        totalSamples++;
                                    }
                                }
                            }
                            capture.ReleaseBuffer(numFrames);
                        }
                        capture.GetNextPacketSize(out packetSize);
                    }
                }

                client.Stop();

                double rms = totalSamples > 0 ? Math.Sqrt(sumSquares / totalSamples) : 0.0;
                double peakDb = peak > 0.00001 ? Math.Round(20.0 * Math.Log10(peak), 1) : -96.0;
                double rmsDb = rms > 0.00001 ? Math.Round(20.0 * Math.Log10(rms), 1) : -96.0;
                bool isSpeaking = peakDb > -45.0;

                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"success\": true, \"peakDecibels\": {0:F1}, \"rmsDecibels\": {1:F1}, \"isSpeaking\": {2}, \"sampleRate\": {3}, \"channels\": {4}, \"bitsPerSample\": {5}, \"samplesCaptured\": {6}, \"durationMs\": {7}}}",
                    peakDb, rmsDb, isSpeaking ? "true" : "false", fmt.nSamplesPerSec, fmt.nChannels, fmt.wBitsPerSample, totalSamples, durationMs));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AudioMicRecordCmd(string outputPath, int durationSeconds) {
            try {
                if (durationSeconds < 1) durationSeconds = 1;
                if (durationSeconds > 30) durationSeconds = 30;

                string fullPath = Path.GetFullPath(outputPath);
                string dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }

                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice dev = null;
                int hr = enumerator.GetDefaultAudioEndpoint(1 /* eCapture */, 1 /* eMultimedia */, out dev);
                if (hr != 0 || dev == null) {
                    hr = enumerator.GetDefaultAudioEndpoint(1 /* eCapture */, 0 /* eConsole */, out dev);
                }
                if (hr != 0 || dev == null) {
                    IMMDeviceCollection col;
                    if (enumerator.EnumAudioEndpoints(1, 1, out col) == 0 && col != null) {
                        uint c;
                        col.GetCount(out c);
                        if (c > 0) col.Item(0, out dev);
                    }
                }

                if (dev == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"isHeadless\": true, \"error\": \"No audio capture endpoint available (0x{0:X8})\"}}", hr));
                    return;
                }

                Guid iidClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
                object objClient;
                hr = dev.Activate(ref iidClient, 1, IntPtr.Zero, out objClient);
                if (hr != 0 || objClient == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to activate IAudioClient (0x{0:X})\"}}", hr));
                    return;
                }

                var client = (IAudioClient)objClient;
                IntPtr pMixFormat;
                hr = client.GetMixFormat(out pMixFormat);
                if (hr != 0 || pMixFormat == IntPtr.Zero) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to get mix format (0x{0:X})\"}}", hr));
                    return;
                }

                var fmt = (WAVEFORMATEX)Marshal.PtrToStructure(pMixFormat, typeof(WAVEFORMATEX));
                Guid session = Guid.Empty;
                hr = client.Initialize(0, 0 /* normal mic capture */, 10000000, 0, pMixFormat, ref session);
                if (hr != 0) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Initialize microphone capture failed (0x{0:X})\"}}", hr));
                    return;
                }

                Guid iidCapture = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
                object objCapture;
                hr = client.GetService(ref iidCapture, out objCapture);
                if (hr != 0 || objCapture == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"GetService IAudioCaptureClient failed (0x{0:X})\"}}", hr));
                    return;
                }

                var capture = (IAudioCaptureClient)objCapture;
                client.Start();

                var pcmStream = new MemoryStream();
                var writer = new BinaryWriter(pcmStream);

                double peak = 0.0;
                double sumSquares = 0.0;
                long totalSamples = 0;

                int durationMs = durationSeconds * 1000;
                int elapsed = 0;
                while (elapsed < durationMs) {
                    Thread.Sleep(20);
                    elapsed += 20;

                    uint packetSize = 0;
                    capture.GetNextPacketSize(out packetSize);
                    while (packetSize > 0) {
                        IntPtr pData;
                        uint numFrames;
                        uint flags;
                        ulong devPos, qpcPos;
                        hr = capture.GetBuffer(out pData, out numFrames, out flags, out devPos, out qpcPos);
                        if (hr == 0 && numFrames > 0) {
                            int samplesToRead = (int)(numFrames * fmt.nChannels);
                            if ((flags & 2) != 0 || pData == IntPtr.Zero) {
                                for (int i = 0; i < samplesToRead; i++) {
                                    writer.Write((short)0);
                                    totalSamples++;
                                }
                            } else {
                                if (fmt.wBitsPerSample == 32) {
                                    float[] floatBuf = new float[samplesToRead];
                                    Marshal.Copy(pData, floatBuf, 0, samplesToRead);
                                    for (int i = 0; i < samplesToRead; i++) {
                                        float f = floatBuf[i];
                                        double val = Math.Abs(f);
                                        if (val > peak) peak = val;
                                        sumSquares += val * val;
                                        totalSamples++;
                                        short s = (short)Math.Max(-32768, Math.Min(32767, (int)(f * 32767.0f)));
                                        writer.Write(s);
                                    }
                                } else if (fmt.wBitsPerSample == 16) {
                                    short[] shortBuf = new short[samplesToRead];
                                    Marshal.Copy(pData, shortBuf, 0, samplesToRead);
                                    for (int i = 0; i < samplesToRead; i++) {
                                        short s = shortBuf[i];
                                        double val = Math.Abs((double)s / 32768.0);
                                        if (val > peak) peak = val;
                                        sumSquares += val * val;
                                        totalSamples++;
                                        writer.Write(s);
                                    }
                                }
                            }
                            capture.ReleaseBuffer(numFrames);
                        }
                        capture.GetNextPacketSize(out packetSize);
                    }
                }

                client.Stop();

                byte[] pcmData = pcmStream.ToArray();
                uint dataSize = (uint)pcmData.Length;
                uint subchunk1Size = 16;
                ushort audioFormat = 1; // PCM
                ushort channels = fmt.nChannels;
                uint sampleRate = fmt.nSamplesPerSec;
                ushort bitsPerSample = 16;
                uint byteRate = sampleRate * channels * (uint)(bitsPerSample / 8);
                ushort blockAlign = (ushort)(channels * (bitsPerSample / 8));
                uint chunkSize = 36 + dataSize;

                using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var bw = new BinaryWriter(fs)) {
                    // RIFF header
                    bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(chunkSize);
                    bw.Write(Encoding.ASCII.GetBytes("WAVE"));

                    // fmt subchunk
                    bw.Write(Encoding.ASCII.GetBytes("fmt "));
                    bw.Write(subchunk1Size);
                    bw.Write(audioFormat);
                    bw.Write(channels);
                    bw.Write(sampleRate);
                    bw.Write(byteRate);
                    bw.Write(blockAlign);
                    bw.Write(bitsPerSample);

                    // data subchunk
                    bw.Write(Encoding.ASCII.GetBytes("data"));
                    bw.Write(dataSize);
                    bw.Write(pcmData);
                }

                double rms = totalSamples > 0 ? Math.Sqrt(sumSquares / totalSamples) : 0.0;
                double peakDb = peak > 0.00001 ? Math.Round(20.0 * Math.Log10(peak), 1) : -96.0;
                double rmsDb = rms > 0.00001 ? Math.Round(20.0 * Math.Log10(rms), 1) : -96.0;

                FileInfo fi = new FileInfo(fullPath);
                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"success\": true, \"path\": \"{0}\", \"durationSeconds\": {1}, \"fileSize\": {2}, \"sampleRate\": {3}, \"channels\": {4}, \"bitsPerSample\": 16, \"peakDecibels\": {5:F1}, \"rmsDecibels\": {6:F1}, \"samplesRecorded\": {7}}}",
                    fullPath.Replace("\\", "/"), durationSeconds, fi.Length, sampleRate, channels, peakDb, rmsDb, totalSamples));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AudioSessionsCmd() {
            try {
                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice defRender;
                int hr = enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out defRender);
                if (hr != 0 || defRender == null) {
                    Console.WriteLine(string.Format("{{\"success\": true, \"isHeadless\": true, \"sessions\": [], \"count\": 0, \"error\": \"No default audio render endpoint (0x{0:X})\"}}", hr));
                    return;
                }

                Guid iidMgr = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
                object objMgr;
                hr = defRender.Activate(ref iidMgr, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out objMgr);
                if (hr != 0 || objMgr == null) {
                    Console.WriteLine(string.Format("{{\"success\": true, \"isHeadless\": true, \"sessions\": [], \"count\": 0, \"error\": \"Failed to activate IAudioSessionManager2 (0x{0:X})\"}}", hr));
                    return;
                }

                var mgr = (IAudioSessionManager2)objMgr;
                IAudioSessionEnumerator sessionEnum;
                hr = mgr.GetSessionEnumerator(out sessionEnum);
                if (hr != 0 || sessionEnum == null) {
                    Console.WriteLine(string.Format("{{\"success\": true, \"isHeadless\": true, \"sessions\": [], \"count\": 0, \"error\": \"Failed to get session enumerator (0x{0:X})\"}}", hr));
                    return;
                }

                int sCount;
                sessionEnum.GetCount(out sCount);
                var sb = new StringBuilder();
                sb.Append("{\"success\": true, \"sessions\": [");
                bool first = true;
                int emitted = 0;

                for (int i = 0; i < sCount; i++) {
                    IAudioSessionControl ctrl;
                    if (sessionEnum.GetSession(i, out ctrl) == 0 && ctrl != null) {
                        var ctrl2 = ctrl as IAudioSessionControl2;
                        uint pid = 0;
                        string procName = "System";
                        string sessionId = "";
                        if (ctrl2 != null) {
                            ctrl2.GetProcessId(out pid);
                            ctrl2.GetSessionIdentifier(out sessionId);
                            if (pid > 0) {
                                try { procName = Process.GetProcessById((int)pid).ProcessName; } catch {}
                            }
                        }
                        var vol = ctrl as ISimpleAudioVolume;
                        float level = 0f;
                        bool mute = false;
                        if (vol != null) {
                            vol.GetMasterVolume(out level);
                            vol.GetMute(out mute);
                        }
                        var meter = ctrl as IAudioMeterInformation;
                        float peak = 0f;
                        if (meter != null) {
                            meter.GetPeakValue(out peak);
                        }

                        if (!first) sb.Append(",");
                        first = false;
                        sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "{{\"index\": {0}, \"processId\": {1}, \"processName\": \"{2}\", \"volume\": {3}, \"level\": {4:F3}, \"isMuted\": {5}, \"peak\": {6:F3}, \"sessionId\": \"{7}\"}}",
                            i, pid, EscapeJson(procName), (int)Math.Round(level * 100f), level, mute ? "true" : "false", peak, EscapeJson(sessionId ?? "")));
                        emitted++;
                    }
                }

                sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "], \"count\": {0}}}", emitted));
                Console.WriteLine(sb.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"isHeadless\": true, \"sessions\": [], \"count\": 0, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AudioSessionSetCmd(string target, float volumePercent, string muteStr) {
            try {
                if (string.IsNullOrEmpty(target)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Target process ID or name is required\"}");
                    return;
                }

                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice defRender;
                int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out defRender);
                if (hr != 0 || defRender == null) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"isHeadless\": true, \"error\": \"No default audio endpoint (0x{0:X})\"}}", hr));
                    return;
                }

                Guid iidMgr = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
                object objMgr;
                hr = defRender.Activate(ref iidMgr, 1, IntPtr.Zero, out objMgr);
                if (hr != 0 || objMgr == null) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Failed to activate audio session manager\"}");
                    return;
                }

                var mgr = (IAudioSessionManager2)objMgr;
                IAudioSessionEnumerator sessionEnum;
                hr = mgr.GetSessionEnumerator(out sessionEnum);
                if (hr != 0 || sessionEnum == null) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Failed to get session enumerator\"}");
                    return;
                }

                int sCount;
                sessionEnum.GetCount(out sCount);

                uint targetPid = 0;
                bool isNumeric = uint.TryParse(target, out targetPid);

                bool matched = false;
                uint matchedPid = 0;
                string matchedProcName = "";
                float finalVol = -1f;
                bool finalMute = false;

                Guid ctx = Guid.Empty;

                for (int i = 0; i < sCount; i++) {
                    IAudioSessionControl ctrl;
                    if (sessionEnum.GetSession(i, out ctrl) == 0 && ctrl != null) {
                        var ctrl2 = ctrl as IAudioSessionControl2;
                        uint pid = 0;
                        string procName = "System";
                        if (ctrl2 != null) {
                            ctrl2.GetProcessId(out pid);
                            if (pid > 0) {
                                try { procName = Process.GetProcessById((int)pid).ProcessName; } catch {}
                            }
                        }

                        bool isMatch = false;
                        if (isNumeric && (pid == targetPid || (targetPid < (uint)sCount && (uint)i == targetPid))) isMatch = true;
                        else if (!string.IsNullOrEmpty(procName) && procName.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0) isMatch = true;

                        if (isMatch) {
                            var vol = ctrl as ISimpleAudioVolume;
                            if (vol != null) {
                                if (volumePercent >= 0f) {
                                    float scalar = Math.Max(0f, Math.Min(1f, volumePercent / 100.0f));
                                    vol.SetMasterVolume(scalar, ref ctx);
                                }
                                if (!string.IsNullOrEmpty(muteStr)) {
                                    bool m = muteStr.Equals("true", StringComparison.OrdinalIgnoreCase) || muteStr == "1";
                                    vol.SetMute(m, ref ctx);
                                }

                                vol.GetMasterVolume(out finalVol);
                                vol.GetMute(out finalMute);
                                matched = true;
                                matchedPid = pid;
                                matchedProcName = procName;
                                break;
                            }
                        }
                    }
                }

                if (matched) {
                    Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{{\"success\": true, \"matched\": true, \"processId\": {0}, \"processName\": \"{1}\", \"volume\": {2}, \"level\": {3:F3}, \"isMuted\": {4}}}",
                        matchedPid, EscapeJson(matchedProcName), (int)Math.Round(finalVol * 100f), finalVol, finalMute ? "true" : "false"));
                } else {
                    Console.WriteLine(string.Format("{{\"success\": false, \"matched\": false, \"error\": \"No active audio session found matching '{0}'\"}}", EscapeJson(target)));
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AudioPlayCmd(string filePath) {
            try {
                if (string.IsNullOrEmpty(filePath) || filePath.Equals("stop", StringComparison.OrdinalIgnoreCase)) {
                    PlaySound(null, IntPtr.Zero, 0);
                    Console.WriteLine("{\"success\": true, \"stopped\": true}");
                    return;
                }

                string fullPath = Path.GetFullPath(filePath);
                if (!File.Exists(fullPath)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Audio file not found: {0}\"}}", EscapeJson(fullPath)));
                    return;
                }

                bool ok = PlaySound(fullPath, IntPtr.Zero, 0x0001 | 0x00020000);
                Console.WriteLine(string.Format("{{\"success\": {0}, \"playing\": {0}, \"filePath\": \"{1}\"}}",
                    ok ? "true" : "false", EscapeJson(fullPath.Replace("\\", "/"))));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AudioBeepCmd(int freqHz, int durationMs) {
            try {
                if (freqHz < 37) freqHz = 37;
                if (freqHz > 32767) freqHz = 32767;
                if (durationMs < 10) durationMs = 10;
                if (durationMs > 5000) durationMs = 5000;

                bool ok = Beep((uint)freqHz, (uint)durationMs);
                bool fallback = false;
                if (!ok) {
                    fallback = MessageBeep(0);
                }
                Console.WriteLine(string.Format("{{\"success\": {0}, \"frequencyHz\": {1}, \"durationMs\": {2}, \"emitted\": {3}}}",
                    (ok || fallback) ? "true" : "false", freqHz, durationMs, (ok || fallback) ? "true" : "false"));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static int ParseNoteFrequency(string noteName) {
            if (string.IsNullOrEmpty(noteName)) return 0;
            string n = noteName.Trim().ToUpperInvariant();
            if (n == "R" || n == "REST" || n == "0" || n == "PAUSE") return 0;

            int octave = 4;
            string key = "";
            char last = n[n.Length - 1];
            if (char.IsDigit(last)) {
                octave = last - '0';
                key = n.Substring(0, n.Length - 1);
            } else {
                key = n;
            }

            int semitoneInOctave = 0;
            if (key == "C") semitoneInOctave = 0;
            else if (key == "C#" || key == "DB") semitoneInOctave = 1;
            else if (key == "D") semitoneInOctave = 2;
            else if (key == "D#" || key == "EB") semitoneInOctave = 3;
            else if (key == "E") semitoneInOctave = 4;
            else if (key == "F") semitoneInOctave = 5;
            else if (key == "F#" || key == "GB") semitoneInOctave = 6;
            else if (key == "G") semitoneInOctave = 7;
            else if (key == "G#" || key == "AB") semitoneInOctave = 8;
            else if (key == "A") semitoneInOctave = 9;
            else if (key == "A#" || key == "BB") semitoneInOctave = 10;
            else if (key == "B") semitoneInOctave = 11;
            else {
                int directHz;
                if (int.TryParse(key, out directHz)) return directHz;
                return 440;
            }

            int midiNote = (octave + 1) * 12 + semitoneInOctave;
            double freq = 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
            return (int)Math.Round(freq);
        }

        static void AudioInspectCmd(string filePath) {
            try {
                if (string.IsNullOrEmpty(filePath)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"WAV file path is required\"}");
                    return;
                }
                string fullPath = Path.GetFullPath(filePath);
                if (!File.Exists(fullPath)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"File not found: {0}\"}}", EscapeJson(fullPath)));
                    return;
                }

                FileInfo fi = new FileInfo(fullPath);
                using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var br = new BinaryReader(fs)) {
                    byte[] riff = br.ReadBytes(4);
                    if (Encoding.ASCII.GetString(riff) != "RIFF") {
                        Console.WriteLine("{\"success\": false, \"error\": \"Not a valid RIFF file\"}");
                        return;
                    }
                    uint chunkSize = br.ReadUInt32();
                    byte[] wave = br.ReadBytes(4);
                    if (Encoding.ASCII.GetString(wave) != "WAVE") {
                        Console.WriteLine("{\"success\": false, \"error\": \"Not a valid WAVE file\"}");
                        return;
                    }

                    ushort audioFormat = 0;
                    ushort channels = 0;
                    uint sampleRate = 0;
                    uint byteRate = 0;
                    ushort blockAlign = 0;
                    ushort bitsPerSample = 0;
                    uint dataSize = 0;
                    long dataOffset = 0;

                    while (fs.Position + 8 <= fs.Length) {
                        string subchunkId = Encoding.ASCII.GetString(br.ReadBytes(4));
                        uint subchunkSize = br.ReadUInt32();

                        if (subchunkId == "fmt ") {
                            long startFmt = fs.Position;
                            audioFormat = br.ReadUInt16();
                            channels = br.ReadUInt16();
                            sampleRate = br.ReadUInt32();
                            byteRate = br.ReadUInt32();
                            blockAlign = br.ReadUInt16();
                            bitsPerSample = br.ReadUInt16();
                            fs.Position = startFmt + subchunkSize;
                        } else if (subchunkId == "data") {
                            dataSize = subchunkSize;
                            dataOffset = fs.Position;
                            break;
                        } else {
                            fs.Position += subchunkSize;
                        }
                    }

                    if (channels == 0 || sampleRate == 0) {
                        Console.WriteLine("{\"success\": false, \"error\": \"Missing or invalid fmt chunk in WAV file\"}");
                        return;
                    }

                    double durationSec = byteRate > 0 ? (double)dataSize / (double)byteRate : 0.0;
                    string formatName = "PCM";
                    if (audioFormat == 3) formatName = "IEEE Float";
                    else if (audioFormat == 6) formatName = "A-law";
                    else if (audioFormat == 7) formatName = "Mu-law";
                    else if (audioFormat == 0xFFFE) formatName = "Extensible";

                    double peak = 0.0;
                    double sumSq = 0.0;
                    long samplesRead = 0;
                    bool clipped = false;

                    if (dataOffset > 0 && fs.Length >= dataOffset) {
                        fs.Position = dataOffset;
                        int samplesToScan = (int)Math.Min((long)100000, (dataSize / Math.Max(1, (bitsPerSample / 8))));
                        if (bitsPerSample == 16) {
                            for (int i = 0; i < samplesToScan && fs.Position + 2 <= fs.Length; i++) {
                                short s = br.ReadInt16();
                                if (s == short.MaxValue || s == short.MinValue) clipped = true;
                                double v = Math.Abs((double)s / 32768.0);
                                if (v > peak) peak = v;
                                sumSq += v * v;
                                samplesRead++;
                            }
                        } else if (bitsPerSample == 8) {
                            for (int i = 0; i < samplesToScan && fs.Position < fs.Length; i++) {
                                byte b = br.ReadByte();
                                double v = Math.Abs(((double)b - 128.0) / 128.0);
                                if (v > peak) peak = v;
                                sumSq += v * v;
                                samplesRead++;
                            }
                        }
                    }

                    double rms = samplesRead > 0 ? Math.Sqrt(sumSq / samplesRead) : 0.0;
                    double peakDb = peak > 0.00001 ? Math.Round(20.0 * Math.Log10(peak), 1) : -96.0;
                    double rmsDb = rms > 0.00001 ? Math.Round(20.0 * Math.Log10(rms), 1) : -96.0;
                    bool isSilent = peakDb <= -60.0;

                    Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{{\"success\": true, \"filePath\": \"{0}\", \"format\": \"{1}\", \"formatTag\": {2}, \"channels\": {3}, \"sampleRate\": {4}, \"bitsPerSample\": {5}, \"byteRate\": {6}, \"blockAlign\": {7}, \"dataSize\": {8}, \"fileSize\": {9}, \"durationSeconds\": {10:F3}, \"peakDecibels\": {11:F1}, \"rmsDecibels\": {12:F1}, \"isSilent\": {13}, \"isClipped\": {14}}}",
                        fullPath.Replace("\\", "/"), formatName, audioFormat, channels, sampleRate, bitsPerSample, byteRate, blockAlign, dataSize, fi.Length, durationSec, peakDb, rmsDb, isSilent ? "true" : "false", clipped ? "true" : "false"));
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AudioSequenceCmd(string sequenceInput) {
            try {
                if (string.IsNullOrEmpty(sequenceInput)) sequenceInput = "success";
                string input = sequenceInput.Trim().ToLowerInvariant();

                var notes = new List<KeyValuePair<int, int>>();

                if (input == "success") {
                    notes.Add(new KeyValuePair<int, int>(523, 100)); // C5
                    notes.Add(new KeyValuePair<int, int>(659, 100)); // E5
                    notes.Add(new KeyValuePair<int, int>(784, 100)); // G5
                    notes.Add(new KeyValuePair<int, int>(1046, 250)); // C6
                } else if (input == "alert") {
                    notes.Add(new KeyValuePair<int, int>(880, 150));
                    notes.Add(new KeyValuePair<int, int>(0, 50));
                    notes.Add(new KeyValuePair<int, int>(880, 150));
                    notes.Add(new KeyValuePair<int, int>(0, 50));
                    notes.Add(new KeyValuePair<int, int>(880, 200));
                } else if (input == "error") {
                    notes.Add(new KeyValuePair<int, int>(330, 180));
                    notes.Add(new KeyValuePair<int, int>(277, 180));
                    notes.Add(new KeyValuePair<int, int>(220, 350));
                } else if (input == "sonar") {
                    notes.Add(new KeyValuePair<int, int>(1200, 120));
                    notes.Add(new KeyValuePair<int, int>(1600, 200));
                } else if (input == "chime") {
                    notes.Add(new KeyValuePair<int, int>(587, 120));
                    notes.Add(new KeyValuePair<int, int>(880, 150));
                    notes.Add(new KeyValuePair<int, int>(1175, 300));
                } else if (input == "ready") {
                    notes.Add(new KeyValuePair<int, int>(440, 80));
                    notes.Add(new KeyValuePair<int, int>(880, 150));
                } else {
                    string[] parts = sequenceInput.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts) {
                        string[] tokens = part.Trim().Split(':');
                        if (tokens.Length >= 1) {
                            int f = ParseNoteFrequency(tokens[0]);
                            int d = 150;
                            if (tokens.Length >= 2) int.TryParse(tokens[1], out d);
                            if (d < 10) d = 10;
                            if (d > 3000) d = 3000;
                            notes.Add(new KeyValuePair<int, int>(f, d));
                        }
                    }
                }

                int totalDuration = 0;
                foreach (var n in notes) totalDuration += n.Value;

                foreach (var n in notes) {
                    if (n.Key <= 0) {
                        Thread.Sleep(n.Value);
                    } else {
                        bool ok = Beep((uint)n.Key, (uint)n.Value);
                        if (!ok) {
                            MessageBeep(0);
                            Thread.Sleep(n.Value);
                        }
                    }
                }

                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"success\": true, \"sequence\": \"{0}\", \"noteCount\": {1}, \"totalDurationMs\": {2}}}",
                    EscapeJson(sequenceInput), notes.Count, totalDuration));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AudioTtsWavCmd(string text, string outputPath, string voiceName, int rate, int volume) {
            try {
                if (string.IsNullOrEmpty(text)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Speech text is required\"}");
                    return;
                }
                string outPath = Path.GetFullPath(string.IsNullOrEmpty(outputPath) ? "speech_output.wav" : outputPath);
                string dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                Type voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
                Type streamType = Type.GetTypeFromProgID("SAPI.SpFileStream");
                if (voiceType == null || streamType == null) {
                    Console.WriteLine("{\"success\": false, \"isHeadless\": true, \"error\": \"Windows SAPI subsystem not available\"}");
                    return;
                }

                dynamic voice = Activator.CreateInstance(voiceType);
                dynamic stream = Activator.CreateInstance(streamType);

                if (!string.IsNullOrEmpty(voiceName)) {
                    try {
                        dynamic voices = voice.GetVoices();
                        for (int i = 0; i < voices.Count; i++) {
                            dynamic v = voices.Item(i);
                            string desc = v.GetDescription();
                            if (desc.IndexOf(voiceName, StringComparison.OrdinalIgnoreCase) >= 0) {
                                voice.Voice = v;
                                break;
                            }
                        }
                    } catch {}
                }

                if (rate >= -10 && rate <= 10) voice.Rate = rate;
                if (volume >= 0 && volume <= 100) voice.Volume = volume;

                stream.Open(outPath, 3 /* SSFMCreateForWrite */, false);
                voice.AudioOutputStream = stream;
                voice.Speak(text);
                stream.Close();

                FileInfo fi = new FileInfo(outPath);
                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"success\": true, \"path\": \"{0}\", \"fileSize\": {1}, \"text\": \"{2}\"}}",
                    outPath.Replace("\\", "/"), fi.Length, EscapeJson(text)));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"isHeadless\": true, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AudioDuckCmd(string target, float duckPercent, int durMs, float restorePercent) {
            try {
                if (string.IsNullOrEmpty(target)) target = "all";
                if (duckPercent < 0f) duckPercent = 0f;
                if (duckPercent > 100f) duckPercent = 100f;
                if (durMs < 100) durMs = 100;
                if (durMs > 60000) durMs = 60000;

                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice defRender;
                int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out defRender);
                if (hr != 0 || defRender == null) {
                    Console.WriteLine(string.Format("{{\"success\": true, \"ducked\": false, \"isHeadless\": true, \"sessionsCount\": 0, \"error\": \"No default audio endpoint (0x{0:X})\"}}", hr));
                    return;
                }

                Guid iidMgr = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
                object objMgr;
                hr = defRender.Activate(ref iidMgr, 1, IntPtr.Zero, out objMgr);
                if (hr != 0 || objMgr == null) {
                    Console.WriteLine(string.Format("{{\"success\": true, \"ducked\": false, \"isHeadless\": true, \"sessionsCount\": 0, \"error\": \"Failed to activate audio session manager (0x{0:X})\"}}", hr));
                    return;
                }

                var mgr = (IAudioSessionManager2)objMgr;
                IAudioSessionEnumerator sessionEnum;
                hr = mgr.GetSessionEnumerator(out sessionEnum);
                if (hr != 0 || sessionEnum == null) {
                    Console.WriteLine(string.Format("{{\"success\": true, \"ducked\": false, \"isHeadless\": true, \"sessionsCount\": 0, \"error\": \"Failed to get session enumerator (0x{0:X})\"}}", hr));
                    return;
                }

                int sCount;
                sessionEnum.GetCount(out sCount);

                uint targetPid = 0;
                bool isNumeric = uint.TryParse(target, out targetPid);
                bool duckAll = target.Equals("all", StringComparison.OrdinalIgnoreCase);

                var duckedSessions = new List<string>();
                var sessionsToRestore = new List<KeyValuePair<ISimpleAudioVolume, float>>();
                Guid ctx = Guid.Empty;

                for (int i = 0; i < sCount; i++) {
                    IAudioSessionControl ctrl;
                    if (sessionEnum.GetSession(i, out ctrl) == 0 && ctrl != null) {
                        var ctrl2 = ctrl as IAudioSessionControl2;
                        uint pid = 0;
                        string procName = "System";
                        if (ctrl2 != null) {
                            ctrl2.GetProcessId(out pid);
                            if (pid > 0) {
                                try { procName = Process.GetProcessById((int)pid).ProcessName; } catch {}
                            }
                        }

                        bool isMatch = duckAll;
                        if (!duckAll) {
                            if (isNumeric && (pid == targetPid || (targetPid < (uint)sCount && (uint)i == targetPid))) isMatch = true;
                            else if (!string.IsNullOrEmpty(procName) && procName.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0) isMatch = true;
                        }

                        if (isMatch) {
                            var vol = ctrl as ISimpleAudioVolume;
                            if (vol != null) {
                                float origLevel = 1.0f;
                                vol.GetMasterVolume(out origLevel);
                                float duckScalar = duckPercent / 100.0f;
                                vol.SetMasterVolume(duckScalar, ref ctx);
                                sessionsToRestore.Add(new KeyValuePair<ISimpleAudioVolume, float>(vol, restorePercent >= 0f ? (restorePercent / 100.0f) : origLevel));
                                duckedSessions.Add(procName);
                            }
                        }
                    }
                }

                if (sessionsToRestore.Count > 0) {
                    ThreadPool.QueueUserWorkItem((state) => {
                        try {
                            Thread.Sleep(durMs);
                            Guid rCtx = Guid.Empty;
                            foreach (var pair in sessionsToRestore) {
                                try { pair.Key.SetMasterVolume(pair.Value, ref rCtx); } catch {}
                            }
                        } catch {}
                    });

                    Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{{\"success\": true, \"ducked\": true, \"duckPercent\": {0:F1}, \"durationMs\": {1}, \"sessionsCount\": {2}, \"sessions\": \"{3}\"}}",
                        duckPercent, durMs, duckedSessions.Count, EscapeJson(string.Join(", ", duckedSessions.ToArray()))));
                } else {
                    Console.WriteLine(string.Format("{{\"success\": true, \"ducked\": false, \"isHeadless\": true, \"sessionsCount\": 0, \"message\": \"No active sessions matched '{0}'\"}}", EscapeJson(target)));
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"isHeadless\": true, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static RegistryHive ParseHive(string path, out string subPath) {
            subPath = "";
            if (string.IsNullOrEmpty(path)) return RegistryHive.LocalMachine;
            string p = path.Trim();
            int slash = p.IndexOf('\\');
            string hiveStr = slash >= 0 ? p.Substring(0, slash).ToUpperInvariant() : p.ToUpperInvariant();
            subPath = slash >= 0 ? p.Substring(slash + 1) : "";

            if (hiveStr == "HKLM" || hiveStr == "HKEY_LOCAL_MACHINE") return RegistryHive.LocalMachine;
            if (hiveStr == "HKCU" || hiveStr == "HKEY_CURRENT_USER") return RegistryHive.CurrentUser;
            if (hiveStr == "HKCR" || hiveStr == "HKEY_CLASSES_ROOT") return RegistryHive.ClassesRoot;
            if (hiveStr == "HKU" || hiveStr == "HKEY_USERS") return RegistryHive.Users;
            if (hiveStr == "HKCC" || hiveStr == "HKEY_CURRENT_CONFIG") return RegistryHive.CurrentConfig;
            return RegistryHive.LocalMachine;
        }

        static RegistryValueKind ParseValueKind(string kindStr) {
            if (string.IsNullOrEmpty(kindStr)) return RegistryValueKind.String;
            string k = kindStr.Trim().ToLowerInvariant();
            if (k == "dword" || k == "int" || k == "reg_dword") return RegistryValueKind.DWord;
            if (k == "qword" || k == "long" || k == "reg_qword") return RegistryValueKind.QWord;
            if (k == "multistring" || k == "multi_sz" || k == "reg_multi_sz") return RegistryValueKind.MultiString;
            if (k == "expandstring" || k == "expand_sz" || k == "reg_expand_sz") return RegistryValueKind.ExpandString;
            if (k == "binary" || k == "reg_binary") return RegistryValueKind.Binary;
            return RegistryValueKind.String;
        }

        static void ServiceControlCmd(string action, string name, string filter, string statusFilter, int timeoutMs) {
            try {
                if (string.IsNullOrEmpty(action)) action = "list";
                string act = action.Trim().ToLowerInvariant();

                if (act == "list") {
                    ServiceController[] all = ServiceController.GetServices();
                    var sb = new StringBuilder();
                    sb.Append("{\"success\": true, \"services\": [");
                    bool first = true;
                    int count = 0;

                    string f = (filter ?? "").Trim().ToLowerInvariant();
                    string sf = (statusFilter ?? "all").Trim().ToLowerInvariant();

                    foreach (var sc in all) {
                        bool statusMatch = true;
                        if (sf == "running" && sc.Status != ServiceControllerStatus.Running) statusMatch = false;
                        else if (sf == "stopped" && sc.Status != ServiceControllerStatus.Stopped) statusMatch = false;

                        if (!statusMatch) continue;

                        if (!string.IsNullOrEmpty(f)) {
                            bool nameMatch = sc.ServiceName.ToLowerInvariant().Contains(f) || sc.DisplayName.ToLowerInvariant().Contains(f);
                            if (!nameMatch) continue;
                        }

                        if (!first) sb.Append(",");
                        first = false;

                        sb.Append(string.Format("{{\"name\": \"{0}\", \"displayName\": \"{1}\", \"status\": \"{2}\", \"canStop\": {3}, \"canPause\": {4}}}",
                            EscapeJson(sc.ServiceName), EscapeJson(sc.DisplayName), sc.Status, sc.CanStop ? "true" : "false", sc.CanPauseAndContinue ? "true" : "false"));
                        count++;
                        if (count >= 100) break;
                    }

                    sb.Append(string.Format("], \"totalMatching\": {0}, \"totalServices\": {1}}}", count, all.Length));
                    Console.WriteLine(sb.ToString());
                    return;
                }

                if (string.IsNullOrEmpty(name)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Service name is required for action '" + act + "'\"}");
                    return;
                }

                ServiceController target = null;
                try {
                    target = new ServiceController(name);
                    var s = target.Status;
                } catch (Exception ex) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Service '{0}' not found: {1}\"}}", EscapeJson(name), EscapeJson(ex.Message)));
                    return;
                }

                using (target) {
                    if (act == "status") {
                        string startType = "Unknown";
                        string imgPath = "";
                        try {
                            using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name)) {
                                if (k != null) {
                                    object stObj = k.GetValue("Start");
                                    if (stObj != null) {
                                        int stVal = Convert.ToInt32(stObj);
                                        if (stVal == 2) startType = "Automatic";
                                        else if (stVal == 3) startType = "Manual";
                                        else if (stVal == 4) startType = "Disabled";
                                        else if (stVal == 0) startType = "Boot";
                                        else if (stVal == 1) startType = "System";
                                    }
                                    imgPath = k.GetValue("ImagePath", "") as string ?? "";
                                }
                            }
                        } catch {}

                        Console.WriteLine(string.Format("{{\"success\": true, \"name\": \"{0}\", \"displayName\": \"{1}\", \"status\": \"{2}\", \"startType\": \"{3}\", \"imagePath\": \"{4}\", \"canStop\": {5}, \"canPause\": {6}}}",
                            EscapeJson(target.ServiceName), EscapeJson(target.DisplayName), target.Status, startType, EscapeJson(imgPath), target.CanStop ? "true" : "false", target.CanPauseAndContinue ? "true" : "false"));
                        return;
                    }

                    TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMs > 0 ? timeoutMs : 5000);

                    if (act == "start") {
                        if (target.Status == ServiceControllerStatus.Running) {
                            Console.WriteLine(string.Format("{{\"success\": true, \"name\": \"{0}\", \"status\": \"Running\", \"alreadyRunning\": true}}", EscapeJson(name)));
                            return;
                        }
                        target.Start();
                        target.WaitForStatus(ServiceControllerStatus.Running, timeout);
                        Console.WriteLine(string.Format("{{\"success\": true, \"name\": \"{0}\", \"status\": \"{1}\", \"started\": true}}", EscapeJson(name), target.Status));
                        return;
                    }

                    if (act == "stop") {
                        if (target.Status == ServiceControllerStatus.Stopped) {
                            Console.WriteLine(string.Format("{{\"success\": true, \"name\": \"{0}\", \"status\": \"Stopped\", \"alreadyStopped\": true}}", EscapeJson(name)));
                            return;
                        }
                        if (!target.CanStop) {
                            Console.WriteLine(string.Format("{{\"success\": false, \"name\": \"{0}\", \"error\": \"Service cannot be stopped\"}}", EscapeJson(name)));
                            return;
                        }
                        target.Stop();
                        target.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                        Console.WriteLine(string.Format("{{\"success\": true, \"name\": \"{0}\", \"status\": \"{1}\", \"stopped\": true}}", EscapeJson(name), target.Status));
                        return;
                    }

                    if (act == "restart") {
                        if (target.Status == ServiceControllerStatus.Running && target.CanStop) {
                            target.Stop();
                            target.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                        }
                        target.Start();
                        target.WaitForStatus(ServiceControllerStatus.Running, timeout);
                        Console.WriteLine(string.Format("{{\"success\": true, \"name\": \"{0}\", \"status\": \"{1}\", \"restarted\": true}}", EscapeJson(name), target.Status));
                        return;
                    }

                    if (act == "pause") {
                        if (!target.CanPauseAndContinue) {
                            Console.WriteLine(string.Format("{{\"success\": false, \"name\": \"{0}\", \"error\": \"Service does not support pause/continue\"}}", EscapeJson(name)));
                            return;
                        }
                        target.Pause();
                        target.WaitForStatus(ServiceControllerStatus.Paused, timeout);
                        Console.WriteLine(string.Format("{{\"success\": true, \"name\": \"{0}\", \"status\": \"{1}\", \"paused\": true}}", EscapeJson(name), target.Status));
                        return;
                    }

                    if (act == "continue") {
                        if (!target.CanPauseAndContinue) {
                            Console.WriteLine(string.Format("{{\"success\": false, \"name\": \"{0}\", \"error\": \"Service does not support pause/continue\"}}", EscapeJson(name)));
                            return;
                        }
                        target.Continue();
                        target.WaitForStatus(ServiceControllerStatus.Running, timeout);
                        Console.WriteLine(string.Format("{{\"success\": true, \"name\": \"{0}\", \"status\": \"{1}\", \"continued\": true}}", EscapeJson(name), target.Status));
                        return;
                    }

                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Unknown action '{0}'\"}}", EscapeJson(act)));
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void EventLogQueryCmd(string channel, string preset, string severity, int hours, int limit, string search) {
            try {
                if (string.IsNullOrEmpty(channel)) channel = "System";
                if (hours <= 0) hours = 24;
                if (limit <= 0) limit = 20;
                if (limit > 100) limit = 100;

                long ms = (long)hours * 3600000L;
                string queryStr = "*[System[TimeCreated[timediff(@SystemTime) <= " + ms + "]";

                string p = (preset ?? "").Trim().ToLowerInvariant();
                string s = (severity ?? "").Trim().ToLowerInvariant();

                if (p == "crashes") {
                    channel = "Application";
                    queryStr += " and (EventID=1000 or EventID=1001 or EventID=1002)]]";
                } else if (p == "bluescreen") {
                    channel = "System";
                    queryStr += " and (EventID=41 or EventID=1001)]]";
                } else if (p == "disk") {
                    channel = "System";
                    queryStr += " and (EventID=153 or EventID=55 or EventID=51 or EventID=137)]]";
                } else {
                    if (s == "critical") queryStr += " and (Level=1)";
                    else if (s == "error" || p == "errors") queryStr += " and (Level=1 or Level=2)";
                    else if (s == "warning" || p == "warnings") queryStr += " and (Level=3)";
                    queryStr += "]]";
                }

                var query = new EventLogQuery(channel, PathType.LogName, queryStr);
                query.ReverseDirection = true;

                var sb = new StringBuilder();
                sb.Append(string.Format("{{\"success\": true, \"channel\": \"{0}\", \"events\": [", EscapeJson(channel)));
                bool first = true;
                int count = 0;

                using (var reader = new EventLogReader(query)) {
                    EventRecord rec;
                    while ((rec = reader.ReadEvent()) != null && count < limit) {
                        using (rec) {
                            string msg = "";
                            try { msg = rec.FormatDescription(); } catch {}
                            if (string.IsNullOrEmpty(msg)) msg = "Event " + rec.Id;

                            if (!string.IsNullOrEmpty(search)) {
                                if (msg.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                                    (rec.ProviderName ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) {
                                    continue;
                                }
                            }

                            if (!first) sb.Append(",");
                            first = false;

                            string timeStr = rec.TimeCreated.HasValue ? rec.TimeCreated.Value.ToUniversalTime().ToString("o") : "";
                            sb.Append(string.Format("{{\"recordId\": {0}, \"id\": {1}, \"provider\": \"{2}\", \"level\": \"{3}\", \"timeCreated\": \"{4}\", \"message\": \"{5}\"}}",
                                rec.RecordId ?? 0, rec.Id, EscapeJson(rec.ProviderName ?? ""), EscapeJson(rec.LevelDisplayName ?? "Info"),
                                timeStr, EscapeJson(msg.Length > 300 ? msg.Substring(0, 300) + "..." : msg)));
                            count++;
                        }
                    }
                }

                sb.Append(string.Format("], \"count\": {0}, \"hours\": {1}}}", count, hours));
                Console.WriteLine(sb.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"channel\": \"{0}\", \"events\": [], \"count\": 0, \"error\": \"{1}\"}}",
                    EscapeJson(channel ?? "System"), EscapeJson(ex.Message)));
            }
        }

        static void RegistryCmd(string action, string path, string name, string value, string kindStr) {
            try {
                if (string.IsNullOrEmpty(action)) action = "get";
                string act = action.Trim().ToLowerInvariant();

                string subPath;
                RegistryHive hive = ParseHive(path, out subPath);

                using (var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)) {
                    if (act == "get") {
                        using (var key = root.OpenSubKey(subPath, false)) {
                            if (key == null) {
                                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Key not found: {0}\"}}", EscapeJson(path)));
                                return;
                            }
                            object val = key.GetValue(name);
                            if (val == null) {
                                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Value '{0}' not found under '{1}'\"}}", EscapeJson(name), EscapeJson(path)));
                                return;
                            }
                            RegistryValueKind kind = key.GetValueKind(name);
                            string valStr = val is string[] ? string.Join("; ", (string[])val) : (val is byte[] ? BitConverter.ToString((byte[])val) : val.ToString());
                            Console.WriteLine(string.Format("{{\"success\": true, \"hive\": \"{0}\", \"path\": \"{1}\", \"name\": \"{2}\", \"value\": \"{3}\", \"kind\": \"{4}\"}}",
                                hive, EscapeJson(subPath), EscapeJson(name), EscapeJson(valStr), kind));
                            return;
                        }
                    }

                    if (act == "list") {
                        using (var key = root.OpenSubKey(subPath, false)) {
                            if (key == null) {
                                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Key not found: {0}\"}}", EscapeJson(path)));
                                return;
                            }
                            string[] subkeys = key.GetSubKeyNames();
                            string[] valNames = key.GetValueNames();

                            var sb = new StringBuilder();
                            sb.Append(string.Format("{{\"success\": true, \"hive\": \"{0}\", \"path\": \"{1}\", \"subkeys\": [", hive, EscapeJson(subPath)));
                            for (int i = 0; i < Math.Min(subkeys.Length, 50); i++) {
                                if (i > 0) sb.Append(",");
                                sb.Append(string.Format("\"{0}\"", EscapeJson(subkeys[i])));
                            }
                            sb.Append(string.Format("], \"subkeysCount\": {0}, \"values\": [", subkeys.Length));
                            for (int i = 0; i < Math.Min(valNames.Length, 50); i++) {
                                if (i > 0) sb.Append(",");
                                string vn = valNames[i];
                                object val = key.GetValue(vn);
                                RegistryValueKind kind = key.GetValueKind(vn);
                                string valStr = val is string[] ? string.Join("; ", (string[])val) : (val is byte[] ? BitConverter.ToString((byte[])val) : (val != null ? val.ToString() : ""));
                                sb.Append(string.Format("{{\"name\": \"{0}\", \"value\": \"{1}\", \"kind\": \"{2}\"}}",
                                    EscapeJson(vn), EscapeJson(valStr.Length > 200 ? valStr.Substring(0, 200) + "..." : valStr), kind));
                            }
                            sb.Append(string.Format("], \"valuesCount\": {0}}}", valNames.Length));
                            Console.WriteLine(sb.ToString());
                            return;
                        }
                    }

                    if (act == "set") {
                        if (string.IsNullOrEmpty(name)) {
                            Console.WriteLine("{\"success\": false, \"error\": \"Value name is required for set\"}");
                            return;
                        }
                        RegistryValueKind kind = ParseValueKind(kindStr);
                        using (var key = root.CreateSubKey(subPath)) {
                            if (key == null) {
                                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to create/open subkey: {0}\"}}", EscapeJson(path)));
                                return;
                            }
                            object valObj = value ?? "";
                            if (kind == RegistryValueKind.DWord) valObj = Convert.ToInt32(value);
                            else if (kind == RegistryValueKind.QWord) valObj = Convert.ToInt64(value);
                            else if (kind == RegistryValueKind.MultiString) valObj = (value ?? "").Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

                            key.SetValue(name, valObj, kind);
                            Console.WriteLine(string.Format("{{\"success\": true, \"hive\": \"{0}\", \"path\": \"{1}\", \"name\": \"{2}\", \"value\": \"{3}\", \"kind\": \"{4}\", \"written\": true}}",
                                hive, EscapeJson(subPath), EscapeJson(name), EscapeJson(value ?? ""), kind));
                            return;
                        }
                    }

                    if (act == "delete") {
                        using (var key = root.OpenSubKey(subPath, true)) {
                            if (key == null) {
                                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Key not found: {0}\"}}", EscapeJson(path)));
                                return;
                            }
                            if (!string.IsNullOrEmpty(name)) {
                                key.DeleteValue(name, false);
                                Console.WriteLine(string.Format("{{\"success\": true, \"path\": \"{0}\", \"name\": \"{1}\", \"deleted\": true}}", EscapeJson(path), EscapeJson(name)));
                            } else {
                                root.DeleteSubKey(subPath, false);
                                Console.WriteLine(string.Format("{{\"success\": true, \"path\": \"{0}\", \"deletedKey\": true}}", EscapeJson(path)));
                            }
                            return;
                        }
                    }

                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Unknown registry action: {0}\"}}", EscapeJson(act)));
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static string GetDeviceProperty(IntPtr devInfoSet, ref SP_DEVINFO_DATA devData, uint prop) {
            uint regType;
            uint reqSize;
            SetupDiGetDeviceRegistryProperty(devInfoSet, ref devData, prop, out regType, null, 0, out reqSize);
            if (reqSize == 0) return "";
            byte[] buf = new byte[reqSize];
            if (SetupDiGetDeviceRegistryProperty(devInfoSet, ref devData, prop, out regType, buf, reqSize, out reqSize)) {
                if (regType == 7) {
                    string[] parts = Encoding.Unicode.GetString(buf).Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                    return string.Join("; ", parts);
                } else if (regType == 1 || regType == 2) {
                    return Encoding.Unicode.GetString(buf).TrimEnd('\0');
                }
            }
            return "";
        }

        static string GetProblemDescription(uint code) {
            switch (code) {
                case 0: return "OK";
                case 1: return "Device not configured properly";
                case 3: return "Driver corrupted or low memory";
                case 10: return "Device cannot start";
                case 12: return "Resource conflict";
                case 14: return "Restart computer to finish device installation";
                case 18: return "Reinstall drivers for this device";
                case 19: return "Registry information corrupted";
                case 21: return "Windows is removing this device";
                case 22: return "Device is disabled";
                case 24: return "Device not present or not working properly";
                case 28: return "Drivers for this device are not installed";
                case 29: return "Device disabled by firmware";
                case 31: return "Device not working properly";
                case 32: return "Driver service is disabled";
                case 37: return "Driver initialization failed";
                case 38: return "Previous driver instance still in memory";
                case 39: return "Corrupted or missing driver";
                case 43: return "Windows stopped device because it reported problems (Code 43)";
                case 44: return "Application or service shut down this device";
                case 45: return "Device is not currently connected to computer";
                case 47: return "Device prepared for safe removal";
                case 48: return "Blocked from starting because known to have problems";
                case 52: return "Driver signature verification failed";
                default: return "Problem Code " + code;
            }
        }

        static void DeviceGraphCmd(bool presentOnly, string classFilter, string search, bool problemsOnly, int limit) {
            try {
                if (limit <= 0) limit = 100;
                if (limit > 300) limit = 300;

                uint flags = (presentOnly ? DIGCF_PRESENT : 0) | DIGCF_ALLCLASSES;
                IntPtr devInfo = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, flags);
                if (devInfo == (IntPtr)(-1)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"SetupDiGetClassDevs failed: 0x{0:X}\"}}", Marshal.GetLastWin32Error()));
                    return;
                }

                try {
                    SP_DEVINFO_DATA data = new SP_DEVINFO_DATA();
                    data.cbSize = (uint)Marshal.SizeOf(data);
                    uint idx = 0;
                    int matchCount = 0;

                    string cf = (classFilter ?? "").Trim().ToLowerInvariant();
                    string s = (search ?? "").Trim().ToLowerInvariant();

                    var sb = new StringBuilder();
                    sb.Append("{\"success\": true, \"devices\": [");
                    bool first = true;

                    while (SetupDiEnumDeviceInfo(devInfo, idx, ref data)) {
                        StringBuilder idSb = new StringBuilder(512);
                        uint reqId;
                        SetupDiGetDeviceInstanceId(devInfo, ref data, idSb, (uint)idSb.Capacity, out reqId);
                        string instId = idSb.ToString();

                        string desc = GetDeviceProperty(devInfo, ref data, SPDRP_DEVICEDESC);
                        string friendly = GetDeviceProperty(devInfo, ref data, SPDRP_FRIENDLYNAME);
                        string cls = GetDeviceProperty(devInfo, ref data, SPDRP_CLASS);
                        string classGuid = GetDeviceProperty(devInfo, ref data, SPDRP_CLASSGUID);
                        string mfg = GetDeviceProperty(devInfo, ref data, SPDRP_MFG);
                        string driver = GetDeviceProperty(devInfo, ref data, SPDRP_DRIVER);
                        string hwId = GetDeviceProperty(devInfo, ref data, SPDRP_HARDWAREID);

                        uint status = 0;
                        uint problem = 0;
                        CM_Get_DevNode_Status(out status, out problem, data.DevInst, 0);

                        bool hasProblem = (status & 0x00000400) != 0 || problem != 0;
                        bool isStarted = (status & 0x00000008) != 0;
                        bool isDisableable = (status & 0x00002000) != 0;
                        bool isRemovable = (status & 0x00004000) != 0;

                        if (problemsOnly && !hasProblem) {
                            idx++;
                            continue;
                        }

                        if (!string.IsNullOrEmpty(cf) && cf != "all" && cf != "*") {
                            if (string.IsNullOrEmpty(cls) || cls.ToLowerInvariant().IndexOf(cf, StringComparison.OrdinalIgnoreCase) < 0) {
                                idx++;
                                continue;
                            }
                        }

                        if (!string.IsNullOrEmpty(s)) {
                            bool textMatch = (desc != null && desc.ToLowerInvariant().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                             (friendly != null && friendly.ToLowerInvariant().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                             (instId != null && instId.ToLowerInvariant().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                             (mfg != null && mfg.ToLowerInvariant().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                             (cls != null && cls.ToLowerInvariant().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                             (hwId != null && hwId.ToLowerInvariant().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
                            if (!textMatch) {
                                idx++;
                                continue;
                            }
                        }

                        string name = !string.IsNullOrEmpty(friendly) ? friendly : desc;
                        if (string.IsNullOrEmpty(name)) name = "Unknown Device";

                        string statusStr = (status & 0x00000400) != 0 ? (problem == 22 ? "disabled" : "problem") : (isStarted ? "started" : "stopped");

                        if (!first) sb.Append(",");
                        first = false;

                        sb.Append(string.Format("{{\"deviceInstanceId\": \"{0}\", \"name\": \"{1}\", \"description\": \"{2}\", \"class\": \"{3}\", \"classGuid\": \"{4}\", \"manufacturer\": \"{5}\", \"driver\": \"{6}\", \"hardwareId\": \"{7}\", \"status\": \"{8}\", \"problemCode\": {9}, \"problemDescription\": \"{10}\", \"isStarted\": {11}, \"hasProblem\": {12}, \"isDisableable\": {13}, \"isRemovable\": {14}}}",
                            EscapeJson(instId), EscapeJson(name), EscapeJson(desc), EscapeJson(cls), EscapeJson(classGuid), EscapeJson(mfg),
                            EscapeJson(driver), EscapeJson(hwId), statusStr, problem, EscapeJson(GetProblemDescription(problem)),
                            isStarted ? "true" : "false", hasProblem ? "true" : "false", isDisableable ? "true" : "false", isRemovable ? "true" : "false"));

                        matchCount++;
                        if (matchCount >= limit) break;

                        idx++;
                    }

                    sb.Append(string.Format("], \"count\": {0}, \"totalScanned\": {1}}}", matchCount, idx));
                    Console.WriteLine(sb.ToString());
                } finally {
                    SetupDiDestroyDeviceInfoList(devInfo);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"devices\": [], \"count\": 0, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void DeviceControlCmd(string action, string deviceInstanceId) {
            try {
                if (string.IsNullOrEmpty(deviceInstanceId)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"deviceInstanceId is required\"}");
                    return;
                }

                string act = (action ?? "reenumerate").Trim().ToLowerInvariant();

                IntPtr devInfo = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DIGCF_ALLCLASSES);
                if (devInfo == (IntPtr)(-1)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"SetupDiGetClassDevs failed: 0x{0:X}\"}}", Marshal.GetLastWin32Error()));
                    return;
                }

                try {
                    SP_DEVINFO_DATA data = new SP_DEVINFO_DATA();
                    data.cbSize = (uint)Marshal.SizeOf(data);
                    uint idx = 0;
                    bool found = false;

                    while (SetupDiEnumDeviceInfo(devInfo, idx, ref data)) {
                        StringBuilder idSb = new StringBuilder(512);
                        uint reqId;
                        SetupDiGetDeviceInstanceId(devInfo, ref data, idSb, (uint)idSb.Capacity, out reqId);
                        string instId = idSb.ToString();

                        if (string.Equals(instId, deviceInstanceId, StringComparison.OrdinalIgnoreCase) ||
                            instId.IndexOf(deviceInstanceId, StringComparison.OrdinalIgnoreCase) >= 0) {
                            found = true;

                            if (act == "reenumerate" || act == "rescan") {
                                int ret = CM_Reenumerate_DevNode(data.DevInst, 0);
                                Console.WriteLine(string.Format("{{\"success\": {0}, \"action\": \"reenumerate\", \"deviceInstanceId\": \"{1}\", \"returnCode\": {2}}}",
                                    ret == 0 ? "true" : "false", EscapeJson(instId), ret));
                                return;
                            }

                            SP_PROPCHANGE_PARAMS pcp = new SP_PROPCHANGE_PARAMS();
                            pcp.ClassInstallHeader.cbSize = (uint)Marshal.SizeOf(typeof(SP_CLASSINSTALL_HEADER));
                            pcp.ClassInstallHeader.InstallFunction = DIF_PROPERTYCHANGE;
                            pcp.Scope = DICS_FLAG_GLOBAL;
                            pcp.HwProfile = 0;

                            if (act == "enable") {
                                pcp.StateChange = DICS_ENABLE;
                                if (!SetupDiSetClassInstallParams(devInfo, ref data, ref pcp, (uint)Marshal.SizeOf(pcp)) ||
                                    !SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, devInfo, ref data)) {
                                    int err = Marshal.GetLastWin32Error();
                                    string errHint = (err == 5) ? "Access denied: elevated administrator privileges required" : ("Error 0x" + err.ToString("X"));
                                    Console.WriteLine(string.Format("{{\"success\": false, \"action\": \"enable\", \"deviceInstanceId\": \"{0}\", \"error\": \"{1}\", \"win32Error\": {2}}}",
                                        EscapeJson(instId), EscapeJson(errHint), err));
                                    return;
                                }
                                Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"enable\", \"deviceInstanceId\": \"{0}\", \"enabled\": true}}", EscapeJson(instId)));
                                return;
                            }

                            if (act == "disable") {
                                pcp.StateChange = DICS_DISABLE;
                                if (!SetupDiSetClassInstallParams(devInfo, ref data, ref pcp, (uint)Marshal.SizeOf(pcp)) ||
                                    !SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, devInfo, ref data)) {
                                    int err = Marshal.GetLastWin32Error();
                                    string errHint = (err == 5) ? "Access denied: elevated administrator privileges required" : ("Error 0x" + err.ToString("X"));
                                    Console.WriteLine(string.Format("{{\"success\": false, \"action\": \"disable\", \"deviceInstanceId\": \"{0}\", \"error\": \"{1}\", \"win32Error\": {2}}}",
                                        EscapeJson(instId), EscapeJson(errHint), err));
                                    return;
                                }
                                Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"disable\", \"deviceInstanceId\": \"{0}\", \"disabled\": true}}", EscapeJson(instId)));
                                return;
                            }

                            if (act == "restart") {
                                pcp.StateChange = DICS_DISABLE;
                                SetupDiSetClassInstallParams(devInfo, ref data, ref pcp, (uint)Marshal.SizeOf(pcp));
                                SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, devInfo, ref data);
                                Thread.Sleep(300);
                                pcp.StateChange = DICS_ENABLE;
                                if (!SetupDiSetClassInstallParams(devInfo, ref data, ref pcp, (uint)Marshal.SizeOf(pcp)) ||
                                    !SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, devInfo, ref data)) {
                                    int err = Marshal.GetLastWin32Error();
                                    string errHint = (err == 5) ? "Access denied: elevated administrator privileges required" : ("Error 0x" + err.ToString("X"));
                                    Console.WriteLine(string.Format("{{\"success\": false, \"action\": \"restart\", \"deviceInstanceId\": \"{0}\", \"error\": \"{1}\", \"win32Error\": {2}}}",
                                        EscapeJson(instId), EscapeJson(errHint), err));
                                    return;
                                }
                                Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"restart\", \"deviceInstanceId\": \"{0}\", \"restarted\": true}}", EscapeJson(instId)));
                                return;
                            }

                            Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Unknown device control action: {0}\"}}", EscapeJson(act)));
                            return;
                        }

                        idx++;
                    }

                    if (!found) {
                        Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Device instance ID not found: {0}\"}}", EscapeJson(deviceInstanceId)));
                    }
                } finally {
                    SetupDiDestroyDeviceInfoList(devInfo);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void NamedPipeCmd(string action, string pipeName, string message, int timeoutMs, string search, int limit) {
            try {
                if (string.IsNullOrEmpty(action)) action = "list";
                string act = action.Trim().ToLowerInvariant();

                if (act == "list") {
                    if (limit <= 0) limit = 50;
                    string[] allPipes = new string[0];
                    try {
                        allPipes = Directory.GetFiles(@"\\.\pipe\");
                    } catch {}

                    var sb = new StringBuilder();
                    sb.Append("{\"success\": true, \"pipes\": [");
                    bool first = true;
                    int count = 0;
                    string s = (search ?? "").Trim().ToLowerInvariant();

                    for (int i = 0; i < allPipes.Length; i++) {
                        string p = allPipes[i];
                        string shortName = p.StartsWith(@"\\.\pipe\") ? p.Substring(@"\\.\pipe\".Length) : p;

                        if (!string.IsNullOrEmpty(s)) {
                            if (shortName.ToLowerInvariant().IndexOf(s, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        }

                        if (!first) sb.Append(",");
                        first = false;

                        sb.Append(string.Format("{{\"name\": \"{0}\", \"path\": \"{1}\"}}", EscapeJson(shortName), EscapeJson(p)));
                        count++;
                        if (count >= limit) break;
                    }

                    sb.Append(string.Format("], \"count\": {0}, \"totalPipes\": {1}}}", count, allPipes.Length));
                    Console.WriteLine(sb.ToString());
                    return;
                }

                if (act == "send") {
                    if (string.IsNullOrEmpty(pipeName)) {
                        Console.WriteLine("{\"success\": false, \"error\": \"Pipe name is required for action 'send'\"}");
                        return;
                    }
                    string cleanPipe = pipeName.StartsWith(@"\\.\pipe\") ? pipeName.Substring(@"\\.\pipe\".Length) : pipeName;
                    int timeout = timeoutMs > 0 ? timeoutMs : 5000;

                    using (var client = new NamedPipeClientStream(".", cleanPipe, PipeDirection.InOut)) {
                        client.Connect(timeout);
                        using (var reader = new StreamReader(client, Encoding.UTF8))
                        using (var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true }) {
                            writer.WriteLine(message ?? "");
                            string reply = "";
                            try {
                                reply = reader.ReadLine() ?? "";
                            } catch {}

                            Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"send\", \"pipe\": \"{0}\", \"sent\": \"{1}\", \"response\": \"{2}\"}}",
                                EscapeJson(cleanPipe), EscapeJson(message ?? ""), EscapeJson(reply)));
                            return;
                        }
                    }
                }

                if (act == "listen") {
                    if (string.IsNullOrEmpty(pipeName)) {
                        Console.WriteLine("{\"success\": false, \"error\": \"Pipe name is required for action 'listen'\"}");
                        return;
                    }
                    string cleanPipe = pipeName.StartsWith(@"\\.\pipe\") ? pipeName.Substring(@"\\.\pipe\".Length) : pipeName;
                    int timeout = timeoutMs > 0 ? timeoutMs : 5000;

                    using (var server = new NamedPipeServerStream(cleanPipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous)) {
                        var ar = server.BeginWaitForConnection(null, null);
                        if (!ar.AsyncWaitHandle.WaitOne(timeout)) {
                            Console.WriteLine(string.Format("{{\"success\": false, \"action\": \"listen\", \"pipe\": \"{0}\", \"error\": \"Timed out waiting for client connection\"}}", EscapeJson(cleanPipe)));
                            return;
                        }
                        server.EndWaitForConnection(ar);

                        using (var reader = new StreamReader(server, Encoding.UTF8))
                        using (var writer = new StreamWriter(server, Encoding.UTF8) { AutoFlush = true }) {
                            string incoming = reader.ReadLine() ?? "";
                            string reply = !string.IsNullOrEmpty(message) ? message : "ACK";
                            try { writer.WriteLine(reply); } catch {}

                            Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"listen\", \"pipe\": \"{0}\", \"received\": \"{1}\", \"replied\": \"{2}\"}}",
                                EscapeJson(cleanPipe), EscapeJson(incoming), EscapeJson(reply)));
                            return;
                        }
                    }
                }

                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Unknown named pipe action '{0}'\"}}", EscapeJson(act)));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void SharedMemoryCmd(string action, string mapName, string data, int size) {
            try {
                if (string.IsNullOrEmpty(action)) action = "read";
                string act = action.Trim().ToLowerInvariant();

                string shmDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "shm");
                if (!Directory.Exists(shmDir)) Directory.CreateDirectory(shmDir);

                if (act == "list") {
                    string[] files = Directory.GetFiles(shmDir, "*.shm");
                    var sb = new StringBuilder();
                    sb.Append("{\"success\": true, \"maps\": [");
                    for (int i = 0; i < files.Length; i++) {
                        if (i > 0) sb.Append(",");
                        var fi = new FileInfo(files[i]);
                        string name = Path.GetFileNameWithoutExtension(fi.Name);
                        sb.Append(string.Format("{{\"name\": \"{0}\", \"sizeBytes\": {1}, \"lastModified\": \"{2}\"}}",
                            EscapeJson(name), fi.Length, fi.LastWriteTimeUtc.ToString("o")));
                    }
                    sb.Append(string.Format("], \"count\": {0}}}", files.Length));
                    Console.WriteLine(sb.ToString());
                    return;
                }

                if (string.IsNullOrEmpty(mapName)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"mapName is required\"}");
                    return;
                }

                string safeName = Path.GetFileName(mapName);
                string filePath = Path.Combine(shmDir, safeName + ".shm");

                if (act == "write") {
                    byte[] bytes = Encoding.UTF8.GetBytes(data ?? "");
                    int allocSize = size > 0 ? size : Math.Max(1024, bytes.Length + 64);
                    if (allocSize < bytes.Length + 4) allocSize = bytes.Length + 64;

                    using (var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)) {
                        fs.SetLength(Math.Max(fs.Length, (long)allocSize));
                        using (var mmf = MemoryMappedFile.CreateFromFile(fs, null, allocSize, MemoryMappedFileAccess.ReadWrite, null, HandleInheritability.None, false)) {
                            using (var accessor = mmf.CreateViewAccessor(0, allocSize, MemoryMappedFileAccess.Write)) {
                                accessor.Write(0, bytes.Length);
                                accessor.WriteArray(4, bytes, 0, bytes.Length);
                            }
                        }
                    }

                    Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"write\", \"mapName\": \"{0}\", \"bytesWritten\": {1}, \"capacityBytes\": {2}}}",
                        EscapeJson(safeName), bytes.Length, allocSize));
                    return;
                }

                if (act == "read") {
                    if (!File.Exists(filePath)) {
                        Console.WriteLine(string.Format("{{\"success\": false, \"mapName\": \"{0}\", \"error\": \"Shared memory map not found\"}}", EscapeJson(safeName)));
                        return;
                    }

                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                        using (var mmf = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.Read, null, HandleInheritability.None, false)) {
                            using (var accessor = mmf.CreateViewAccessor(0, fs.Length, MemoryMappedFileAccess.Read)) {
                                int len = accessor.ReadInt32(0);
                                if (len < 0 || len > (fs.Length - 4)) len = (int)(fs.Length - 4);
                                byte[] readBuf = new byte[len];
                                accessor.ReadArray(4, readBuf, 0, len);
                                string text = Encoding.UTF8.GetString(readBuf);

                                Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"read\", \"mapName\": \"{0}\", \"bytesRead\": {1}, \"data\": \"{2}\"}}",
                                    EscapeJson(safeName), len, EscapeJson(text)));
                                return;
                            }
                        }
                    }
                }

                if (act == "info") {
                    bool exists = File.Exists(filePath);
                    long fileLen = exists ? new FileInfo(filePath).Length : 0;
                    string lastMod = exists ? new FileInfo(filePath).LastWriteTimeUtc.ToString("o") : "";
                    Console.WriteLine(string.Format("{{\"success\": true, \"mapName\": \"{0}\", \"exists\": {1}, \"sizeBytes\": {2}, \"lastModified\": \"{3}\"}}",
                        EscapeJson(safeName), exists ? "true" : "false", fileLen, lastMod));
                    return;
                }

                if (act == "delete") {
                    if (File.Exists(filePath)) {
                        File.Delete(filePath);
                        Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"delete\", \"mapName\": \"{0}\", \"deleted\": true}}", EscapeJson(safeName)));
                    } else {
                        Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"delete\", \"mapName\": \"{0}\", \"deleted\": false, \"message\": \"Map did not exist\"}}", EscapeJson(safeName)));
                    }
                    return;
                }

                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Unknown shared memory action '{0}'\"}}", EscapeJson(act)));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void GetThermalVitalsCmd() {
            try {
                int count = Environment.ProcessorCount;
                int structSize = Marshal.SizeOf(typeof(PROCESSOR_POWER_INFORMATION));
                uint bufferSize = (uint)(count * structSize);
                IntPtr pOut = Marshal.AllocHGlobal((int)bufferSize);

                var cores = new List<string>();
                double totalCurrentMhz = 0;
                uint maxDetectedMhz = 0;
                bool isCpuThrottled = false;

                try {
                    int status = CallNtPowerInformation(11 /* ProcessorPowerInformation */, IntPtr.Zero, 0, pOut, bufferSize);
                    if (status == 0) {
                        for (int i = 0; i < count; i++) {
                            IntPtr pItem = (IntPtr)((long)pOut + (i * structSize));
                            var info = (PROCESSOR_POWER_INFORMATION)Marshal.PtrToStructure(pItem, typeof(PROCESSOR_POWER_INFORMATION));
                            totalCurrentMhz += info.CurrentMhz;
                            if (info.MaxMhz > maxDetectedMhz) maxDetectedMhz = info.MaxMhz;
                            bool coreThrottled = (info.MhzLimit < info.MaxMhz) || (info.MaxMhz > 0 && info.CurrentMhz < (info.MaxMhz * 0.85));
                            if (coreThrottled) isCpuThrottled = true;

                            cores.Add(string.Format("{{\"core\": {0}, \"currentMhz\": {1}, \"maxMhz\": {2}, \"mhzLimit\": {3}, \"maxIdleState\": {4}, \"currentIdleState\": {5}, \"isThrottled\": {6}}}",
                                info.Number, info.CurrentMhz, info.MaxMhz, info.MhzLimit, info.MaxIdleState, info.CurrentIdleState, coreThrottled ? "true" : "false"));
                        }
                    }
                } finally {
                    Marshal.FreeHGlobal(pOut);
                }

                double avgMhz = count > 0 ? (totalCurrentMhz / count) : 0;

                if (cores.Count == 0 || maxDetectedMhz == 0) {
                    uint regMhz = 2400;
                    try {
                        object mhzObj = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz", 2400);
                        if (mhzObj != null) regMhz = Convert.ToUInt32(mhzObj);
                    } catch {}
                    if (maxDetectedMhz == 0) maxDetectedMhz = regMhz;
                    if (avgMhz <= 0) avgMhz = regMhz;
                    if (cores.Count == 0) {
                        for (int i = 0; i < count; i++) {
                            cores.Add(string.Format("{{\"core\": {0}, \"currentMhz\": {1}, \"maxMhz\": {2}, \"mhzLimit\": {3}, \"maxIdleState\": 0, \"currentIdleState\": 0, \"isThrottled\": false}}",
                                i, regMhz, regMhz, regMhz));
                        }
                    }
                }

                // Query WMI ThermalZoneInformation
                var zones = new List<string>();
                double maxTempCelsius = 0.0;
                bool hasTemp = false;
                bool isThermalThrottled = false;

                try {
                    using (var searcher = new ManagementObjectSearcher(@"root\cimv2", "SELECT Name, HighPrecisionTemperature, PercentPassiveLimit, ThrottleReasons FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation")) {
                        foreach (ManagementObject mo in searcher.Get()) {
                            string name = mo["Name"] != null ? mo["Name"].ToString() : "Zone";
                            ulong hpTemp = mo["HighPrecisionTemperature"] != null ? Convert.ToUInt64(mo["HighPrecisionTemperature"]) : 0;
                            uint limit = mo["PercentPassiveLimit"] != null ? Convert.ToUInt32(mo["PercentPassiveLimit"]) : 100;
                            uint reasons = mo["ThrottleReasons"] != null ? Convert.ToUInt32(mo["ThrottleReasons"]) : 0;

                            double c = (hpTemp > 2732) ? ((hpTemp - 2732.0) / 10.0) : 0.0;
                            if (c > 0) {
                                hasTemp = true;
                                if (c > maxTempCelsius) maxTempCelsius = c;
                            }
                            if (limit < 100 || reasons > 0) isThermalThrottled = true;

                            zones.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                "{{\"name\": \"{0}\", \"tempCelsius\": {1:F2}, \"percentPassiveLimit\": {2}, \"throttleReasons\": {3}}}",
                                EscapeJson(name), c, limit, reasons));
                        }
                    }
                } catch {}

                bool overallThrottled = isCpuThrottled || isThermalThrottled;
                string maxTempStr = hasTemp ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2}", maxTempCelsius) : "null";

                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"success\": true, \"logicalCores\": {0}, \"averageMhz\": {1:F1}, \"maxMhz\": {2}, \"maxTempCelsius\": {3}, \"isThermalThrottled\": {4}, \"cores\": [{5}], \"thermalZones\": [{6}]}}",
                    count, avgMhz, maxDetectedMhz, maxTempStr, overallThrottled ? "true" : "false",
                    string.Join(", ", cores.ToArray()), string.Join(", ", zones.ToArray())));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static List<string> EnumerateVirtualDesktopIds() {
            var list = new List<string>();
            try {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops\Desktops")) {
                    if (key != null) {
                        foreach (string subName in key.GetSubKeyNames()) {
                            if (!string.IsNullOrEmpty(subName) && subName.StartsWith("{") && subName.EndsWith("}")) {
                                list.Add(subName.ToUpperInvariant());
                            }
                        }
                    }
                }
            } catch {}

            if (list.Count == 0) {
                try {
                    object raw = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops", "VirtualDesktopIDs", null);
                    byte[] bytes = raw as byte[];
                    if (bytes != null && bytes.Length >= 16) {
                        for (int i = 0; i <= bytes.Length - 16; i += 16) {
                            byte[] guidBytes = new byte[16];
                            Array.Copy(bytes, i, guidBytes, 0, 16);
                            var guid = new Guid(guidBytes);
                            list.Add("{" + guid.ToString().ToUpperInvariant() + "}");
                        }
                    }
                } catch {}
            }

            return list;
        }

        static void VirtualDesktopsListCmd() {
            try {
                var mgr = (IVirtualDesktopManager)new VirtualDesktopManagerComObject();
                var ids = EnumerateVirtualDesktopIds();

                string currentIdStr = "";
                IntPtr hFore = GetForegroundWindow();
                if (hFore != IntPtr.Zero) {
                    Guid foreId;
                    int hr = mgr.GetWindowDesktopId(hFore, out foreId);
                    if (hr == 0 && foreId != Guid.Empty) {
                        currentIdStr = "{" + foreId.ToString().ToUpperInvariant() + "}";
                    }
                }

                if (string.IsNullOrEmpty(currentIdStr)) {
                    IntPtr hDesk = EnsureInteractiveDesktop();
                    EnumDesktopWindows(hDesk, (hWnd, lParam) => {
                        if (IsWindowVisible(hWnd)) {
                            Guid wId;
                            if (mgr.GetWindowDesktopId(hWnd, out wId) == 0 && wId != Guid.Empty) {
                                currentIdStr = "{" + wId.ToString().ToUpperInvariant() + "}";
                                return false;
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                }

                if (!string.IsNullOrEmpty(currentIdStr) && !ids.Contains(currentIdStr)) {
                    ids.Insert(0, currentIdStr);
                }

                if (ids.Count == 0) {
                    ids.Add("{00000000-0000-0000-0000-000000000000}");
                }

                var desktopList = new List<string>();
                for (int i = 0; i < ids.Count; i++) {
                    string id = ids[i];
                    bool isCurrent = (!string.IsNullOrEmpty(currentIdStr) && string.Equals(id, currentIdStr, StringComparison.OrdinalIgnoreCase)) || (string.IsNullOrEmpty(currentIdStr) && i == 0);
                    
                    string customName = "";
                    try {
                        object val = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops\Desktops\" + id, "Name", null);
                        if (val != null) customName = val.ToString();
                    } catch {}
                    if (string.IsNullOrEmpty(customName)) {
                        customName = string.Format("Desktop {0}", i + 1);
                    }

                    desktopList.Add(string.Format("{{\"id\": \"{0}\", \"index\": {1}, \"name\": \"{2}\", \"isCurrent\": {3}}}",
                        id, i, EscapeJson(customName), isCurrent ? "true" : "false"));
                }

                Console.WriteLine(string.Format("{{\"success\": true, \"currentDesktopId\": \"{0}\", \"count\": {1}, \"desktops\": [{2}]}}",
                    currentIdStr, ids.Count, string.Join(", ", desktopList.ToArray())));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void VirtualDesktopWindowCmd(string query) {
            try {
                IntPtr hDesk = EnsureInteractiveDesktop();
                IntPtr targetHwnd;
                string actualTitle;
                if (!FindWindow(hDesk, query, out targetHwnd, out actualTitle)) {
                    targetHwnd = GetShellWindow();
                    if (targetHwnd == IntPtr.Zero) targetHwnd = GetDesktopWindow();
                    if (targetHwnd != IntPtr.Zero) {
                        actualTitle = "Desktop";
                    } else {
                        Console.WriteLine("{\"success\": false, \"isHeadless\": true, \"error\": \"Window not found\"}");
                        return;
                    }
                }

                var mgr = (IVirtualDesktopManager)new VirtualDesktopManagerComObject();
                bool onCurrent = true;
                try { mgr.IsWindowOnCurrentVirtualDesktop(targetHwnd, out onCurrent); } catch { onCurrent = true; }
                Guid desktopId = Guid.Empty;
                try { mgr.GetWindowDesktopId(targetHwnd, out desktopId); } catch {}

                string idStr = desktopId != Guid.Empty ? ("{" + desktopId.ToString().ToUpperInvariant() + "}") : "";
                Console.WriteLine(string.Format("{{\"success\": true, \"hwnd\": \"0x{0:X}\", \"title\": \"{1}\", \"desktopId\": \"{2}\", \"isOnCurrentDesktop\": {3}}}",
                    targetHwnd.ToInt64(), EscapeJson(actualTitle), idStr, onCurrent ? "true" : "false"));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void VirtualDesktopMoveCmd(string query, string targetDesktop) {
            try {
                IntPtr hDesk = EnsureInteractiveDesktop();
                IntPtr targetHwnd;
                string actualTitle;
                if (!FindWindow(hDesk, query, out targetHwnd, out actualTitle)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Window not found\"}");
                    return;
                }

                Guid targetGuid = Guid.Empty;
                int desktopIdx;
                if (int.TryParse(targetDesktop, out desktopIdx)) {
                    var ids = EnumerateVirtualDesktopIds();
                    if (desktopIdx >= 0 && desktopIdx < ids.Count) {
                        targetGuid = new Guid(ids[desktopIdx]);
                    } else {
                        Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Desktop index out of range: {0} (count: {1})\"}}", desktopIdx, ids.Count));
                        return;
                    }
                } else {
                    try {
                        targetGuid = new Guid(targetDesktop);
                    } catch {
                        Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Invalid GUID or desktop index: {0}\"}}", EscapeJson(targetDesktop)));
                        return;
                    }
                }

                var mgr = (IVirtualDesktopManager)new VirtualDesktopManagerComObject();
                int hr = mgr.MoveWindowToDesktop(targetHwnd, targetGuid);
                if (hr != 0) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"MoveWindowToDesktop failed (0x{0:X})\"}}", hr));
                    return;
                }

                Console.WriteLine(string.Format("{{\"success\": true, \"hwnd\": \"0x{0:X}\", \"title\": \"{1}\", \"targetDesktopId\": \"{{{2}}}\"}}",
                    targetHwnd.ToInt64(), EscapeJson(actualTitle), targetGuid.ToString().ToUpperInvariant()));
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
            } else if (cmd == "audio_devices" || cmd == "audiodevices" || cmd == "audio_endpoints") {
                AudioDevicesCmd();
            } else if (cmd == "audio_listen" || cmd == "audiolisten" || cmd == "listen") {
                int durationMs = args.Length >= 2 ? int.Parse(args[1]) : 300;
                AudioListenCmd(durationMs);
            } else if (cmd == "audio_record" || cmd == "audiorecord" || cmd == "record_wav") {
                string outPath = args.Length >= 2 ? args[1] : "recording.wav";
                int durSec = args.Length >= 3 ? int.Parse(args[2]) : 3;
                AudioRecordCmd(outPath, durSec);
            } else if (cmd == "audio_mic_listen" || cmd == "audiomiclisten" || cmd == "mic_listen") {
                int durationMs = args.Length >= 2 ? int.Parse(args[1]) : 300;
                AudioMicListenCmd(durationMs);
            } else if (cmd == "audio_mic_record" || cmd == "audiomicrecord" || cmd == "mic_record" || cmd == "record_mic") {
                string outPath = args.Length >= 2 ? args[1] : "mic_recording.wav";
                int durSec = args.Length >= 3 ? int.Parse(args[2]) : 3;
                AudioMicRecordCmd(outPath, durSec);
            } else if (cmd == "audio_sessions" || cmd == "audiosessions" || cmd == "sessions" || cmd == "volume_mixer") {
                AudioSessionsCmd();
            } else if (cmd == "audio_session_set" || cmd == "audiosessionset" || cmd == "set_session_volume") {
                string target = args.Length >= 2 ? args[1] : "";
                float vol = -1f;
                if (args.Length >= 3 && !string.IsNullOrEmpty(args[2])) float.TryParse(args[2], out vol);
                string muteStr = args.Length >= 4 ? args[3] : null;
                AudioSessionSetCmd(target, vol, muteStr);
            } else if (cmd == "audio_play" || cmd == "audioplay" || cmd == "play_sound" || cmd == "play_wav") {
                string filePath = args.Length >= 2 ? args[1] : "stop";
                AudioPlayCmd(filePath);
            } else if (cmd == "audio_beep" || cmd == "audiobeep" || cmd == "beep") {
                int freq = args.Length >= 2 ? int.Parse(args[1]) : 880;
                int dur = args.Length >= 3 ? int.Parse(args[2]) : 200;
                AudioBeepCmd(freq, dur);
            } else if (cmd == "audio_inspect" || cmd == "audioinspect" || cmd == "inspect_audio" || cmd == "inspect_wav") {
                string filePath = args.Length >= 2 ? args[1] : "";
                AudioInspectCmd(filePath);
            } else if (cmd == "audio_sequence" || cmd == "audiosequence" || cmd == "melody" || cmd == "play_sequence") {
                string seq = args.Length >= 2 ? args[1] : "success";
                AudioSequenceCmd(seq);
            } else if (cmd == "audio_tts_wav" || cmd == "audiottswav" || cmd == "tts_wav" || cmd == "speech_to_wav") {
                string text = args.Length >= 2 ? args[1] : "";
                string outPath = args.Length >= 3 ? args[2] : "speech_output.wav";
                string voice = args.Length >= 4 ? args[3] : "";
                int rate = args.Length >= 5 ? int.Parse(args[4]) : 0;
                int vol = args.Length >= 6 ? int.Parse(args[5]) : 100;
                AudioTtsWavCmd(text, outPath, voice, rate, vol);
            } else if (cmd == "audio_duck" || cmd == "audioduck" || cmd == "duck_audio") {
                string target = args.Length >= 2 ? args[1] : "all";
                float duckPercent = args.Length >= 3 ? float.Parse(args[2]) : 20f;
                int durMs = args.Length >= 4 ? int.Parse(args[3]) : 2500;
                float restorePercent = args.Length >= 5 ? float.Parse(args[4]) : -1f;
                AudioDuckCmd(target, duckPercent, durMs, restorePercent);
            } else if (cmd == "service" || cmd == "service_control" || cmd == "services" || cmd == "scm") {
                string act = args.Length >= 2 ? args[1] : "list";
                string name = args.Length >= 3 ? args[2] : "";
                string filter = args.Length >= 4 ? args[3] : "";
                string sf = args.Length >= 5 ? args[4] : "all";
                int timeout = args.Length >= 6 ? int.Parse(args[5]) : 5000;
                ServiceControlCmd(act, name, filter, sf, timeout);
            } else if (cmd == "eventlog" || cmd == "event_log" || cmd == "events" || cmd == "winevent") {
                string ch = args.Length >= 2 ? args[1] : "System";
                string preset = args.Length >= 3 ? args[2] : "";
                string sev = args.Length >= 4 ? args[3] : "";
                int hrs = args.Length >= 5 ? int.Parse(args[4]) : 24;
                int lim = args.Length >= 6 ? int.Parse(args[5]) : 20;
                string search = args.Length >= 7 ? args[6] : "";
                EventLogQueryCmd(ch, preset, sev, hrs, lim, search);
            } else if (cmd == "registry" || cmd == "reg" || cmd == "winreg") {
                string act = args.Length >= 2 ? args[1] : "get";
                string path = args.Length >= 3 ? args[2] : "";
                string name = args.Length >= 4 ? args[3] : "";
                string val = args.Length >= 5 ? args[4] : "";
                string kind = args.Length >= 6 ? args[5] : "string";
                RegistryCmd(act, path, name, val, kind);
            } else if (cmd == "device_graph" || cmd == "devices" || cmd == "pnp_devices" || cmd == "devicegraph") {
                bool presentOnly = args.Length >= 2 ? (args[1].Equals("all", StringComparison.OrdinalIgnoreCase) || args[1].Equals("false", StringComparison.OrdinalIgnoreCase) ? false : true) : true;
                string classFilter = args.Length >= 3 ? args[2] : "";
                string search = args.Length >= 4 ? args[3] : "";
                bool problemsOnly = args.Length >= 5 ? (args[4].Equals("problems", StringComparison.OrdinalIgnoreCase) || args[4].Equals("true", StringComparison.OrdinalIgnoreCase)) : false;
                int limit = args.Length >= 6 ? int.Parse(args[5]) : 100;
                DeviceGraphCmd(presentOnly, classFilter, search, problemsOnly, limit);
            } else if (cmd == "device_control" || cmd == "dev_control" || cmd == "device_action") {
                string act = args.Length >= 2 ? args[1] : "reenumerate";
                string devId = args.Length >= 3 ? args[2] : "";
                DeviceControlCmd(act, devId);
            } else if (cmd == "named_pipe" || cmd == "pipe" || cmd == "pipes") {
                string act = args.Length >= 2 ? args[1] : "list";
                string pipe = args.Length >= 3 ? args[2] : "";
                string msg = args.Length >= 4 ? args[3] : "";
                int timeout = args.Length >= 5 ? int.Parse(args[4]) : 5000;
                string search = args.Length >= 6 ? args[5] : "";
                int limit = args.Length >= 7 ? int.Parse(args[6]) : 50;
                NamedPipeCmd(act, pipe, msg, timeout, search, limit);
            } else if (cmd == "shared_memory" || cmd == "shm" || cmd == "mmf") {
                string act = args.Length >= 2 ? args[1] : "read";
                string map = args.Length >= 3 ? args[2] : "";
                string data = args.Length >= 4 ? args[3] : "";
                int size = args.Length >= 5 ? int.Parse(args[4]) : 0;
                SharedMemoryCmd(act, map, data, size);
            } else if (cmd == "firewall_status" || cmd == "fw_status") {
                GetFirewallStatusCmd();
            } else if (cmd == "firewall_rules" || cmd == "fw_rules") {
                string dir = args.Length >= 2 ? args[1] : "all";
                string act = args.Length >= 3 ? args[2] : "all";
                string pro = args.Length >= 4 ? args[3] : "any";
                int port = args.Length >= 5 ? int.Parse(args[4]) : 0;
                string search = args.Length >= 6 ? args[5] : "";
                int limit = args.Length >= 7 ? int.Parse(args[6]) : 50;
                GetFirewallRulesCmd(dir, act, pro, port, search, limit);
            } else if (cmd == "firewall_rule_set" || cmd == "fw_rule_set" || cmd == "manage_firewall_rule") {
                string act = args.Length >= 2 ? args[1] : "add";
                string name = args.Length >= 3 ? args[2] : "";
                string desc = args.Length >= 4 ? args[3] : "";
                string dir = args.Length >= 5 ? args[4] : "inbound";
                string pro = args.Length >= 6 ? args[5] : "tcp";
                string ports = args.Length >= 7 ? args[6] : "";
                string app = args.Length >= 8 ? args[7] : "";
                string rAct = args.Length >= 9 ? args[8] : "allow";
                string prof = args.Length >= 10 ? args[9] : "all";
                ManageFirewallRuleCmd(act, name, desc, dir, pro, ports, app, rAct, prof);
            } else if (cmd == "task_scheduler_list" || cmd == "ts_list" || cmd == "task_list") {
                string folder = args.Length >= 2 ? args[1] : "\\";
                bool rec = args.Length >= 3 && (args[2].ToLowerInvariant() == "true" || args[2] == "1");
                string state = args.Length >= 4 ? args[3] : "all";
                string search = args.Length >= 5 ? args[4] : "";
                int limit = args.Length >= 6 ? int.Parse(args[5]) : 50;
                TaskSchedulerListCmd(folder, rec, state, search, limit);
            } else if (cmd == "task_scheduler_info" || cmd == "ts_info" || cmd == "task_info") {
                string taskPath = args.Length >= 2 ? args[1] : "";
                TaskSchedulerInfoCmd(taskPath);
            } else if (cmd == "task_scheduler_action" || cmd == "ts_action" || cmd == "task_action") {
                string act = args.Length >= 2 ? args[1] : "run";
                string taskPath = args.Length >= 3 ? args[2] : "";
                TaskSchedulerActionCmd(act, taskPath);
            } else if (cmd == "cert_store_list" || cmd == "certificate_store" || cmd == "certs_list") {
                string store = args.Length >= 2 ? args[1] : "My";
                string loc = args.Length >= 3 ? args[2] : "LocalMachine";
                string search = args.Length >= 4 ? args[3] : "";
                int expDays = args.Length >= 5 ? int.Parse(args[4]) : 0;
                bool hasKey = args.Length >= 6 && (args[5].ToLowerInvariant() == "true" || args[5] == "1");
                int limit = args.Length >= 7 ? int.Parse(args[6]) : 50;
                CertificateStoreListCmd(store, loc, search, expDays, hasKey, limit);
            } else if (cmd == "cert_info" || cmd == "certificate_info") {
                string thumb = args.Length >= 2 ? args[1] : "";
                string store = args.Length >= 3 ? args[2] : "";
                string loc = args.Length >= 4 ? args[3] : "";
                CertificateInfoCmd(thumb, store, loc);
            } else if (cmd == "cert_export" || cmd == "certificate_export") {
                string thumb = args.Length >= 2 ? args[1] : "";
                string fmt = args.Length >= 3 ? args[2] : "pem";
                string store = args.Length >= 4 ? args[3] : "";
                string loc = args.Length >= 5 ? args[4] : "";
                CertificateExportCmd(thumb, fmt, store, loc);
            } else if (cmd == "rm_find_locks" || cmd == "restart_manager_find_locks" || cmd == "find_locks") {
                string files = args.Length >= 2 ? args[1] : "";
                RestartManagerFindLocksCmd(files);
            } else if (cmd == "rm_session" || cmd == "restart_manager_session") {
                string files = args.Length >= 2 ? args[1] : "";
                bool force = args.Length >= 3 && (args[2].ToLowerInvariant() == "true" || args[2] == "1");
                RestartManagerSessionCmd(files, force);
            } else if (cmd == "rm_shutdown" || cmd == "restart_manager_shutdown") {
                string files = args.Length >= 2 ? args[1] : "";
                bool force = args.Length >= 3 && (args[2].ToLowerInvariant() == "true" || args[2] == "1");
                RestartManagerShutdownCmd(files, force);
            } else if (cmd == "rm_restart" || cmd == "restart_manager_restart") {
                string key = args.Length >= 2 ? args[1] : "";
                RestartManagerRestartCmd(key);
            } else if (cmd == "wmi_query" || cmd == "wmi" || cmd == "wql") {
                string q = args.Length >= 2 ? args[1] : "";
                string ns = args.Length >= 3 ? args[2] : "root\\cimv2";
                int limit = args.Length >= 4 ? int.Parse(args[3]) : 100;
                WmiQueryCmd(q, ns, limit);
            } else if (cmd == "wmi_hardware" || cmd == "wmi_hardware_spec" || cmd == "hardware_spec") {
                WmiHardwareSpecCmd();
            } else if (cmd == "wmi_os_health" || cmd == "os_health" || cmd == "os_telemetry") {
                WmiOsHealthCmd();
            } else if (cmd == "thermal_vitals" || cmd == "thermals" || cmd == "thermal" || cmd == "cpu_thermals") {
                GetThermalVitalsCmd();
            } else if (cmd == "vdesktops" || cmd == "virtual_desktops" || cmd == "list_desktops") {
                VirtualDesktopsListCmd();
            } else if (cmd == "vdesktop_window" || cmd == "desktop_window" || cmd == "window_desktop") {
                string query = args.Length >= 2 ? args[1] : "active";
                VirtualDesktopWindowCmd(query);
            } else if (cmd == "vdesktop_move" || cmd == "desktop_move" || cmd == "move_to_desktop") {
                string query = args.Length >= 2 ? args[1] : "active";
                string target = args.Length >= 3 ? args[2] : "0";
                VirtualDesktopMoveCmd(query, target);
            } else {
                Console.WriteLine("{\"error\": \"Invalid arguments\"}");
            }
            Environment.Exit(0);
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

            if (string.IsNullOrEmpty(query) || query.Equals("active", StringComparison.OrdinalIgnoreCase) || query.Equals("foreground", StringComparison.OrdinalIgnoreCase)) {
                IntPtr hFore = GetForegroundWindow();
                if (hFore != IntPtr.Zero) {
                    targetHwnd = hFore;
                    var sb = new StringBuilder(256);
                    GetWindowText(hFore, sb, sb.Capacity);
                    actualTitle = sb.ToString();
                    return true;
                }
            }

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

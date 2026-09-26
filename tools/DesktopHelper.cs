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

        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        public static extern int DwmGetWindowAttributeUint(IntPtr hwnd, uint dwAttribute, out uint pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

        [DllImport("dwmapi.dll")]
        public static extern int DwmIsCompositionEnabled(out bool pfEnabled);

        [DllImport("dwmapi.dll")]
        public static extern int DwmFlush();

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
        public static extern int DwmSetWindowAttributeColor(IntPtr hwnd, uint dwAttribute, ref uint pvAttribute, int cbAttribute);

        const uint DWMWA_NCRENDERING_ENABLED = 1;
        const uint DWMWA_NCRENDERING_POLICY = 2;
        const uint DWMWA_TRANSITIONS_FORCEDISABLED = 3;
        const uint DWMWA_ALLOW_NCPAINT = 4;
        const uint DWMWA_CAPTION_BUTTON_BOUNDS = 5;
        const uint DWMWA_NONCLIENT_RTL_LAYOUT = 6;
        const uint DWMWA_FORCE_ICONIC_REPRESENTATION = 7;
        const uint DWMWA_FLIP3D_POLICY = 8;
        const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        const uint DWMWA_HAS_ICONIC_BITMAP = 10;
        const uint DWMWA_DISALLOW_PEEK = 11;
        const uint DWMWA_EXCLUDED_FROM_PEEK = 12;
        const uint DWMWA_CLOAK = 13;
        const uint DWMWA_CLOAKED = 14;
        const uint DWMWA_FREEZE_REPRESENTATION = 15;
        const uint DWMWA_PASSIVE_UPDATE_MODE = 16;
        const uint DWMWA_USE_HOSTBACKDROPBRUSH = 17;
        const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const uint DWMWA_BORDER_COLOR = 34;
        const uint DWMWA_CAPTION_COLOR = 35;
        const uint DWMWA_TEXT_COLOR = 36;
        const uint DWMWA_VISIBLE_FRAME_BORDER_THICKNESS = 37;
        const uint DWMWA_SYSTEMBACKDROP_TYPE = 38;

        const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;
        const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

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
        public struct SYSTEM_INFO {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        [DllImport("kernel32.dll")]
        public static extern void GetNativeSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll")]
        public static extern uint EnumSystemFirmwareTables(uint FirmwareTableProviderSignature, IntPtr pFirmwareTableEnumBuffer, uint BufferSize);

        [DllImport("kernel32.dll")]
        public static extern uint GetSystemFirmwareTable(uint FirmwareTableProviderSignature, uint FirmwareTableID, IntPtr pFirmwareTableBuffer, uint BufferSize);

        [DllImport("kernel32.dll")]
        public static extern void GetSystemTimePreciseAsFileTime(out long lpSystemTimeAsFileTime);

        [DllImport("kernel32.dll")]
        public static extern bool GetProductInfo(uint dwOSMajorVersion, uint dwOSMinorVersion, uint dwSpMajorVersion, uint dwSpMinorVersion, out uint pdwReturnedProductType);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern uint GetSystemDirectory([Out] StringBuilder lpBuffer, uint uSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern uint GetWindowsDirectory([Out] StringBuilder lpBuffer, uint uSize);

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

        static void DwmStatusCmd() {
            try {
                bool isComp = false;
                int hrComp = DwmIsCompositionEnabled(out isComp);

                uint colorization = 0;
                bool opaqueBlend = false;
                int hrColor = DwmGetColorizationColor(out colorization, out opaqueBlend);

                uint alpha = (colorization >> 24) & 0xFF;
                uint red = (colorization >> 16) & 0xFF;
                uint green = (colorization >> 8) & 0xFF;
                uint blue = colorization & 0xFF;
                string hexColor = string.Format("#{0:X2}{1:X2}{2:X2}", red, green, blue);

                var sw = Stopwatch.StartNew();
                int hrFlush = DwmFlush();
                sw.Stop();
                double latencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"isCompositionEnabled\": {0}, \"colorizationColor\": {{\"raw\": \"0x{1:X8}\", \"hex\": \"{2}\", \"alpha\": {3}, \"red\": {4}, \"green\": {5}, \"blue\": {6}, \"opaqueBlend\": {7}}}, \"flush\": {{\"hr\": {8}, \"latencyMs\": {9}}}}}",
                    isComp ? "true" : "false",
                    colorization,
                    hexColor,
                    alpha, red, green, blue,
                    opaqueBlend ? "true" : "false",
                    hrFlush,
                    latencyMs
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static string FormatDwmColor(uint col) {
            if (col == DWMWA_COLOR_DEFAULT) return "\"default\"";
            if (col == DWMWA_COLOR_NONE) return "\"none\"";
            uint r = col & 0xFF;
            uint g = (col >> 8) & 0xFF;
            uint b = (col >> 16) & 0xFF;
            return string.Format("\"#{0:X2}{1:X2}{2:X2}\"", r, g, b);
        }

        static bool ResolveHwnd(string query, out IntPtr targetHwnd, out string actualTitle) {
            targetHwnd = IntPtr.Zero;
            actualTitle = "";
            IntPtr hDesk = EnsureInteractiveDesktop();

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

            if (query != null && query.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                try {
                    long h = Convert.ToInt64(query, 16);
                    targetHwnd = new IntPtr(h);
                    var sb = new StringBuilder(256);
                    GetWindowText(targetHwnd, sb, sb.Capacity);
                    actualTitle = sb.ToString();
                    return true;
                } catch {}
            }

            return FindWindow(hDesk, query, out targetHwnd, out actualTitle);
        }

        static void DwmWindowAttributesCmd(string query) {
            try {
                IntPtr targetHwnd;
                string actualTitle;
                if (!ResolveHwnd(query, out targetHwnd, out actualTitle)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Window not found matching query\"}");
                    return;
                }

                uint pid = 0;
                GetWindowThreadProcessId(targetHwnd, out pid);
                string procName = "";
                try { procName = Process.GetProcessById((int)pid).ProcessName; } catch {}

                RECT winRect;
                GetWindowRect(targetHwnd, out winRect);

                RECT frameRect = winRect;
                RECT extRect;
                if (DwmGetWindowAttribute(targetHwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out extRect, Marshal.SizeOf(typeof(RECT))) == 0) {
                    frameRect = extRect;
                }

                int cloaked = 0;
                try { DwmGetWindowAttribute(targetHwnd, DWMWA_CLOAKED, out cloaked, sizeof(int)); } catch {}

                var cloakedReasons = new List<string>();
                if ((cloaked & 1) != 0) cloakedReasons.Add("\"app\"");
                if ((cloaked & 2) != 0) cloakedReasons.Add("\"shell\"");
                if ((cloaked & 4) != 0) cloakedReasons.Add("\"inherited\"");

                int ncRendering = 0;
                try { DwmGetWindowAttribute(targetHwnd, DWMWA_NCRENDERING_ENABLED, out ncRendering, sizeof(int)); } catch {}

                int darkMode = 0;
                try { DwmGetWindowAttribute(targetHwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, out darkMode, sizeof(int)); } catch {}

                int cornerPref = 0;
                try { DwmGetWindowAttribute(targetHwnd, DWMWA_WINDOW_CORNER_PREFERENCE, out cornerPref, sizeof(int)); } catch {}
                string cornerName = "default";
                if (cornerPref == 1) cornerName = "do_not_round";
                else if (cornerPref == 2) cornerName = "round";
                else if (cornerPref == 3) cornerName = "round_small";

                uint borderColor = DWMWA_COLOR_DEFAULT;
                try { DwmGetWindowAttributeUint(targetHwnd, DWMWA_BORDER_COLOR, out borderColor, sizeof(uint)); } catch {}

                uint captionColor = DWMWA_COLOR_DEFAULT;
                try { DwmGetWindowAttributeUint(targetHwnd, DWMWA_CAPTION_COLOR, out captionColor, sizeof(uint)); } catch {}

                uint textColor = DWMWA_COLOR_DEFAULT;
                try { DwmGetWindowAttributeUint(targetHwnd, DWMWA_TEXT_COLOR, out textColor, sizeof(uint)); } catch {}

                uint borderThickness = 0;
                try { DwmGetWindowAttributeUint(targetHwnd, DWMWA_VISIBLE_FRAME_BORDER_THICKNESS, out borderThickness, sizeof(uint)); } catch {}

                int backdropType = 0;
                try { DwmGetWindowAttribute(targetHwnd, DWMWA_SYSTEMBACKDROP_TYPE, out backdropType, sizeof(int)); } catch {}
                string backdropName = "auto";
                if (backdropType == 1) backdropName = "none";
                else if (backdropType == 2) backdropName = "mica";
                else if (backdropType == 3) backdropName = "acrylic";
                else if (backdropType == 4) backdropName = "tabbed";

                int winW = winRect.Right - winRect.Left;
                int winH = winRect.Bottom - winRect.Top;
                int frameW = frameRect.Right - frameRect.Left;
                int frameH = frameRect.Bottom - frameRect.Top;

                int shadowLeft = frameRect.Left - winRect.Left;
                int shadowTop = frameRect.Top - winRect.Top;
                int shadowRight = winRect.Right - frameRect.Right;
                int shadowBottom = winRect.Bottom - frameRect.Bottom;

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"hwnd\": \"0x{0:X}\", \"title\": \"{1}\", \"process\": \"{2}\", \"pid\": {3}, " +
                    "\"extendedFrameBounds\": {{\"left\": {4}, \"top\": {5}, \"right\": {6}, \"bottom\": {7}, \"width\": {8}, \"height\": {9}}}, " +
                    "\"windowRect\": {{\"left\": {10}, \"top\": {11}, \"right\": {12}, \"bottom\": {13}, \"width\": {14}, \"height\": {15}}}, " +
                    "\"dropShadowMargin\": {{\"left\": {16}, \"top\": {17}, \"right\": {18}, \"bottom\": {19}}}, " +
                    "\"cloaked\": {{\"isCloaked\": {20}, \"flags\": {21}, \"reasons\": [{22}]}}, " +
                    "\"ncRenderingEnabled\": {23}, \"immersiveDarkMode\": {24}, \"cornerPreference\": \"{25}\", " +
                    "\"backdropType\": \"{26}\", \"borderColor\": {27}, \"captionColor\": {28}, \"textColor\": {29}, " +
                    "\"visibleBorderThickness\": {30}}}",
                    targetHwnd.ToInt64(), EscapeJson(actualTitle), EscapeJson(procName), pid,
                    frameRect.Left, frameRect.Top, frameRect.Right, frameRect.Bottom, frameW, frameH,
                    winRect.Left, winRect.Top, winRect.Right, winRect.Bottom, winW, winH,
                    shadowLeft, shadowTop, shadowRight, shadowBottom,
                    cloaked != 0 ? "true" : "false", cloaked, string.Join(", ", cloakedReasons.ToArray()),
                    ncRendering != 0 ? "true" : "false", darkMode != 0 ? "true" : "false", cornerName,
                    backdropName, FormatDwmColor(borderColor), FormatDwmColor(captionColor), FormatDwmColor(textColor),
                    borderThickness
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static uint ParseColorRef(string colorStr) {
            if (string.IsNullOrEmpty(colorStr)) return DWMWA_COLOR_DEFAULT;
            string s = colorStr.Trim().ToLowerInvariant();
            if (s == "default" || s == "reset") return DWMWA_COLOR_DEFAULT;
            if (s == "none") return DWMWA_COLOR_NONE;
            if (s.StartsWith("#")) s = s.Substring(1);
            if (s.Length == 6) {
                uint r = Convert.ToByte(s.Substring(0, 2), 16);
                uint g = Convert.ToByte(s.Substring(2, 2), 16);
                uint b = Convert.ToByte(s.Substring(4, 2), 16);
                return r | (g << 8) | (b << 16);
            }
            return DWMWA_COLOR_DEFAULT;
        }

        static string ExtractJsonValue(string json, string key) {
            if (string.IsNullOrEmpty(json)) return null;
            int idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + key.Length);
            if (colon < 0) return null;
            string remainder = json.Substring(colon + 1).Trim();
            if (remainder.StartsWith("\"")) {
                int endQuote = remainder.IndexOf('\"', 1);
                if (endQuote > 1) return remainder.Substring(1, endQuote - 1).Trim();
            } else {
                int endComma = remainder.IndexOfAny(new char[] { ',', '}', ' ', '\r', '\n' });
                if (endComma >= 0) return remainder.Substring(0, endComma).Trim();
                return remainder;
            }
            return null;
        }

        static void DwmSetWindowAttributeCmd(string query, string jsonArgs) {
            try {
                IntPtr targetHwnd;
                string actualTitle;
                if (!ResolveHwnd(query, out targetHwnd, out actualTitle)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Window not found matching query\"}");
                    return;
                }

                var applied = new List<string>();
                string args = jsonArgs ?? "";

                // 1. Dark Mode
                string darkStr = ExtractJsonValue(args, "immersiveDarkMode");
                if (darkStr != null) {
                    bool dark = darkStr.Equals("true", StringComparison.OrdinalIgnoreCase) || darkStr == "1";
                    int val = dark ? 1 : 0;
                    int hr = DwmSetWindowAttribute(targetHwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref val, sizeof(int));
                    applied.Add(string.Format("{{\"attribute\": \"immersiveDarkMode\", \"value\": {0}, \"hr\": {1}, \"success\": {2}}}", val, hr, hr == 0 ? "true" : "false"));
                }

                // 2. Corner Preference
                string cornerStr = ExtractJsonValue(args, "cornerPreference");
                if (cornerStr != null) {
                    int cornerVal = 0;
                    if (cornerStr.Equals("do_not_round", StringComparison.OrdinalIgnoreCase) || cornerStr == "1") cornerVal = 1;
                    else if (cornerStr.Equals("round_small", StringComparison.OrdinalIgnoreCase) || cornerStr == "3") cornerVal = 3;
                    else if (cornerStr.Equals("round", StringComparison.OrdinalIgnoreCase) || cornerVal == 2) cornerVal = 2;
                    int hr = DwmSetWindowAttribute(targetHwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerVal, sizeof(int));
                    applied.Add(string.Format("{{\"attribute\": \"cornerPreference\", \"value\": {0}, \"hr\": {1}, \"success\": {2}}}", cornerVal, hr, hr == 0 ? "true" : "false"));
                }

                // 3. System Backdrop Type (Mica / Acrylic)
                string backdropStr = ExtractJsonValue(args, "backdropType");
                if (backdropStr != null) {
                    int backdropVal = 0;
                    if (backdropStr.Equals("none", StringComparison.OrdinalIgnoreCase) || backdropStr == "1") backdropVal = 1;
                    else if (backdropStr.Equals("mica", StringComparison.OrdinalIgnoreCase) || backdropStr.Equals("main_window", StringComparison.OrdinalIgnoreCase) || backdropStr == "2") backdropVal = 2;
                    else if (backdropStr.Equals("acrylic", StringComparison.OrdinalIgnoreCase) || backdropStr.Equals("transient_window", StringComparison.OrdinalIgnoreCase) || backdropStr == "3") backdropVal = 3;
                    else if (backdropStr.Equals("tabbed", StringComparison.OrdinalIgnoreCase) || backdropStr == "4") backdropVal = 4;
                    int hr = DwmSetWindowAttribute(targetHwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropVal, sizeof(int));
                    applied.Add(string.Format("{{\"attribute\": \"backdropType\", \"value\": {0}, \"hr\": {1}, \"success\": {2}}}", backdropVal, hr, hr == 0 ? "true" : "false"));
                }

                // 4. Border Color
                string borderStr = ExtractJsonValue(args, "borderColor");
                if (borderStr != null) {
                    uint colorRef = ParseColorRef(borderStr);
                    int hr = DwmSetWindowAttributeColor(targetHwnd, DWMWA_BORDER_COLOR, ref colorRef, sizeof(uint));
                    applied.Add(string.Format("{{\"attribute\": \"borderColor\", \"color\": \"{0}\", \"hr\": {1}, \"success\": {2}}}", EscapeJson(borderStr), hr, hr == 0 ? "true" : "false"));
                }

                // 5. Caption Color
                string captionStr = ExtractJsonValue(args, "captionColor");
                if (captionStr != null) {
                    uint colorRef = ParseColorRef(captionStr);
                    int hr = DwmSetWindowAttributeColor(targetHwnd, DWMWA_CAPTION_COLOR, ref colorRef, sizeof(uint));
                    applied.Add(string.Format("{{\"attribute\": \"captionColor\", \"color\": \"{0}\", \"hr\": {1}, \"success\": {2}}}", EscapeJson(captionStr), hr, hr == 0 ? "true" : "false"));
                }

                // 6. Text Color
                string textStr = ExtractJsonValue(args, "textColor");
                if (textStr != null) {
                    uint colorRef = ParseColorRef(textStr);
                    int hr = DwmSetWindowAttributeColor(targetHwnd, DWMWA_TEXT_COLOR, ref colorRef, sizeof(uint));
                    applied.Add(string.Format("{{\"attribute\": \"textColor\", \"color\": \"{0}\", \"hr\": {1}, \"success\": {2}}}", EscapeJson(textStr), hr, hr == 0 ? "true" : "false"));
                }

                // 7. Transitions Forced Disabled
                string transStr = ExtractJsonValue(args, "transitionsForcedDisabled");
                if (transStr != null) {
                    bool dis = transStr.Equals("true", StringComparison.OrdinalIgnoreCase) || transStr == "1";
                    int val = dis ? 1 : 0;
                    int hr = DwmSetWindowAttribute(targetHwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref val, sizeof(int));
                    applied.Add(string.Format("{{\"attribute\": \"transitionsForcedDisabled\", \"value\": {0}, \"hr\": {1}, \"success\": {2}}}", val, hr, hr == 0 ? "true" : "false"));
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"hwnd\": \"0x{0:X}\", \"title\": \"{1}\", \"results\": [{2}]}}",
                    targetHwnd.ToInt64(), EscapeJson(actualTitle), string.Join(", ", applied.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void SystemArchitectureCmd() {
            try {
                SYSTEM_INFO si;
                GetNativeSystemInfo(out si);

                string archName = "UNKNOWN";
                switch (si.wProcessorArchitecture) {
                    case 0: archName = "x86"; break;
                    case 5: archName = "ARM"; break;
                    case 6: archName = "IA64"; break;
                    case 9: archName = "x64"; break;
                    case 12: archName = "ARM64"; break;
                }

                long preciseTime = 0;
                try { GetSystemTimePreciseAsFileTime(out preciseTime); } catch {}

                uint productType = 0;
                try { GetProductInfo(10, 0, 0, 0, out productType); } catch {}

                var sbSys = new StringBuilder(260);
                try { GetSystemDirectory(sbSys, 260); } catch {}
                var sbWin = new StringBuilder(260);
                try { GetWindowsDirectory(sbWin, 260); } catch {}

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"processorArchitecture\": \"{0}\", \"architectureId\": {1}, \"numberOfProcessors\": {2}, \"pageSize\": {3}, \"allocationGranularity\": {4}, \"minimumApplicationAddress\": \"0x{5:X}\", \"maximumApplicationAddress\": \"0x{6:X}\", \"activeProcessorMask\": \"0x{7:X}\", \"processorLevel\": {8}, \"processorRevision\": {9}, \"productType\": {10}, \"preciseFileTime\": {11}, \"systemDirectory\": \"{12}\", \"windowsDirectory\": \"{13}\"}}",
                    archName, si.wProcessorArchitecture, si.dwNumberOfProcessors, si.dwPageSize, si.dwAllocationGranularity,
                    si.lpMinimumApplicationAddress.ToInt64(), si.lpMaximumApplicationAddress.ToInt64(),
                    si.dwActiveProcessorMask.ToInt64(), si.wProcessorLevel, si.wProcessorRevision,
                    productType, preciseTime,
                    EscapeJson(sbSys.ToString()), EscapeJson(sbWin.ToString())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void SystemMemoryStatusCmd() {
            try {
                MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
                mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (!GlobalMemoryStatusEx(ref mem)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"GlobalMemoryStatusEx failed\"}");
                    return;
                }

                double totPhysMB = Math.Round(mem.ullTotalPhys / (1024.0 * 1024.0), 1);
                double availPhysMB = Math.Round(mem.ullAvailPhys / (1024.0 * 1024.0), 1);
                double usedPhysMB = Math.Round((mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0), 1);
                double totPhysGB = Math.Round(mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0), 2);
                double availPhysGB = Math.Round(mem.ullAvailPhys / (1024.0 * 1024.0 * 1024.0), 2);

                double totCommitMB = Math.Round(mem.ullTotalPageFile / (1024.0 * 1024.0), 1);
                double availCommitMB = Math.Round(mem.ullAvailPageFile / (1024.0 * 1024.0), 1);
                double usedCommitMB = Math.Round((mem.ullTotalPageFile - mem.ullAvailPageFile) / (1024.0 * 1024.0), 1);
                double commitLoadPct = mem.ullTotalPageFile > 0 ? Math.Round((double)(mem.ullTotalPageFile - mem.ullAvailPageFile) / mem.ullTotalPageFile * 100.0, 1) : 0.0;

                double totVirtGB = Math.Round(mem.ullTotalVirtual / (1024.0 * 1024.0 * 1024.0), 1);
                double availVirtGB = Math.Round(mem.ullAvailVirtual / (1024.0 * 1024.0 * 1024.0), 1);

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"memoryLoadPercent\": {0}, \"physical\": {{\"totalMB\": {1}, \"availableMB\": {2}, \"usedMB\": {3}, \"totalGB\": {4}, \"availableGB\": {5}}}, \"commit\": {{\"totalMB\": {6}, \"availableMB\": {7}, \"usedMB\": {8}, \"loadPercent\": {9}}}, \"virtual\": {{\"totalGB\": {10}, \"availableGB\": {11}}}}}",
                    mem.dwMemoryLoad, totPhysMB, availPhysMB, usedPhysMB, totPhysGB, availPhysGB,
                    totCommitMB, availCommitMB, usedCommitMB, commitLoadPct,
                    totVirtGB, availVirtGB
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void SystemFirmwareTablesCmd(string provider, string targetTable) {
            try {
                string prov = string.IsNullOrEmpty(provider) ? "ACPI" : provider.Trim().ToUpperInvariant();
                uint provSig = 0x41435049; // 'ACPI'
                if (prov == "RSMB" || prov == "SMBIOS") {
                    provSig = 0x52534D42; // 'RSMB'
                } else if (prov == "FIRM" || prov == "BIOS") {
                    provSig = 0x4649524D; // 'FIRM'
                }

                if (provSig == 0x52534D42) {
                    uint rsmbSize = GetSystemFirmwareTable(provSig, 0, IntPtr.Zero, 0);
                    if (rsmbSize < 8) {
                        Console.WriteLine("{\"success\": false, \"error\": \"SMBIOS table not available\"}");
                        return;
                    }
                    IntPtr pBuf = Marshal.AllocHGlobal((int)rsmbSize);
                    try {
                        uint fetched = GetSystemFirmwareTable(provSig, 0, pBuf, rsmbSize);
                        byte[] data = new byte[fetched];
                        Marshal.Copy(pBuf, data, 0, (int)fetched);

                        byte major = data.Length > 1 ? data[1] : (byte)0;
                        byte minor = data.Length > 2 ? data[2] : (byte)0;
                        byte dmi = data.Length > 3 ? data[3] : (byte)0;
                        uint len = data.Length >= 8 ? BitConverter.ToUInt32(data, 4) : 0;

                        Console.WriteLine(string.Format(
                            "{{\"success\": true, \"provider\": \"RSMB\", \"smbiosVersion\": \"{0}.{1}\", \"dmiRevision\": {2}, \"tableLength\": {3}, \"rawBufferBytes\": {4}}}",
                            major, minor, dmi, len, fetched
                        ));
                    } finally {
                        Marshal.FreeHGlobal(pBuf);
                    }
                    return;
                }

                uint size = EnumSystemFirmwareTables(provSig, IntPtr.Zero, 0);
                if (size == 0) {
                    Console.WriteLine(string.Format("{{\"success\": true, \"provider\": \"{0}\", \"count\": 0, \"tables\": []}}", prov));
                    return;
                }

                IntPtr pEnum = Marshal.AllocHGlobal((int)size);
                var tableList = new List<string>();
                var tableMap = new Dictionary<string, uint>();
                try {
                    uint fetched = EnumSystemFirmwareTables(provSig, pEnum, size);
                    byte[] bytes = new byte[fetched];
                    Marshal.Copy(pEnum, bytes, 0, (int)fetched);
                    for (int i = 0; i + 3 < bytes.Length; i += 4) {
                        string name = Encoding.ASCII.GetString(bytes, i, 4);
                        uint dwordId = BitConverter.ToUInt32(bytes, i);
                        tableList.Add("\"" + EscapeJson(name) + "\"");
                        if (!tableMap.ContainsKey(name)) {
                            tableMap[name] = dwordId;
                        }
                    }
                } finally {
                    Marshal.FreeHGlobal(pEnum);
                }

                if (!string.IsNullOrEmpty(targetTable) && provSig == 0x41435049) {
                    string tblName = targetTable.Trim().ToUpperInvariant();
                    if (!tableMap.ContainsKey(tblName)) {
                        Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Table '{0}' not found in ACPI tables\"}}", EscapeJson(tblName)));
                        return;
                    }
                    uint tableDword = tableMap[tblName];
                    uint tSize = GetSystemFirmwareTable(provSig, tableDword, IntPtr.Zero, 0);
                    if (tSize == 0) {
                        Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Unable to fetch ACPI table '{0}'\"}}", EscapeJson(tblName)));
                        return;
                    }

                    IntPtr pTbl = Marshal.AllocHGlobal((int)tSize);
                    try {
                        uint got = GetSystemFirmwareTable(provSig, tableDword, pTbl, tSize);
                        byte[] tData = new byte[got];
                        Marshal.Copy(pTbl, tData, 0, (int)got);

                        string sig = Encoding.ASCII.GetString(tData, 0, Math.Min(4, tData.Length));
                        uint length = tData.Length >= 8 ? BitConverter.ToUInt32(tData, 4) : got;
                        byte rev = tData.Length >= 9 ? tData[8] : (byte)0;
                        string oemId = tData.Length >= 16 ? Encoding.ASCII.GetString(tData, 10, 6).Trim() : "";
                        string oemTableId = tData.Length >= 24 ? Encoding.ASCII.GetString(tData, 16, 8).Trim() : "";
                        uint oemRev = tData.Length >= 28 ? BitConverter.ToUInt32(tData, 24) : 0;
                        string creatorId = tData.Length >= 32 ? Encoding.ASCII.GetString(tData, 28, 4).Trim() : "";

                        Console.WriteLine(string.Format(
                            "{{\"success\": true, \"provider\": \"{0}\", \"table\": \"{1}\", \"signature\": \"{2}\", \"length\": {3}, \"revision\": {4}, \"oemId\": \"{5}\", \"oemTableId\": \"{6}\", \"oemRevision\": {7}, \"creatorId\": \"{8}\"}}",
                            prov, EscapeJson(tblName), EscapeJson(sig), length, rev, EscapeJson(oemId), EscapeJson(oemTableId), oemRev, EscapeJson(creatorId)
                        ));
                    } finally {
                        Marshal.FreeHGlobal(pTbl);
                    }
                    return;
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"provider\": \"{0}\", \"count\": {1}, \"tables\": [{2}]}}",
                    prov, tableMap.Count, string.Join(", ", tableList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        // --- WinTrust Subsystem (wintrust.h / softpub.h / wintrust.dll) ---

        private const uint WTD_UI_NONE = 2;
        private const uint WTD_REVOKE_NONE = 0;
        private const uint WTD_CHOICE_FILE = 1;
        private const uint WTD_CHOICE_CATALOG = 2;
        private const uint WTD_STATEACTION_IGNORE = 0;
        private const uint WTD_REVOCATION_CHECK_NONE = 0x00000010;
        private const uint WTD_REVOCATION_CHECK_WHOLECHAIN = 0x00000020;
        private const uint WTD_CACHE_ONLY_URL_RETRIEVAL = 0x00004000;

        private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new Guid("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");
        private static readonly Guid DRIVER_ACTION_VERIFY = new Guid("{F750E6C3-38EE-11d1-85E5-00C04FC295EE}");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WINTRUST_FILE_INFO {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WINTRUST_CATALOG_INFO {
            public uint cbStruct;
            public uint dwCatalogVersion;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pcwszCatalogFilePath;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pcwszMemberTag;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pcwszMemberFilePath;
            public IntPtr hMemberFile;
            public IntPtr pbCalculatedHash;
            public uint cbCalculatedHash;
            public IntPtr pcCatalogContext;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WINTRUST_DATA {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pUnionData;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct CATALOG_INFO {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string wszCatalogFile;
        }

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
        static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, IntPtr pWVTData);

        [DllImport("wintrust.dll", SetLastError = true)]
        static extern bool CryptCATAdminAcquireContext(out IntPtr phCatAdmin, ref Guid pgSubsystem, uint dwFlags);

        [DllImport("wintrust.dll", SetLastError = true)]
        static extern bool CryptCATAdminReleaseContext(IntPtr hCatAdmin, uint dwFlags);

        [DllImport("wintrust.dll", SetLastError = true)]
        static extern bool CryptCATAdminCalcHashFromFileHandle(IntPtr hFile, ref uint pcbHash, IntPtr pbHash, uint dwFlags);

        [DllImport("wintrust.dll", SetLastError = true)]
        static extern IntPtr CryptCATAdminEnumCatalogFromHash(IntPtr hCatAdmin, IntPtr pbHash, uint cbHash, uint dwFlags, ref IntPtr phPrevCatInfo);

        [DllImport("wintrust.dll", SetLastError = true)]
        static extern bool CryptCATCatalogInfoFromContext(IntPtr hCatInfo, ref CATALOG_INFO psCatInfo, uint dwFlags);

        [DllImport("wintrust.dll", SetLastError = true)]
        static extern bool CryptCATAdminReleaseCatalogContext(IntPtr hCatAdmin, IntPtr hCatInfo, uint dwFlags);

        static string GetWinTrustStatusText(uint status, out string message) {
            switch (status) {
                case 0:
                    message = "The digital signature is verified and trusted.";
                    return "TRUSTED_AND_VERIFIED";
                case 0x800B0003:
                    message = "The form specified for the subject is not supported or recognized by the trust provider.";
                    return "TRUST_E_SUBJECT_FORM_UNKNOWN";
                case 0x800B0100:
                    message = "No signature was present in the subject.";
                    return "TRUST_E_NOSIGNATURE";
                case 0x800B0101:
                    message = "The signature or certificate is explicitly distrusted.";
                    return "TRUST_E_EXPLICIT_DISTRUST";
                case 0x800B0108:
                    message = "One of the CA certificates in the chain is not trusted.";
                    return "CERT_E_UNTRUSTEDCA";
                case 0x800B0109:
                    message = "A certificate chain terminated in a root certificate not trusted by the trust provider.";
                    return "CERT_E_UNTRUSTEDROOT";
                case 0x800B010A:
                    message = "A certificate chain could not be built to a trusted root authority.";
                    return "CERT_E_CHAINING";
                case 0x800B010C:
                    message = "A certificate's basic constraint extension has not been observed.";
                    return "CERT_E_ROLE";
                case 0x800B010E:
                    message = "The certificate was revoked by the issuer.";
                    return "CERT_E_REVOKED";
                case 0x800B010F:
                    message = "The certificate is expired or outside its validity period.";
                    return "CERT_E_EXPIRED";
                case 0x80096010:
                    message = "The digital signature hash does not match the file contents (possible tampering detected).";
                    return "TRUST_E_BAD_DIGEST";
                case 0x80096004:
                    message = "The trust verification provider is unknown.";
                    return "TRUST_E_PROVIDER_UNKNOWN";
                case 0x80092026:
                    message = "The cryptographic or security settings could not be verified.";
                    return "CRYPT_E_SECURITY_SETTINGS";
                default:
                    message = "Cryptographic trust verification returned error code 0x" + status.ToString("X8") + ".";
                    return "ERROR_0x" + status.ToString("X8");
            }
        }

        static string BuildSignerJson(X509Certificate2 cert) {
            if (cert == null) return "null";
            bool isSelfSigned = string.Equals(cert.Subject, cert.Issuer, StringComparison.OrdinalIgnoreCase);
            bool isExpired = DateTime.Now < cert.NotBefore || DateTime.Now > cert.NotAfter;
            return string.Format(
                "{{\"subject\": \"{0}\", \"issuer\": \"{1}\", \"thumbprint\": \"{2}\", \"serialNumber\": \"{3}\", \"validFrom\": \"{4}\", \"validTo\": \"{5}\", \"isSelfSigned\": {6}, \"isExpired\": {7}, \"keyAlgorithm\": \"{8}\"}}",
                EscapeJson(cert.Subject),
                EscapeJson(cert.Issuer),
                EscapeJson(cert.Thumbprint),
                EscapeJson(cert.SerialNumber),
                cert.NotBefore.ToString("o"),
                cert.NotAfter.ToString("o"),
                isSelfSigned ? "true" : "false",
                isExpired ? "true" : "false",
                EscapeJson(cert.GetKeyAlgorithm())
            );
        }

        static bool TryFindCatalogForFile(string filePath, out string catalogPath, out string memberHash, out int trustResult) {
            catalogPath = null;
            memberHash = null;
            trustResult = -1;

            if (!File.Exists(filePath)) return false;

            try {
                using (FileStream fs = File.OpenRead(filePath)) {
                    IntPtr hCatAdmin;
                    Guid driverAction = DRIVER_ACTION_VERIFY;
                    if (!CryptCATAdminAcquireContext(out hCatAdmin, ref driverAction, 0)) return false;

                    try {
                        uint cbHash = 0;
                        CryptCATAdminCalcHashFromFileHandle(fs.SafeFileHandle.DangerousGetHandle(), ref cbHash, IntPtr.Zero, 0);
                        if (cbHash == 0) return false;

                        IntPtr pbHash = Marshal.AllocHGlobal((int)cbHash);
                        try {
                            if (!CryptCATAdminCalcHashFromFileHandle(fs.SafeFileHandle.DangerousGetHandle(), ref cbHash, pbHash, 0)) return false;

                            byte[] hashBytes = new byte[cbHash];
                            Marshal.Copy(pbHash, hashBytes, 0, (int)cbHash);
                            StringBuilder sb = new StringBuilder();
                            foreach (byte b in hashBytes) sb.Append(b.ToString("X2"));
                            memberHash = sb.ToString();

                            IntPtr hPrev = IntPtr.Zero;
                            IntPtr hCatInfo = CryptCATAdminEnumCatalogFromHash(hCatAdmin, pbHash, cbHash, 0, ref hPrev);
                            if (hCatInfo == IntPtr.Zero) return false;

                            CATALOG_INFO catInfo = new CATALOG_INFO();
                            catInfo.cbStruct = (uint)Marshal.SizeOf(typeof(CATALOG_INFO));
                            if (CryptCATCatalogInfoFromContext(hCatInfo, ref catInfo, 0)) {
                                catalogPath = catInfo.wszCatalogFile;

                                WINTRUST_CATALOG_INFO catWti = new WINTRUST_CATALOG_INFO();
                                catWti.cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_CATALOG_INFO));
                                catWti.dwCatalogVersion = 0;
                                catWti.pcwszCatalogFilePath = catalogPath;
                                catWti.pcwszMemberTag = memberHash;
                                catWti.pcwszMemberFilePath = filePath;
                                catWti.hMemberFile = IntPtr.Zero;
                                catWti.pbCalculatedHash = pbHash;
                                catWti.cbCalculatedHash = cbHash;
                                catWti.pcCatalogContext = IntPtr.Zero;

                                IntPtr pCatWti = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_CATALOG_INFO)));
                                Marshal.StructureToPtr(catWti, pCatWti, false);

                                WINTRUST_DATA wtd = new WINTRUST_DATA();
                                wtd.cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA));
                                wtd.dwUIChoice = WTD_UI_NONE;
                                wtd.fdwRevocationChecks = WTD_REVOKE_NONE;
                                wtd.dwUnionChoice = WTD_CHOICE_CATALOG;
                                wtd.pUnionData = pCatWti;
                                wtd.dwStateAction = WTD_STATEACTION_IGNORE;
                                wtd.dwProvFlags = WTD_REVOCATION_CHECK_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL;

                                IntPtr pWtd = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_DATA)));
                                Marshal.StructureToPtr(wtd, pWtd, false);

                                try {
                                    trustResult = WinVerifyTrust(IntPtr.Zero, WINTRUST_ACTION_GENERIC_VERIFY_V2, pWtd);
                                } finally {
                                    Marshal.FreeHGlobal(pCatWti);
                                    Marshal.FreeHGlobal(pWtd);
                                }
                            }
                            CryptCATAdminReleaseCatalogContext(hCatAdmin, hCatInfo, 0);
                            return !string.IsNullOrEmpty(catalogPath);
                        } finally {
                            Marshal.FreeHGlobal(pbHash);
                        }
                    } finally {
                        CryptCATAdminReleaseContext(hCatAdmin, 0);
                    }
                }
            } catch {
                return false;
            }
        }

        static void WinTrustVerifyFileCmd(string targetPath, bool allowCatalog, bool checkRevocation) {
            try {
                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Target file not found: {0}\"}}", EscapeJson(targetPath ?? "")));
                    return;
                }

                string fullPath = Path.GetFullPath(targetPath);

                // 1. Try embedded signature verification
                WINTRUST_FILE_INFO fileInfo = new WINTRUST_FILE_INFO();
                fileInfo.cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO));
                fileInfo.pcwszFilePath = fullPath;
                fileInfo.hFile = IntPtr.Zero;
                fileInfo.pgKnownSubject = IntPtr.Zero;

                IntPtr pFileInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)));
                Marshal.StructureToPtr(fileInfo, pFileInfo, false);

                WINTRUST_DATA data = new WINTRUST_DATA();
                data.cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA));
                data.pPolicyCallbackData = IntPtr.Zero;
                data.pSIPClientData = IntPtr.Zero;
                data.dwUIChoice = WTD_UI_NONE;
                data.fdwRevocationChecks = checkRevocation ? WTD_REVOCATION_CHECK_WHOLECHAIN : WTD_REVOKE_NONE;
                data.dwUnionChoice = WTD_CHOICE_FILE;
                data.pUnionData = pFileInfo;
                data.dwStateAction = WTD_STATEACTION_IGNORE;
                data.hWVTStateData = IntPtr.Zero;
                data.pwszURLReference = null;
                data.dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL;
                if (!checkRevocation) data.dwProvFlags |= WTD_REVOCATION_CHECK_NONE;
                data.dwUIContext = 0;
                data.pSignatureSettings = IntPtr.Zero;

                IntPtr pData = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_DATA)));
                Marshal.StructureToPtr(data, pData, false);

                int lStatus;
                try {
                    lStatus = WinVerifyTrust(IntPtr.Zero, WINTRUST_ACTION_GENERIC_VERIFY_V2, pData);
                } finally {
                    Marshal.FreeHGlobal(pFileInfo);
                    Marshal.FreeHGlobal(pData);
                }

                uint uStatus = (uint)lStatus;

                if (lStatus == 0) {
                    string msg;
                    string statusStr = GetWinTrustStatusText(uStatus, out msg);
                    string signerJson = "null";
                    try {
                        X509Certificate rawCert = X509Certificate.CreateFromSignedFile(fullPath);
                        if (rawCert != null) {
                            using (X509Certificate2 cert2 = new X509Certificate2(rawCert)) {
                                signerJson = BuildSignerJson(cert2);
                            }
                        }
                    } catch {}

                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"path\": \"{0}\", \"isTrusted\": true, \"signatureType\": \"embedded\", \"statusCode\": \"0x{1:X8}\", \"status\": \"{2}\", \"statusMessage\": \"{3}\", \"catalogFile\": null, \"catalogHash\": null, \"signer\": {4}}}",
                        EscapeJson(fullPath), uStatus, statusStr, EscapeJson(msg), signerJson
                    ));
                    return;
                }

                // If no embedded signature and catalog lookup is allowed, try catalog
                if (allowCatalog && uStatus == 0x800B0100) {
                    string catPath, memHash;
                    int catTrust;
                    if (TryFindCatalogForFile(fullPath, out catPath, out memHash, out catTrust)) {
                        uint uCatStatus = (uint)catTrust;
                        string catMsg;
                        string catStatusStr = GetWinTrustStatusText(uCatStatus, out catMsg);
                        bool isCatTrusted = (catTrust == 0);
                        string catSignerJson = "null";
                        try {
                            if (File.Exists(catPath)) {
                                X509Certificate rawCert = X509Certificate.CreateFromSignedFile(catPath);
                                if (rawCert != null) {
                                    using (X509Certificate2 cert2 = new X509Certificate2(rawCert)) {
                                        catSignerJson = BuildSignerJson(cert2);
                                    }
                                }
                            }
                        } catch {}

                        Console.WriteLine(string.Format(
                            "{{\"success\": true, \"path\": \"{0}\", \"isTrusted\": {1}, \"signatureType\": \"catalog\", \"statusCode\": \"0x{2:X8}\", \"status\": \"{3}\", \"statusMessage\": \"{4}\", \"catalogFile\": \"{5}\", \"catalogHash\": \"{6}\", \"signer\": {7}}}",
                            EscapeJson(fullPath), isCatTrusted ? "true" : "false", uCatStatus, catStatusStr, EscapeJson(catMsg), EscapeJson(catPath), memHash, catSignerJson
                        ));
                        return;
                    }
                }

                string failMsg;
                string failStatusStr = GetWinTrustStatusText(uStatus, out failMsg);
                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"path\": \"{0}\", \"isTrusted\": false, \"signatureType\": \"{1}\", \"statusCode\": \"0x{2:X8}\", \"status\": \"{3}\", \"statusMessage\": \"{4}\", \"catalogFile\": null, \"catalogHash\": null, \"signer\": null}}",
                    EscapeJson(fullPath), (uStatus == 0x800B0100 || uStatus == 0x800B0003 ? "none" : "embedded"), uStatus, failStatusStr, EscapeJson(failMsg)
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void WinTrustSignerInfoCmd(string targetPath) {
            try {
                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Target file not found: {0}\"}}", EscapeJson(targetPath ?? "")));
                    return;
                }

                string fullPath = Path.GetFullPath(targetPath);

                // 1. Try embedded signature certificate
                try {
                    X509Certificate rawCert = X509Certificate.CreateFromSignedFile(fullPath);
                    if (rawCert != null) {
                        using (X509Certificate2 cert2 = new X509Certificate2(rawCert)) {
                            string signerJson = BuildSignerJson(cert2);
                            Console.WriteLine(string.Format(
                                "{{\"success\": true, \"path\": \"{0}\", \"hasSignature\": true, \"signatureSource\": \"embedded\", \"catalogFile\": null, \"signer\": {1}}}",
                                EscapeJson(fullPath), signerJson
                            ));
                            return;
                        }
                    }
                } catch {}

                // 2. Try catalog certificate
                string catPath, memHash;
                int catTrust;
                if (TryFindCatalogForFile(fullPath, out catPath, out memHash, out catTrust)) {
                    try {
                        if (File.Exists(catPath)) {
                            X509Certificate rawCert = X509Certificate.CreateFromSignedFile(catPath);
                            if (rawCert != null) {
                                using (X509Certificate2 cert2 = new X509Certificate2(rawCert)) {
                                    string signerJson = BuildSignerJson(cert2);
                                    Console.WriteLine(string.Format(
                                        "{{\"success\": true, \"path\": \"{0}\", \"hasSignature\": true, \"signatureSource\": \"catalog\", \"catalogFile\": \"{1}\", \"signer\": {2}}}",
                                        EscapeJson(fullPath), EscapeJson(catPath), signerJson
                                    ));
                                    return;
                                }
                            }
                        }
                    } catch {}
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"path\": \"{0}\", \"hasSignature\": false, \"signatureSource\": \"none\", \"catalogFile\": null, \"signer\": null}}",
                    EscapeJson(fullPath)
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void WinTrustCatalogSearchCmd(string targetPath) {
            try {
                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Target file not found: {0}\"}}", EscapeJson(targetPath ?? "")));
                    return;
                }

                string fullPath = Path.GetFullPath(targetPath);
                string catPath, memHash;
                int catTrust;
                bool found = TryFindCatalogForFile(fullPath, out catPath, out memHash, out catTrust);

                if (found) {
                    uint uStatus = (uint)catTrust;
                    string msg;
                    string statusStr = GetWinTrustStatusText(uStatus, out msg);
                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"path\": \"{0}\", \"hasCatalog\": true, \"catalogHash\": \"{1}\", \"catalogFile\": \"{2}\", \"isCatalogTrusted\": {3}, \"statusCode\": \"0x{4:X8}\", \"status\": \"{5}\"}}",
                        EscapeJson(fullPath), memHash, EscapeJson(catPath), (catTrust == 0 ? "true" : "false"), uStatus, statusStr
                    ));
                } else {
                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"path\": \"{0}\", \"hasCatalog\": false, \"catalogHash\": null, \"catalogFile\": null, \"isCatalogTrusted\": false, \"statusCode\": null, \"status\": null}}",
                        EscapeJson(fullPath)
                    ));
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #region Windows Multi-Provider Router & Network Drive Management (WNet)

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NETRESOURCE_RAW {
            public uint dwScope;
            public uint dwType;
            public uint dwDisplayType;
            public uint dwUsage;
            public IntPtr lpLocalName;
            public IntPtr lpRemoteName;
            public IntPtr lpComment;
            public IntPtr lpProvider;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NETRESOURCE_IN {
            public uint dwScope;
            public uint dwType;
            public uint dwDisplayType;
            public uint dwUsage;
            public string lpLocalName;
            public string lpRemoteName;
            public string lpComment;
            public string lpProvider;
        }

        const uint RESOURCETYPE_ANY = 0x00000000;
        const uint RESOURCETYPE_DISK = 0x00000001;
        const uint RESOURCETYPE_PRINT = 0x00000002;

        const uint RESOURCE_CONNECTED = 0x00000001;
        const uint RESOURCE_GLOBALNET = 0x00000002;
        const uint RESOURCE_REMEMBERED = 0x00000003;
        const uint RESOURCE_RECENT = 0x00000004;
        const uint RESOURCE_CONTEXT = 0x00000005;

        const uint CONNECT_UPDATE_PROFILE = 0x00000001;
        const uint CONNECT_TEMPORARY = 0x00000004;

        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint WNetOpenEnumW(uint dwScope, uint dwType, uint dwUsage, IntPtr lpNetResource, out IntPtr lphEnum);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint WNetEnumResourceW(IntPtr hEnum, ref uint lpcCount, IntPtr lpBuffer, ref uint lpBufferSize);

        [DllImport("mpr.dll", SetLastError = true)]
        static extern uint WNetCloseEnum(IntPtr hEnum);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint WNetGetConnectionW(string lpLocalName, [Out] StringBuilder lpRemoteName, ref uint lpnLength);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint WNetGetUserW(string lpName, [Out] StringBuilder lpUserName, ref uint lpnLength);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint WNetAddConnection2W(ref NETRESOURCE_IN lpNetResource, string lpPassword, string lpUserName, uint dwFlags);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint WNetCancelConnection2W(string lpName, uint dwFlags, bool fForce);

        static string GetWNetScopeName(uint scope) {
            switch (scope) {
                case 1: return "connected";
                case 2: return "globalnet";
                case 3: return "remembered";
                case 4: return "recent";
                case 5: return "context";
                default: return "unknown_" + scope;
            }
        }

        static string GetWNetTypeName(uint type) {
            switch (type) {
                case 0: return "all";
                case 1: return "disk";
                case 2: return "print";
                case 8: return "reserved";
                default: return "type_" + type;
            }
        }

        static string GetWNetDisplayTypeName(uint dt) {
            switch (dt) {
                case 0: return "generic";
                case 1: return "domain";
                case 2: return "server";
                case 3: return "share";
                case 4: return "file";
                case 5: return "group";
                case 6: return "network";
                case 7: return "root";
                case 8: return "shareadmin";
                case 9: return "directory";
                case 10: return "tree";
                case 11: return "ndscontainer";
                default: return "display_" + dt;
            }
        }

        static string GetWNetUsageJson(uint usage) {
            var list = new List<string>();
            if ((usage & 0x01) != 0) list.Add("\"connectable\"");
            if ((usage & 0x02) != 0) list.Add("\"container\"");
            if ((usage & 0x04) != 0) list.Add("\"no_local_device\"");
            if ((usage & 0x08) != 0) list.Add("\"sibling\"");
            if ((usage & 0x10) != 0) list.Add("\"attached\"");
            return "[" + string.Join(", ", list.ToArray()) + "]";
        }

        static string GetWNetErrorMessage(uint code) {
            switch (code) {
                case 0: return "Success";
                case 5: return "Access is denied";
                case 53: return "The network path was not found";
                case 67: return "The network name cannot be found";
                case 85: return "The local device name is already in use";
                case 86: return "The specified network password is not correct";
                case 1200: return "The specified device name is invalid";
                case 1202: return "The local device has a remembered connection to another network resource";
                case 1203: return "No network provider accepted the given network path";
                case 1208: return "An extended error occurred";
                case 2250: return "This network connection does not exist";
                case 2401: return "There are open files or pending requests on the connection";
                default:
                    try {
                        return new System.ComponentModel.Win32Exception((int)code).Message;
                    } catch {
                        return "System error " + code;
                    }
            }
        }

        static void WNetNetworkDrivesCmd(string scopeStr, string typeStr) {
            try {
                string sLower = (scopeStr ?? "connected").Trim().ToLowerInvariant();
                uint dwScope = RESOURCE_CONNECTED;
                if (sLower == "remembered") dwScope = RESOURCE_REMEMBERED;
                else if (sLower == "global" || sLower == "globalnet") dwScope = RESOURCE_GLOBALNET;
                else if (sLower == "recent") dwScope = RESOURCE_RECENT;
                else if (sLower == "context") dwScope = RESOURCE_CONTEXT;

                string tLower = (typeStr ?? "all").Trim().ToLowerInvariant();
                uint dwType = RESOURCETYPE_ANY;
                if (tLower == "disk") dwType = RESOURCETYPE_DISK;
                else if (tLower == "print") dwType = RESOURCETYPE_PRINT;

                IntPtr hEnum;
                uint openRes = WNetOpenEnumW(dwScope, dwType, 0, IntPtr.Zero, out hEnum);
                if (openRes != 0) {
                    Console.WriteLine(string.Format(
                        "{{\"success\": false, \"scope\": \"{0}\", \"type\": \"{1}\", \"errorCode\": {2}, \"error\": \"{3}\"}}",
                        EscapeJson(sLower), EscapeJson(tLower), openRes, EscapeJson(GetWNetErrorMessage(openRes))
                    ));
                    return;
                }

                List<string> resJsonList = new List<string>();
                uint bufSize = 65536;
                IntPtr pBuf = Marshal.AllocHGlobal((int)bufSize);

                try {
                    while (true) {
                        uint count = 0xFFFFFFFF;
                        uint currentBufSize = bufSize;
                        uint enumRes = WNetEnumResourceW(hEnum, ref count, pBuf, ref currentBufSize);

                        if (enumRes == 0 || enumRes == 259 /* ERROR_NO_MORE_ITEMS */) {
                            if (count > 0 && count != 0xFFFFFFFF) {
                                int structSize = Marshal.SizeOf(typeof(NETRESOURCE_RAW));
                                for (int i = 0; i < count; i++) {
                                    IntPtr itemPtr = new IntPtr(pBuf.ToInt64() + (i * structSize));
                                    NETRESOURCE_RAW nr = (NETRESOURCE_RAW)Marshal.PtrToStructure(itemPtr, typeof(NETRESOURCE_RAW));
                                    string local = nr.lpLocalName != IntPtr.Zero ? Marshal.PtrToStringUni(nr.lpLocalName) : null;
                                    string remote = nr.lpRemoteName != IntPtr.Zero ? Marshal.PtrToStringUni(nr.lpRemoteName) : null;
                                    string comment = nr.lpComment != IntPtr.Zero ? Marshal.PtrToStringUni(nr.lpComment) : null;
                                    string provider = nr.lpProvider != IntPtr.Zero ? Marshal.PtrToStringUni(nr.lpProvider) : null;

                                    string itemJson = string.Format(
                                        "{{\"localName\": {0}, \"remoteName\": {1}, \"comment\": {2}, \"provider\": {3}, \"scope\": \"{4}\", \"type\": \"{5}\", \"displayType\": \"{6}\", \"usage\": {7}}}",
                                        local != null ? "\"" + EscapeJson(local) + "\"" : "null",
                                        remote != null ? "\"" + EscapeJson(remote) + "\"" : "null",
                                        comment != null ? "\"" + EscapeJson(comment) + "\"" : "null",
                                        provider != null ? "\"" + EscapeJson(provider) + "\"" : "null",
                                        GetWNetScopeName(nr.dwScope),
                                        GetWNetTypeName(nr.dwType),
                                        GetWNetDisplayTypeName(nr.dwDisplayType),
                                        GetWNetUsageJson(nr.dwUsage)
                                    );
                                    resJsonList.Add(itemJson);
                                }
                            }
                            if (enumRes == 259) break;
                        } else {
                            break;
                        }
                    }
                } finally {
                    WNetCloseEnum(hEnum);
                    Marshal.FreeHGlobal(pBuf);
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"scope\": \"{0}\", \"type\": \"{1}\", \"count\": {2}, \"resources\": [{3}]}}",
                    EscapeJson(sLower), EscapeJson(tLower), resJsonList.Count, string.Join(", ", resJsonList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void WNetGetConnectionCmd(string localName) {
            try {
                StringBuilder userBuf = new StringBuilder(512);
                uint userLen = (uint)userBuf.Capacity;
                uint userRes = WNetGetUserW(null, userBuf, ref userLen);
                string currentUser = (userRes == 0 ? userBuf.ToString() : null);

                if (string.IsNullOrEmpty(localName)) {
                    List<string> driveConns = new List<string>();
                    foreach (DriveInfo di in DriveInfo.GetDrives()) {
                        string driveRoot = di.Name.TrimEnd('\\');
                        StringBuilder rBuf = new StringBuilder(1024);
                        uint rLen = (uint)rBuf.Capacity;
                        uint cRes = WNetGetConnectionW(driveRoot, rBuf, ref rLen);
                        string remote = (cRes == 0 ? rBuf.ToString() : null);
                        string status = (cRes == 0 ? "connected" : (cRes == 2250 ? "not_connected" : "error"));

                        driveConns.Add(string.Format(
                            "{{\"localName\": \"{0}\", \"remoteName\": {1}, \"driveType\": \"{2}\", \"status\": \"{3}\", \"statusCode\": {4}}}",
                            EscapeJson(driveRoot),
                            remote != null ? "\"" + EscapeJson(remote) + "\"" : "null",
                            di.DriveType.ToString(),
                            status,
                            cRes
                        ));
                    }

                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"currentUser\": {0}, \"drives\": [{1}]}}",
                        currentUser != null ? "\"" + EscapeJson(currentUser) + "\"" : "null",
                        string.Join(", ", driveConns.ToArray())
                    ));
                    return;
                }

                string cleanName = localName.Trim();
                if (cleanName.EndsWith("\\")) cleanName = cleanName.TrimEnd('\\');
                if (cleanName.Length == 1 && char.IsLetter(cleanName[0])) cleanName += ":";

                StringBuilder remBuf = new StringBuilder(1024);
                uint remLen = (uint)remBuf.Capacity;
                uint res = WNetGetConnectionW(cleanName, remBuf, ref remLen);

                StringBuilder devUserBuf = new StringBuilder(512);
                uint devUserLen = (uint)devUserBuf.Capacity;
                uint devUserRes = WNetGetUserW(cleanName, devUserBuf, ref devUserLen);
                string devUser = (devUserRes == 0 ? devUserBuf.ToString() : currentUser);

                if (res == 0) {
                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"localName\": \"{0}\", \"remoteName\": \"{1}\", \"currentUser\": {2}, \"status\": \"connected\", \"statusCode\": 0, \"statusMessage\": \"Connected\"}}",
                        EscapeJson(cleanName), EscapeJson(remBuf.ToString()),
                        devUser != null ? "\"" + EscapeJson(devUser) + "\"" : "null"
                    ));
                } else if (res == 2250 /* ERROR_NOT_CONNECTED */) {
                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"localName\": \"{0}\", \"remoteName\": null, \"currentUser\": {1}, \"status\": \"not_connected\", \"statusCode\": 2250, \"statusMessage\": \"This network connection does not exist (local or unmapped device)\"}}",
                        EscapeJson(cleanName),
                        devUser != null ? "\"" + EscapeJson(devUser) + "\"" : "null"
                    ));
                } else {
                    Console.WriteLine(string.Format(
                        "{{\"success\": false, \"localName\": \"{0}\", \"remoteName\": null, \"currentUser\": {1}, \"status\": \"error\", \"statusCode\": {2}, \"error\": \"{3}\"}}",
                        EscapeJson(cleanName),
                        devUser != null ? "\"" + EscapeJson(devUser) + "\"" : "null",
                        res,
                        EscapeJson(GetWNetErrorMessage(res))
                    ));
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void WNetManageConnectionCmd(string action, string remoteName, string localName, string userName, string password, bool persistent, bool force) {
            try {
                string act = (action ?? "").Trim().ToLowerInvariant();
                if (act == "connect" || act == "add" || act == "map") {
                    if (string.IsNullOrEmpty(remoteName)) {
                        Console.WriteLine("{\"success\": false, \"error\": \"remoteName (UNC path, e.g. \\\\server\\share) is required for connect\"}");
                        return;
                    }

                    string cleanLocal = string.IsNullOrEmpty(localName) ? null : localName.Trim().TrimEnd('\\');
                    if (!string.IsNullOrEmpty(cleanLocal) && cleanLocal.Length == 1 && char.IsLetter(cleanLocal[0])) cleanLocal += ":";

                    NETRESOURCE_IN nr = new NETRESOURCE_IN();
                    nr.dwType = RESOURCETYPE_DISK;
                    nr.lpLocalName = cleanLocal;
                    nr.lpRemoteName = remoteName.Trim();
                    nr.lpProvider = null;

                    uint flags = persistent ? CONNECT_UPDATE_PROFILE : CONNECT_TEMPORARY;
                    string u = string.IsNullOrEmpty(userName) ? null : userName;
                    string p = string.IsNullOrEmpty(password) ? null : password;

                    uint res = WNetAddConnection2W(ref nr, p, u, flags);
                    if (res == 0) {
                        Console.WriteLine(string.Format(
                            "{{\"success\": true, \"action\": \"connect\", \"localName\": {0}, \"remoteName\": \"{1}\", \"persistent\": {2}, \"message\": \"Successfully connected network resource\"}}",
                            cleanLocal != null ? "\"" + EscapeJson(cleanLocal) + "\"" : "null",
                            EscapeJson(remoteName),
                            persistent ? "true" : "false"
                        ));
                    } else {
                        Console.WriteLine(string.Format(
                            "{{\"success\": false, \"action\": \"connect\", \"localName\": {0}, \"remoteName\": \"{1}\", \"errorCode\": {2}, \"error\": \"{3}\"}}",
                            cleanLocal != null ? "\"" + EscapeJson(cleanLocal) + "\"" : "null",
                            EscapeJson(remoteName),
                            res,
                            EscapeJson(GetWNetErrorMessage(res))
                        ));
                    }
                } else if (act == "disconnect" || act == "cancel" || act == "unmap") {
                    string target = !string.IsNullOrEmpty(localName) ? localName.Trim().TrimEnd('\\') : remoteName;
                    if (string.IsNullOrEmpty(target)) {
                        Console.WriteLine("{\"success\": false, \"error\": \"Either localName (e.g. Z:) or remoteName (e.g. \\\\server\\share) must be provided to disconnect\"}");
                        return;
                    }
                    if (target.Length == 1 && char.IsLetter(target[0])) target += ":";

                    uint flags = persistent ? CONNECT_UPDATE_PROFILE : 0;
                    uint res = WNetCancelConnection2W(target, flags, force);

                    if (res == 0) {
                        Console.WriteLine(string.Format(
                            "{{\"success\": true, \"action\": \"disconnect\", \"target\": \"{0}\", \"force\": {1}, \"message\": \"Successfully disconnected network resource\"}}",
                            EscapeJson(target),
                            force ? "true" : "false"
                        ));
                    } else {
                        Console.WriteLine(string.Format(
                            "{{\"success\": false, \"action\": \"disconnect\", \"target\": \"{0}\", \"errorCode\": {1}, \"error\": \"{2}\"}}",
                            EscapeJson(target),
                            res,
                            EscapeJson(GetWNetErrorMessage(res))
                        ));
                    }
                } else {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Invalid action '{0}'. Expected 'connect' or 'disconnect'.\"}}", EscapeJson(act)));
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows ToolHelp32 Snapshot Subsystem (tlhelp32.h)

        const uint TH32CS_SNAPHEAPLIST = 0x00000001;
        const uint TH32CS_SNAPPROCESS  = 0x00000002;
        const uint TH32CS_SNAPTHREAD   = 0x00000004;
        const uint TH32CS_SNAPMODULE   = 0x00000008;
        const uint TH32CS_SNAPMODULE32 = 0x00000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MODULEENTRY32 {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            public IntPtr modBaseAddr;
            public uint modBaseSize;
            public IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct THREADENTRY32 {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ThreadID;
            public uint th32OwnerProcessID;
            public int tpBasePri;
            public int tpDeltaPri;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PROCESSENTRY32 {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool Module32FirstW(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool Module32NextW(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Thread32First(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Thread32Next(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        static bool ResolveProcessTarget(string target, out uint pid, out string procName) {
            pid = 0;
            procName = "";
            string t = (target ?? "").Trim();
            if (string.IsNullOrEmpty(t) || t.ToLowerInvariant() == "current" || t.ToLowerInvariant() == "self") {
                Process cur = Process.GetCurrentProcess();
                pid = (uint)cur.Id;
                procName = cur.ProcessName;
                return true;
            }
            if (uint.TryParse(t, out pid)) {
                try {
                    procName = Process.GetProcessById((int)pid).ProcessName;
                } catch {
                    procName = "PID_" + pid;
                }
                return true;
            }
            string q = t.ToLowerInvariant();
            if (q.EndsWith(".exe")) q = q.Substring(0, q.Length - 4);
            foreach (Process p in Process.GetProcesses()) {
                if (p.ProcessName.ToLowerInvariant() == q || p.ProcessName.ToLowerInvariant().Contains(q)) {
                    pid = (uint)p.Id;
                    procName = p.ProcessName;
                    return true;
                }
            }
            return false;
        }

        static void ToolHelpModulesCmd(string target, string search, int limit) {
            try {
                if (limit <= 0) limit = 100;
                string q = (search ?? "").Trim().ToLowerInvariant();

                uint targetPid;
                string targetName;
                if (!ResolveProcessTarget(target, out targetPid, out targetName)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Process target not found: '{0}'\"}}", EscapeJson(target ?? "")));
                    return;
                }

                IntPtr hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, targetPid);
                if (hSnap == IntPtr.Zero || hSnap == new IntPtr(-1)) {
                    int err = Marshal.GetLastWin32Error();
                    Console.WriteLine(string.Format("{{\"success\": false, \"targetPid\": {0}, \"errorCode\": {1}, \"error\": \"Failed to create module snapshot: {2}\"}}",
                        targetPid, err, EscapeJson(new System.ComponentModel.Win32Exception(err).Message)));
                    return;
                }

                List<string> modules = new List<string>();
                int totalCount = 0;
                try {
                    MODULEENTRY32 me = new MODULEENTRY32();
                    me.dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32));

                    if (Module32FirstW(hSnap, ref me)) {
                        do {
                            totalCount++;
                            string mName = me.szModule ?? "";
                            string mPath = me.szExePath ?? "";

                            if (!string.IsNullOrEmpty(q)) {
                                if (!mName.ToLowerInvariant().Contains(q) && !mPath.ToLowerInvariant().Contains(q)) {
                                    continue;
                                }
                            }

                            if (modules.Count < limit) {
                                string baseAddrHex = "0x" + me.modBaseAddr.ToString("X");
                                double kb = Math.Round(me.modBaseSize / 1024.0, 1);
                                modules.Add(string.Format(
                                    "{{\"moduleName\": \"{0}\", \"baseAddress\": \"{1}\", \"baseSize\": {2}, \"baseSizeKB\": {3}, \"exePath\": \"{4}\", \"globalUsage\": {5}, \"processUsage\": {6}}}",
                                    EscapeJson(mName), baseAddrHex, me.modBaseSize, kb, EscapeJson(mPath), me.GlblcntUsage, me.ProccntUsage
                                ));
                            }
                        } while (Module32NextW(hSnap, ref me));
                    }
                } finally {
                    CloseHandle(hSnap);
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"targetPid\": {0}, \"targetProcess\": \"{1}\", \"totalModules\": {2}, \"returnedCount\": {3}, \"modules\": [{4}]}}",
                    targetPid, EscapeJson(targetName), totalCount, modules.Count, string.Join(", ", modules.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void ToolHelpThreadsCmd(string target, int limit) {
            try {
                if (limit <= 0) limit = 100;
                uint targetPid = 0;
                string targetName = null;
                bool filterByPid = false;

                if (!string.IsNullOrEmpty(target) && target != "all" && target != "0") {
                    if (ResolveProcessTarget(target, out targetPid, out targetName)) {
                        filterByPid = true;
                    }
                }

                IntPtr hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
                if (hSnap == IntPtr.Zero || hSnap == new IntPtr(-1)) {
                    int err = Marshal.GetLastWin32Error();
                    Console.WriteLine(string.Format("{{\"success\": false, \"errorCode\": {0}, \"error\": \"Failed to create thread snapshot: {1}\"}}",
                        err, EscapeJson(new System.ComponentModel.Win32Exception(err).Message)));
                    return;
                }

                List<string> threads = new List<string>();
                int totalMatched = 0;
                try {
                    THREADENTRY32 te = new THREADENTRY32();
                    te.dwSize = (uint)Marshal.SizeOf(typeof(THREADENTRY32));

                    if (Thread32First(hSnap, ref te)) {
                        do {
                            if (filterByPid && te.th32OwnerProcessID != targetPid) {
                                continue;
                            }
                            totalMatched++;
                            if (threads.Count < limit) {
                                threads.Add(string.Format(
                                    "{{\"threadId\": {0}, \"ownerPid\": {1}, \"basePriority\": {2}, \"deltaPriority\": {3}}}",
                                    te.th32ThreadID, te.th32OwnerProcessID, te.tpBasePri, te.tpDeltaPri
                                ));
                            }
                        } while (Thread32Next(hSnap, ref te));
                    }
                } finally {
                    CloseHandle(hSnap);
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"targetPid\": {0}, \"targetProcess\": {1}, \"totalThreads\": {2}, \"returnedCount\": {3}, \"threads\": [{4}]}}",
                    filterByPid ? targetPid.ToString() : "null",
                    targetName != null ? "\"" + EscapeJson(targetName) + "\"" : "null",
                    totalMatched, threads.Count, string.Join(", ", threads.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        class ProcNode {
            public uint Pid;
            public uint ParentPid;
            public string Name;
            public uint Threads;
            public int PriorityBase;
            public List<ProcNode> Children = new List<ProcNode>();
        }

        static string BuildProcNodeJson(ProcNode node) {
            var childStrs = new List<string>();
            foreach (var ch in node.Children) {
                childStrs.Add(BuildProcNodeJson(ch));
            }
            return string.Format(
                "{{\"pid\": {0}, \"name\": \"{1}\", \"parentPid\": {2}, \"threads\": {3}, \"priorityBase\": {4}, \"children\": [{5}]}}",
                node.Pid, EscapeJson(node.Name), node.ParentPid, node.Threads, node.PriorityBase, string.Join(", ", childStrs.ToArray())
            );
        }

        static void ToolHelpProcessTreeCmd(uint rootPid, string search, int limit) {
            try {
                if (limit <= 0) limit = 150;
                string q = (search ?? "").Trim().ToLowerInvariant();

                IntPtr hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
                if (hSnap == IntPtr.Zero || hSnap == new IntPtr(-1)) {
                    int err = Marshal.GetLastWin32Error();
                    Console.WriteLine(string.Format("{{\"success\": false, \"errorCode\": {0}, \"error\": \"Failed to create process snapshot: {1}\"}}",
                        err, EscapeJson(new System.ComponentModel.Win32Exception(err).Message)));
                    return;
                }

                var allNodes = new Dictionary<uint, ProcNode>();
                int totalCount = 0;
                try {
                    PROCESSENTRY32 pe = new PROCESSENTRY32();
                    pe.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));

                    if (Process32FirstW(hSnap, ref pe)) {
                        do {
                            totalCount++;
                            ProcNode node = new ProcNode {
                                Pid = pe.th32ProcessID,
                                ParentPid = pe.th32ParentProcessID,
                                Name = pe.szExeFile ?? "",
                                Threads = pe.cntThreads,
                                PriorityBase = pe.pcPriClassBase
                            };
                            allNodes[node.Pid] = node;
                        } while (Process32NextW(hSnap, ref pe));
                    }
                } finally {
                    CloseHandle(hSnap);
                }

                var roots = new List<ProcNode>();
                foreach (var kvp in allNodes) {
                    ProcNode node = kvp.Value;
                    if (node.Pid == 0) continue;
                    ProcNode parent;
                    if (node.ParentPid != 0 && node.ParentPid != node.Pid && allNodes.TryGetValue(node.ParentPid, out parent)) {
                        parent.Children.Add(node);
                    } else {
                        roots.Add(node);
                    }
                }

                List<ProcNode> targetRoots = new List<ProcNode>();
                if (rootPid > 0) {
                    ProcNode targetNode;
                    if (allNodes.TryGetValue(rootPid, out targetNode)) {
                        targetRoots.Add(targetNode);
                    }
                } else {
                    targetRoots = roots;
                }

                var resultStrs = new List<string>();
                if (!string.IsNullOrEmpty(q)) {
                    foreach (var kvp in allNodes) {
                        if (kvp.Value.Name.ToLowerInvariant().Contains(q)) {
                            resultStrs.Add(BuildProcNodeJson(kvp.Value));
                            if (resultStrs.Count >= limit) break;
                        }
                    }
                } else {
                    foreach (var r in targetRoots) {
                        resultStrs.Add(BuildProcNodeJson(r));
                        if (resultStrs.Count >= limit) break;
                    }
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"totalProcesses\": {0}, \"returnedCount\": {1}, \"rootPid\": {2}, \"tree\": [{3}]}}",
                    totalCount, resultStrs.Count, rootPid, string.Join(", ", resultStrs.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows System Event Notification Service & Network Perception Subsystem (sensapi.h / netlistmgr.h)

        [StructLayout(LayoutKind.Sequential)]
        struct SENS_QOCINFO {
            public uint dwSize;
            public uint dwFlags;
            public uint dwInSpeed;
            public uint dwOutSpeed;
        }

        const uint SENS_NETWORK_ALIVE_LAN = 0x00000001;
        const uint SENS_NETWORK_ALIVE_WAN = 0x00000002;
        const uint SENS_NETWORK_ALIVE_AOL = 0x00000004;
        const uint SENS_NETWORK_ALIVE_INTERNET = 0x00000008;

        const uint SENS_QOCINFO_PATH_IS_GATEWAY = 0x00000001;

        [DllImport("sensapi.dll", SetLastError = true)]
        static extern bool IsNetworkAlive(out uint pdwFlags);

        [DllImport("sensapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool IsDestinationReachableW(string lpszDestination, ref SENS_QOCINFO lpQOCInfo);

        static void SensNetworkAliveCmd() {
            try {
                uint flags = 0;
                bool alive = false;
                int err = 0;
                try {
                    alive = IsNetworkAlive(out flags);
                    if (!alive) err = Marshal.GetLastWin32Error();
                } catch {
                    alive = NetworkInterface.GetIsNetworkAvailable();
                    flags = alive ? SENS_NETWORK_ALIVE_LAN : 0;
                }

                bool lan = (flags & SENS_NETWORK_ALIVE_LAN) != 0;
                bool wan = (flags & SENS_NETWORK_ALIVE_WAN) != 0;
                bool aol = (flags & SENS_NETWORK_ALIVE_AOL) != 0;
                bool internet = (flags & SENS_NETWORK_ALIVE_INTERNET) != 0;

                bool netAvailable = NetworkInterface.GetIsNetworkAvailable();
                if (!alive && netAvailable) {
                    alive = true;
                    lan = true;
                }

                var types = new List<string>();
                if (lan) types.Add("\"LAN\"");
                if (wan) types.Add("\"WAN\"");
                if (aol) types.Add("\"AOL\"");
                if (internet) types.Add("\"INTERNET\"");

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"isAlive\": {0}, \"rawFlags\": {1}, \"lanConnected\": {2}, \"wanConnected\": {3}, \"aolConnected\": {4}, \"internetReachable\": {5}, \"connectionTypes\": [{6}], \"networkAvailable\": {7}, \"errorCode\": {8}}}",
                    alive ? "true" : "false",
                    flags,
                    lan ? "true" : "false",
                    wan ? "true" : "false",
                    aol ? "true" : "false",
                    internet ? "true" : "false",
                    string.Join(", ", types.ToArray()),
                    netAvailable ? "true" : "false",
                    err
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void SensDestinationReachableCmd(string destination, int timeoutMs) {
            try {
                if (string.IsNullOrEmpty(destination)) {
                    destination = "8.8.8.8";
                }
                if (timeoutMs <= 0) timeoutMs = 3000;

                SENS_QOCINFO qoc = new SENS_QOCINFO();
                qoc.dwSize = (uint)Marshal.SizeOf(typeof(SENS_QOCINFO));

                bool sensReachable = false;
                int sensErr = 0;
                try {
                    sensReachable = IsDestinationReachableW(destination, ref qoc);
                    if (!sensReachable) sensErr = Marshal.GetLastWin32Error();
                } catch {
                    sensErr = Marshal.GetLastWin32Error();
                }

                bool pingSuccess = false;
                long latencyMs = -1;
                string ipStr = "";
                string pingStatus = "Unknown";
                try {
                    using (var ping = new Ping()) {
                        var reply = ping.Send(destination, timeoutMs);
                        if (reply != null) {
                            pingStatus = reply.Status.ToString();
                            if (reply.Status == IPStatus.Success) {
                                pingSuccess = true;
                                latencyMs = reply.RoundtripTime;
                                if (reply.Address != null) ipStr = reply.Address.ToString();
                            }
                        }
                    }
                } catch (Exception px) {
                    pingStatus = px.InnerException != null ? px.InnerException.Message : px.Message;
                }

                bool reachable = sensReachable || pingSuccess;
                bool isGateway = (qoc.dwFlags & SENS_QOCINFO_PATH_IS_GATEWAY) != 0;
                double inKbps = qoc.dwInSpeed / 1000.0;
                double outKbps = qoc.dwOutSpeed / 1000.0;
                double inMbps = qoc.dwInSpeed / 1000000.0;
                double outMbps = qoc.dwOutSpeed / 1000000.0;

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"destination\": \"{0}\", \"reachable\": {1}, \"sensReachable\": {2}, \"pingReachable\": {3}, \"latencyMs\": {4}, \"ipAddress\": \"{5}\", \"status\": \"{6}\", \"inSpeedBps\": {7}, \"outSpeedBps\": {8}, \"inSpeedKbps\": {9:F2}, \"outSpeedKbps\": {10:F2}, \"inSpeedMbps\": {11:F2}, \"outSpeedMbps\": {12:F2}, \"isGateway\": {13}, \"qocFlags\": {14}, \"sensErrorCode\": {15}}}",
                    EscapeJson(destination),
                    reachable ? "true" : "false",
                    sensReachable ? "true" : "false",
                    pingSuccess ? "true" : "false",
                    latencyMs,
                    EscapeJson(ipStr),
                    EscapeJson(pingStatus),
                    qoc.dwInSpeed,
                    qoc.dwOutSpeed,
                    inKbps,
                    outKbps,
                    inMbps,
                    outMbps,
                    isGateway ? "true" : "false",
                    qoc.dwFlags,
                    sensErr
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"destination\": \"{0}\", \"error\": \"{1}\"}}", EscapeJson(destination ?? ""), EscapeJson(ex.Message)));
            }
        }

        static void SensNetworkConnectivityCmd(bool includeProfiles, bool includeAdapters) {
            try {
                bool isConnected = false;
                bool isInternet = false;
                int rawConnectivity = 0;
                string ipv4Conn = "disconnected";
                string ipv6Conn = "disconnected";
                var profilesList = new List<string>();

                try {
                    Type nlmType = Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"));
                    if (nlmType != null) {
                        object nlm = Activator.CreateInstance(nlmType);
                        isConnected = (bool)nlmType.InvokeMember("IsConnected", System.Reflection.BindingFlags.GetProperty, null, nlm, null);
                        isInternet = (bool)nlmType.InvokeMember("IsConnectedToInternet", System.Reflection.BindingFlags.GetProperty, null, nlm, null);
                        rawConnectivity = (int)nlmType.InvokeMember("GetConnectivity", System.Reflection.BindingFlags.InvokeMethod, null, nlm, null);

                        if ((rawConnectivity & 0x40) != 0) ipv4Conn = "internet";
                        else if ((rawConnectivity & 0x20) != 0) ipv4Conn = "local";
                        else if ((rawConnectivity & 0x10) != 0) ipv4Conn = "subnet";

                        if ((rawConnectivity & 0x400) != 0) ipv6Conn = "internet";
                        else if ((rawConnectivity & 0x200) != 0) ipv6Conn = "local";
                        else if ((rawConnectivity & 0x100) != 0) ipv6Conn = "subnet";

                        if (includeProfiles) {
                            try {
                                System.Collections.IEnumerable networks = (System.Collections.IEnumerable)nlmType.InvokeMember("GetNetworks", System.Reflection.BindingFlags.InvokeMethod, null, nlm, new object[] { 1 });
                                foreach (object net in networks) {
                                    Type netType = net.GetType();
                                    string pName = (string)netType.InvokeMember("GetName", System.Reflection.BindingFlags.InvokeMethod, null, net, null);
                                    string pDesc = (string)netType.InvokeMember("GetDescription", System.Reflection.BindingFlags.InvokeMethod, null, net, null);
                                    int pCat = (int)netType.InvokeMember("GetCategory", System.Reflection.BindingFlags.InvokeMethod, null, net, null);
                                    int pDom = (int)netType.InvokeMember("GetDomainType", System.Reflection.BindingFlags.InvokeMethod, null, net, null);
                                    bool pConn = (bool)netType.InvokeMember("IsConnected", System.Reflection.BindingFlags.GetProperty, null, net, null);
                                    bool pNet = (bool)netType.InvokeMember("IsConnectedToInternet", System.Reflection.BindingFlags.GetProperty, null, net, null);

                                    string catStr = pCat == 1 ? "Private" : (pCat == 2 ? "DomainAuthenticated" : "Public");
                                    string domStr = pDom == 1 ? "Domain" : (pDom == 2 ? "DomainAuthenticated" : "NonDomain");

                                    profilesList.Add(string.Format(
                                        "{{\"name\": \"{0}\", \"description\": \"{1}\", \"category\": \"{2}\", \"domainType\": \"{3}\", \"isConnected\": {4}, \"isConnectedToInternet\": {5}}}",
                                        EscapeJson(pName ?? ""),
                                        EscapeJson(pDesc ?? ""),
                                        catStr,
                                        domStr,
                                        pConn ? "true" : "false",
                                        pNet ? "true" : "false"
                                    ));
                                }
                            } catch {}
                        }
                    }
                } catch {
                    isConnected = NetworkInterface.GetIsNetworkAvailable();
                    isInternet = isConnected;
                }

                var adapterList = new List<string>();
                if (includeAdapters) {
                    try {
                        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
                            if (nic.OperationalStatus != OperationalStatus.Up &&
                                nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                            var ipProps = nic.GetIPProperties();
                            var ipv4List = new List<string>();
                            var gatewayList = new List<string>();
                            var dnsList = new List<string>();

                            if (ipProps != null) {
                                foreach (var uni in ipProps.UnicastAddresses) {
                                    if (uni.Address != null && uni.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                                        ipv4List.Add("\"" + uni.Address.ToString() + "\"");
                                    }
                                }
                                foreach (var gw in ipProps.GatewayAddresses) {
                                    if (gw.Address != null) gatewayList.Add("\"" + gw.Address.ToString() + "\"");
                                }
                                foreach (var dns in ipProps.DnsAddresses) {
                                    if (dns != null) dnsList.Add("\"" + dns.ToString() + "\"");
                                }
                            }

                            adapterList.Add(string.Format(
                                "{{\"id\": \"{0}\", \"name\": \"{1}\", \"description\": \"{2}\", \"type\": \"{3}\", \"status\": \"{4}\", \"speedBps\": {5}, \"speedMbps\": {6:F1}, \"mac\": \"{7}\", \"ipv4\": [{8}], \"gateways\": [{9}], \"dns\": [{10}]}}",
                                EscapeJson(nic.Id ?? ""),
                                EscapeJson(nic.Name ?? ""),
                                EscapeJson(nic.Description ?? ""),
                                nic.NetworkInterfaceType.ToString(),
                                nic.OperationalStatus.ToString(),
                                nic.Speed,
                                nic.Speed / 1000000.0,
                                EscapeJson(nic.GetPhysicalAddress().ToString()),
                                string.Join(", ", ipv4List.ToArray()),
                                string.Join(", ", gatewayList.ToArray()),
                                string.Join(", ", dnsList.ToArray())
                            ));
                        }
                    } catch {}
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"isConnected\": {0}, \"isConnectedToInternet\": {1}, \"rawConnectivity\": {2}, \"connectivity\": {{\"ipv4\": \"{3}\", \"ipv6\": \"{4}\"}}, \"profiles\": [{5}], \"adapters\": [{6}]}}",
                    isConnected ? "true" : "false",
                    isInternet ? "true" : "false",
                    rawConnectivity,
                    ipv4Conn,
                    ipv6Conn,
                    string.Join(", ", profilesList.ToArray()),
                    string.Join(", ", adapterList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows System Time, Dynamic Time Zones & Chronometry Subsystem (timezoneapi.h / sysinfoapi.h)

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct TIME_SYSTEMTIME {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct TIME_DYNAMIC_TIME_ZONE_INFORMATION {
            public int Bias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string StandardName;
            public TIME_SYSTEMTIME StandardDate;
            public int StandardBias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DaylightName;
            public TIME_SYSTEMTIME DaylightDate;
            public int DaylightBias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string TimeZoneKeyName;
            [MarshalAs(UnmanagedType.I1)]
            public bool DynamicDaylightTimeDisabled;
        }

        const uint TIME_ZONE_ID_INVALID = 0xFFFFFFFF;
        const uint TIME_ZONE_ID_UNKNOWN = 0;
        const uint TIME_ZONE_ID_STANDARD = 1;
        const uint TIME_ZONE_ID_DAYLIGHT = 2;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetDynamicTimeZoneInformation(out TIME_DYNAMIC_TIME_ZONE_INFORMATION pTimeZoneInformation);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint EnumDynamicTimeZoneInformation(uint dwIndex, out TIME_DYNAMIC_TIME_ZONE_INFORMATION lpTimeZoneInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetSystemTimeAdjustment(out uint lpTimeAdjustment, out uint lpTimeIncrement, out bool lpTimeAdjustmentDisabled);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool QueryPerformanceFrequency(out long lpFrequency);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool QueryUnbiasedInterruptTime(out ulong UnbiasedTime);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        delegate void QueryTimePreciseDelegate(out ulong lpTimePrecise);

        static string FormatSystemTime(TIME_SYSTEMTIME st) {
            return string.Format("{{\"month\": {0}, \"dayOfWeek\": {1}, \"day\": {2}, \"hour\": {3}, \"minute\": {4}}}",
                st.wMonth, st.wDayOfWeek, st.wDay, st.wHour, st.wMinute);
        }

        static void TimeGetZoneInfoCmd(bool enumerateAll, string filter, string utcTimestamp) {
            try {
                TIME_DYNAMIC_TIME_ZONE_INFORMATION tz = new TIME_DYNAMIC_TIME_ZONE_INFORMATION();
                uint tzId = GetDynamicTimeZoneInformation(out tz);
                int totalBias = tz.Bias + (tzId == TIME_ZONE_ID_DAYLIGHT ? tz.DaylightBias : tz.StandardBias);
                double utcOffsetHours = -((double)totalBias / 60.0);
                bool isDaylightActive = (tzId == TIME_ZONE_ID_DAYLIGHT);

                string convertedLocalTime = "";
                if (!string.IsNullOrEmpty(utcTimestamp)) {
                    DateTime parsedUtc;
                    if (DateTime.TryParse(utcTimestamp, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out parsedUtc)) {
                        DateTime localConverted = parsedUtc.AddMinutes(-totalBias);
                        convertedLocalTime = localConverted.ToString("yyyy-MM-ddTHH:mm:ss.fff");
                    }
                }

                var tzList = new List<string>();
                if (enumerateAll || !string.IsNullOrEmpty(filter)) {
                    string filterLower = (filter ?? "").ToLowerInvariant();
                    for (uint i = 0; i < 200; i++) {
                        TIME_DYNAMIC_TIME_ZONE_INFORMATION enumTz = new TIME_DYNAMIC_TIME_ZONE_INFORMATION();
                        uint res = EnumDynamicTimeZoneInformation(i, out enumTz);
                        if (res != 0) break;

                        string key = enumTz.TimeZoneKeyName ?? "";
                        string std = enumTz.StandardName ?? "";
                        string dlt = enumTz.DaylightName ?? "";

                        if (!string.IsNullOrEmpty(filterLower)) {
                            if (!key.ToLowerInvariant().Contains(filterLower) &&
                                !std.ToLowerInvariant().Contains(filterLower) &&
                                !dlt.ToLowerInvariant().Contains(filterLower)) {
                                continue;
                            }
                        }

                        double offsetHours = -((double)enumTz.Bias / 60.0);
                        tzList.Add(string.Format(
                            "{{\"index\": {0}, \"keyName\": \"{1}\", \"standardName\": \"{2}\", \"daylightName\": \"{3}\", \"bias\": {4}, \"utcOffsetHours\": {5:F1}, \"dynamicDisabled\": {6}}}",
                            i, EscapeJson(key), EscapeJson(std), EscapeJson(dlt), enumTz.Bias, offsetHours, enumTz.DynamicDaylightTimeDisabled ? "true" : "false"
                        ));
                    }
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"timeZoneKeyName\": \"{0}\", \"standardName\": \"{1}\", \"daylightName\": \"{2}\", \"timeZoneId\": {3}, \"isDaylightSavingsActive\": {4}, \"baseBiasMinutes\": {5}, \"totalBiasMinutes\": {6}, \"utcOffsetHours\": {7:F2}, \"dynamicDaylightTimeDisabled\": {8}, \"standardTransition\": {9}, \"daylightTransition\": {10}, \"convertedLocalTime\": \"{11}\", \"enumeratedCount\": {12}, \"timeZones\": [{13}]}}",
                    EscapeJson(tz.TimeZoneKeyName ?? ""),
                    EscapeJson(tz.StandardName ?? ""),
                    EscapeJson(tz.DaylightName ?? ""),
                    tzId,
                    isDaylightActive ? "true" : "false",
                    tz.Bias,
                    totalBias,
                    utcOffsetHours,
                    tz.DynamicDaylightTimeDisabled ? "true" : "false",
                    FormatSystemTime(tz.StandardDate),
                    FormatSystemTime(tz.DaylightDate),
                    EscapeJson(convertedLocalTime),
                    tzList.Count,
                    string.Join(", ", tzList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void TimeGetChronometryCmd() {
            try {
                long qpc = 0;
                long freq = 1;
                QueryPerformanceCounter(out qpc);
                QueryPerformanceFrequency(out freq);
                if (freq <= 0) freq = 1;
                double tickNs = (1000000000.0) / (double)freq;

                long preciseFileTime = 0;
                string utcIso = "";
                try {
                    GetSystemTimePreciseAsFileTime(out preciseFileTime);
                    DateTime utcDt = DateTime.FromFileTimeUtc(preciseFileTime);
                    utcIso = utcDt.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
                } catch {
                    utcIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
                }

                ulong unbiased = 0;
                bool unbiasedSuccess = false;
                try {
                    unbiasedSuccess = QueryUnbiasedInterruptTime(out unbiased);
                } catch {}
                double unbiasedSeconds = unbiased / 10000000.0;

                ulong interruptPrecise = unbiased;
                bool interruptPreciseFound = false;
                try {
                    IntPtr hKernel = GetModuleHandle("kernel32.dll");
                    IntPtr pIntPrecise = GetProcAddress(hKernel, "QueryInterruptTimePrecise");
                    if (pIntPrecise != IntPtr.Zero) {
                        var fn = (QueryTimePreciseDelegate)Marshal.GetDelegateForFunctionPointer(pIntPrecise, typeof(QueryTimePreciseDelegate));
                        fn(out interruptPrecise);
                        interruptPreciseFound = true;
                    }
                } catch {}

                ulong uptimeMs = 0;
                try {
                    uptimeMs = GetTickCount64();
                } catch {
                    uptimeMs = (ulong)Environment.TickCount;
                }
                double uptimeHours = uptimeMs / 3600000.0;

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"qpcTicks\": {0}, \"qpcFrequencyHz\": {1}, \"tickResolutionNanoseconds\": {2}, \"preciseFileTime\": {3}, \"utcTimestamp\": \"{4}\", \"unbiasedInterruptTime100ns\": {5}, \"unbiasedUptimeSeconds\": {6}, \"interruptTimePrecise100ns\": {7}, \"hasPreciseInterrupt\": {8}, \"uptimeMs\": {9}, \"uptimeHours\": {10}}}",
                    qpc, freq, tickNs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    preciseFileTime, EscapeJson(utcIso), unbiased,
                    unbiasedSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    interruptPrecise, interruptPreciseFound ? "true" : "false", uptimeMs,
                    uptimeHours.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void TimeGetAdjustmentCmd() {
            try {
                uint timeAdjustment = 0;
                uint timeIncrement = 0;
                bool timeAdjustmentDisabled = false;

                bool adjSuccess = GetSystemTimeAdjustment(out timeAdjustment, out timeIncrement, out timeAdjustmentDisabled);
                int err = adjSuccess ? 0 : Marshal.GetLastWin32Error();

                double nominalTickMs = (timeIncrement > 0 ? timeIncrement : 156250) / 10000.0;
                double adjustmentTickMs = (timeAdjustment > 0 ? timeAdjustment : 156250) / 10000.0;
                double skewPpm = 0.0;
                if (timeIncrement > 0 && timeAdjustment > 0) {
                    skewPpm = (((double)timeAdjustment - (double)timeIncrement) / (double)timeIncrement) * 1000000.0;
                }

                string w32timeStatus = "NotInstalled";
                try {
                    using (var sc = new ServiceController("w32time")) {
                        w32timeStatus = sc.Status.ToString();
                    }
                } catch {}

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"apiSuccess\": {0}, \"timeAdjustment100ns\": {1}, \"timeIncrement100ns\": {2}, \"timeAdjustmentDisabled\": {3}, \"nominalTickMs\": {4}, \"adjustmentTickMs\": {5}, \"driftRatePpm\": {6}, \"w32timeServiceStatus\": \"{7}\", \"errorCode\": {8}}}",
                    adjSuccess ? "true" : "false",
                    timeAdjustment,
                    timeIncrement,
                    timeAdjustmentDisabled ? "true" : "false",
                    nominalTickMs.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                    adjustmentTickMs.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                    skewPpm.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    EscapeJson(w32timeStatus),
                    err
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows Power Policy, Execution State & Battery Subsystem (powrprof.h / powerbase.h / poclass.h)

        const uint ES_SYSTEM_REQUIRED = 0x00000001;
        const uint ES_DISPLAY_REQUIRED = 0x00000002;
        const uint ES_USER_PRESENT = 0x00000004;
        const uint ES_AWAYMODE_REQUIRED = 0x00000040;
        const uint ES_CONTINUOUS = 0x80000000;

        const uint POWER_ACCESS_SCHEME = 16;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("powrprof.dll")]
        static extern uint PowerEnumerate(IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid, uint AccessFlags, uint Index, byte[] Buffer, ref uint BufferSize);

        [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
        static extern uint PowerReadDescription(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid, IntPtr PowerSettingGuid, StringBuilder Buffer, ref uint BufferSize);

        [StructLayout(LayoutKind.Sequential)]
        struct POWER_SYSTEM_BATTERY_STATE {
            [MarshalAs(UnmanagedType.I1)] public bool AcOnLine;
            [MarshalAs(UnmanagedType.I1)] public bool BatteryPresent;
            [MarshalAs(UnmanagedType.I1)] public bool Charging;
            [MarshalAs(UnmanagedType.I1)] public bool Discharging;
            public byte Spare1;
            public byte Spare2;
            public byte Spare3;
            public byte Spare4;
            public uint Tag;
            public uint MaxCapacity;
            public uint RemainingCapacity;
            public int Rate;
            public uint EstimatedTime;
            public uint DefaultAlert1;
            public uint DefaultAlert2;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POWER_SYSTEM_POWER_CAPABILITIES {
            [MarshalAs(UnmanagedType.I1)] public bool PowerButtonPresent;
            [MarshalAs(UnmanagedType.I1)] public bool SleepButtonPresent;
            [MarshalAs(UnmanagedType.I1)] public bool LidPresent;
            [MarshalAs(UnmanagedType.I1)] public bool SystemS1;
            [MarshalAs(UnmanagedType.I1)] public bool SystemS2;
            [MarshalAs(UnmanagedType.I1)] public bool SystemS3;
            [MarshalAs(UnmanagedType.I1)] public bool SystemS4;
            [MarshalAs(UnmanagedType.I1)] public bool SystemS5;
            [MarshalAs(UnmanagedType.I1)] public bool HiberFilePresent;
            [MarshalAs(UnmanagedType.I1)] public bool FullWake;
            [MarshalAs(UnmanagedType.I1)] public bool VideoDimPresent;
            [MarshalAs(UnmanagedType.I1)] public bool ApmPresent;
            [MarshalAs(UnmanagedType.I1)] public bool UpsPresent;
            [MarshalAs(UnmanagedType.I1)] public bool ThermalControl;
            [MarshalAs(UnmanagedType.I1)] public bool ProcessorThrottle;
            public byte ProcessorMinThrottle;
            public byte ProcessorMaxThrottle;
            [MarshalAs(UnmanagedType.I1)] public bool FastSystemS4;
            [MarshalAs(UnmanagedType.I1)] public bool Hiberboot;
            [MarshalAs(UnmanagedType.I1)] public bool WakeAlarmPresent;
            [MarshalAs(UnmanagedType.I1)] public bool AoAc;
            [MarshalAs(UnmanagedType.I1)] public bool DiskSpinDown;
            public byte HiberFileType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] Spare2;
            public uint SystemBatteriesPresent;
            public int DefaultLowLatencyWake;
        }

        static void PowerSchemesListCmd() {
            try {
                IntPtr pActive;
                Guid activeGuid = Guid.Empty;
                if (PowerGetActiveScheme(IntPtr.Zero, out pActive) == 0 && pActive != IntPtr.Zero) {
                    activeGuid = (Guid)Marshal.PtrToStructure(pActive, typeof(Guid));
                }

                string activeName = "";
                var schemeList = new List<string>();
                uint idx = 0;
                while (idx < 64) {
                    byte[] buf = new byte[16];
                    uint bufSize = (uint)buf.Length;
                    uint res = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, POWER_ACCESS_SCHEME, idx, buf, ref bufSize);
                    if (res != 0) break;

                    Guid g = new Guid(buf);
                    StringBuilder sbName = new StringBuilder(256);
                    uint nameSize = (uint)sbName.Capacity * 2;
                    string name = "";
                    if (PowerReadFriendlyName(IntPtr.Zero, ref g, IntPtr.Zero, IntPtr.Zero, sbName, ref nameSize) == 0) {
                        name = sbName.ToString();
                    }

                    StringBuilder sbDesc = new StringBuilder(512);
                    uint descSize = (uint)sbDesc.Capacity * 2;
                    string desc = "";
                    if (PowerReadDescription(IntPtr.Zero, ref g, IntPtr.Zero, IntPtr.Zero, sbDesc, ref descSize) == 0) {
                        desc = sbDesc.ToString();
                    }

                    bool isActive = (g == activeGuid);
                    if (isActive) activeName = name;

                    schemeList.Add(string.Format(
                        "{{\"index\": {0}, \"guid\": \"{1}\", \"friendlyName\": \"{2}\", \"description\": \"{3}\", \"isActive\": {4}}}",
                        idx, g.ToString(), EscapeJson(name), EscapeJson(desc), isActive ? "true" : "false"
                    ));
                    idx++;
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"activeSchemeGuid\": \"{0}\", \"activeSchemeName\": \"{1}\", \"schemeCount\": {2}, \"schemes\": [{3}]}}",
                    activeGuid.ToString(), EscapeJson(activeName), schemeList.Count, string.Join(", ", schemeList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void PowerExecutionStateCmd(bool systemRequired, bool displayRequired, bool awayMode, bool continuous, bool restore) {
            try {
                uint flags = 0;
                var flagNames = new List<string>();

                if (restore) {
                    flags = ES_CONTINUOUS;
                    flagNames.Add("\"ES_CONTINUOUS\"");
                } else {
                    if (continuous) {
                        flags |= ES_CONTINUOUS;
                        flagNames.Add("\"ES_CONTINUOUS\"");
                    }
                    if (systemRequired) {
                        flags |= ES_SYSTEM_REQUIRED;
                        flagNames.Add("\"ES_SYSTEM_REQUIRED\"");
                    }
                    if (displayRequired) {
                        flags |= ES_DISPLAY_REQUIRED;
                        flagNames.Add("\"ES_DISPLAY_REQUIRED\"");
                    }
                    if (awayMode) {
                        flags |= ES_AWAYMODE_REQUIRED;
                        flagNames.Add("\"ES_AWAYMODE_REQUIRED\"");
                    }
                }

                uint prev = SetThreadExecutionState(flags);
                bool success = (prev != 0);

                string stateDesc = restore ? "DEFAULT_OS_POLICY_RESTORED" :
                    ((flags & ES_SYSTEM_REQUIRED) != 0 ? "KEEP_AWAKE_ACTIVE" : "EXECUTION_STATE_ASSERTED");

                Console.WriteLine(string.Format(
                    "{{\"success\": {0}, \"state\": \"{1}\", \"requestedFlagsHex\": \"0x{2:X8}\", \"previousStateHex\": \"0x{3:X8}\", \"appliedFlags\": [{4}], \"isSystemRequired\": {5}, \"isDisplayRequired\": {6}, \"isAwayMode\": {7}, \"isContinuous\": {8}}}",
                    success ? "true" : "false",
                    stateDesc,
                    flags,
                    prev,
                    string.Join(", ", flagNames.ToArray()),
                    (flags & ES_SYSTEM_REQUIRED) != 0 ? "true" : "false",
                    (flags & ES_DISPLAY_REQUIRED) != 0 ? "true" : "false",
                    (flags & ES_AWAYMODE_REQUIRED) != 0 ? "true" : "false",
                    (flags & ES_CONTINUOUS) != 0 ? "true" : "false"
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void PowerHardwareTelemetryCmd() {
            try {
                int coreCount = Environment.ProcessorCount;
                int structSize = Marshal.SizeOf(typeof(PROCESSOR_POWER_INFORMATION));
                IntPtr pOut = Marshal.AllocHGlobal(structSize * coreCount);
                var coreList = new List<string>();
                double totalMhz = 0;
                bool throttled = false;

                try {
                    int status = CallNtPowerInformation(11, IntPtr.Zero, 0, pOut, (uint)(structSize * coreCount));
                    if (status == 0) {
                        for (int i = 0; i < coreCount; i++) {
                            IntPtr pItem = new IntPtr(pOut.ToInt64() + i * structSize);
                            var info = (PROCESSOR_POWER_INFORMATION)Marshal.PtrToStructure(pItem, typeof(PROCESSOR_POWER_INFORMATION));
                            totalMhz += info.CurrentMhz;
                            if (info.MhzLimit < info.MaxMhz) throttled = true;

                            coreList.Add(string.Format(
                                "{{\"core\": {0}, \"currentMhz\": {1}, \"maxMhz\": {2}, \"mhzLimit\": {3}, \"currentIdleState\": {4}, \"maxIdleState\": {5}}}",
                                info.Number, info.CurrentMhz, info.MaxMhz, info.MhzLimit, info.CurrentIdleState, info.MaxIdleState
                            ));
                        }
                    }
                } finally {
                    Marshal.FreeHGlobal(pOut);
                }
                double avgMhz = coreCount > 0 ? (totalMhz / coreCount) : 0;

                int batSize = Marshal.SizeOf(typeof(POWER_SYSTEM_BATTERY_STATE));
                IntPtr pBat = Marshal.AllocHGlobal(batSize);
                bool acOnLine = true;
                bool batteryPresent = false;
                bool charging = false;
                bool discharging = false;
                uint maxCap = 0;
                uint remCap = 0;
                int rateMw = 0;
                uint estSec = 0;

                try {
                    int status = CallNtPowerInformation(5, IntPtr.Zero, 0, pBat, (uint)batSize);
                    if (status == 0) {
                        var bat = (POWER_SYSTEM_BATTERY_STATE)Marshal.PtrToStructure(pBat, typeof(POWER_SYSTEM_BATTERY_STATE));
                        acOnLine = bat.AcOnLine;
                        batteryPresent = bat.BatteryPresent;
                        charging = bat.Charging;
                        discharging = bat.Discharging;
                        maxCap = bat.MaxCapacity;
                        remCap = bat.RemainingCapacity;
                        rateMw = bat.Rate;
                        estSec = bat.EstimatedTime;
                    }
                } finally {
                    Marshal.FreeHGlobal(pBat);
                }

                int capSize = Marshal.SizeOf(typeof(POWER_SYSTEM_POWER_CAPABILITIES));
                IntPtr pCap = Marshal.AllocHGlobal(capSize);
                var sleepList = new List<string>();
                bool thermalCtrl = false;
                bool procThrottle = false;

                try {
                    int status = CallNtPowerInformation(4, IntPtr.Zero, 0, pCap, (uint)capSize);
                    if (status == 0) {
                        var cap = (POWER_SYSTEM_POWER_CAPABILITIES)Marshal.PtrToStructure(pCap, typeof(POWER_SYSTEM_POWER_CAPABILITIES));
                        if (cap.SystemS1) sleepList.Add("\"S1\"");
                        if (cap.SystemS2) sleepList.Add("\"S2\"");
                        if (cap.SystemS3) sleepList.Add("\"S3_Standby\"");
                        if (cap.SystemS4) sleepList.Add("\"S4_Hibernate\"");
                        if (cap.SystemS5) sleepList.Add("\"S5_Shutdown\"");
                        thermalCtrl = cap.ThermalControl;
                        procThrottle = cap.ProcessorThrottle;
                    }
                } finally {
                    Marshal.FreeHGlobal(pCap);
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"cpu\": {{\"logicalCoreCount\": {0}, \"avgCurrentMhz\": {1}, \"throttlingDetected\": {2}, \"cores\": [{3}]}}, \"battery\": {{\"acOnLine\": {4}, \"batteryPresent\": {5}, \"charging\": {6}, \"discharging\": {7}, \"maxCapacityMWh\": {8}, \"remainingCapacityMWh\": {9}, \"rateMW\": {10}, \"estimatedTimeSeconds\": {11}}}, \"capabilities\": {{\"sleepStatesSupported\": [{12}], \"thermalControl\": {13}, \"processorThrottle\": {14}}}}}",
                    coreCount,
                    avgMhz.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                    throttled ? "true" : "false",
                    string.Join(", ", coreList.ToArray()),
                    acOnLine ? "true" : "false",
                    batteryPresent ? "true" : "false",
                    charging ? "true" : "false",
                    discharging ? "true" : "false",
                    maxCap,
                    remCap,
                    rateMw,
                    estSec,
                    string.Join(", ", sleepList.ToArray()),
                    thermalCtrl ? "true" : "false",
                    procThrottle ? "true" : "false"
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows Network Management, SMB Shares & Local Accounts Subsystem (netapi32.dll / lm.h)

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetApiBufferFree(IntPtr Buffer);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetGetJoinInformation([MarshalAs(UnmanagedType.LPWStr)] string lpServer, out IntPtr lpNameBuffer, out int BufferType);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHARE_INFO_2 {
            public string shi2_netname;
            public uint shi2_type;
            public string shi2_remark;
            public uint shi2_permissions;
            public uint shi2_max_uses;
            public uint shi2_current_uses;
            public string shi2_path;
            public string shi2_passwd;
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetShareEnum([MarshalAs(UnmanagedType.LPWStr)] string servername, uint level, ref IntPtr bufptr, uint prefmaxlen, out uint entriesread, out uint totalentries, ref uint resume_handle);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetShareGetInfo([MarshalAs(UnmanagedType.LPWStr)] string servername, [MarshalAs(UnmanagedType.LPWStr)] string netname, uint level, out IntPtr bufptr);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SESSION_INFO_1 {
            public string sesi1_cname;
            public string sesi1_username;
            public uint sesi1_num_opens;
            public uint sesi1_time;
            public uint sesi1_idle_time;
            public uint sesi1_user_flags;
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetSessionEnum([MarshalAs(UnmanagedType.LPWStr)] string servername, [MarshalAs(UnmanagedType.LPWStr)] string UncClientName, [MarshalAs(UnmanagedType.LPWStr)] string username, uint level, ref IntPtr bufptr, uint prefmaxlen, out uint entriesread, out uint totalentries, ref uint resume_handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct FILE_INFO_3 {
            public uint fi3_id;
            public uint fi3_permissions;
            public uint fi3_num_locks;
            public string fi3_pathname;
            public string fi3_username;
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetFileEnum([MarshalAs(UnmanagedType.LPWStr)] string servername, [MarshalAs(UnmanagedType.LPWStr)] string basepath, [MarshalAs(UnmanagedType.LPWStr)] string username, uint level, ref IntPtr bufptr, uint prefmaxlen, out uint entriesread, out uint totalentries, ref uint resume_handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct LOCALGROUP_INFO_1 {
            public string lgrpi1_name;
            public string lgrpi1_comment;
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetLocalGroupEnum([MarshalAs(UnmanagedType.LPWStr)] string servername, uint level, ref IntPtr bufptr, uint prefmaxlen, out uint entriesread, out uint totalentries, ref uint resumehandle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct LOCALGROUP_MEMBERS_INFO_1 {
            public IntPtr lgrmi1_sid;
            public int lgrmi1_sidusage;
            public string lgrmi1_name;
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetLocalGroupGetMembers([MarshalAs(UnmanagedType.LPWStr)] string servername, [MarshalAs(UnmanagedType.LPWStr)] string localgroupname, uint level, ref IntPtr bufptr, uint prefmaxlen, out uint entriesread, out uint totalentries, ref uint resumehandle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct USER_INFO_1 {
            public string usri1_name;
            public string usri1_password;
            public uint usri1_password_age;
            public uint usri1_priv;
            public string usri1_home_dir;
            public string usri1_comment;
            public uint usri1_flags;
            public string usri1_script_path;
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetUserEnum([MarshalAs(UnmanagedType.LPWStr)] string servername, uint level, uint filter, ref IntPtr bufptr, uint prefmaxlen, out uint entriesread, out uint totalentries, ref uint resume_handle);

        static void NetSharesCmd(string targetShare, string typeFilter) {
            try {
                string serverName = Environment.MachineName;
                string filter = (typeFilter ?? "all").Trim().ToLowerInvariant();
                var list = new List<string>();

                if (!string.IsNullOrEmpty(targetShare)) {
                    IntPtr pSingle = IntPtr.Zero;
                    int singleRes = NetShareGetInfo(null, targetShare, 2, out pSingle);
                    if (singleRes == 0 && pSingle != IntPtr.Zero) {
                        try {
                            var sh = (SHARE_INFO_2)Marshal.PtrToStructure(pSingle, typeof(SHARE_INFO_2));
                            FormatShareJson(sh, list, filter);
                        } finally {
                            NetApiBufferFree(pSingle);
                        }
                    } else {
                        Console.WriteLine(string.Format("{{\"success\": false, \"server\": \"{0}\", \"error\": \"Share not found or access denied (Win32 error: {1})\"}}",
                            EscapeJson(serverName), singleRes));
                        return;
                    }
                } else {
                    IntPtr pBuf = IntPtr.Zero;
                    uint entriesRead, totalEntries, resume = 0;
                    int res = NetShareEnum(null, 2, ref pBuf, 0xFFFFFFFF, out entriesRead, out totalEntries, ref resume);
                    if (res == 0 && pBuf != IntPtr.Zero) {
                        try {
                            int sz = Marshal.SizeOf(typeof(SHARE_INFO_2));
                            for (int i = 0; i < entriesRead; i++) {
                                IntPtr pItem = new IntPtr(pBuf.ToInt64() + i * sz);
                                var sh = (SHARE_INFO_2)Marshal.PtrToStructure(pItem, typeof(SHARE_INFO_2));
                                FormatShareJson(sh, list, filter);
                            }
                        } finally {
                            NetApiBufferFree(pBuf);
                        }
                    } else {
                        Console.WriteLine(string.Format("{{\"success\": false, \"server\": \"{0}\", \"error\": \"NetShareEnum failed with Win32 error: {1}\"}}",
                            EscapeJson(serverName), res));
                        return;
                    }
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"server\": \"{0}\", \"typeFilter\": \"{1}\", \"count\": {2}, \"shares\": [{3}]}}",
                    EscapeJson(serverName), EscapeJson(filter), list.Count, string.Join(", ", list.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void FormatShareJson(SHARE_INFO_2 sh, List<string> list, string filter) {
            uint rawType = sh.shi2_type;
            bool isSpecial = (rawType & 0x80000000) != 0;
            uint baseType = rawType & 0x0FFFFFFF;

            string typeName = "Unknown";
            if (baseType == 0) typeName = isSpecial ? "SpecialDiskTree" : "DiskTree";
            else if (baseType == 1) typeName = isSpecial ? "SpecialPrintQueue" : "PrintQueue";
            else if (baseType == 2) typeName = isSpecial ? "SpecialDevice" : "Device";
            else if (baseType == 3) typeName = isSpecial ? "SpecialIPC" : "IPC";

            if (filter == "disk" && baseType != 0) return;
            if (filter == "ipc" && baseType != 3) return;
            if (filter == "print" && baseType != 1) return;
            if (filter == "special" && !isSpecial) return;

            list.Add(string.Format(
                "{{\"name\": \"{0}\", \"path\": \"{1}\", \"type\": \"{2}\", \"typeRaw\": {3}, \"isSpecial\": {4}, \"comment\": \"{5}\", \"currentUses\": {6}, \"maxUses\": {7}, \"permissions\": {8}}}",
                EscapeJson(sh.shi2_netname ?? ""),
                EscapeJson(sh.shi2_path ?? ""),
                typeName,
                rawType,
                isSpecial ? "true" : "false",
                EscapeJson(sh.shi2_remark ?? ""),
                sh.shi2_current_uses,
                (int)sh.shi2_max_uses,
                sh.shi2_permissions
            ));
        }

        static void NetSessionsCmd(string clientFilter, string userFilter, bool includeFiles) {
            try {
                string serverName = Environment.MachineName;
                var sessList = new List<string>();
                var fileList = new List<string>();

                IntPtr pSess = IntPtr.Zero;
                uint sRead, sTotal, sResume = 0;
                string cFilter = string.IsNullOrEmpty(clientFilter) ? null : clientFilter;
                string uFilter = string.IsNullOrEmpty(userFilter) ? null : userFilter;

                int sRes = NetSessionEnum(null, cFilter, uFilter, 1, ref pSess, 0xFFFFFFFF, out sRead, out sTotal, ref sResume);
                if (sRes == 0 && pSess != IntPtr.Zero) {
                    try {
                        int sz = Marshal.SizeOf(typeof(SESSION_INFO_1));
                        for (int i = 0; i < sRead; i++) {
                            IntPtr pItem = new IntPtr(pSess.ToInt64() + i * sz);
                            var s = (SESSION_INFO_1)Marshal.PtrToStructure(pItem, typeof(SESSION_INFO_1));
                            sessList.Add(string.Format(
                                "{{\"clientName\": \"{0}\", \"userName\": \"{1}\", \"numOpens\": {2}, \"activeTimeSeconds\": {3}, \"idleTimeSeconds\": {4}, \"userFlags\": {5}}}",
                                EscapeJson(s.sesi1_cname ?? ""),
                                EscapeJson(s.sesi1_username ?? ""),
                                s.sesi1_num_opens,
                                s.sesi1_time,
                                s.sesi1_idle_time,
                                s.sesi1_user_flags
                            ));
                        }
                    } finally {
                        NetApiBufferFree(pSess);
                    }
                }

                if (includeFiles) {
                    IntPtr pFiles = IntPtr.Zero;
                    uint fRead, fTotal, fResume = 0;
                    int fRes = NetFileEnum(null, null, uFilter, 3, ref pFiles, 0xFFFFFFFF, out fRead, out fTotal, ref fResume);
                    if (fRes == 0 && pFiles != IntPtr.Zero) {
                        try {
                            int fSz = Marshal.SizeOf(typeof(FILE_INFO_3));
                            for (int i = 0; i < fRead; i++) {
                                IntPtr pItem = new IntPtr(pFiles.ToInt64() + i * fSz);
                                var f = (FILE_INFO_3)Marshal.PtrToStructure(pItem, typeof(FILE_INFO_3));
                                fileList.Add(string.Format(
                                    "{{\"fileId\": {0}, \"path\": \"{1}\", \"userName\": \"{2}\", \"numLocks\": {3}, \"permissions\": {4}}}",
                                    f.fi3_id,
                                    EscapeJson(f.fi3_pathname ?? ""),
                                    EscapeJson(f.fi3_username ?? ""),
                                    f.fi3_num_locks,
                                    f.fi3_permissions
                                ));
                            }
                        } finally {
                            NetApiBufferFree(pFiles);
                        }
                    }
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"server\": \"{0}\", \"sessionCount\": {1}, \"sessions\": [{2}], \"openFileCount\": {3}, \"openFiles\": [{4}]}}",
                    EscapeJson(serverName),
                    sessList.Count,
                    string.Join(", ", sessList.ToArray()),
                    fileList.Count,
                    string.Join(", ", fileList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void NetAccountsCmd(bool includeUsers, bool includeGroups, string targetGroup) {
            try {
                string serverName = Environment.MachineName;

                // 1. Join Information
                IntPtr pJoinName = IntPtr.Zero;
                int joinType = 0;
                string domainOrWorkgroup = "";
                string joinStatus = "Unknown";
                int jRes = NetGetJoinInformation(null, out pJoinName, out joinType);
                if (jRes == 0 && pJoinName != IntPtr.Zero) {
                    try {
                        domainOrWorkgroup = Marshal.PtrToStringUni(pJoinName) ?? "";
                        if (joinType == 1) joinStatus = "Unjoined";
                        else if (joinType == 2) joinStatus = "Workgroup";
                        else if (joinType == 3) joinStatus = "Domain";
                    } finally {
                        NetApiBufferFree(pJoinName);
                    }
                }

                // 2. Users
                var usersList = new List<string>();
                if (includeUsers) {
                    IntPtr pUsers = IntPtr.Zero;
                    uint uRead, uTotal, uResume = 0;
                    int uRes = NetUserEnum(null, 1, 0x0002 /* FILTER_NORMAL_ACCOUNT */, ref pUsers, 0xFFFFFFFF, out uRead, out uTotal, ref uResume);
                    if (uRes == 0 && pUsers != IntPtr.Zero) {
                        try {
                            int uSz = Marshal.SizeOf(typeof(USER_INFO_1));
                            for (int i = 0; i < uRead; i++) {
                                IntPtr pItem = new IntPtr(pUsers.ToInt64() + i * uSz);
                                var u = (USER_INFO_1)Marshal.PtrToStructure(pItem, typeof(USER_INFO_1));
                                string priv = "Guest";
                                if (u.usri1_priv == 1) priv = "User";
                                else if (u.usri1_priv == 2) priv = "Admin";

                                bool disabled = (u.usri1_flags & 0x0001) != 0;
                                bool pwdNotReqd = (u.usri1_flags & 0x0020) != 0;
                                bool lockedOut = (u.usri1_flags & 0x0010) != 0;

                                usersList.Add(string.Format(
                                    "{{\"name\": \"{0}\", \"privilege\": \"{1}\", \"privilegeLevel\": {2}, \"accountDisabled\": {3}, \"passwordNotRequired\": {4}, \"accountLockedOut\": {5}, \"passwordAgeSeconds\": {6}, \"comment\": \"{7}\", \"homeDir\": \"{8}\"}}",
                                    EscapeJson(u.usri1_name ?? ""),
                                    priv,
                                    u.usri1_priv,
                                    disabled ? "true" : "false",
                                    pwdNotReqd ? "true" : "false",
                                    lockedOut ? "true" : "false",
                                    u.usri1_password_age,
                                    EscapeJson(u.usri1_comment ?? ""),
                                    EscapeJson(u.usri1_home_dir ?? "")
                                ));
                            }
                        } finally {
                            NetApiBufferFree(pUsers);
                        }
                    }
                }

                // 3. Local Groups
                var grpList = new List<string>();
                if (includeGroups) {
                    IntPtr pGroups = IntPtr.Zero;
                    uint gRead, gTotal, gResume = 0;
                    int gRes = NetLocalGroupEnum(null, 1, ref pGroups, 0xFFFFFFFF, out gRead, out gTotal, ref gResume);
                    if (gRes == 0 && pGroups != IntPtr.Zero) {
                        try {
                            int gSz = Marshal.SizeOf(typeof(LOCALGROUP_INFO_1));
                            for (int i = 0; i < gRead; i++) {
                                IntPtr pItem = new IntPtr(pGroups.ToInt64() + i * gSz);
                                var g = (LOCALGROUP_INFO_1)Marshal.PtrToStructure(pItem, typeof(LOCALGROUP_INFO_1));
                                grpList.Add(string.Format(
                                    "{{\"name\": \"{0}\", \"comment\": \"{1}\"}}",
                                    EscapeJson(g.lgrpi1_name ?? ""),
                                    EscapeJson(g.lgrpi1_comment ?? "")
                                ));
                            }
                        } finally {
                            NetApiBufferFree(pGroups);
                        }
                    }
                }

                // 4. Group Members
                var memList = new List<string>();
                string resolvedTargetGroup = string.IsNullOrEmpty(targetGroup) ? "Administrators" : targetGroup;
                IntPtr pMem = IntPtr.Zero;
                uint mRead, mTotal, mResume = 0;
                int mRes = NetLocalGroupGetMembers(null, resolvedTargetGroup, 1, ref pMem, 0xFFFFFFFF, out mRead, out mTotal, ref mResume);
                if (mRes == 0 && pMem != IntPtr.Zero) {
                    try {
                        int mSz = Marshal.SizeOf(typeof(LOCALGROUP_MEMBERS_INFO_1));
                        for (int i = 0; i < mRead; i++) {
                            IntPtr pItem = new IntPtr(pMem.ToInt64() + i * mSz);
                            var m = (LOCALGROUP_MEMBERS_INFO_1)Marshal.PtrToStructure(pItem, typeof(LOCALGROUP_MEMBERS_INFO_1));
                            memList.Add(string.Format("\"{0}\"", EscapeJson(m.lgrmi1_name ?? "")));
                        }
                    } finally {
                        NetApiBufferFree(pMem);
                    }
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"server\": \"{0}\", \"joinInfo\": {{\"joinStatus\": \"{1}\", \"joinStatusCode\": {2}, \"domainOrWorkgroup\": \"{3}\"}}, \"userCount\": {4}, \"users\": [{5}], \"groupCount\": {6}, \"groups\": [{7}], \"targetGroup\": \"{8}\", \"targetGroupMembers\": [{9}]}}",
                    EscapeJson(serverName),
                    joinStatus,
                    joinType,
                    EscapeJson(domainOrWorkgroup),
                    usersList.Count,
                    string.Join(", ", usersList.ToArray()),
                    grpList.Count,
                    string.Join(", ", grpList.ToArray()),
                    EscapeJson(resolvedTargetGroup),
                    string.Join(", ", memList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows Virtual Memory, Heap Allocations & Working Set Subsystem (heapapi.h / memoryapi.h)

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_BASIC_INFORMATION64 {
            public ulong BaseAddress;
            public ulong AllocationBase;
            public uint AllocationProtect;
            public uint __alignment1;
            public ulong RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
            public uint __alignment2;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION64 lpBuffer, uint dwLength);

        [StructLayout(LayoutKind.Sequential)]
        struct HEAP_SUMMARY {
            public uint cb;
            public UIntPtr cbAllocated;
            public UIntPtr cbCommitted;
            public UIntPtr cbReserved;
            public UIntPtr cbMaxReserve;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetProcessHeap();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetProcessHeaps(uint NumberOfHeaps, [Out] IntPtr[] ProcessHeaps);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool HeapSummary(IntPtr hHeap, uint dwFlags, out HEAP_SUMMARY lpSummary);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetProcessWorkingSetSizeEx(IntPtr hProcess, out UIntPtr lpMinimumWorkingSetSize, out UIntPtr lpMaximumWorkingSetSize, out uint Flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetProcessWorkingSetSizeEx(IntPtr hProcess, UIntPtr dwMinimumWorkingSetSize, UIntPtr dwMaximumWorkingSetSize, uint Flags);

        static void MemoryVirtualQueryCmd(int targetPid, int maxRegions, string stateFilter) {
            try {
                Process proc = null;
                bool isSelf = (targetPid <= 0);
                if (isSelf) {
                    proc = Process.GetCurrentProcess();
                } else {
                    proc = Process.GetProcessById(targetPid);
                }

                int limit = Math.Max(1, Math.Min(maxRegions <= 0 ? 50 : maxRegions, 200));
                string filter = (stateFilter ?? "commit").Trim().ToLowerInvariant();

                var regionList = new List<string>();
                ulong totalCommitted = 0;
                ulong totalReserved = 0;
                ulong totalImage = 0;
                ulong totalMapped = 0;
                ulong totalPrivate = 0;
                int regionsSampled = 0;

                MEMORY_BASIC_INFORMATION64 mbi;
                IntPtr addr = IntPtr.Zero;
                int structSize = Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION64));
                IntPtr hProc = proc.Handle;

                while (VirtualQueryEx(hProc, addr, out mbi, (uint)structSize) != 0) {
                    regionsSampled++;

                    string stateStr = "Unknown";
                    if (mbi.State == 0x1000) { stateStr = "MEM_COMMIT"; totalCommitted += mbi.RegionSize; }
                    else if (mbi.State == 0x2000) { stateStr = "MEM_RESERVE"; totalReserved += mbi.RegionSize; }
                    else if (mbi.State == 0x10000) { stateStr = "MEM_FREE"; }

                    string typeStr = "None";
                    if (mbi.Type == 0x1000000) { typeStr = "MEM_IMAGE"; totalImage += mbi.RegionSize; }
                    else if (mbi.Type == 0x40000) { typeStr = "MEM_MAPPED"; totalMapped += mbi.RegionSize; }
                    else if (mbi.Type == 0x20000) { typeStr = "MEM_PRIVATE"; totalPrivate += mbi.RegionSize; }

                    string protectStr = FormatMemoryProtect(mbi.Protect);

                    bool matchesFilter = true;
                    if (filter == "commit" && mbi.State != 0x1000) matchesFilter = false;
                    else if (filter == "reserve" && mbi.State != 0x2000) matchesFilter = false;
                    else if (filter == "free" && mbi.State != 0x10000) matchesFilter = false;

                    if (matchesFilter && regionList.Count < limit) {
                        double sizeKb = Math.Round((double)mbi.RegionSize / 1024.0, 1);
                        regionList.Add(string.Format(
                            "{{\"baseAddress\": \"0x{0:X}\", \"allocationBase\": \"0x{1:X}\", \"regionSizeBytes\": {2}, \"regionSizeKB\": {3}, \"state\": \"{4}\", \"stateRaw\": {5}, \"protect\": \"{6}\", \"protectRaw\": {7}, \"type\": \"{8}\", \"typeRaw\": {9}}}",
                            mbi.BaseAddress,
                            mbi.AllocationBase,
                            mbi.RegionSize,
                            sizeKb.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                            stateStr,
                            mbi.State,
                            protectStr,
                            mbi.Protect,
                            typeStr,
                            mbi.Type
                        ));
                    }

                    ulong next = mbi.BaseAddress + mbi.RegionSize;
                    if (next <= (ulong)addr.ToInt64()) break;
                    addr = new IntPtr((long)next);
                }

                double comMb = Math.Round((double)totalCommitted / (1024.0 * 1024.0), 2);
                double resMb = Math.Round((double)totalReserved / (1024.0 * 1024.0), 2);
                double imgMb = Math.Round((double)totalImage / (1024.0 * 1024.0), 2);
                double mapMb = Math.Round((double)totalMapped / (1024.0 * 1024.0), 2);
                double prvMb = Math.Round((double)totalPrivate / (1024.0 * 1024.0), 2);

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"pid\": {0}, \"process\": \"{1}\", \"stateFilter\": \"{2}\", \"regionsSampled\": {3}, \"regionsReported\": {4}, \"totalCommittedMB\": {5}, \"totalReservedMB\": {6}, \"totalImageMB\": {7}, \"totalMappedMB\": {8}, \"totalPrivateMB\": {9}, \"regions\": [{10}]}}",
                    proc.Id,
                    EscapeJson(proc.ProcessName),
                    EscapeJson(filter),
                    regionsSampled,
                    regionList.Count,
                    comMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    resMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    imgMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    mapMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    prvMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    string.Join(", ", regionList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static string FormatMemoryProtect(uint protect) {
            uint p = protect & 0xFF;
            string s = "PAGE_NOACCESS";
            if (p == 0x02) s = "PAGE_READONLY";
            else if (p == 0x04) s = "PAGE_READWRITE";
            else if (p == 0x08) s = "PAGE_WRITECOPY";
            else if (p == 0x10) s = "PAGE_EXECUTE";
            else if (p == 0x20) s = "PAGE_EXECUTE_READ";
            else if (p == 0x40) s = "PAGE_EXECUTE_READWRITE";
            else if (p == 0x80) s = "PAGE_EXECUTE_WRITECOPY";

            if ((protect & 0x100) != 0) s += "|PAGE_GUARD";
            if ((protect & 0x200) != 0) s += "|PAGE_NOCACHE";
            if ((protect & 0x400) != 0) s += "|PAGE_WRITECOMBINE";
            return s;
        }

        static void MemoryHeapSummaryCmd() {
            try {
                IntPtr defHeap = GetProcessHeap();
                uint heapCount = GetProcessHeaps(0, null);
                IntPtr[] heaps = new IntPtr[heapCount];
                GetProcessHeaps(heapCount, heaps);

                ulong totalAlloc = 0;
                ulong totalCommit = 0;
                ulong totalRes = 0;
                var list = new List<string>();

                for (int i = 0; i < heaps.Length; i++) {
                    HEAP_SUMMARY hs = new HEAP_SUMMARY();
                    hs.cb = (uint)Marshal.SizeOf(typeof(HEAP_SUMMARY));
                    bool isDef = (heaps[i] == defHeap);

                    if (HeapSummary(heaps[i], 0, out hs)) {
                        ulong alloc = hs.cbAllocated.ToUInt64();
                        ulong commit = hs.cbCommitted.ToUInt64();
                        ulong res = hs.cbReserved.ToUInt64();
                        ulong maxBlock = hs.cbMaxReserve.ToUInt64();

                        totalAlloc += alloc;
                        totalCommit += commit;
                        totalRes += res;

                        double aMb = Math.Round((double)alloc / (1024.0 * 1024.0), 3);
                        double cMb = Math.Round((double)commit / (1024.0 * 1024.0), 3);
                        double rMb = Math.Round((double)res / (1024.0 * 1024.0), 3);

                        list.Add(string.Format(
                            "{{\"index\": {0}, \"handle\": \"0x{1:X}\", \"isDefault\": {2}, \"allocatedMB\": {3}, \"committedMB\": {4}, \"reservedMB\": {5}, \"allocatedBytes\": {6}, \"committedBytes\": {7}, \"reservedBytes\": {8}, \"maxReserveBlockBytes\": {9}}}",
                            i,
                            heaps[i].ToInt64(),
                            isDef ? "true" : "false",
                            aMb.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            cMb.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            rMb.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            alloc,
                            commit,
                            res,
                            maxBlock
                        ));
                    }
                }

                double totAllocMb = Math.Round((double)totalAlloc / (1024.0 * 1024.0), 2);
                double totCommitMb = Math.Round((double)totalCommit / (1024.0 * 1024.0), 2);
                double totResMb = Math.Round((double)totalRes / (1024.0 * 1024.0), 2);

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"defaultHeapHandle\": \"0x{0:X}\", \"heapCount\": {1}, \"totalAllocatedMB\": {2}, \"totalCommittedMB\": {3}, \"totalReservedMB\": {4}, \"heaps\": [{5}]}}",
                    defHeap.ToInt64(),
                    heaps.Length,
                    totAllocMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    totCommitMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    totResMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    string.Join(", ", list.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void MemoryWorkingSetTuneCmd(int targetPid, int minWsMb, int maxWsMb, bool emptyWs) {
            try {
                Process proc = (targetPid <= 0) ? Process.GetCurrentProcess() : Process.GetProcessById(targetPid);
                IntPtr hProc = proc.Handle;

                bool emptied = false;
                if (emptyWs) {
                    emptied = EmptyWorkingSet(hProc);
                }

                UIntPtr curMin, curMax;
                uint curFlags = 0;
                GetProcessWorkingSetSizeEx(hProc, out curMin, out curMax, out curFlags);

                bool tuned = false;
                if (minWsMb > 0 || maxWsMb > 0) {
                    ulong newMinBytes = minWsMb > 0 ? ((ulong)minWsMb * 1024UL * 1024UL) : curMin.ToUInt64();
                    ulong newMaxBytes = maxWsMb > 0 ? ((ulong)maxWsMb * 1024UL * 1024UL) : curMax.ToUInt64();

                    uint flags = curFlags;
                    if (minWsMb > 0) flags |= 0x00000004; // QUOTA_LIMITS_HARDWS_MIN_ENABLE
                    if (maxWsMb > 0) flags |= 0x00000001; // QUOTA_LIMITS_HARDWS_MAX_ENABLE

                    tuned = SetProcessWorkingSetSizeEx(hProc, new UIntPtr(newMinBytes), new UIntPtr(newMaxBytes), flags);
                    GetProcessWorkingSetSizeEx(hProc, out curMin, out curMax, out curFlags);
                }

                double minMb = Math.Round((double)curMin.ToUInt64() / (1024.0 * 1024.0), 2);
                double maxMb = Math.Round((double)curMax.ToUInt64() / (1024.0 * 1024.0), 2);
                bool hardMin = (curFlags & 0x00000004) != 0;
                bool hardMax = (curFlags & 0x00000001) != 0;

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"pid\": {0}, \"process\": \"{1}\", \"minWorkingSetMB\": {2}, \"maxWorkingSetMB\": {3}, \"minWorkingSetBytes\": {4}, \"maxWorkingSetBytes\": {5}, \"flags\": {6}, \"hardMinEnabled\": {7}, \"hardMaxEnabled\": {8}, \"tuned\": {9}, \"emptied\": {10}}}",
                    proc.Id,
                    EscapeJson(proc.ProcessName),
                    minMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    maxMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    curMin.ToUInt64(),
                    curMax.ToUInt64(),
                    curFlags,
                    hardMin ? "true" : "false",
                    hardMax ? "true" : "false",
                    tuned ? "true" : "false",
                    emptied ? "true" : "false"
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Phase 19: Win32 Console Subsystem (Windows.Win32.System.Console / wincon.h / consoleapi.h)

        [StructLayout(LayoutKind.Sequential)]
        public struct COORD {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SMALL_RECT {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONSOLE_SCREEN_BUFFER_INFO {
            public COORD dwSize;
            public COORD dwCursorPosition;
            public ushort wAttributes;
            public SMALL_RECT srWindow;
            public COORD dwMaximumWindowSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONSOLE_CURSOR_INFO {
            public uint dwSize;
            public bool bVisible;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONSOLE_SELECTION_INFO {
            public uint dwFlags;
            public COORD dwSelectionAnchor;
            public SMALL_RECT srSelection;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetConsoleTitle(StringBuilder lpConsoleTitle, uint nSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SetConsoleTitle(string lpConsoleTitle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetConsoleProcessList([Out] uint[] ProcessList, uint ProcessCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleCursorInfo(IntPtr hConsoleOutput, out CONSOLE_CURSOR_INFO lpConsoleCursorInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetConsoleCursorInfo(IntPtr hConsoleOutput, ref CONSOLE_CURSOR_INFO lpConsoleCursorInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleSelectionInfo(out CONSOLE_SELECTION_INFO lpConsoleSelectionInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleDisplayMode(out uint lpModeFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetNumberOfConsoleMouseButtons(out uint lpNumberOfMouseButtons);

        static IntPtr OpenConsoleOutputHandle() {
            return CreateFile("CONOUT$", 0x80000000 | 0x40000000, 0x1 | 0x2, IntPtr.Zero, 3, 0, IntPtr.Zero);
        }

        static IntPtr OpenConsoleInputHandle() {
            return CreateFile("CONIN$", 0x80000000 | 0x40000000, 0x1 | 0x2, IntPtr.Zero, 3, 0, IntPtr.Zero);
        }

        static void ConsoleInfoCmd(bool includeProcesses) {
            try {
                IntPtr hwnd = GetConsoleWindow();
                StringBuilder sb = new StringBuilder(1024);
                uint tLen = 0;
                try { tLen = GetConsoleTitle(sb, 1024); } catch {}
                string title = sb.ToString();

                bool isAttached = (hwnd != IntPtr.Zero);

                uint mouseButtons = 0;
                try { GetNumberOfConsoleMouseButtons(out mouseButtons); } catch {}

                uint dispMode = 0;
                string displayModeStr = "WINDOWED";
                if (GetConsoleDisplayMode(out dispMode)) {
                    if ((dispMode & 0x0001) != 0) displayModeStr = "FULLSCREEN";
                    else if ((dispMode & 0x0002) != 0) displayModeStr = "WINDOWED";
                }

                var procList = new List<uint>();
                if (includeProcesses) {
                    uint[] procs = new uint[128];
                    uint pCount = GetConsoleProcessList(procs, (uint)procs.Length);
                    for (int i = 0; i < pCount && i < procs.Length; i++) {
                        procList.Add(procs[i]);
                    }
                }

                IntPtr hConOut = OpenConsoleOutputHandle();
                bool hasBuffer = false;
                int bufW = 0, bufH = 0;
                int curX = 0, curY = 0;
                ushort attr = 0;
                int winL = 0, winT = 0, winR = 0, winB = 0;
                int maxWinW = 0, maxWinH = 0;
                uint curSize = 0;
                bool curVisible = false;

                if (hConOut != IntPtr.Zero && hConOut.ToInt64() != -1) {
                    isAttached = true;
                    CONSOLE_SCREEN_BUFFER_INFO csbi;
                    if (GetConsoleScreenBufferInfo(hConOut, out csbi)) {
                        hasBuffer = true;
                        bufW = csbi.dwSize.X;
                        bufH = csbi.dwSize.Y;
                        curX = csbi.dwCursorPosition.X;
                        curY = csbi.dwCursorPosition.Y;
                        attr = csbi.wAttributes;
                        winL = csbi.srWindow.Left;
                        winT = csbi.srWindow.Top;
                        winR = csbi.srWindow.Right;
                        winB = csbi.srWindow.Bottom;
                        maxWinW = csbi.dwMaximumWindowSize.X;
                        maxWinH = csbi.dwMaximumWindowSize.Y;
                    }
                    CONSOLE_CURSOR_INFO cci;
                    if (GetConsoleCursorInfo(hConOut, out cci)) {
                        curSize = cci.dwSize;
                        curVisible = cci.bVisible;
                    }
                    CloseHandle(hConOut);
                }

                CONSOLE_SELECTION_INFO csi;
                bool hasSel = false;
                uint selFlags = 0;
                int selAncX = 0, selAncY = 0;
                int selL = 0, selT = 0, selR = 0, selB = 0;
                if (GetConsoleSelectionInfo(out csi)) {
                    hasSel = true;
                    selFlags = csi.dwFlags;
                    selAncX = csi.dwSelectionAnchor.X;
                    selAncY = csi.dwSelectionAnchor.Y;
                    selL = csi.srSelection.Left;
                    selT = csi.srSelection.Top;
                    selR = csi.srSelection.Right;
                    selB = csi.srSelection.Bottom;
                }

                string bufferJson = hasBuffer ? string.Format(
                    "{{\"width\": {0}, \"height\": {1}, \"cursorX\": {2}, \"cursorY\": {3}, \"cursorSizePercent\": {4}, \"cursorVisible\": {5}, \"windowLeft\": {6}, \"windowTop\": {7}, \"windowRight\": {8}, \"windowBottom\": {9}, \"maxWindowWidth\": {10}, \"maxWindowHeight\": {11}, \"attributes\": {12}}}",
                    bufW, bufH, curX, curY, curSize, curVisible ? "true" : "false", winL, winT, winR, winB, maxWinW, maxWinH, attr) : "null";

                string selJson = string.Format(
                    "{{\"active\": {0}, \"flags\": {1}, \"anchorX\": {2}, \"anchorY\": {3}, \"left\": {4}, \"top\": {5}, \"right\": {6}, \"bottom\": {7}}}",
                    hasSel && (selFlags != 0) ? "true" : "false", selFlags, selAncX, selAncY, selL, selT, selR, selB);

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"isAttached\": {0}, \"isHeadless\": {1}, \"hwnd\": \"0x{2:X}\", \"title\": \"{3}\", \"displayMode\": \"{4}\", \"mouseButtons\": {5}, \"processCount\": {6}, \"processIds\": [{7}], \"buffer\": {8}, \"selection\": {9}}}",
                    isAttached ? "true" : "false",
                    (!isAttached) ? "true" : "false",
                    hwnd.ToInt64(),
                    EscapeJson(title),
                    EscapeJson(displayModeStr),
                    mouseButtons,
                    procList.Count,
                    string.Join(", ", procList.ConvertAll(p => p.ToString()).ToArray()),
                    bufferJson,
                    selJson
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void ConsoleModeCmd(string vtProcessingOpt, string quickEditOpt, string mouseInputOpt, string extFlagsOpt) {
            try {
                IntPtr hConIn = OpenConsoleInputHandle();
                IntPtr hConOut = OpenConsoleOutputHandle();

                uint inMode = 0;
                uint outMode = 0;
                bool hasIn = false;
                bool hasOut = false;

                if (hConIn != IntPtr.Zero && hConIn.ToInt64() != -1) {
                    hasIn = GetConsoleMode(hConIn, out inMode);
                }
                if (hConOut != IntPtr.Zero && hConOut.ToInt64() != -1) {
                    hasOut = GetConsoleMode(hConOut, out outMode);
                }

                bool tuned = false;

                // Output Mode Tuning: VT Processing (0x0004)
                if (hasOut && !string.IsNullOrEmpty(vtProcessingOpt)) {
                    bool enableVt = (vtProcessingOpt.ToLowerInvariant() == "true" || vtProcessingOpt == "1");
                    uint newOutMode = outMode;
                    if (enableVt) newOutMode |= 0x0004; // ENABLE_VIRTUAL_TERMINAL_PROCESSING
                    else newOutMode &= ~0x0004u;
                    if (SetConsoleMode(hConOut, newOutMode)) {
                        outMode = newOutMode;
                        tuned = true;
                    }
                }

                // Input Mode Tuning: QuickEdit (0x0040), MouseInput (0x0010), ExtendedFlags (0x0080)
                if (hasIn && (!string.IsNullOrEmpty(quickEditOpt) || !string.IsNullOrEmpty(mouseInputOpt) || !string.IsNullOrEmpty(extFlagsOpt))) {
                    uint newInMode = inMode;
                    newInMode |= 0x0080; // ENABLE_EXTENDED_FLAGS is mandatory to modify QuickEdit

                    if (!string.IsNullOrEmpty(quickEditOpt)) {
                        bool qe = (quickEditOpt.ToLowerInvariant() == "true" || quickEditOpt == "1");
                        if (qe) newInMode |= 0x0040; // ENABLE_QUICK_EDIT_MODE
                        else newInMode &= ~0x0040u;
                    }
                    if (!string.IsNullOrEmpty(mouseInputOpt)) {
                        bool mi = (mouseInputOpt.ToLowerInvariant() == "true" || mouseInputOpt == "1");
                        if (mi) newInMode |= 0x0010; // ENABLE_MOUSE_INPUT
                        else newInMode &= ~0x0010u;
                    }
                    if (!string.IsNullOrEmpty(extFlagsOpt)) {
                        bool ef = (extFlagsOpt.ToLowerInvariant() == "true" || extFlagsOpt == "1");
                        if (ef) newInMode |= 0x0080;
                        else newInMode &= ~0x0080u;
                    }

                    if (SetConsoleMode(hConIn, newInMode)) {
                        inMode = newInMode;
                        tuned = true;
                    }
                }

                if (hConIn != IntPtr.Zero && hConIn.ToInt64() != -1) CloseHandle(hConIn);
                if (hConOut != IntPtr.Zero && hConOut.ToInt64() != -1) CloseHandle(hConOut);

                bool isAttached = hasIn || hasOut;

                // Breakdown of input modes
                bool processedInput = (inMode & 0x0001) != 0;
                bool lineInput = (inMode & 0x0002) != 0;
                bool echoInput = (inMode & 0x0004) != 0;
                bool windowInput = (inMode & 0x0008) != 0;
                bool mouseInput = (inMode & 0x0010) != 0;
                bool insertMode = (inMode & 0x0020) != 0;
                bool quickEditMode = (inMode & 0x0040) != 0;
                bool extendedFlags = (inMode & 0x0080) != 0;
                bool autoPosition = (inMode & 0x0100) != 0;
                bool virtualTerminalInput = (inMode & 0x0200) != 0;

                // Breakdown of output modes
                bool processedOutput = (outMode & 0x0001) != 0;
                bool wrapAtEol = (outMode & 0x0002) != 0;
                bool virtualTerminalProcessing = (outMode & 0x0004) != 0;
                bool disableNewlineAutoReturn = (outMode & 0x0008) != 0;
                bool lvbGridWorldwide = (outMode & 0x0010) != 0;

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"isAttached\": {0}, \"isHeadless\": {1}, \"tuned\": {2}, \"inputModeRaw\": {3}, \"outputModeRaw\": {4}, \"input\": {{\"processedInput\": {5}, \"lineInput\": {6}, \"echoInput\": {7}, \"windowInput\": {8}, \"mouseInput\": {9}, \"insertMode\": {10}, \"quickEdit\": {11}, \"extendedFlags\": {12}, \"autoPosition\": {13}, \"virtualTerminalInput\": {14}}}, \"output\": {{\"processedOutput\": {15}, \"wrapAtEol\": {16}, \"virtualTerminalProcessing\": {17}, \"disableNewlineAutoReturn\": {18}, \"lvbGridWorldwide\": {19}}}}}",
                    isAttached ? "true" : "false",
                    (!isAttached) ? "true" : "false",
                    tuned ? "true" : "false",
                    inMode,
                    outMode,
                    processedInput ? "true" : "false",
                    lineInput ? "true" : "false",
                    echoInput ? "true" : "false",
                    windowInput ? "true" : "false",
                    mouseInput ? "true" : "false",
                    insertMode ? "true" : "false",
                    quickEditMode ? "true" : "false",
                    extendedFlags ? "true" : "false",
                    autoPosition ? "true" : "false",
                    virtualTerminalInput ? "true" : "false",
                    processedOutput ? "true" : "false",
                    wrapAtEol ? "true" : "false",
                    virtualTerminalProcessing ? "true" : "false",
                    disableNewlineAutoReturn ? "true" : "false",
                    lvbGridWorldwide ? "true" : "false"
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void ConsoleControlCmd(string title, string cursorVisibleOpt, string cursorSizeOpt, string activateOpt) {
            try {
                IntPtr hwnd = GetConsoleWindow();
                bool titleChanged = false;
                string prevTitle = "";
                StringBuilder sb = new StringBuilder(1024);
                try {
                    GetConsoleTitle(sb, 1024);
                    prevTitle = sb.ToString();
                } catch {}

                if (!string.IsNullOrEmpty(title)) {
                    titleChanged = SetConsoleTitle(title);
                }

                bool cursorChanged = false;
                uint curSize = 25;
                bool curVis = true;

                IntPtr hConOut = OpenConsoleOutputHandle();
                if (hConOut != IntPtr.Zero && hConOut.ToInt64() != -1) {
                    CONSOLE_CURSOR_INFO cci;
                    if (GetConsoleCursorInfo(hConOut, out cci)) {
                        curSize = cci.dwSize;
                        curVis = cci.bVisible;
                        bool needUpdate = false;
                        if (!string.IsNullOrEmpty(cursorVisibleOpt)) {
                            bool v = (cursorVisibleOpt.ToLowerInvariant() == "true" || cursorVisibleOpt == "1");
                            cci.bVisible = v;
                            curVis = v;
                            needUpdate = true;
                        }
                        if (!string.IsNullOrEmpty(cursorSizeOpt)) {
                            uint s;
                            if (uint.TryParse(cursorSizeOpt, out s)) {
                                cci.dwSize = Math.Max(1, Math.Min(s, 100));
                                curSize = cci.dwSize;
                                needUpdate = true;
                            }
                        }
                        if (needUpdate) {
                            cursorChanged = SetConsoleCursorInfo(hConOut, ref cci);
                        }
                    }
                    CloseHandle(hConOut);
                }

                bool activated = false;
                if (!string.IsNullOrEmpty(activateOpt) && (activateOpt.ToLowerInvariant() == "true" || activateOpt == "1")) {
                    if (hwnd != IntPtr.Zero) {
                        try {
                            ForceForegroundWindow(hwnd);
                            activated = true;
                        } catch {}
                    }
                }

                StringBuilder curTitleSb = new StringBuilder(1024);
                try { GetConsoleTitle(curTitleSb, 1024); } catch {}

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"hwnd\": \"0x{0:X}\", \"previousTitle\": \"{1}\", \"currentTitle\": \"{2}\", \"titleChanged\": {3}, \"cursorChanged\": {4}, \"cursorVisible\": {5}, \"cursorSizePercent\": {6}, \"activated\": {7}}}",
                    hwnd.ToInt64(),
                    EscapeJson(prevTitle),
                    EscapeJson(curTitleSb.ToString()),
                    titleChanged ? "true" : "false",
                    cursorChanged ? "true" : "false",
                    curVis ? "true" : "false",
                    curSize,
                    activated ? "true" : "false"
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Phase 20: Windows Terminal Services & Remote Desktop (Windows.Win32.System.RemoteDesktop / WtsApi32.h / wtsapi32.dll)

        [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool WTSEnumerateSessionsW(
            IntPtr hServer,
            uint Reserved,
            uint Version,
            out IntPtr ppSessionInfo,
            out uint pCount);

        [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool WTSQuerySessionInformationW(
            IntPtr hServer,
            uint sessionId,
            int wtsInfoClass,
            out IntPtr ppBuffer,
            out uint pBytesReturned);

        [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool WTSEnumerateProcessesW(
            IntPtr hServer,
            uint Reserved,
            uint Version,
            out IntPtr ppProcessInfo,
            out uint pCount);

        [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool WTSSendMessageW(
            IntPtr hServer,
            uint SessionId,
            string pTitle,
            uint TitleLength,
            string pMessage,
            uint MessageLength,
            uint Style,
            uint Timeout,
            out uint pResponse,
            bool bWait);

        [StructLayout(LayoutKind.Sequential)]
        public struct WTS_SESSION_INFOW {
            public uint SessionId;
            public IntPtr pWinStationName;
            public int State;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WTS_PROCESS_INFOW {
            public uint SessionId;
            public uint ProcessId;
            public IntPtr pProcessName;
            public IntPtr pUserSid;
        }

        static string FormatWtsState(int state) {
            switch (state) {
                case 0: return "WTSActive";
                case 1: return "WTSConnected";
                case 2: return "WTSConnectQuery";
                case 3: return "WTSShadow";
                case 4: return "WTSDisconnected";
                case 5: return "WTSIdle";
                case 6: return "WTSListen";
                case 7: return "WTSReset";
                case 8: return "WTSDown";
                case 9: return "WTSInit";
                default: return "Unknown (" + state + ")";
            }
        }

        static string FormatWtsProtocol(int proto) {
            switch (proto) {
                case 0: return "Console";
                case 1: return "ICA";
                case 2: return "RDP";
                default: return "Other (" + proto + ")";
            }
        }

        static void WtsSessionsCmd(bool includeDetails) {
            try {
                IntPtr pSessions = IntPtr.Zero;
                uint sCount = 0;
                bool ok = WTSEnumerateSessionsW(IntPtr.Zero, 0, 1, out pSessions, out sCount);
                if (!ok || pSessions == IntPtr.Zero) {
                    int err = Marshal.GetLastWin32Error();
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"WTSEnumerateSessionsW failed (Win32: {0})\"}}", err));
                    return;
                }

                uint currentSessionId = 0;
                try {
                    currentSessionId = (uint)Process.GetCurrentProcess().SessionId;
                } catch {}

                var sessionList = new List<string>();
                int structSize = Marshal.SizeOf(typeof(WTS_SESSION_INFOW));

                for (int i = 0; i < sCount; i++) {
                    IntPtr itemPtr = new IntPtr(pSessions.ToInt64() + (i * structSize));
                    WTS_SESSION_INFOW si = (WTS_SESSION_INFOW)Marshal.PtrToStructure(itemPtr, typeof(WTS_SESSION_INFOW));
                    string station = Marshal.PtrToStringUni(si.pWinStationName) ?? "";
                    string stateStr = FormatWtsState(si.State);
                    bool isCurrent = (si.SessionId == currentSessionId);

                    string userName = "";
                    string domainName = "";
                    string clientName = "";
                    string protocolStr = "Unknown";
                    int dispW = 0, dispH = 0, bpp = 0;

                    if (includeDetails) {
                        IntPtr pBuf = IntPtr.Zero;
                        uint bytes = 0;
                        if (WTSQuerySessionInformationW(IntPtr.Zero, si.SessionId, 5 /* WTSUserName */, out pBuf, out bytes)) {
                            if (pBuf != IntPtr.Zero) {
                                userName = Marshal.PtrToStringUni(pBuf) ?? "";
                                WTSFreeMemory(pBuf);
                            }
                        }
                        if (WTSQuerySessionInformationW(IntPtr.Zero, si.SessionId, 7 /* WTSDomainName */, out pBuf, out bytes)) {
                            if (pBuf != IntPtr.Zero) {
                                domainName = Marshal.PtrToStringUni(pBuf) ?? "";
                                WTSFreeMemory(pBuf);
                            }
                        }
                        if (WTSQuerySessionInformationW(IntPtr.Zero, si.SessionId, 10 /* WTSClientName */, out pBuf, out bytes)) {
                            if (pBuf != IntPtr.Zero) {
                                clientName = Marshal.PtrToStringUni(pBuf) ?? "";
                                WTSFreeMemory(pBuf);
                            }
                        }
                        if (WTSQuerySessionInformationW(IntPtr.Zero, si.SessionId, 16 /* WTSClientProtocolType */, out pBuf, out bytes)) {
                            if (pBuf != IntPtr.Zero) {
                                short p = Marshal.ReadInt16(pBuf);
                                protocolStr = FormatWtsProtocol((int)p);
                                WTSFreeMemory(pBuf);
                            }
                        }
                        if (WTSQuerySessionInformationW(IntPtr.Zero, si.SessionId, 15 /* WTSClientDisplay */, out pBuf, out bytes)) {
                            if (pBuf != IntPtr.Zero) {
                                WTS_CLIENT_DISPLAY disp = (WTS_CLIENT_DISPLAY)Marshal.PtrToStructure(pBuf, typeof(WTS_CLIENT_DISPLAY));
                                dispW = (int)disp.HorizontalResolution;
                                dispH = (int)disp.VerticalResolution;
                                bpp = (int)disp.ColorDepth;
                                WTSFreeMemory(pBuf);
                            }
                        }
                    }

                    sessionList.Add(string.Format(
                        "{{\"sessionId\": {0}, \"winStationName\": \"{1}\", \"state\": \"{2}\", \"stateRaw\": {3}, \"userName\": \"{4}\", \"domainName\": \"{5}\", \"clientName\": \"{6}\", \"protocol\": \"{7}\", \"displayWidth\": {8}, \"displayHeight\": {9}, \"colorDepth\": {10}, \"isCurrentSession\": {11}}}",
                        si.SessionId,
                        EscapeJson(station),
                        EscapeJson(stateStr),
                        si.State,
                        EscapeJson(userName),
                        EscapeJson(domainName),
                        EscapeJson(clientName),
                        EscapeJson(protocolStr),
                        dispW,
                        dispH,
                        bpp,
                        isCurrent ? "true" : "false"
                    ));
                }

                WTSFreeMemory(pSessions);

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"currentSessionId\": {0}, \"sessionCount\": {1}, \"sessions\": [{2}]}}",
                    currentSessionId,
                    sessionList.Count,
                    string.Join(", ", sessionList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void WtsProcessesCmd(int targetSessionId, string nameFilter, int maxLimit) {
            try {
                IntPtr pProcs = IntPtr.Zero;
                uint pCount = 0;
                bool ok = WTSEnumerateProcessesW(IntPtr.Zero, 0, 1, out pProcs, out pCount);
                if (!ok || pProcs == IntPtr.Zero) {
                    int err = Marshal.GetLastWin32Error();
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"WTSEnumerateProcessesW failed (Win32: {0})\"}}", err));
                    return;
                }

                int limit = Math.Max(1, Math.Min(maxLimit <= 0 ? 50 : maxLimit, 200));
                string filter = (nameFilter ?? "").Trim().ToLowerInvariant();

                var procList = new List<string>();
                var sessionCounts = new Dictionary<uint, int>();
                int matchedCount = 0;

                int structSize = Marshal.SizeOf(typeof(WTS_PROCESS_INFOW));

                for (int i = 0; i < pCount; i++) {
                    IntPtr itemPtr = new IntPtr(pProcs.ToInt64() + (i * structSize));
                    WTS_PROCESS_INFOW pi = (WTS_PROCESS_INFOW)Marshal.PtrToStructure(itemPtr, typeof(WTS_PROCESS_INFOW));

                    if (!sessionCounts.ContainsKey(pi.SessionId)) sessionCounts[pi.SessionId] = 0;
                    sessionCounts[pi.SessionId]++;

                    if (targetSessionId >= 0 && pi.SessionId != (uint)targetSessionId) continue;

                    string pName = Marshal.PtrToStringUni(pi.pProcessName) ?? "";
                    if (!string.IsNullOrEmpty(filter) && !pName.ToLowerInvariant().Contains(filter)) continue;

                    matchedCount++;

                    if (procList.Count < limit) {
                        string sidStr = "";
                        if (pi.pUserSid != IntPtr.Zero) {
                            try {
                                var sid = new System.Security.Principal.SecurityIdentifier(pi.pUserSid);
                                sidStr = sid.Value;
                            } catch {}
                        }

                        procList.Add(string.Format(
                            "{{\"sessionId\": {0}, \"pid\": {1}, \"processName\": \"{2}\", \"userSid\": \"{3}\"}}",
                            pi.SessionId,
                            pi.ProcessId,
                            EscapeJson(pName),
                            EscapeJson(sidStr)
                        ));
                    }
                }

                WTSFreeMemory(pProcs);

                var sCountsJson = new List<string>();
                foreach (var kvp in sessionCounts) {
                    sCountsJson.Add(string.Format("{{\"sessionId\": {0}, \"count\": {1}}}", kvp.Key, kvp.Value));
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"totalProcesses\": {0}, \"matchedProcesses\": {1}, \"returnedProcesses\": {2}, \"sessionDistribution\": [{3}], \"processes\": [{4}]}}",
                    pCount,
                    matchedCount,
                    procList.Count,
                    string.Join(", ", sCountsJson.ToArray()),
                    string.Join(", ", procList.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void WtsSessionMessageCmd(int sessionId, string title, string message, uint style, uint timeoutSec, bool wait) {
            try {
                uint targetSession = 0;
                if (sessionId >= 0) {
                    targetSession = (uint)sessionId;
                } else {
                    targetSession = (uint)Process.GetCurrentProcess().SessionId;
                }

                string msgTitle = string.IsNullOrEmpty(title) ? "Gemini Super System" : title;
                string msgBody = string.IsNullOrEmpty(message) ? "Notice from Gemini Super System" : message;
                uint msgStyle = style == 0 ? 0x00000040u /* MB_ICONINFORMATION | MB_OK */ : style;
                uint timeout = timeoutSec <= 0 ? 10u : timeoutSec;

                uint response = 0;
                uint titleLen = (uint)(msgTitle.Length * sizeof(char));
                uint msgLen = (uint)(msgBody.Length * sizeof(char));

                bool ok = WTSSendMessageW(
                    IntPtr.Zero,
                    targetSession,
                    msgTitle,
                    titleLen,
                    msgBody,
                    msgLen,
                    msgStyle,
                    timeout,
                    out response,
                    wait
                );

                int lastErr = 0;
                if (!ok) lastErr = Marshal.GetLastWin32Error();

                string responseStr = "ASYNC_DISPATCHED";
                if (wait && ok) {
                    switch (response) {
                        case 1: responseStr = "IDOK"; break;
                        case 2: responseStr = "IDCANCEL"; break;
                        case 3: responseStr = "IDABORT"; break;
                        case 4: responseStr = "IDRETRY"; break;
                        case 5: responseStr = "IDIGNORE"; break;
                        case 6: responseStr = "IDYES"; break;
                        case 7: responseStr = "IDNO"; break;
                        case 32000: responseStr = "IDTIMEOUT"; break;
                        case 32001: responseStr = "IDASYNC"; break;
                        default: responseStr = "CODE_" + response; break;
                    }
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": {0}, \"sessionId\": {1}, \"title\": \"{2}\", \"wait\": {3}, \"response\": \"{4}\", \"responseRaw\": {5}, \"win32Error\": {6}}}",
                    ok ? "true" : "false",
                    targetSession,
                    EscapeJson(msgTitle),
                    wait ? "true" : "false",
                    EscapeJson(responseStr),
                    response,
                    lastErr
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Phase 21: Windows Process Status Subsystem (Windows.Win32.System.ProcessStatus / psapi.h / psapi.dll)

        [DllImport("psapi.dll", SetLastError = true)]
        static extern bool EnumDeviceDrivers([Out] IntPtr[] lpImageBase, uint cb, out uint lpcbNeeded);

        [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetDeviceDriverBaseNameW(IntPtr ImageBase, StringBuilder lpBaseName, uint nSize);

        [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetDeviceDriverFileNameW(IntPtr ImageBase, StringBuilder lpFilename, uint nSize);

        [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetMappedFileNameW(IntPtr hProcess, IntPtr lpv, StringBuilder lpFilename, uint nSize);

        static void PsapiPerformanceCmd() {
            try {
                PERFORMANCE_INFORMATION pi = new PERFORMANCE_INFORMATION();
                pi.cb = (uint)Marshal.SizeOf(typeof(PERFORMANCE_INFORMATION));
                if (!GetPerformanceInfo(out pi, pi.cb)) {
                    int err = Marshal.GetLastWin32Error();
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"GetPerformanceInfo failed with error {0}\"}}", err));
                    return;
                }

                long pageSize = (long)pi.PageSize.ToUInt64();
                long commitTotal = (long)pi.CommitTotal.ToUInt64() * pageSize;
                long commitLimit = (long)pi.CommitLimit.ToUInt64() * pageSize;
                long commitPeak = (long)pi.CommitPeak.ToUInt64() * pageSize;
                double commitPct = commitLimit > 0 ? Math.Round((double)commitTotal / commitLimit * 100.0, 2) : 0;

                long physTotal = (long)pi.PhysicalTotal.ToUInt64() * pageSize;
                long physAvail = (long)pi.PhysicalAvailable.ToUInt64() * pageSize;
                long physUsed = physTotal - physAvail;
                double physPct = physTotal > 0 ? Math.Round((double)physUsed / physTotal * 100.0, 2) : 0;

                long sysCache = (long)pi.SystemCache.ToUInt64() * pageSize;
                long kernTotal = (long)pi.KernelTotal.ToUInt64() * pageSize;
                long kernPaged = (long)pi.KernelPaged.ToUInt64() * pageSize;
                long kernNonpaged = (long)pi.KernelNonpaged.ToUInt64() * pageSize;

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"pageSize\": {0}, \"commitTotalBytes\": {1}, \"commitLimitBytes\": {2}, \"commitPeakBytes\": {3}, \"commitUsagePercent\": {4}, \"physicalTotalBytes\": {5}, \"physicalAvailableBytes\": {6}, \"physicalUsedBytes\": {7}, \"physicalUsagePercent\": {8}, \"systemCacheBytes\": {9}, \"kernelTotalBytes\": {10}, \"kernelPagedBytes\": {11}, \"kernelNonpagedBytes\": {12}, \"handlesCount\": {13}, \"processesCount\": {14}, \"threadsCount\": {15}}}",
                    pageSize, commitTotal, commitLimit, commitPeak, commitPct,
                    physTotal, physAvail, physUsed, physPct,
                    sysCache, kernTotal, kernPaged, kernNonpaged,
                    pi.HandleCount, pi.ProcessCount, pi.ThreadCount
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void PsapiDeviceDriversCmd(string filter, int limit) {
            try {
                uint bytesNeeded = 0;
                EnumDeviceDrivers(null, 0, out bytesNeeded);
                if (bytesNeeded == 0) bytesNeeded = 4096;

                int count = (int)(bytesNeeded / (uint)IntPtr.Size);
                if (count < 1024) count = 1024;
                IntPtr[] bases = new IntPtr[count];

                if (!EnumDeviceDrivers(bases, (uint)(bases.Length * IntPtr.Size), out bytesNeeded)) {
                    int err = Marshal.GetLastWin32Error();
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"EnumDeviceDrivers failed with error {0}\"}}", err));
                    return;
                }

                int totalCount = (int)(bytesNeeded / (uint)IntPtr.Size);
                string filterLower = (filter ?? "").Trim().ToLowerInvariant();
                int maxEntries = limit <= 0 ? 100 : Math.Min(limit, 500);

                var driverItems = new List<string>();
                int matched = 0;
                StringBuilder sbName = new StringBuilder(512);
                StringBuilder sbPath = new StringBuilder(1024);

                for (int i = 0; i < totalCount; i++) {
                    IntPtr baseAddr = bases[i];
                    if (baseAddr == IntPtr.Zero) continue;

                    sbName.Length = 0;
                    GetDeviceDriverBaseNameW(baseAddr, sbName, (uint)sbName.Capacity);
                    string baseName = sbName.ToString();

                    sbPath.Length = 0;
                    GetDeviceDriverFileNameW(baseAddr, sbPath, (uint)sbPath.Capacity);
                    string fileName = sbPath.ToString();

                    bool matches = true;
                    if (!string.IsNullOrEmpty(filterLower)) {
                        matches = (baseName.ToLowerInvariant().Contains(filterLower) || fileName.ToLowerInvariant().Contains(filterLower));
                    }

                    if (matches) {
                        matched++;
                        if (driverItems.Count < maxEntries) {
                            driverItems.Add(string.Format(
                                "{{\"baseAddress\": \"0x{0:X}\", \"baseName\": \"{1}\", \"fileName\": \"{2}\"}}",
                                baseAddr.ToInt64(), EscapeJson(baseName), EscapeJson(fileName)
                            ));
                        }
                    }
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"totalDriversCount\": {0}, \"matchedCount\": {1}, \"returnedCount\": {2}, \"drivers\": [{3}]}}",
                    totalCount, matched, driverItems.Count, string.Join(", ", driverItems.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void PsapiProcessMemoryCmd(int targetPid, bool includeMappedFiles) {
            try {
                int pid = targetPid <= 0 ? Process.GetCurrentProcess().Id : targetPid;
                string procName = "";
                try {
                    procName = Process.GetProcessById(pid).ProcessName;
                } catch {
                    procName = "PID_" + pid;
                }

                IntPtr hProcess = OpenProcess(0x0400 | 0x0010 /* PROCESS_QUERY_INFORMATION | PROCESS_VM_READ */, false, (uint)pid);
                if (hProcess == IntPtr.Zero) {
                    hProcess = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, (uint)pid);
                }

                if (hProcess == IntPtr.Zero) {
                    int err = Marshal.GetLastWin32Error();
                    Console.WriteLine(string.Format("{{\"success\": false, \"processId\": {0}, \"processName\": \"{1}\", \"error\": \"OpenProcess failed with error {2}\"}}", pid, EscapeJson(procName), err));
                    return;
                }

                try {
                    PROCESS_MEMORY_COUNTERS_EX pmc = new PROCESS_MEMORY_COUNTERS_EX();
                    pmc.cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS_EX));

                    if (!GetProcessMemoryInfo(hProcess, out pmc, pmc.cb)) {
                        int err = Marshal.GetLastWin32Error();
                        Console.WriteLine(string.Format("{{\"success\": false, \"processId\": {0}, \"processName\": \"{1}\", \"error\": \"GetProcessMemoryInfo failed with error {2}\"}}", pid, EscapeJson(procName), err));
                        return;
                    }

                    long workingSet = (long)pmc.WorkingSetSize.ToUInt64();
                    long peakWorkingSet = (long)pmc.PeakWorkingSetSize.ToUInt64();
                    long pagedPool = (long)pmc.QuotaPagedPoolUsage.ToUInt64();
                    long peakPagedPool = (long)pmc.QuotaPeakPagedPoolUsage.ToUInt64();
                    long nonPagedPool = (long)pmc.QuotaNonPagedPoolUsage.ToUInt64();
                    long peakNonPagedPool = (long)pmc.QuotaPeakNonPagedPoolUsage.ToUInt64();
                    long pagefile = (long)pmc.PagefileUsage.ToUInt64();
                    long peakPagefile = (long)pmc.PeakPagefileUsage.ToUInt64();
                    long privateUsage = (long)pmc.PrivateUsage.ToUInt64();

                    var mappedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var mappedDetails = new List<string>();

                    if (includeMappedFiles) {
                        MEMORY_BASIC_INFORMATION64 mbi;
                        int structSize = Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION64));
                        IntPtr addr = IntPtr.Zero;
                        StringBuilder sbPath = new StringBuilder(1024);

                        while (VirtualQueryEx(hProcess, addr, out mbi, (uint)structSize) != 0) {
                            if (mbi.State == 0x1000 && (mbi.Type == 0x40000 /* MEM_MAPPED */ || mbi.Type == 0x1000000 /* MEM_IMAGE */)) {
                                sbPath.Length = 0;
                                uint len = GetMappedFileNameW(hProcess, new IntPtr((long)mbi.BaseAddress), sbPath, (uint)sbPath.Capacity);
                                if (len > 0) {
                                    string mappedPath = sbPath.ToString();
                                    if (!string.IsNullOrEmpty(mappedPath) && mappedFiles.Add(mappedPath)) {
                                        mappedDetails.Add(string.Format(
                                            "{{\"baseAddress\": \"0x{0:X}\", \"path\": \"{1}\", \"type\": \"{2}\"}}",
                                            mbi.BaseAddress,
                                            EscapeJson(mappedPath),
                                            mbi.Type == 0x1000000 ? "MEM_IMAGE" : "MEM_MAPPED"
                                        ));
                                    }
                                }
                            }

                            ulong nextAddr = mbi.BaseAddress + mbi.RegionSize;
                            if (nextAddr <= mbi.BaseAddress || nextAddr >= 0x7FFFFFFEFFFFUL) break;
                            addr = new IntPtr((long)nextAddr);
                        }
                    }

                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"processId\": {0}, \"processName\": \"{1}\", \"pageFaultCount\": {2}, \"workingSetBytes\": {3}, \"peakWorkingSetBytes\": {4}, \"pagedPoolBytes\": {5}, \"peakPagedPoolBytes\": {6}, \"nonPagedPoolBytes\": {7}, \"peakNonPagedPoolBytes\": {8}, \"pagefileUsageBytes\": {9}, \"peakPagefileUsageBytes\": {10}, \"privateUsageBytes\": {11}, \"mappedFilesCount\": {12}, \"mappedFiles\": [{13}]}}",
                        pid, EscapeJson(procName), pmc.PageFaultCount,
                        workingSet, peakWorkingSet,
                        pagedPool, peakPagedPool,
                        nonPagedPool, peakNonPagedPool,
                        pagefile, peakPagefile, privateUsage,
                        mappedFiles.Count, string.Join(", ", mappedDetails.ToArray())
                    ));
                } finally {
                    CloseHandle(hProcess);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Phase 22: Windows Credential Management Subsystem (Windows.Win32.Security.Credentials / wincred.h / advapi32.dll)

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct CREDENTIALW {
            public uint Flags;
            public uint Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CredEnumerateW(
            string filter,
            int flags,
            out int count,
            out IntPtr pCredentials);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CredReadW(
            string targetName,
            uint type,
            int flags,
            out IntPtr pCredential);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CredWriteW(
            [In] ref CREDENTIALW userCredential,
            uint flags);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CredDeleteW(
            string targetName,
            uint type,
            int flags);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern void CredFree(IntPtr buffer);

        static string FormatCredType(uint type) {
            switch (type) {
                case 1: return "Generic";
                case 2: return "DomainPassword";
                case 3: return "DomainCertificate";
                case 4: return "DomainVisiblePassword";
                case 5: return "GenericCertificate";
                case 6: return "DomainExtended";
                default: return "Type_" + type;
            }
        }

        static string FormatCredPersist(uint persist) {
            switch (persist) {
                case 1: return "Session";
                case 2: return "LocalMachine";
                case 3: return "Enterprise";
                default: return "Persist_" + persist;
            }
        }

        static void CredEnumerateCmd(string filter, int limit) {
            try {
                int count = 0;
                IntPtr pCredentials = IntPtr.Zero;
                string filterArg = string.IsNullOrEmpty(filter) ? null : filter;

                if (!CredEnumerateW(filterArg, 0, out count, out pCredentials)) {
                    int err = Marshal.GetLastWin32Error();
                    if (err == 1168 /* ERROR_NOT_FOUND */) {
                        Console.WriteLine("{\"success\": true, \"credentialCount\": 0, \"returnedCount\": 0, \"credentials\": []}");
                        return;
                    }
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"CredEnumerate failed with error {0}\"}}", err));
                    return;
                }

                try {
                    int maxEntries = limit <= 0 ? 50 : Math.Min(limit, 200);
                    var credList = new List<string>();

                    for (int i = 0; i < count; i++) {
                        IntPtr pCred = Marshal.ReadIntPtr(pCredentials, i * IntPtr.Size);
                        if (pCred == IntPtr.Zero) continue;

                        CREDENTIALW cred = (CREDENTIALW)Marshal.PtrToStructure(pCred, typeof(CREDENTIALW));
                        string targetName = Marshal.PtrToStringUni(cred.TargetName) ?? "";
                        string comment = Marshal.PtrToStringUni(cred.Comment) ?? "";
                        string userName = Marshal.PtrToStringUni(cred.UserName) ?? "";
                        string targetAlias = Marshal.PtrToStringUni(cred.TargetAlias) ?? "";

                        long fileTime = ((long)cred.LastWritten.dwHighDateTime << 32) | (uint)cred.LastWritten.dwLowDateTime;
                        string isoDate = "";
                        try {
                            isoDate = DateTime.FromFileTimeUtc(fileTime).ToString("o");
                        } catch {
                            isoDate = "Unknown";
                        }

                        if (credList.Count < maxEntries) {
                            credList.Add(string.Format(
                                "{{\"targetName\": \"{0}\", \"userName\": \"{1}\", \"type\": \"{2}\", \"typeId\": {3}, \"persist\": \"{4}\", \"persistId\": {5}, \"blobSizeBytes\": {6}, \"comment\": \"{7}\", \"targetAlias\": \"{8}\", \"lastWritten\": \"{9}\"}}",
                                EscapeJson(targetName),
                                EscapeJson(userName),
                                EscapeJson(FormatCredType(cred.Type)),
                                cred.Type,
                                EscapeJson(FormatCredPersist(cred.Persist)),
                                cred.Persist,
                                cred.CredentialBlobSize,
                                EscapeJson(comment),
                                EscapeJson(targetAlias),
                                EscapeJson(isoDate)
                            ));
                        }
                    }

                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"credentialCount\": {0}, \"returnedCount\": {1}, \"credentials\": [{2}]}}",
                        count, credList.Count, string.Join(", ", credList.ToArray())
                    ));
                } finally {
                    CredFree(pCredentials);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void CredReadCmd(string targetName, uint type, bool includeSecret) {
            try {
                if (string.IsNullOrEmpty(targetName)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"TargetName is required\"}");
                    return;
                }

                uint credType = type <= 0 ? 1 /* CRED_TYPE_GENERIC */ : type;
                IntPtr pCredential = IntPtr.Zero;

                if (!CredReadW(targetName, credType, 0, out pCredential)) {
                    int err = Marshal.GetLastWin32Error();
                    Console.WriteLine(string.Format("{{\"success\": false, \"targetName\": \"{0}\", \"error\": \"CredRead failed with error {1}\"}}", EscapeJson(targetName), err));
                    return;
                }

                try {
                    CREDENTIALW cred = (CREDENTIALW)Marshal.PtrToStructure(pCredential, typeof(CREDENTIALW));
                    string target = Marshal.PtrToStringUni(cred.TargetName) ?? targetName;
                    string userName = Marshal.PtrToStringUni(cred.UserName) ?? "";
                    string comment = Marshal.PtrToStringUni(cred.Comment) ?? "";
                    string targetAlias = Marshal.PtrToStringUni(cred.TargetAlias) ?? "";

                    long fileTime = ((long)cred.LastWritten.dwHighDateTime << 32) | (uint)cred.LastWritten.dwLowDateTime;
                    string isoDate = "";
                    try {
                        isoDate = DateTime.FromFileTimeUtc(fileTime).ToString("o");
                    } catch {
                        isoDate = "Unknown";
                    }

                    string secretText = "";
                    if (includeSecret && cred.CredentialBlobSize > 0 && cred.CredentialBlob != IntPtr.Zero) {
                        byte[] blob = new byte[cred.CredentialBlobSize];
                        Marshal.Copy(cred.CredentialBlob, blob, 0, (int)cred.CredentialBlobSize);
                        string decoded = Encoding.Unicode.GetString(blob).TrimEnd('\0');
                        bool hasInvalidChar = false;
                        foreach (char c in decoded) {
                            if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t') {
                                hasInvalidChar = true;
                                break;
                            }
                        }
                        if (hasInvalidChar) {
                            decoded = Encoding.UTF8.GetString(blob).TrimEnd('\0');
                        }
                        secretText = decoded;
                    }

                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"targetName\": \"{0}\", \"userName\": \"{1}\", \"type\": \"{2}\", \"typeId\": {3}, \"persist\": \"{4}\", \"persistId\": {5}, \"blobSizeBytes\": {6}, \"comment\": \"{7}\", \"targetAlias\": \"{8}\", \"lastWritten\": \"{9}\", \"hasSecret\": {10}, \"secret\": \"{11}\"}}",
                        EscapeJson(target),
                        EscapeJson(userName),
                        EscapeJson(FormatCredType(cred.Type)),
                        cred.Type,
                        EscapeJson(FormatCredPersist(cred.Persist)),
                        cred.Persist,
                        cred.CredentialBlobSize,
                        EscapeJson(comment),
                        EscapeJson(targetAlias),
                        EscapeJson(isoDate),
                        cred.CredentialBlobSize > 0 ? "true" : "false",
                        includeSecret ? EscapeJson(secretText) : ""
                    ));
                } finally {
                    CredFree(pCredential);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void CredManageCmd(string action, string targetName, string userName, string secret, string comment, uint type, uint persist) {
            try {
                if (string.IsNullOrEmpty(targetName)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"TargetName is required\"}");
                    return;
                }

                string act = (action ?? "write").Trim().ToLowerInvariant();
                uint credType = type <= 0 ? 1 /* CRED_TYPE_GENERIC */ : type;

                if (act == "delete" || act == "remove") {
                    if (!CredDeleteW(targetName, credType, 0)) {
                        int err = Marshal.GetLastWin32Error();
                        Console.WriteLine(string.Format("{{\"success\": false, \"action\": \"delete\", \"targetName\": \"{0}\", \"error\": \"CredDelete failed with error {1}\"}}", EscapeJson(targetName), err));
                        return;
                    }
                    Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"delete\", \"targetName\": \"{0}\", \"deleted\": true}}", EscapeJson(targetName)));
                    return;
                }

                // Write/Update credential
                uint persistVal = persist <= 0 ? 2 /* CRED_PERSIST_LOCAL_MACHINE */ : persist;
                string sec = secret ?? "";
                byte[] blob = Encoding.Unicode.GetBytes(sec);
                IntPtr pBlob = Marshal.AllocHGlobal(blob.Length);
                Marshal.Copy(blob, 0, pBlob, blob.Length);

                IntPtr pTarget = Marshal.StringToHGlobalUni(targetName);
                IntPtr pUser = Marshal.StringToHGlobalUni(userName ?? "");
                IntPtr pComment = Marshal.StringToHGlobalUni(comment ?? "Gemini Super System Credential");

                try {
                    CREDENTIALW cred = new CREDENTIALW();
                    cred.Flags = 0;
                    cred.Type = credType;
                    cred.TargetName = pTarget;
                    cred.Comment = pComment;
                    cred.CredentialBlobSize = (uint)blob.Length;
                    cred.CredentialBlob = pBlob;
                    cred.Persist = persistVal;
                    cred.AttributeCount = 0;
                    cred.Attributes = IntPtr.Zero;
                    cred.TargetAlias = IntPtr.Zero;
                    cred.UserName = pUser;

                    if (!CredWriteW(ref cred, 0)) {
                        int err = Marshal.GetLastWin32Error();
                        Console.WriteLine(string.Format("{{\"success\": false, \"action\": \"write\", \"targetName\": \"{0}\", \"error\": \"CredWrite failed with error {1}\"}}", EscapeJson(targetName), err));
                        return;
                    }

                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"action\": \"write\", \"targetName\": \"{0}\", \"userName\": \"{1}\", \"type\": \"{2}\", \"persist\": \"{3}\", \"blobSizeBytes\": {4}}}",
                        EscapeJson(targetName),
                        EscapeJson(userName ?? ""),
                        EscapeJson(FormatCredType(credType)),
                        EscapeJson(FormatCredPersist(persistVal)),
                        blob.Length
                    ));
                } finally {
                    Marshal.FreeHGlobal(pBlob);
                    Marshal.FreeHGlobal(pTarget);
                    Marshal.FreeHGlobal(pUser);
                    Marshal.FreeHGlobal(pComment);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows DNS Client Subsystem (windns.h / dnsapi.dll)

        const ushort DNS_TYPE_A = 0x0001;
        const ushort DNS_TYPE_NS = 0x0002;
        const ushort DNS_TYPE_CNAME = 0x0005;
        const ushort DNS_TYPE_SOA = 0x0006;
        const ushort DNS_TYPE_PTR = 0x000c;
        const ushort DNS_TYPE_MX = 0x000f;
        const ushort DNS_TYPE_TXT = 0x0010;
        const ushort DNS_TYPE_AAAA = 0x001c;
        const ushort DNS_TYPE_SRV = 0x0021;
        const ushort DNS_TYPE_ANY = 0x00ff;

        const uint DNS_QUERY_STANDARD = 0x00000000;
        const uint DNS_QUERY_ACCEPT_TRUNCATED_RESPONSE = 0x00000001;
        const uint DNS_QUERY_USE_TCP_ONLY = 0x00000002;
        const uint DNS_QUERY_BYPASS_CACHE = 0x00000008;
        const uint DNS_QUERY_NO_HOSTS_FILE = 0x00000040;
        const uint DNS_QUERY_NO_LOCAL_NAME = 0x00000080;
        const uint DNS_QUERY_WIRE_ONLY = 0x00000100;

        [DllImport("dnsapi.dll", EntryPoint = "DnsQuery_W", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int DnsQuery_W(
            string pszName,
            ushort wType,
            uint options,
            IntPtr pExtra,
            out IntPtr ppQueryResultsSet,
            IntPtr pReserved
        );

        [DllImport("dnsapi.dll", SetLastError = true)]
        static extern void DnsRecordListFree(IntPtr pRecordList, int FreeType);

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        static extern int DnsFlushResolverCache();

        static ushort ParseDnsType(string typeStr) {
            if (string.IsNullOrEmpty(typeStr)) return DNS_TYPE_A;
            switch (typeStr.Trim().ToUpperInvariant()) {
                case "A": return DNS_TYPE_A;
                case "AAAA": return DNS_TYPE_AAAA;
                case "CNAME": return DNS_TYPE_CNAME;
                case "MX": return DNS_TYPE_MX;
                case "TXT": return DNS_TYPE_TXT;
                case "NS": return DNS_TYPE_NS;
                case "SOA": return DNS_TYPE_SOA;
                case "PTR": return DNS_TYPE_PTR;
                case "SRV": return DNS_TYPE_SRV;
                case "ANY": case "*": return DNS_TYPE_ANY;
                default:
                    ushort parsed;
                    if (ushort.TryParse(typeStr, out parsed)) return parsed;
                    return DNS_TYPE_A;
            }
        }

        static string FormatDnsType(ushort type) {
            switch (type) {
                case DNS_TYPE_A: return "A";
                case DNS_TYPE_AAAA: return "AAAA";
                case DNS_TYPE_CNAME: return "CNAME";
                case DNS_TYPE_MX: return "MX";
                case DNS_TYPE_TXT: return "TXT";
                case DNS_TYPE_NS: return "NS";
                case DNS_TYPE_SOA: return "SOA";
                case DNS_TYPE_PTR: return "PTR";
                case DNS_TYPE_SRV: return "SRV";
                case DNS_TYPE_ANY: return "ANY";
                default: return "TYPE_" + type;
            }
        }

        static void DnsQueryCmd(string name, string typeStr, bool bypassCache) {
            try {
                if (string.IsNullOrEmpty(name)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Name or host parameter is required\"}");
                    return;
                }

                ushort wType = ParseDnsType(typeStr);
                uint options = DNS_QUERY_STANDARD;
                if (bypassCache) {
                    options |= DNS_QUERY_BYPASS_CACHE | DNS_QUERY_WIRE_ONLY;
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                IntPtr pRecords = IntPtr.Zero;
                int status = DnsQuery_W(name, wType, options, IntPtr.Zero, out pRecords, IntPtr.Zero);
                sw.Stop();

                if (status != 0 && status != 9003 /* DNS_ERROR_RCODE_NAME_ERROR */ && status != 9501 /* DNS_INFO_NO_RECORDS */) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"query\": \"{0}\", \"type\": \"{1}\", \"errorCode\": {2}, \"error\": \"DnsQuery failed with status {2}\"}}", EscapeJson(name), FormatDnsType(wType), status));
                    return;
                }

                var records = new List<string>();
                int recordCount = 0;

                if (pRecords != IntPtr.Zero) {
                    try {
                        IntPtr curr = pRecords;
                        while (curr != IntPtr.Zero && recordCount < 100) {
                            string rName = Marshal.PtrToStringUni(Marshal.ReadIntPtr(curr, 8)) ?? name;
                            ushort rType = (ushort)Marshal.ReadInt16(curr, 16);
                            uint rFlags = (uint)Marshal.ReadInt32(curr, 20);
                            uint rTtl = (uint)Marshal.ReadInt32(curr, 24);

                            string dataJson = "{}";

                            if (rType == DNS_TYPE_A) {
                                byte b0 = Marshal.ReadByte(curr, 32);
                                byte b1 = Marshal.ReadByte(curr, 33);
                                byte b2 = Marshal.ReadByte(curr, 34);
                                byte b3 = Marshal.ReadByte(curr, 35);
                                dataJson = string.Format("{{\"ip\": \"{0}.{1}.{2}.{3}\"}}", b0, b1, b2, b3);
                            } else if (rType == DNS_TYPE_AAAA) {
                                byte[] ip6 = new byte[16];
                                Marshal.Copy(new IntPtr(curr.ToInt64() + 32), ip6, 0, 16);
                                string ipStr = new System.Net.IPAddress(ip6).ToString();
                                dataJson = string.Format("{{\"ip\": \"{0}\"}}", EscapeJson(ipStr));
                            } else if (rType == DNS_TYPE_CNAME || rType == DNS_TYPE_PTR || rType == DNS_TYPE_NS) {
                                IntPtr pTarget = Marshal.ReadIntPtr(curr, 32);
                                string target = Marshal.PtrToStringUni(pTarget) ?? "";
                                dataJson = string.Format("{{\"target\": \"{0}\"}}", EscapeJson(target));
                            } else if (rType == DNS_TYPE_MX) {
                                IntPtr pExchange = Marshal.ReadIntPtr(curr, 32);
                                string exchange = Marshal.PtrToStringUni(pExchange) ?? "";
                                ushort pref = (ushort)Marshal.ReadInt16(curr, 40);
                                dataJson = string.Format("{{\"exchange\": \"{0}\", \"preference\": {1}}}", EscapeJson(exchange), pref);
                            } else if (rType == DNS_TYPE_TXT) {
                                uint strCount = (uint)Marshal.ReadInt32(curr, 32);
                                var strList = new List<string>();
                                for (int s = 0; s < strCount && s < 25; s++) {
                                    IntPtr pStr = Marshal.ReadIntPtr(curr, 40 + s * IntPtr.Size);
                                    if (pStr != IntPtr.Zero) {
                                        strList.Add("\"" + EscapeJson(Marshal.PtrToStringUni(pStr) ?? "") + "\"");
                                    }
                                }
                                dataJson = string.Format("{{\"stringCount\": {0}, \"strings\": [{1}]}}", strCount, string.Join(", ", strList.ToArray()));
                            } else if (rType == DNS_TYPE_SRV) {
                                IntPtr pTarget = Marshal.ReadIntPtr(curr, 32);
                                string target = Marshal.PtrToStringUni(pTarget) ?? "";
                                ushort prio = (ushort)Marshal.ReadInt16(curr, 40);
                                ushort weight = (ushort)Marshal.ReadInt16(curr, 42);
                                ushort port = (ushort)Marshal.ReadInt16(curr, 44);
                                dataJson = string.Format("{{\"target\": \"{0}\", \"priority\": {1}, \"weight\": {2}, \"port\": {3}}}", EscapeJson(target), prio, weight, port);
                            } else if (rType == DNS_TYPE_SOA) {
                                IntPtr pPrimary = Marshal.ReadIntPtr(curr, 32);
                                IntPtr pAdmin = Marshal.ReadIntPtr(curr, 40);
                                string prim = Marshal.PtrToStringUni(pPrimary) ?? "";
                                string admin = Marshal.PtrToStringUni(pAdmin) ?? "";
                                uint serial = (uint)Marshal.ReadInt32(curr, 48);
                                uint refresh = (uint)Marshal.ReadInt32(curr, 52);
                                uint retry = (uint)Marshal.ReadInt32(curr, 56);
                                uint expire = (uint)Marshal.ReadInt32(curr, 60);
                                uint defTtl = (uint)Marshal.ReadInt32(curr, 64);
                                dataJson = string.Format("{{\"primaryServer\": \"{0}\", \"administrator\": \"{1}\", \"serial\": {2}, \"refresh\": {3}, \"retry\": {4}, \"expire\": {5}, \"defaultTtl\": {6}}}",
                                    EscapeJson(prim), EscapeJson(admin), serial, refresh, retry, expire, defTtl);
                            }

                            records.Add(string.Format("{{\"name\": \"{0}\", \"type\": \"{1}\", \"typeId\": {2}, \"ttl\": {3}, \"flags\": {4}, \"data\": {5}}}",
                                EscapeJson(rName), FormatDnsType(rType), rType, rTtl, rFlags, dataJson));

                            recordCount++;
                            curr = Marshal.ReadIntPtr(curr, 0); // pNext
                        }
                    } finally {
                        DnsRecordListFree(pRecords, 1 /* DnsFreeRecordList */);
                    }
                }

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"query\": \"{0}\", \"recordType\": \"{1}\", \"recordTypeId\": {2}, \"bypassCache\": {3}, \"latencyMs\": {4:F2}, \"recordCount\": {5}, \"records\": [{6}]}}",
                    EscapeJson(name), FormatDnsType(wType), wType, bypassCache ? "true" : "false", sw.Elapsed.TotalMilliseconds, records.Count, string.Join(", ", records.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void DnsFlushCmd() {
            try {
                int res = DnsFlushResolverCache();
                bool ok = res != 0;
                Console.WriteLine(string.Format(
                    "{{\"success\": {0}, \"flushed\": {0}, \"message\": \"{1}\", \"timestamp\": \"{2}\"}}",
                    ok ? "true" : "false",
                    ok ? "Windows DNS Resolver Cache flushed successfully." : "DnsFlushResolverCache returned 0",
                    DateTime.UtcNow.ToString("o")
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"flushed\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void DnsResolveCmd(string host, bool bypassCache) {
            try {
                if (string.IsNullOrEmpty(host)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Host parameter is required\"}");
                    return;
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                uint options = DNS_QUERY_STANDARD;
                if (bypassCache) options |= DNS_QUERY_BYPASS_CACHE | DNS_QUERY_WIRE_ONLY;

                var v4List = new List<string>();
                var v6List = new List<string>();
                string canonicalName = host;
                uint minTtl = uint.MaxValue;
                var allRecords = new List<string>();

                // Query A records
                IntPtr pA;
                if (DnsQuery_W(host, DNS_TYPE_A, options, IntPtr.Zero, out pA, IntPtr.Zero) == 0 && pA != IntPtr.Zero) {
                    try {
                        IntPtr curr = pA;
                        while (curr != IntPtr.Zero) {
                            ushort rType = (ushort)Marshal.ReadInt16(curr, 16);
                            uint rTtl = (uint)Marshal.ReadInt32(curr, 24);
                            if (rTtl < minTtl) minTtl = rTtl;

                            if (rType == DNS_TYPE_A) {
                                byte b0 = Marshal.ReadByte(curr, 32);
                                byte b1 = Marshal.ReadByte(curr, 33);
                                byte b2 = Marshal.ReadByte(curr, 34);
                                byte b3 = Marshal.ReadByte(curr, 35);
                                string ip = string.Format("{0}.{1}.{2}.{3}", b0, b1, b2, b3);
                                if (!v4List.Contains(ip)) v4List.Add(ip);
                                allRecords.Add(string.Format("{{\"type\": \"A\", \"address\": \"{0}\", \"ttl\": {1}}}", ip, rTtl));
                            } else if (rType == DNS_TYPE_CNAME) {
                                IntPtr pTarget = Marshal.ReadIntPtr(curr, 32);
                                canonicalName = Marshal.PtrToStringUni(pTarget) ?? canonicalName;
                                allRecords.Add(string.Format("{{\"type\": \"CNAME\", \"target\": \"{0}\", \"ttl\": {1}}}", EscapeJson(canonicalName), rTtl));
                            }
                            curr = Marshal.ReadIntPtr(curr, 0);
                        }
                    } finally {
                        DnsRecordListFree(pA, 1);
                    }
                }

                // Query AAAA records
                IntPtr pAAAA;
                if (DnsQuery_W(host, DNS_TYPE_AAAA, options, IntPtr.Zero, out pAAAA, IntPtr.Zero) == 0 && pAAAA != IntPtr.Zero) {
                    try {
                        IntPtr curr = pAAAA;
                        while (curr != IntPtr.Zero) {
                            ushort rType = (ushort)Marshal.ReadInt16(curr, 16);
                            uint rTtl = (uint)Marshal.ReadInt32(curr, 24);
                            if (rTtl < minTtl) minTtl = rTtl;

                            if (rType == DNS_TYPE_AAAA) {
                                byte[] ip6 = new byte[16];
                                Marshal.Copy(new IntPtr(curr.ToInt64() + 32), ip6, 0, 16);
                                string ipStr = new System.Net.IPAddress(ip6).ToString();
                                if (!v6List.Contains(ipStr)) v6List.Add(ipStr);
                                allRecords.Add(string.Format("{{\"type\": \"AAAA\", \"address\": \"{0}\", \"ttl\": {1}}}", EscapeJson(ipStr), rTtl));
                            }
                            curr = Marshal.ReadIntPtr(curr, 0);
                        }
                    } finally {
                        DnsRecordListFree(pAAAA, 1);
                    }
                }

                sw.Stop();
                if (minTtl == uint.MaxValue) minTtl = 0;

                var v4Json = new List<string>();
                foreach (var ip in v4List) v4Json.Add("\"" + EscapeJson(ip) + "\"");
                var v6Json = new List<string>();
                foreach (var ip in v6List) v6Json.Add("\"" + EscapeJson(ip) + "\"");

                Console.WriteLine(string.Format(
                    "{{\"success\": true, \"host\": \"{0}\", \"canonicalName\": \"{1}\", \"latencyMs\": {2:F2}, \"minTtl\": {3}, \"ipv4Addresses\": [{4}], \"ipv6Addresses\": [{5}], \"records\": [{6}]}}",
                    EscapeJson(host), EscapeJson(canonicalName), sw.Elapsed.TotalMilliseconds, minTtl, string.Join(", ", v4Json.ToArray()), string.Join(", ", v6Json.ToArray()), string.Join(", ", allRecords.ToArray())
                ));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows Data Protection API (DPAPI) Subsystem (dpapi.h / crypt32.dll)

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DPAPI_DATA_BLOB {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptProtectData(
            ref DPAPI_DATA_BLOB pDataIn,
            string szDataDescr,
            ref DPAPI_DATA_BLOB pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            uint dwFlags,
            out DPAPI_DATA_BLOB pDataOut
        );

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptUnprotectData(
            ref DPAPI_DATA_BLOB pDataIn,
            out IntPtr ppszDataDescr,
            ref DPAPI_DATA_BLOB pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            uint dwFlags,
            out DPAPI_DATA_BLOB pDataOut
        );

        const uint DPAPI_CRYPTPROTECT_UI_FORBIDDEN = 0x1;
        const uint DPAPI_CRYPTPROTECT_LOCAL_MACHINE = 0x4;

        static void DpapiProtectCmd(string data, string description, string scope, string entropy) {
            try {
                if (data == null) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Data parameter is required\"}");
                    return;
                }

                byte[] plainBytes = Encoding.UTF8.GetBytes(data);
                string desc = description ?? "Gemini DPAPI Protected Secret";
                string sc = (scope ?? "CurrentUser").Trim();
                bool isLocalMachine = sc.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase) || sc.Equals("machine", StringComparison.OrdinalIgnoreCase);

                uint flags = DPAPI_CRYPTPROTECT_UI_FORBIDDEN;
                if (isLocalMachine) flags |= DPAPI_CRYPTPROTECT_LOCAL_MACHINE;

                DPAPI_DATA_BLOB inBlob = new DPAPI_DATA_BLOB();
                inBlob.cbData = plainBytes.Length;
                inBlob.pbData = Marshal.AllocHGlobal(plainBytes.Length);
                Marshal.Copy(plainBytes, 0, inBlob.pbData, plainBytes.Length);

                DPAPI_DATA_BLOB entBlob = new DPAPI_DATA_BLOB();
                bool hasEntropy = !string.IsNullOrEmpty(entropy);
                if (hasEntropy) {
                    byte[] entBytes = Encoding.UTF8.GetBytes(entropy);
                    entBlob.cbData = entBytes.Length;
                    entBlob.pbData = Marshal.AllocHGlobal(entBytes.Length);
                    Marshal.Copy(entBytes, 0, entBlob.pbData, entBytes.Length);
                }

                DPAPI_DATA_BLOB outBlob = new DPAPI_DATA_BLOB();

                try {
                    bool ok = CryptProtectData(
                        ref inBlob,
                        desc,
                        ref entBlob,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        flags,
                        out outBlob
                    );

                    if (!ok) {
                        int err = Marshal.GetLastWin32Error();
                        Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"CryptProtectData failed with error {0}\"}}", err));
                        return;
                    }

                    byte[] cipherBytes = new byte[outBlob.cbData];
                    Marshal.Copy(outBlob.pbData, cipherBytes, 0, outBlob.cbData);
                    string base64 = Convert.ToBase64String(cipherBytes);

                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"cipherBase64\": \"{0}\", \"description\": \"{1}\", \"scope\": \"{2}\", \"cipherSizeBytes\": {3}, \"entropyUsed\": {4}}}",
                        base64,
                        EscapeJson(desc),
                        isLocalMachine ? "LocalMachine" : "CurrentUser",
                        cipherBytes.Length,
                        hasEntropy ? "true" : "false"
                    ));
                } finally {
                    if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
                    Marshal.FreeHGlobal(inBlob.pbData);
                    if (entBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(entBlob.pbData);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void DpapiUnprotectCmd(string cipherBase64, string entropy) {
            try {
                if (string.IsNullOrEmpty(cipherBase64)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"cipherBase64 parameter is required\"}");
                    return;
                }

                byte[] cipherBytes = Convert.FromBase64String(cipherBase64);

                DPAPI_DATA_BLOB inBlob = new DPAPI_DATA_BLOB();
                inBlob.cbData = cipherBytes.Length;
                inBlob.pbData = Marshal.AllocHGlobal(cipherBytes.Length);
                Marshal.Copy(cipherBytes, 0, inBlob.pbData, cipherBytes.Length);

                DPAPI_DATA_BLOB entBlob = new DPAPI_DATA_BLOB();
                bool hasEntropy = !string.IsNullOrEmpty(entropy);
                if (hasEntropy) {
                    byte[] entBytes = Encoding.UTF8.GetBytes(entropy);
                    entBlob.cbData = entBytes.Length;
                    entBlob.pbData = Marshal.AllocHGlobal(entBytes.Length);
                    Marshal.Copy(entBytes, 0, entBlob.pbData, entBytes.Length);
                }

                DPAPI_DATA_BLOB decBlob = new DPAPI_DATA_BLOB();
                IntPtr pDescr = IntPtr.Zero;

                try {
                    bool ok = CryptUnprotectData(
                        ref inBlob,
                        out pDescr,
                        ref entBlob,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        DPAPI_CRYPTPROTECT_UI_FORBIDDEN,
                        out decBlob
                    );

                    if (!ok) {
                        int err = Marshal.GetLastWin32Error();
                        Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"CryptUnprotectData failed with error {0}\"}}", err));
                        return;
                    }

                    byte[] decBytes = new byte[decBlob.cbData];
                    Marshal.Copy(decBlob.pbData, decBytes, 0, decBlob.cbData);
                    string recovered = Encoding.UTF8.GetString(decBytes);
                    string recoveredLabel = pDescr != IntPtr.Zero ? Marshal.PtrToStringUni(pDescr) ?? "" : "";

                    Console.WriteLine(string.Format(
                        "{{\"success\": true, \"data\": \"{0}\", \"description\": \"{1}\", \"plainSizeBytes\": {2}, \"entropyUsed\": {3}}}",
                        EscapeJson(recovered),
                        EscapeJson(recoveredLabel),
                        decBytes.Length,
                        hasEntropy ? "true" : "false"
                    ));
                } finally {
                    if (pDescr != IntPtr.Zero) LocalFree(pDescr);
                    if (decBlob.pbData != IntPtr.Zero) LocalFree(decBlob.pbData);
                    Marshal.FreeHGlobal(inBlob.pbData);
                    if (entBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(entBlob.pbData);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void DpapiProtectFileCmd(string action, string sourcePath, string targetPath, string scope, string description, string entropy) {
            try {
                if (string.IsNullOrEmpty(sourcePath) || !System.IO.File.Exists(sourcePath)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Source file does not exist: {0}\"}}", EscapeJson(sourcePath ?? "")));
                    return;
                }

                string act = (action ?? "encrypt").Trim().ToLowerInvariant();
                string outPath = string.IsNullOrEmpty(targetPath) ? sourcePath : targetPath;
                byte[] inBytes = System.IO.File.ReadAllBytes(sourcePath);

                if (act == "decrypt" || act == "unprotect") {
                    DPAPI_DATA_BLOB inBlob = new DPAPI_DATA_BLOB();
                    inBlob.cbData = inBytes.Length;
                    inBlob.pbData = Marshal.AllocHGlobal(inBytes.Length);
                    Marshal.Copy(inBytes, 0, inBlob.pbData, inBytes.Length);

                    DPAPI_DATA_BLOB entBlob = new DPAPI_DATA_BLOB();
                    bool hasEntropy = !string.IsNullOrEmpty(entropy);
                    if (hasEntropy) {
                        byte[] entBytes = Encoding.UTF8.GetBytes(entropy);
                        entBlob.cbData = entBytes.Length;
                        entBlob.pbData = Marshal.AllocHGlobal(entBytes.Length);
                        Marshal.Copy(entBytes, 0, entBlob.pbData, entBytes.Length);
                    }

                    DPAPI_DATA_BLOB decBlob = new DPAPI_DATA_BLOB();
                    IntPtr pDescr = IntPtr.Zero;

                    try {
                        bool ok = CryptUnprotectData(
                            ref inBlob,
                            out pDescr,
                            ref entBlob,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            DPAPI_CRYPTPROTECT_UI_FORBIDDEN,
                            out decBlob
                        );

                        if (!ok) {
                            int err = Marshal.GetLastWin32Error();
                            Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"CryptUnprotectData failed on file with error {0}\"}}", err));
                            return;
                        }

                        byte[] decBytes = new byte[decBlob.cbData];
                        Marshal.Copy(decBlob.pbData, decBytes, 0, decBlob.cbData);
                        System.IO.File.WriteAllBytes(outPath, decBytes);

                        string recoveredLabel = pDescr != IntPtr.Zero ? Marshal.PtrToStringUni(pDescr) ?? "" : "";
                        Console.WriteLine(string.Format(
                            "{{\"success\": true, \"action\": \"decrypt\", \"sourcePath\": \"{0}\", \"targetPath\": \"{1}\", \"bytesIn\": {2}, \"bytesOut\": {3}, \"description\": \"{4}\"}}",
                            EscapeJson(sourcePath), EscapeJson(outPath), inBytes.Length, decBytes.Length, EscapeJson(recoveredLabel)
                        ));
                    } finally {
                        if (pDescr != IntPtr.Zero) LocalFree(pDescr);
                        if (decBlob.pbData != IntPtr.Zero) LocalFree(decBlob.pbData);
                        Marshal.FreeHGlobal(inBlob.pbData);
                        if (entBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(entBlob.pbData);
                    }
                } else {
                    string sc = (scope ?? "CurrentUser").Trim();
                    bool isLocalMachine = sc.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase) || sc.Equals("machine", StringComparison.OrdinalIgnoreCase);
                    uint flags = DPAPI_CRYPTPROTECT_UI_FORBIDDEN;
                    if (isLocalMachine) flags |= DPAPI_CRYPTPROTECT_LOCAL_MACHINE;

                    string desc = description ?? ("DPAPI File Protection - " + System.IO.Path.GetFileName(sourcePath));

                    DPAPI_DATA_BLOB inBlob = new DPAPI_DATA_BLOB();
                    inBlob.cbData = inBytes.Length;
                    inBlob.pbData = Marshal.AllocHGlobal(inBytes.Length);
                    Marshal.Copy(inBytes, 0, inBlob.pbData, inBytes.Length);

                    DPAPI_DATA_BLOB entBlob = new DPAPI_DATA_BLOB();
                    bool hasEntropy = !string.IsNullOrEmpty(entropy);
                    if (hasEntropy) {
                        byte[] entBytes = Encoding.UTF8.GetBytes(entropy);
                        entBlob.cbData = entBytes.Length;
                        entBlob.pbData = Marshal.AllocHGlobal(entBytes.Length);
                        Marshal.Copy(entBytes, 0, entBlob.pbData, entBytes.Length);
                    }

                    DPAPI_DATA_BLOB outBlob = new DPAPI_DATA_BLOB();

                    try {
                        bool ok = CryptProtectData(
                            ref inBlob,
                            desc,
                            ref entBlob,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            flags,
                            out outBlob
                        );

                        if (!ok) {
                            int err = Marshal.GetLastWin32Error();
                            Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"CryptProtectData failed on file with error {0}\"}}", err));
                            return;
                        }

                        byte[] cipherBytes = new byte[outBlob.cbData];
                        Marshal.Copy(outBlob.pbData, cipherBytes, 0, outBlob.cbData);
                        System.IO.File.WriteAllBytes(outPath, cipherBytes);

                        Console.WriteLine(string.Format(
                            "{{\"success\": true, \"action\": \"encrypt\", \"sourcePath\": \"{0}\", \"targetPath\": \"{1}\", \"bytesIn\": {2}, \"bytesOut\": {3}, \"scope\": \"{4}\", \"description\": \"{5}\"}}",
                            EscapeJson(sourcePath), EscapeJson(outPath), inBytes.Length, cipherBytes.Length, isLocalMachine ? "LocalMachine" : "CurrentUser", EscapeJson(desc)
                        ));
                    } finally {
                        if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
                        Marshal.FreeHGlobal(inBlob.pbData);
                        if (entBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(entBlob.pbData);
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows File System Volume & Storage Mount Management (fileapi.h / kernel32.dll)

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindFirstVolumeW(StringBuilder lpszVolumeName, uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindNextVolumeW(IntPtr hFindVolume, StringBuilder lpszVolumeName, uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindVolumeClose(IntPtr hFindVolume);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetVolumeInformationW(
            string lpRootPathName,
            StringBuilder lpVolumeNameBuffer,
            uint nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
            out uint lpFileSystemFlags,
            StringBuilder lpFileSystemNameBuffer,
            uint nFileSystemNameSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetVolumePathNamesForVolumeNameW(
            string lpszVolumeName,
            char[] lpszVolumePathNames,
            uint cchBufferLength,
            out uint lpcchReturnLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetVolumeNameForVolumeMountPointW(
            string lpszVolumeMountPoint,
            StringBuilder lpszVolumeName,
            uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetVolumePathNameW(
            string lpszFileName,
            StringBuilder lpszVolumePathName,
            uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceExW(
            string lpDirectoryName,
            out ulong lpFreeBytesAvailableToCaller,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindFirstVolumeMountPointW(
            string lpszRootPathName,
            StringBuilder lpszVolumeMountPoint,
            uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindNextVolumeMountPointW(
            IntPtr hFindVolumeMountPoint,
            StringBuilder lpszVolumeMountPoint,
            uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindVolumeMountPointClose(IntPtr hFindVolumeMountPoint);

        [DllImport("kernel32.dll")]
        public static extern uint GetLogicalDrives();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint GetDriveTypeW(string lpRootPathName);

        static List<string> DecodeFileSystemFlags(uint flags) {
            var list = new List<string>();
            if ((flags & 0x00000001) != 0) list.Add("FILE_CASE_SENSITIVE_SEARCH");
            if ((flags & 0x00000002) != 0) list.Add("FILE_CASE_PRESERVED_NAMES");
            if ((flags & 0x00000004) != 0) list.Add("FILE_UNICODE_ON_DISK");
            if ((flags & 0x00000008) != 0) list.Add("FILE_PERSISTENT_ACLS");
            if ((flags & 0x00000010) != 0) list.Add("FILE_FILE_COMPRESSION");
            if ((flags & 0x00000020) != 0) list.Add("FILE_VOLUME_QUOTAS");
            if ((flags & 0x00000040) != 0) list.Add("FILE_SUPPORTS_SPARSE_FILES");
            if ((flags & 0x00000080) != 0) list.Add("FILE_SUPPORTS_REPARSE_POINTS");
            if ((flags & 0x00000100) != 0) list.Add("FILE_SUPPORTS_REMOTE_STORAGE");
            if ((flags & 0x00008000) != 0) list.Add("FILE_VOLUME_IS_COMPRESSED");
            if ((flags & 0x00010000) != 0) list.Add("FILE_SUPPORTS_OBJECT_IDS");
            if ((flags & 0x00020000) != 0) list.Add("FILE_SUPPORTS_ENCRYPTION");
            if ((flags & 0x00040000) != 0) list.Add("FILE_NAMED_STREAMS");
            if ((flags & 0x00080000) != 0) list.Add("FILE_READ_ONLY_VOLUME");
            if ((flags & 0x00100000) != 0) list.Add("FILE_SEQUENTIAL_WRITE_ONCE");
            if ((flags & 0x00200000) != 0) list.Add("FILE_SUPPORTS_TRANSACTIONS");
            if ((flags & 0x00400000) != 0) list.Add("FILE_SUPPORTS_HARD_LINKS");
            if ((flags & 0x00800000) != 0) list.Add("FILE_SUPPORTS_EXTENDED_ATTRIBUTES");
            if ((flags & 0x01000000) != 0) list.Add("FILE_SUPPORTS_OPEN_BY_FILE_ID");
            if ((flags & 0x02000000) != 0) list.Add("FILE_SUPPORTS_USN_JOURNAL");
            if ((flags & 0x04000000) != 0) list.Add("FILE_SUPPORTS_INTEGRITY_STREAMS");
            if ((flags & 0x08000000) != 0) list.Add("FILE_SUPPORTS_BLOCK_REFCOUNTING");
            if ((flags & 0x10000000) != 0) list.Add("FILE_SUPPORTS_SPARSE_VDL");
            if ((flags & 0x20000000) != 0) list.Add("FILE_DAX_VOLUME");
            return list;
        }

        static string GetDriveTypeName(uint driveType) {
            switch (driveType) {
                case 0: return "DRIVE_UNKNOWN";
                case 1: return "DRIVE_NO_ROOT_DIR";
                case 2: return "DRIVE_REMOVABLE";
                case 3: return "DRIVE_FIXED";
                case 4: return "DRIVE_REMOTE";
                case 5: return "DRIVE_CDROM";
                case 6: return "DRIVE_RAMDISK";
                default: return string.Format("DRIVE_OTHER_{0}", driveType);
            }
        }

        static List<string> GetVolumeMountPaths(string volumeGuidPath) {
            var paths = new List<string>();
            uint returnLength = 0;
            GetVolumePathNamesForVolumeNameW(volumeGuidPath, null, 0, out returnLength);
            if (returnLength > 1) {
                char[] buffer = new char[returnLength];
                if (GetVolumePathNamesForVolumeNameW(volumeGuidPath, buffer, returnLength, out returnLength)) {
                    int start = 0;
                    for (int i = 0; i < returnLength; i++) {
                        if (buffer[i] == '\0') {
                            if (i > start) {
                                paths.Add(new string(buffer, start, i - start));
                            }
                            start = i + 1;
                        }
                    }
                }
            }
            return paths;
        }

        class VolumeMetaRecord {
            public string VolumeGuid = "";
            public List<string> MountPaths = new List<string>();
            public string VolumeLabel = "";
            public string FileSystemName = "";
            public uint SerialNumber = 0;
            public string SerialNumberHex = "0000-0000";
            public uint MaxComponentLength = 0;
            public uint Flags = 0;
            public List<string> DecodedFlags = new List<string>();
            public ulong FreeBytesAvailable = 0;
            public ulong TotalBytes = 0;
            public ulong TotalFreeBytes = 0;
            public double FreePercentage = 0.0;
            public bool HasCapacity = false;
        }

        static VolumeMetaRecord QueryVolumeDetails(string path) {
            var meta = new VolumeMetaRecord();
            meta.VolumeGuid = path ?? "";
            
            string root = path ?? "";
            if (!root.EndsWith("\\")) root += "\\";
            
            if (root.StartsWith("\\\\?\\Volume{", StringComparison.OrdinalIgnoreCase)) {
                meta.VolumeGuid = root;
                meta.MountPaths = GetVolumeMountPaths(root);
            } else {
                var sbGuid = new StringBuilder(128);
                if (GetVolumeNameForVolumeMountPointW(root, sbGuid, (uint)sbGuid.Capacity)) {
                    meta.VolumeGuid = sbGuid.ToString();
                    meta.MountPaths = GetVolumeMountPaths(meta.VolumeGuid);
                } else {
                    meta.MountPaths.Add(root);
                }
            }

            var sbVolName = new StringBuilder(260);
            var sbFsName = new StringBuilder(260);
            uint serialNum = 0;
            uint maxCompLen = 0;
            uint fsFlags = 0;

            if (GetVolumeInformationW(root, sbVolName, (uint)sbVolName.Capacity, out serialNum, out maxCompLen, out fsFlags, sbFsName, (uint)sbFsName.Capacity)) {
                meta.VolumeLabel = sbVolName.ToString();
                meta.FileSystemName = sbFsName.ToString();
                meta.SerialNumber = serialNum;
                meta.SerialNumberHex = string.Format("{0:X4}-{1:X4}", (serialNum >> 16) & 0xFFFF, serialNum & 0xFFFF);
                meta.MaxComponentLength = maxCompLen;
                meta.Flags = fsFlags;
                meta.DecodedFlags = DecodeFileSystemFlags(fsFlags);
            }

            ulong freeCaller = 0, totalBytes = 0, totalFree = 0;
            if (GetDiskFreeSpaceExW(root, out freeCaller, out totalBytes, out totalFree)) {
                meta.FreeBytesAvailable = freeCaller;
                meta.TotalBytes = totalBytes;
                meta.TotalFreeBytes = totalFree;
                meta.FreePercentage = totalBytes > 0 ? Math.Round((double)totalFree / (double)totalBytes * 100.0, 2) : 0.0;
                meta.HasCapacity = true;
            }

            return meta;
        }

        static void FsVolumesCmd() {
            try {
                var volumes = new List<VolumeMetaRecord>();
                var sbName = new StringBuilder(260);
                IntPtr hFind = FindFirstVolumeW(sbName, (uint)sbName.Capacity);
                if (hFind != new IntPtr(-1)) {
                    try {
                        do {
                            string volGuid = sbName.ToString();
                            VolumeMetaRecord vMeta = QueryVolumeDetails(volGuid);
                            volumes.Add(vMeta);
                        } while (FindNextVolumeW(hFind, sbName, (uint)sbName.Capacity));
                    } finally {
                        FindVolumeClose(hFind);
                    }
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{\"success\": true, \"count\": " + volumes.Count + ", \"volumes\": [");
                for (int i = 0; i < volumes.Count; i++) {
                    var v = volumes[i];
                    if (i > 0) sbOut.Append(", ");
                    sbOut.Append("{");
                    sbOut.AppendFormat("\"volumeGuid\": \"{0}\", ", EscapeJson(v.VolumeGuid));
                    sbOut.AppendFormat("\"volumeLabel\": \"{0}\", ", EscapeJson(v.VolumeLabel));
                    sbOut.AppendFormat("\"fileSystemName\": \"{0}\", ", EscapeJson(v.FileSystemName));
                    sbOut.AppendFormat("\"serialNumberHex\": \"{0}\", ", v.SerialNumberHex);
                    sbOut.AppendFormat("\"maxComponentLength\": {0}, ", v.MaxComponentLength);
                    sbOut.AppendFormat("\"flags\": {0}, ", v.Flags);
                    
                    sbOut.Append("\"decodedFlags\": [");
                    for (int f = 0; f < v.DecodedFlags.Count; f++) {
                        if (f > 0) sbOut.Append(", ");
                        sbOut.AppendFormat("\"{0}\"", v.DecodedFlags[f]);
                    }
                    sbOut.Append("], ");

                    sbOut.Append("\"mountPaths\": [");
                    for (int p = 0; p < v.MountPaths.Count; p++) {
                        if (p > 0) sbOut.Append(", ");
                        sbOut.AppendFormat("\"{0}\"", EscapeJson(v.MountPaths[p]));
                    }
                    sbOut.Append("], ");

                    sbOut.AppendFormat("\"totalBytes\": {0}, ", v.TotalBytes);
                    sbOut.AppendFormat("\"freeBytes\": {0}, ", v.TotalFreeBytes);
                    sbOut.AppendFormat("\"availableBytes\": {0}, ", v.FreeBytesAvailable);
                    sbOut.AppendFormat("\"freePercentage\": {0}, ", v.FreePercentage);
                    sbOut.AppendFormat("\"totalGB\": {0}, ", Math.Round((double)v.TotalBytes / (1024.0 * 1024.0 * 1024.0), 2));
                    sbOut.AppendFormat("\"freeGB\": {0}", Math.Round((double)v.TotalFreeBytes / (1024.0 * 1024.0 * 1024.0), 2));
                    sbOut.Append("}");
                }
                sbOut.Append("]}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void FsVolumeMountPointsCmd(string rootPath) {
            try {
                if (string.IsNullOrEmpty(rootPath)) rootPath = "C:\\";
                if (!rootPath.EndsWith("\\")) rootPath += "\\";

                var mountPoints = new List<string>();
                var sbMp = new StringBuilder(260);
                IntPtr hFind = FindFirstVolumeMountPointW(rootPath, sbMp, (uint)sbMp.Capacity);
                if (hFind != new IntPtr(-1)) {
                    try {
                        do {
                            string mp = sbMp.ToString();
                            if (!string.IsNullOrEmpty(mp)) {
                                mountPoints.Add(mp);
                            }
                        } while (FindNextVolumeMountPointW(hFind, sbMp, (uint)sbMp.Capacity));
                    } finally {
                        FindVolumeMountPointClose(hFind);
                    }
                }

                var sbResolved = new StringBuilder(260);
                string resolvedVolumePath = "";
                if (GetVolumePathNameW(rootPath, sbResolved, (uint)sbResolved.Capacity)) {
                    resolvedVolumePath = sbResolved.ToString();
                }

                var sbGuid = new StringBuilder(128);
                string rootVolumeGuid = "";
                if (GetVolumeNameForVolumeMountPointW(rootPath, sbGuid, (uint)sbGuid.Capacity)) {
                    rootVolumeGuid = sbGuid.ToString();
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"rootPath\": \"{0}\", ", EscapeJson(rootPath));
                sbOut.AppendFormat("\"resolvedVolumePath\": \"{0}\", ", EscapeJson(resolvedVolumePath));
                sbOut.AppendFormat("\"rootVolumeGuid\": \"{0}\", ", EscapeJson(rootVolumeGuid));
                sbOut.AppendFormat("\"mountPointCount\": {0}, ", mountPoints.Count);
                sbOut.Append("\"mountPoints\": [");
                for (int i = 0; i < mountPoints.Count; i++) {
                    if (i > 0) sbOut.Append(", ");
                    sbOut.AppendFormat("\"{0}\"", EscapeJson(mountPoints[i]));
                }
                sbOut.Append("]}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void FsDrivesCmd(string driveFilter) {
            try {
                uint bitmask = GetLogicalDrives();
                string filterClean = (driveFilter ?? "").Trim().ToUpperInvariant();
                if (filterClean.Length > 0 && !filterClean.EndsWith("\\")) {
                    if (filterClean.Length == 1 || (filterClean.Length == 2 && filterClean[1] == ':')) {
                        filterClean = filterClean.Substring(0, 1) + ":\\";
                    }
                }

                var list = new List<string>();
                for (int i = 0; i < 26; i++) {
                    if ((bitmask & (1u << i)) != 0) {
                        char letter = (char)('A' + i);
                        string root = letter + ":\\";
                        if (!string.IsNullOrEmpty(filterClean) && !root.Equals(filterClean, StringComparison.OrdinalIgnoreCase)) {
                            continue;
                        }

                        uint dType = GetDriveTypeW(root);
                        string dTypeName = GetDriveTypeName(dType);
                        VolumeMetaRecord meta = QueryVolumeDetails(root);

                        var sbItem = new StringBuilder();
                        sbItem.Append("{");
                        sbItem.AppendFormat("\"driveLetter\": \"{0}:\", ", letter);
                        sbItem.AppendFormat("\"rootPath\": \"{0}\", ", EscapeJson(root));
                        sbItem.AppendFormat("\"driveType\": {0}, ", dType);
                        sbItem.AppendFormat("\"driveTypeName\": \"{0}\", ", dTypeName);
                        sbItem.AppendFormat("\"isReady\": {0}, ", meta.HasCapacity ? "true" : "false");
                        sbItem.AppendFormat("\"volumeLabel\": \"{0}\", ", EscapeJson(meta.VolumeLabel));
                        sbItem.AppendFormat("\"fileSystemName\": \"{0}\", ", EscapeJson(meta.FileSystemName));
                        sbItem.AppendFormat("\"serialNumberHex\": \"{0}\", ", meta.SerialNumberHex);
                        sbItem.AppendFormat("\"volumeGuid\": \"{0}\", ", EscapeJson(meta.VolumeGuid));
                        sbItem.AppendFormat("\"totalBytes\": {0}, ", meta.TotalBytes);
                        sbItem.AppendFormat("\"freeBytes\": {0}, ", meta.TotalFreeBytes);
                        sbItem.AppendFormat("\"availableBytes\": {0}, ", meta.FreeBytesAvailable);
                        sbItem.AppendFormat("\"freePercentage\": {0}, ", meta.FreePercentage);
                        sbItem.AppendFormat("\"totalGB\": {0}, ", Math.Round((double)meta.TotalBytes / (1024.0 * 1024.0 * 1024.0), 2));
                        sbItem.AppendFormat("\"freeGB\": {0}, ", Math.Round((double)meta.TotalFreeBytes / (1024.0 * 1024.0 * 1024.0), 2));
                        
                        sbItem.Append("\"decodedFlags\": [");
                        for (int f = 0; f < meta.DecodedFlags.Count; f++) {
                            if (f > 0) sbItem.Append(", ");
                            sbItem.AppendFormat("\"{0}\"", meta.DecodedFlags[f]);
                        }
                        sbItem.Append("]}");

                        list.Add(sbItem.ToString());
                    }
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{\"success\": true, \"count\": " + list.Count + ", \"drives\": [");
                for (int i = 0; i < list.Count; i++) {
                    if (i > 0) sbOut.Append(", ");
                    sbOut.Append(list[i]);
                }
                sbOut.Append("]}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows Print Spooler Subsystem (winspool.drv / winspool.h)

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PRINTER_INFO_2_WIN32 {
            public string pServerName;
            public string pPrinterName;
            public string pShareName;
            public string pPortName;
            public string pDriverName;
            public string pComment;
            public string pLocation;
            public IntPtr pDevMode;
            public string pSepFile;
            public string pPrintProcessor;
            public string pDatatype;
            public string pParameters;
            public IntPtr pSecurityDescriptor;
            public uint Attributes;
            public uint Priority;
            public uint DefaultPriority;
            public uint StartTime;
            public uint UntilTime;
            public uint Status;
            public uint cJobs;
            public uint AveragePPM;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SPOOLER_SYSTEMTIME {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct JOB_INFO_1_WIN32 {
            public uint JobId;
            public string pPrinterName;
            public string pMachineName;
            public string pUserName;
            public string pDocument;
            public string pDatatype;
            public string pStatus;
            public uint Status;
            public uint Priority;
            public uint Position;
            public uint TotalPages;
            public uint PagesPrinted;
            public SPOOLER_SYSTEMTIME Submitted;
        }

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumPrintersW(
            uint Flags,
            string Name,
            uint Level,
            IntPtr pPrinterEnum,
            uint cbBuf,
            out uint pcbNeeded,
            out uint pcReturned);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDefaultPrinterW(
            StringBuilder pszBuffer,
            ref uint pcchBuffer);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetDefaultPrinterW(
            string pszPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenPrinterW(
            string pPrinterName,
            out IntPtr phPrinter,
            IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClosePrinter(
            IntPtr hPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumJobsW(
            IntPtr hPrinter,
            uint FirstJob,
            uint NoJobs,
            uint Level,
            IntPtr pJob,
            uint cbBuf,
            out uint pcbNeeded,
            out uint pcReturned);

        static List<string> DecodePrinterAttributes(uint attrs) {
            var list = new List<string>();
            if ((attrs & 0x00000001) != 0) list.Add("PRINTER_ATTRIBUTE_QUEUED");
            if ((attrs & 0x00000002) != 0) list.Add("PRINTER_ATTRIBUTE_DIRECT");
            if ((attrs & 0x00000004) != 0) list.Add("PRINTER_ATTRIBUTE_DEFAULT");
            if ((attrs & 0x00000008) != 0) list.Add("PRINTER_ATTRIBUTE_SHARED");
            if ((attrs & 0x00000010) != 0) list.Add("PRINTER_ATTRIBUTE_NETWORK");
            if ((attrs & 0x00000020) != 0) list.Add("PRINTER_ATTRIBUTE_HIDDEN");
            if ((attrs & 0x00000040) != 0) list.Add("PRINTER_ATTRIBUTE_LOCAL");
            if ((attrs & 0x00000080) != 0) list.Add("PRINTER_ATTRIBUTE_ENABLE_DEVQ");
            if ((attrs & 0x00000100) != 0) list.Add("PRINTER_ATTRIBUTE_KEEPPRINTEDJOBS");
            if ((attrs & 0x00000200) != 0) list.Add("PRINTER_ATTRIBUTE_DO_COMPLETE_FIRST");
            if ((attrs & 0x00000400) != 0) list.Add("PRINTER_ATTRIBUTE_WORK_OFFLINE");
            if ((attrs & 0x00000800) != 0) list.Add("PRINTER_ATTRIBUTE_ENABLE_BIDI");
            if ((attrs & 0x00001000) != 0) list.Add("PRINTER_ATTRIBUTE_RAW_ONLY");
            if ((attrs & 0x00002000) != 0) list.Add("PRINTER_ATTRIBUTE_PUBLISHED");
            if ((attrs & 0x00004000) != 0) list.Add("PRINTER_ATTRIBUTE_FAX");
            if ((attrs & 0x00008000) != 0) list.Add("PRINTER_ATTRIBUTE_TS");
            return list;
        }

        static List<string> DecodePrinterStatus(uint status) {
            var list = new List<string>();
            if (status == 0) { list.Add("PRINTER_STATUS_READY"); return list; }
            if ((status & 0x00000001) != 0) list.Add("PRINTER_STATUS_PAUSED");
            if ((status & 0x00000002) != 0) list.Add("PRINTER_STATUS_ERROR");
            if ((status & 0x00000004) != 0) list.Add("PRINTER_STATUS_PENDING_DELETION");
            if ((status & 0x00000008) != 0) list.Add("PRINTER_STATUS_PAPER_JAM");
            if ((status & 0x00000010) != 0) list.Add("PRINTER_STATUS_PAPER_OUT");
            if ((status & 0x00000020) != 0) list.Add("PRINTER_STATUS_MANUAL_FEED");
            if ((status & 0x00000040) != 0) list.Add("PRINTER_STATUS_PAPER_PROBLEM");
            if ((status & 0x00000080) != 0) list.Add("PRINTER_STATUS_OFFLINE");
            if ((status & 0x00000100) != 0) list.Add("PRINTER_STATUS_IO_ACTIVE");
            if ((status & 0x00000200) != 0) list.Add("PRINTER_STATUS_BUSY");
            if ((status & 0x00000400) != 0) list.Add("PRINTER_STATUS_PRINTING");
            if ((status & 0x00000800) != 0) list.Add("PRINTER_STATUS_OUTPUT_BIN_FULL");
            if ((status & 0x00001000) != 0) list.Add("PRINTER_STATUS_NOT_AVAILABLE");
            if ((status & 0x00002000) != 0) list.Add("PRINTER_STATUS_WAITING");
            if ((status & 0x00004000) != 0) list.Add("PRINTER_STATUS_PROCESSING");
            if ((status & 0x00008000) != 0) list.Add("PRINTER_STATUS_INITIALIZING");
            if ((status & 0x00010000) != 0) list.Add("PRINTER_STATUS_WARMING_UP");
            if ((status & 0x00020000) != 0) list.Add("PRINTER_STATUS_TONER_LOW");
            if ((status & 0x00040000) != 0) list.Add("PRINTER_STATUS_NO_TONER");
            if ((status & 0x00080000) != 0) list.Add("PRINTER_STATUS_PAGE_PUNT");
            if ((status & 0x00100000) != 0) list.Add("PRINTER_STATUS_USER_INTERVENTION");
            if ((status & 0x00200000) != 0) list.Add("PRINTER_STATUS_OUT_OF_MEMORY");
            if ((status & 0x00400000) != 0) list.Add("PRINTER_STATUS_DOOR_OPEN");
            if ((status & 0x00800000) != 0) list.Add("PRINTER_STATUS_SERVER_UNKNOWN");
            if ((status & 0x01000000) != 0) list.Add("PRINTER_STATUS_POWER_SAVE");
            return list;
        }

        static List<string> DecodeJobStatus(uint status) {
            var list = new List<string>();
            if (status == 0) { list.Add("JOB_STATUS_READY"); return list; }
            if ((status & 0x00000001) != 0) list.Add("JOB_STATUS_PAUSED");
            if ((status & 0x00000002) != 0) list.Add("JOB_STATUS_ERROR");
            if ((status & 0x00000004) != 0) list.Add("JOB_STATUS_DELETING");
            if ((status & 0x00000008) != 0) list.Add("JOB_STATUS_SPOOLING");
            if ((status & 0x00000010) != 0) list.Add("JOB_STATUS_PRINTING");
            if ((status & 0x00000020) != 0) list.Add("JOB_STATUS_OFFLINE");
            if ((status & 0x00000040) != 0) list.Add("JOB_STATUS_PAPEROUT");
            if ((status & 0x00000080) != 0) list.Add("JOB_STATUS_PRINTED");
            if ((status & 0x00000100) != 0) list.Add("JOB_STATUS_DELETED");
            if ((status & 0x00000200) != 0) list.Add("JOB_STATUS_BLOCKED_DEVQ");
            if ((status & 0x00000400) != 0) list.Add("JOB_STATUS_USER_INTERVENTION");
            if ((status & 0x00000800) != 0) list.Add("JOB_STATUS_RESTART");
            if ((status & 0x00001000) != 0) list.Add("JOB_STATUS_COMPLETE");
            if ((status & 0x00002000) != 0) list.Add("JOB_STATUS_RETAINED");
            return list;
        }

        static string QueryDefaultPrinterName() {
            var sb = new StringBuilder(260);
            uint size = (uint)sb.Capacity;
            if (GetDefaultPrinterW(sb, ref size)) {
                return sb.ToString();
            }
            return "";
        }

        static void SpoolerPrintersCmd() {
            try {
                string defaultPrinter = QueryDefaultPrinterName();
                uint flags = 0x00000002 | 0x00000004; // PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS
                uint cbNeeded = 0;
                uint cReturned = 0;
                EnumPrintersW(flags, null, 2, IntPtr.Zero, 0, out cbNeeded, out cReturned);

                var printers = new List<string>();
                if (cbNeeded > 0) {
                    IntPtr pBuf = Marshal.AllocHGlobal((int)cbNeeded);
                    try {
                        if (EnumPrintersW(flags, null, 2, pBuf, cbNeeded, out cbNeeded, out cReturned)) {
                            int structSize = Marshal.SizeOf(typeof(PRINTER_INFO_2_WIN32));
                            for (int i = 0; i < cReturned; i++) {
                                IntPtr pItem = new IntPtr(pBuf.ToInt64() + i * structSize);
                                PRINTER_INFO_2_WIN32 pi = (PRINTER_INFO_2_WIN32)Marshal.PtrToStructure(pItem, typeof(PRINTER_INFO_2_WIN32));

                                bool isDefault = string.Equals(pi.pPrinterName, defaultPrinter, StringComparison.OrdinalIgnoreCase);
                                var attrList = DecodePrinterAttributes(pi.Attributes);
                                var statusList = DecodePrinterStatus(pi.Status);

                                var sbItem = new StringBuilder();
                                sbItem.Append("{");
                                sbItem.AppendFormat("\"printerName\": \"{0}\", ", EscapeJson(pi.pPrinterName ?? ""));
                                sbItem.AppendFormat("\"driverName\": \"{0}\", ", EscapeJson(pi.pDriverName ?? ""));
                                sbItem.AppendFormat("\"portName\": \"{0}\", ", EscapeJson(pi.pPortName ?? ""));
                                sbItem.AppendFormat("\"shareName\": \"{0}\", ", EscapeJson(pi.pShareName ?? ""));
                                sbItem.AppendFormat("\"serverName\": \"{0}\", ", EscapeJson(pi.pServerName ?? ""));
                                sbItem.AppendFormat("\"comment\": \"{0}\", ", EscapeJson(pi.pComment ?? ""));
                                sbItem.AppendFormat("\"location\": \"{0}\", ", EscapeJson(pi.pLocation ?? ""));
                                sbItem.AppendFormat("\"printProcessor\": \"{0}\", ", EscapeJson(pi.pPrintProcessor ?? ""));
                                sbItem.AppendFormat("\"datatype\": \"{0}\", ", EscapeJson(pi.pDatatype ?? ""));
                                sbItem.AppendFormat("\"isDefault\": {0}, ", isDefault ? "true" : "false");
                                sbItem.AppendFormat("\"cJobs\": {0}, ", pi.cJobs);
                                sbItem.AppendFormat("\"attributes\": {0}, ", pi.Attributes);
                                sbItem.AppendFormat("\"status\": {0}, ", pi.Status);

                                sbItem.Append("\"decodedAttributes\": [");
                                for (int a = 0; a < attrList.Count; a++) {
                                    if (a > 0) sbItem.Append(", ");
                                    sbItem.AppendFormat("\"{0}\"", attrList[a]);
                                }
                                sbItem.Append("], ");

                                sbItem.Append("\"decodedStatus\": [");
                                for (int s = 0; s < statusList.Count; s++) {
                                    if (s > 0) sbItem.Append(", ");
                                    sbItem.AppendFormat("\"{0}\"", statusList[s]);
                                }
                                sbItem.Append("]}");

                                printers.Add(sbItem.ToString());
                            }
                        }
                    } finally {
                        Marshal.FreeHGlobal(pBuf);
                    }
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"defaultPrinter\": \"{0}\", ", EscapeJson(defaultPrinter));
                sbOut.AppendFormat("\"count\": {0}, ", printers.Count);
                sbOut.Append("\"printers\": [");
                for (int i = 0; i < printers.Count; i++) {
                    if (i > 0) sbOut.Append(", ");
                    sbOut.Append(printers[i]);
                }
                sbOut.Append("]}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void SpoolerJobsCmd(string printerName) {
            try {
                if (string.IsNullOrEmpty(printerName)) {
                    printerName = QueryDefaultPrinterName();
                }
                if (string.IsNullOrEmpty(printerName)) {
                    Console.WriteLine("{\"success\": false, \"error\": \"No printer specified and no default printer found\"}");
                    return;
                }

                IntPtr hPrinter = IntPtr.Zero;
                if (!OpenPrinterW(printerName, out hPrinter, IntPtr.Zero)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to open printer '{0}' with code {1}\"}}", EscapeJson(printerName), Marshal.GetLastWin32Error()));
                    return;
                }

                try {
                    uint cbNeeded = 0;
                    uint cReturned = 0;
                    EnumJobsW(hPrinter, 0, 100, 1, IntPtr.Zero, 0, out cbNeeded, out cReturned);

                    var jobs = new List<string>();
                    if (cbNeeded > 0) {
                        IntPtr pBuf = Marshal.AllocHGlobal((int)cbNeeded);
                        try {
                            if (EnumJobsW(hPrinter, 0, 100, 1, pBuf, cbNeeded, out cbNeeded, out cReturned)) {
                                int structSize = Marshal.SizeOf(typeof(JOB_INFO_1_WIN32));
                                for (int i = 0; i < cReturned; i++) {
                                    IntPtr pItem = new IntPtr(pBuf.ToInt64() + i * structSize);
                                    JOB_INFO_1_WIN32 ji = (JOB_INFO_1_WIN32)Marshal.PtrToStructure(pItem, typeof(JOB_INFO_1_WIN32));

                                    var statusList = DecodeJobStatus(ji.Status);
                                    string timeStr = string.Format("{0:D4}-{1:D2}-{2:D2}T{3:D2}:{4:D2}:{5:D2}Z",
                                        ji.Submitted.wYear, ji.Submitted.wMonth, ji.Submitted.wDay,
                                        ji.Submitted.wHour, ji.Submitted.wMinute, ji.Submitted.wSecond);

                                    var sbJob = new StringBuilder();
                                    sbJob.Append("{");
                                    sbJob.AppendFormat("\"jobId\": {0}, ", ji.JobId);
                                    sbJob.AppendFormat("\"document\": \"{0}\", ", EscapeJson(ji.pDocument ?? ""));
                                    sbJob.AppendFormat("\"userName\": \"{0}\", ", EscapeJson(ji.pUserName ?? ""));
                                    sbJob.AppendFormat("\"printerName\": \"{0}\", ", EscapeJson(ji.pPrinterName ?? ""));
                                    sbJob.AppendFormat("\"datatype\": \"{0}\", ", EscapeJson(ji.pDatatype ?? ""));
                                    sbJob.AppendFormat("\"totalPages\": {0}, ", ji.TotalPages);
                                    sbJob.AppendFormat("\"pagesPrinted\": {0}, ", ji.PagesPrinted);
                                    sbJob.AppendFormat("\"priority\": {0}, ", ji.Priority);
                                    sbJob.AppendFormat("\"position\": {0}, ", ji.Position);
                                    sbJob.AppendFormat("\"submitted\": \"{0}\", ", timeStr);
                                    sbJob.AppendFormat("\"status\": {0}, ", ji.Status);

                                    sbJob.Append("\"decodedStatus\": [");
                                    for (int s = 0; s < statusList.Count; s++) {
                                        if (s > 0) sbJob.Append(", ");
                                        sbJob.AppendFormat("\"{0}\"", statusList[s]);
                                    }
                                    sbJob.Append("]}");

                                    jobs.Add(sbJob.ToString());
                                }
                            }
                        } finally {
                            Marshal.FreeHGlobal(pBuf);
                        }
                    }

                    var sbOut = new StringBuilder();
                    sbOut.Append("{");
                    sbOut.Append("\"success\": true, ");
                    sbOut.AppendFormat("\"printerName\": \"{0}\", ", EscapeJson(printerName));
                    sbOut.AppendFormat("\"jobCount\": {0}, ", jobs.Count);
                    sbOut.Append("\"jobs\": [");
                    for (int i = 0; i < jobs.Count; i++) {
                        if (i > 0) sbOut.Append(", ");
                        sbOut.Append(jobs[i]);
                    }
                    sbOut.Append("]}");
                    Console.WriteLine(sbOut.ToString());
                } finally {
                    ClosePrinter(hPrinter);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void SpoolerDefaultPrinterCmd(string newDefault) {
            try {
                if (!string.IsNullOrEmpty(newDefault)) {
                    if (SetDefaultPrinterW(newDefault)) {
                        Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"set\", \"defaultPrinter\": \"{0}\"}}", EscapeJson(newDefault)));
                    } else {
                        int err = Marshal.GetLastWin32Error();
                        Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"SetDefaultPrinterW failed with code {0}\"}}", err));
                    }
                } else {
                    string def = QueryDefaultPrinterName();
                    Console.WriteLine(string.Format("{{\"success\": true, \"action\": \"get\", \"defaultPrinter\": \"{0}\"}}", EscapeJson(def)));
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows National Language Support (NLS) & Internationalization (winnls.h / kernel32.dll)

        public delegate bool EnumLocalesProcEx(
            [MarshalAs(UnmanagedType.LPWStr)] string lpLocaleString,
            uint dwFlags,
            IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumSystemLocalesEx(
            EnumLocalesProcEx lpLocaleEnumProcEx,
            uint dwFlags,
            IntPtr lParam,
            IntPtr lpReserved);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetUserDefaultLocaleName(
            StringBuilder lpLocaleName,
            int cchLocaleName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetSystemDefaultLocaleName(
            StringBuilder lpLocaleName,
            int cchLocaleName);

        [DllImport("kernel32.dll")]
        public static extern uint GetUserDefaultLCID();

        [DllImport("kernel32.dll")]
        public static extern uint GetSystemDefaultLCID();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetLocaleInfoEx(
            string lpLocaleName,
            uint LCType,
            StringBuilder lpLCData,
            int cchData);

        [DllImport("kernel32.dll")]
        public static extern uint GetACP();

        [DllImport("kernel32.dll")]
        public static extern uint GetOEMCP();

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsValidCodePage(uint CodePage);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CPINFOEXW {
            public uint MaxCharSize;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] DefaultChar;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public byte[] LeadByte;
            public char UnicodeDefaultChar;
            public uint CodePage;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string CodePageName;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCPInfoExW(
            uint CodePage,
            uint dwFlags,
            out CPINFOEXW lpCPInfoEx);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemPreferredUILanguages(
            uint dwFlags,
            out uint pulNumLanguages,
            [Out] char[] pwszLanguagesBuffer,
            ref uint pcchLanguagesBuffer);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetUserPreferredUILanguages(
            uint dwFlags,
            out uint pulNumLanguages,
            [Out] char[] pwszLanguagesBuffer,
            ref uint pcchLanguagesBuffer);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetThreadPreferredUILanguages(
            uint dwFlags,
            out uint pulNumLanguages,
            [Out] char[] pwszLanguagesBuffer,
            ref uint pcchLanguagesBuffer);

        const uint LOCALE_SENGLISHDISPLAYNAME = 0x00000072;
        const uint LOCALE_SNATIVEDISPLAYNAME = 0x00000073;
        const uint LOCALE_SISO639LANGNAME = 0x00000059;
        const uint LOCALE_SISO3166CTRYNAME = 0x0000005A;
        const uint LOCALE_SENGLISHLANGUAGENAME = 0x00001001;
        const uint LOCALE_SENGLISHCOUNTRYNAME = 0x00001002;
        const uint LOCALE_SNATIVELANGUAGENAME = 0x00000004;
        const uint LOCALE_SNATIVECTRYNAME = 0x00000008;
        const uint LOCALE_SCURRENCY = 0x00000014;
        const uint LOCALE_SSHORTDATE = 0x0000001F;
        const uint LOCALE_STIMEFORMAT = 0x00001003;
        const uint LOCALE_SDECIMAL = 0x0000000E;
        const uint LOCALE_IFIRSTDAYOFWEEK = 0x0000100B;

        static string QueryLocaleString(string localeName, uint lcType) {
            var sb = new StringBuilder(256);
            int len = GetLocaleInfoEx(localeName, lcType, sb, sb.Capacity);
            if (len > 0) return sb.ToString();
            return "";
        }

        static void IntlLocalesCmd(string filter, int limit, bool detailed) {
            try {
                var userDefSb = new StringBuilder(85);
                GetUserDefaultLocaleName(userDefSb, userDefSb.Capacity);
                string userDefault = userDefSb.ToString();

                var sysDefSb = new StringBuilder(85);
                GetSystemDefaultLocaleName(sysDefSb, sysDefSb.Capacity);
                string sysDefault = sysDefSb.ToString();

                uint userLcid = GetUserDefaultLCID();
                uint sysLcid = GetSystemDefaultLCID();

                var allLocales = new List<string>();
                EnumLocalesProcEx callback = delegate(string localeName, uint flags, IntPtr lParam) {
                    if (!string.IsNullOrEmpty(localeName)) {
                        allLocales.Add(localeName);
                    }
                    return true;
                };

                EnumSystemLocalesEx(callback, 0x00000001 /* LOCALE_WINDOWS */, IntPtr.Zero, IntPtr.Zero);

                var filtered = new List<string>();
                foreach (var loc in allLocales) {
                    if (string.IsNullOrEmpty(filter) || loc.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) {
                        filtered.Add(loc);
                    }
                }

                int totalCount = filtered.Count;
                if (limit > 0 && filtered.Count > limit) {
                    filtered = filtered.GetRange(0, limit);
                }

                var localeItems = new List<string>();
                foreach (var loc in filtered) {
                    string engName = QueryLocaleString(loc, LOCALE_SENGLISHDISPLAYNAME);
                    string natName = QueryLocaleString(loc, LOCALE_SNATIVEDISPLAYNAME);
                    string isoLang = QueryLocaleString(loc, LOCALE_SISO639LANGNAME);
                    string isoCtry = QueryLocaleString(loc, LOCALE_SISO3166CTRYNAME);

                    var sbItem = new StringBuilder();
                    sbItem.Append("{");
                    sbItem.AppendFormat("\"name\": \"{0}\", ", EscapeJson(loc));
                    sbItem.AppendFormat("\"englishDisplayName\": \"{0}\", ", EscapeJson(engName));
                    sbItem.AppendFormat("\"nativeDisplayName\": \"{0}\", ", EscapeJson(natName));
                    sbItem.AppendFormat("\"iso639Lang\": \"{0}\", ", EscapeJson(isoLang));
                    sbItem.AppendFormat("\"iso3166Country\": \"{0}\", ", EscapeJson(isoCtry));
                    sbItem.AppendFormat("\"isUserDefault\": {0}, ", string.Equals(loc, userDefault, StringComparison.OrdinalIgnoreCase) ? "true" : "false");
                    sbItem.AppendFormat("\"isSystemDefault\": {0}", string.Equals(loc, sysDefault, StringComparison.OrdinalIgnoreCase) ? "true" : "false");

                    if (detailed) {
                        string engLang = QueryLocaleString(loc, LOCALE_SENGLISHLANGUAGENAME);
                        string engCtry = QueryLocaleString(loc, LOCALE_SENGLISHCOUNTRYNAME);
                        string natLang = QueryLocaleString(loc, LOCALE_SNATIVELANGUAGENAME);
                        string natCtry = QueryLocaleString(loc, LOCALE_SNATIVECTRYNAME);
                        string currency = QueryLocaleString(loc, LOCALE_SCURRENCY);
                        string shortDate = QueryLocaleString(loc, LOCALE_SSHORTDATE);
                        string timeFormat = QueryLocaleString(loc, LOCALE_STIMEFORMAT);
                        string decimalSep = QueryLocaleString(loc, LOCALE_SDECIMAL);
                        string firstDay = QueryLocaleString(loc, LOCALE_IFIRSTDAYOFWEEK);

                        sbItem.AppendFormat(", \"englishLanguage\": \"{0}\"", EscapeJson(engLang));
                        sbItem.AppendFormat(", \"englishCountry\": \"{0}\"", EscapeJson(engCtry));
                        sbItem.AppendFormat(", \"nativeLanguage\": \"{0}\"", EscapeJson(natLang));
                        sbItem.AppendFormat(", \"nativeCountry\": \"{0}\"", EscapeJson(natCtry));
                        sbItem.AppendFormat(", \"currencySymbol\": \"{0}\"", EscapeJson(currency));
                        sbItem.AppendFormat(", \"shortDateFormat\": \"{0}\"", EscapeJson(shortDate));
                        sbItem.AppendFormat(", \"timeFormat\": \"{0}\"", EscapeJson(timeFormat));
                        sbItem.AppendFormat(", \"decimalSeparator\": \"{0}\"", EscapeJson(decimalSep));
                        sbItem.AppendFormat(", \"firstDayOfWeek\": \"{0}\"", EscapeJson(firstDay));
                    }
                    sbItem.Append("}");
                    localeItems.Add(sbItem.ToString());
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"userDefaultLocale\": \"{0}\", ", EscapeJson(userDefault));
                sbOut.AppendFormat("\"systemDefaultLocale\": \"{0}\", ", EscapeJson(sysDefault));
                sbOut.AppendFormat("\"userDefaultLCID\": {0}, ", userLcid);
                sbOut.AppendFormat("\"systemDefaultLCID\": {0}, ", sysLcid);
                sbOut.AppendFormat("\"totalMatched\": {0}, ", totalCount);
                sbOut.AppendFormat("\"count\": {0}, ", localeItems.Count);
                sbOut.Append("\"locales\": [");
                for (int i = 0; i < localeItems.Count; i++) {
                    if (i > 0) sbOut.Append(", ");
                    sbOut.Append(localeItems[i]);
                }
                sbOut.Append("]}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void IntlCodePagesCmd(string cpQuery) {
            try {
                uint acp = GetACP();
                uint oemcp = GetOEMCP();

                var cpList = new List<uint>();
                if (!string.IsNullOrEmpty(cpQuery)) {
                    string[] parts = cpQuery.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in parts) {
                        uint cpId;
                        if (uint.TryParse(p.Trim(), out cpId)) {
                            cpList.Add(cpId);
                        }
                    }
                }

                if (cpList.Count == 0) {
                    uint[] defaults = new uint[] { acp, oemcp, 65001, 1252, 437, 1200, 1201, 28591, 932, 936, 949, 950 };
                    var set = new HashSet<uint>();
                    foreach (var c in defaults) {
                        if (!set.Contains(c) && IsValidCodePage(c)) {
                            set.Add(c);
                            cpList.Add(c);
                        }
                    }
                }

                var items = new List<string>();
                foreach (var cp in cpList) {
                    bool isValid = IsValidCodePage(cp);
                    CPINFOEXW info = new CPINFOEXW();
                    bool gotInfo = false;
                    if (isValid) {
                        try {
                            gotInfo = GetCPInfoExW(cp, 0, out info);
                        } catch {}
                    }

                    var sbItem = new StringBuilder();
                    sbItem.Append("{");
                    sbItem.AppendFormat("\"codePage\": {0}, ", cp);
                    sbItem.AppendFormat("\"isValid\": {0}, ", isValid ? "true" : "false");
                    sbItem.AppendFormat("\"isACP\": {0}, ", (cp == acp) ? "true" : "false");
                    sbItem.AppendFormat("\"isOEMCP\": {0}", (cp == oemcp) ? "true" : "false");

                    if (gotInfo) {
                        string name = (info.CodePageName ?? "").Trim();
                        sbItem.AppendFormat(", \"name\": \"{0}\"", EscapeJson(name));
                        sbItem.AppendFormat(", \"maxCharSize\": {0}", info.MaxCharSize);
                        sbItem.AppendFormat(", \"unicodeDefaultChar\": \"{0}\"", EscapeJson(info.UnicodeDefaultChar.ToString()));

                        var leadBytes = new List<string>();
                        if (info.LeadByte != null) {
                            for (int b = 0; b < info.LeadByte.Length - 1; b += 2) {
                                if (info.LeadByte[b] == 0 && info.LeadByte[b + 1] == 0) break;
                                leadBytes.Add(string.Format("\"{0:X2}-{1:X2}\"", info.LeadByte[b], info.LeadByte[b + 1]));
                            }
                        }
                        sbItem.AppendFormat(", \"leadBytes\": [{0}]", string.Join(", ", leadBytes.ToArray()));
                    }
                    sbItem.Append("}");
                    items.Add(sbItem.ToString());
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"ansiCodePage\": {0}, ", acp);
                sbOut.AppendFormat("\"oemCodePage\": {0}, ", oemcp);
                sbOut.AppendFormat("\"count\": {0}, ", items.Count);
                sbOut.Append("\"codePages\": [");
                for (int i = 0; i < items.Count; i++) {
                    if (i > 0) sbOut.Append(", ");
                    sbOut.Append(items[i]);
                }
                sbOut.Append("]}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static List<string> ParseMuiLanguages(char[] buffer, uint charCount) {
            var list = new List<string>();
            if (buffer == null || charCount == 0) return list;
            int start = 0;
            for (int i = 0; i < charCount; i++) {
                if (buffer[i] == '\0') {
                    if (i > start) {
                        list.Add(new string(buffer, start, i - start));
                    }
                    start = i + 1;
                }
            }
            return list;
        }

        static void IntlUiLanguagesCmd() {
            try {
                const uint MUI_LANGUAGE_NAME = 0x8;
                uint sysCount = 0;
                uint sysChars = 0;
                GetSystemPreferredUILanguages(MUI_LANGUAGE_NAME, out sysCount, null, ref sysChars);
                var sysLangs = new List<string>();
                if (sysChars > 0) {
                    char[] buf = new char[sysChars];
                    if (GetSystemPreferredUILanguages(MUI_LANGUAGE_NAME, out sysCount, buf, ref sysChars)) {
                        sysLangs = ParseMuiLanguages(buf, sysChars);
                    }
                }

                uint userCount = 0;
                uint userChars = 0;
                GetUserPreferredUILanguages(MUI_LANGUAGE_NAME, out userCount, null, ref userChars);
                var userLangs = new List<string>();
                if (userChars > 0) {
                    char[] buf = new char[userChars];
                    if (GetUserPreferredUILanguages(MUI_LANGUAGE_NAME, out userCount, buf, ref userChars)) {
                        userLangs = ParseMuiLanguages(buf, userChars);
                    }
                }

                uint threadCount = 0;
                uint threadChars = 0;
                GetThreadPreferredUILanguages(MUI_LANGUAGE_NAME, out threadCount, null, ref threadChars);
                var threadLangs = new List<string>();
                if (threadChars > 0) {
                    char[] buf = new char[threadChars];
                    if (GetThreadPreferredUILanguages(MUI_LANGUAGE_NAME, out threadCount, buf, ref threadChars)) {
                        threadLangs = ParseMuiLanguages(buf, threadChars);
                    }
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.Append("\"systemPreferredLanguages\": [");
                for (int i = 0; i < sysLangs.Count; i++) {
                    if (i > 0) sbOut.Append(", ");
                    sbOut.AppendFormat("\"{0}\"", EscapeJson(sysLangs[i]));
                }
                sbOut.Append("], ");

                sbOut.Append("\"userPreferredLanguages\": [");
                for (int i = 0; i < userLangs.Count; i++) {
                    if (i > 0) sbOut.Append(", ");
                    sbOut.AppendFormat("\"{0}\"", EscapeJson(userLangs[i]));
                }
                sbOut.Append("], ");

                sbOut.Append("\"threadPreferredLanguages\": [");
                for (int i = 0; i < threadLangs.Count; i++) {
                    if (i > 0) sbOut.Append(", ");
                    sbOut.AppendFormat("\"{0}\"", EscapeJson(threadLangs[i]));
                }
                sbOut.Append("]}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows IP Helper & Network Routing Subsystem (iphlpapi.h / iphlpapi.dll)

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_IPFORWARDROW {
            public uint dwForwardDest;
            public uint dwForwardMask;
            public uint dwForwardPolicy;
            public uint dwForwardNextHop;
            public uint dwForwardIfIndex;
            public uint dwForwardType;
            public uint dwForwardProto;
            public uint dwForwardAge;
            public uint dwForwardNextHopAS;
            public uint dwForwardMetric1;
            public uint dwForwardMetric2;
            public uint dwForwardMetric3;
            public uint dwForwardMetric4;
            public uint dwForwardMetric5;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetIpForwardTable(
            IntPtr pIpForwardTable,
            ref uint pdwSize,
            bool bOrder);

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_IPNETROW {
            public uint dwIndex;
            public uint dwPhysAddrLen;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] bPhysAddr;
            public uint dwAddr;
            public uint dwType;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetIpNetTable(
            IntPtr pIpNetTable,
            ref uint pdwSize,
            bool bOrder);

        static string FormatIpv4Address(uint ip) {
            byte[] bytes = BitConverter.GetBytes(ip);
            return string.Format("{0}.{1}.{2}.{3}", bytes[0], bytes[1], bytes[2], bytes[3]);
        }

        static string FormatMacAddress(byte[] addr, uint len) {
            if (addr == null || len == 0) return "";
            var parts = new List<string>();
            for (int i = 0; i < len && i < addr.Length; i++) {
                parts.Add(addr[i].ToString("X2"));
            }
            return string.Join("-", parts.ToArray());
        }

        static string DecodeRouteProtocol(uint proto) {
            switch (proto) {
                case 1: return "OTHER";
                case 2: return "LOCAL";
                case 3: return "NETMGMT";
                case 4: return "ICMP";
                case 5: return "EGP";
                case 6: return "GGP";
                case 7: return "HELLO";
                case 8: return "RIP";
                case 9: return "IS_IS";
                case 10: return "ES_IS";
                case 11: return "CISCO";
                case 12: return "BBN";
                case 13: return "OSPF";
                case 14: return "BGP";
                default: return "PROTO_" + proto;
            }
        }

        static string DecodeRouteType(uint type) {
            switch (type) {
                case 1: return "OTHER";
                case 2: return "INVALID";
                case 3: return "DIRECT";
                case 4: return "INDIRECT";
                default: return "TYPE_" + type;
            }
        }

        static string DecodeArpType(uint type) {
            switch (type) {
                case 1: return "OTHER";
                case 2: return "INVALID";
                case 3: return "DYNAMIC";
                case 4: return "STATIC";
                default: return "TYPE_" + type;
            }
        }

        static void IpHlpRoutingTableCmd(string filter, int limit) {
            try {
                uint size = 0;
                GetIpForwardTable(IntPtr.Zero, ref size, true);

                var routes = new List<string>();
                int defaultGatewayCount = 0;

                if (size > 0) {
                    IntPtr pBuf = Marshal.AllocHGlobal((int)size);
                    try {
                        uint ret = GetIpForwardTable(pBuf, ref size, true);
                        if (ret == 0) {
                            int numEntries = Marshal.ReadInt32(pBuf);
                            int rowSize = Marshal.SizeOf(typeof(MIB_IPFORWARDROW));
                            IntPtr pRowStart = new IntPtr(pBuf.ToInt64() + 4);

                            for (int i = 0; i < numEntries; i++) {
                                IntPtr pRow = new IntPtr(pRowStart.ToInt64() + i * rowSize);
                                var row = (MIB_IPFORWARDROW)Marshal.PtrToStructure(pRow, typeof(MIB_IPFORWARDROW));

                                string destIp = FormatIpv4Address(row.dwForwardDest);
                                string mask = FormatIpv4Address(row.dwForwardMask);
                                string nextHop = FormatIpv4Address(row.dwForwardNextHop);
                                bool isDefaultGateway = (destIp == "0.0.0.0" && mask == "0.0.0.0");
                                if (isDefaultGateway) defaultGatewayCount++;

                                if (!string.IsNullOrEmpty(filter)) {
                                    if (destIp.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                                        nextHop.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) {
                                        continue;
                                    }
                                }

                                var sbItem = new StringBuilder();
                                sbItem.Append("{");
                                sbItem.AppendFormat("\"destination\": \"{0}\", ", destIp);
                                sbItem.AppendFormat("\"netmask\": \"{0}\", ", mask);
                                sbItem.AppendFormat("\"nextHop\": \"{0}\", ", nextHop);
                                sbItem.AppendFormat("\"interfaceIndex\": {0}, ", row.dwForwardIfIndex);
                                sbItem.AppendFormat("\"metric\": {0}, ", row.dwForwardMetric1);
                                sbItem.AppendFormat("\"type\": \"{0}\", ", DecodeRouteType(row.dwForwardType));
                                sbItem.AppendFormat("\"protocol\": \"{0}\", ", DecodeRouteProtocol(row.dwForwardProto));
                                sbItem.AppendFormat("\"ageSeconds\": {0}, ", row.dwForwardAge);
                                sbItem.AppendFormat("\"isDefaultGateway\": {0}", isDefaultGateway ? "true" : "false");
                                sbItem.Append("}");
                                routes.Add(sbItem.ToString());
                            }
                        }
                    } finally {
                        Marshal.FreeHGlobal(pBuf);
                    }
                }

                int totalCount = routes.Count;
                if (limit > 0 && routes.Count > limit) {
                    routes = routes.GetRange(0, limit);
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"totalRoutes\": {0}, ", totalCount);
                sbOut.AppendFormat("\"defaultGatewayCount\": {0}, ", defaultGatewayCount);
                sbOut.AppendFormat("\"returnedCount\": {0}, ", routes.Count);
                sbOut.Append("\"routes\": [");
                for (int i = 0; i < routes.Count; i++) {
                    if (i > 0) sbOut.Append(", ");
                    sbOut.Append(routes[i]);
                }
                sbOut.Append("]}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void IpHlpArpTableCmd(string filter, int limit) {
            try {
                uint size = 0;
                GetIpNetTable(IntPtr.Zero, ref size, true);

                var entries = new List<string>();

                if (size > 0) {
                    IntPtr pBuf = Marshal.AllocHGlobal((int)size);
                    try {
                        uint ret = GetIpNetTable(pBuf, ref size, true);
                        if (ret == 0) {
                            int numEntries = Marshal.ReadInt32(pBuf);
                            int rowSize = Marshal.SizeOf(typeof(MIB_IPNETROW));
                            IntPtr pRowStart = new IntPtr(pBuf.ToInt64() + 4);

                            for (int i = 0; i < numEntries; i++) {
                                IntPtr pRow = new IntPtr(pRowStart.ToInt64() + i * rowSize);
                                var row = (MIB_IPNETROW)Marshal.PtrToStructure(pRow, typeof(MIB_IPNETROW));

                                string ip = FormatIpv4Address(row.dwAddr);
                                string mac = FormatMacAddress(row.bPhysAddr, row.dwPhysAddrLen);
                                string type = DecodeArpType(row.dwType);

                                if (!string.IsNullOrEmpty(filter)) {
                                    if (ip.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                                        mac.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) {
                                        continue;
                                    }
                                }

                                var sbItem = new StringBuilder();
                                sbItem.Append("{");
                                sbItem.AppendFormat("\"ipAddress\": \"{0}\", ", ip);
                                sbItem.AppendFormat("\"macAddress\": \"{0}\", ", mac);
                                sbItem.AppendFormat("\"interfaceIndex\": {0}, ", row.dwIndex);
                                sbItem.AppendFormat("\"type\": \"{0}\"", type);
                                sbItem.Append("}");
                                entries.Add(sbItem.ToString());
                            }
                        }
                    } finally {
                        Marshal.FreeHGlobal(pBuf);
                    }
                }

                int totalCount = entries.Count;
                if (limit > 0 && entries.Count > limit) {
                    entries = entries.GetRange(0, limit);
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"totalEntries\": {0}, ", totalCount);
                sbOut.AppendFormat("\"returnedCount\": {0}, ", entries.Count);
                sbOut.Append("\"arpEntries\": [");
                for (int i = 0; i < entries.Count; i++) {
                    if (i > 0) sbOut.Append(", ");
                    sbOut.Append(entries[i]);
                }
                sbOut.Append("]}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void IpHlpInterfacesCmd(string filter) {
            try {
                var ifaces = new List<string>();
                var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

                foreach (var nic in nics) {
                    if (!string.IsNullOrEmpty(filter)) {
                        if (nic.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                            nic.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                            nic.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) {
                            continue;
                        }
                    }

                    var ipProps = nic.GetIPProperties();
                    var stats = nic.GetIPv4Statistics();

                    var ipList = new List<string>();
                    if (ipProps.UnicastAddresses != null) {
                        foreach (var u in ipProps.UnicastAddresses) {
                            if (u.Address != null) ipList.Add(string.Format("\"{0}\"", u.Address.ToString()));
                        }
                    }

                    var gwList = new List<string>();
                    if (ipProps.GatewayAddresses != null) {
                        foreach (var g in ipProps.GatewayAddresses) {
                            if (g.Address != null) gwList.Add(string.Format("\"{0}\"", g.Address.ToString()));
                        }
                    }

                    var dnsList = new List<string>();
                    if (ipProps.DnsAddresses != null) {
                        foreach (var d in ipProps.DnsAddresses) {
                            dnsList.Add(string.Format("\"{0}\"", d.ToString()));
                        }
                    }

                    long bytesSent = 0;
                    long bytesReceived = 0;
                    long inErrors = 0;
                    long outErrors = 0;
                    if (stats != null) {
                        try { bytesSent = stats.BytesSent; } catch {}
                        try { bytesReceived = stats.BytesReceived; } catch {}
                        try { inErrors = stats.IncomingPacketsWithErrors; } catch {}
                        try { outErrors = stats.OutgoingPacketsWithErrors; } catch {}
                    }

                    string mac = "";
                    try {
                        byte[] phys = nic.GetPhysicalAddress().GetAddressBytes();
                        mac = FormatMacAddress(phys, (uint)phys.Length);
                    } catch {}

                    var sbItem = new StringBuilder();
                    sbItem.Append("{");
                    sbItem.AppendFormat("\"id\": \"{0}\", ", EscapeJson(nic.Id));
                    sbItem.AppendFormat("\"name\": \"{0}\", ", EscapeJson(nic.Name));
                    sbItem.AppendFormat("\"description\": \"{0}\", ", EscapeJson(nic.Description));
                    sbItem.AppendFormat("\"type\": \"{0}\", ", nic.NetworkInterfaceType.ToString());
                    sbItem.AppendFormat("\"status\": \"{0}\", ", nic.OperationalStatus.ToString());
                    sbItem.AppendFormat("\"speedBitsPerSecond\": {0}, ", nic.Speed);
                    sbItem.AppendFormat("\"speedMbps\": {0:F1}, ", (double)nic.Speed / 1000000.0);
                    sbItem.AppendFormat("\"macAddress\": \"{0}\", ", mac);
                    sbItem.AppendFormat("\"supportsMulticast\": {0}, ", nic.SupportsMulticast ? "true" : "false");
                    sbItem.AppendFormat("\"bytesSent\": {0}, ", bytesSent);
                    sbItem.AppendFormat("\"bytesReceived\": {0}, ", bytesReceived);
                    sbItem.AppendFormat("\"incomingErrors\": {0}, ", inErrors);
                    sbItem.AppendFormat("\"outgoingErrors\": {0}, ", outErrors);
                    sbItem.AppendFormat("\"ipAddresses\": [{0}], ", string.Join(", ", ipList.ToArray()));
                    sbItem.AppendFormat("\"gatewayAddresses\": [{0}], ", string.Join(", ", gwList.ToArray()));
                    sbItem.AppendFormat("\"dnsAddresses\": [{0}]", string.Join(", ", dnsList.ToArray()));
                    sbItem.Append("}");
                    ifaces.Add(sbItem.ToString());
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"count\": {0}, ", ifaces.Count);
                sbOut.Append("\"interfaces\": [");
                for (int i = 0; i < ifaces.Count; i++) {
                    if (i > 0) sbOut.Append(", ");
                    sbOut.Append(ifaces[i]);
                }
                sbOut.Append("]}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Windows Display Devices, Monitor Topology & Graphics Modes (wingdi.h / winuser.h)

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE_FULL {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool EnumDisplayDevicesFull(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE_FULL lpDisplayDevice, uint dwFlags);

        [DllImport("gdi32.dll", EntryPoint = "CreateDCW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateDisplayDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll", SetLastError = true)]
        static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        const int GDC_HORZSIZE = 4;
        const int GDC_VERTSIZE = 6;
        const int GDC_HORZRES = 8;
        const int GDC_VERTRES = 10;
        const int GDC_BITSPIXEL = 12;
        const int GDC_PLANES = 14;
        const int GDC_NUMCOLORS = 24;
        const int GDC_RASTERCAPS = 38;
        const int GDC_LOGPIXELSX = 88;
        const int GDC_LOGPIXELSY = 90;
        const int GDC_COLORRES = 108;
        const int GDC_VREFRESH = 116;
        const int GDC_DESKTOPVERTRES = 117;
        const int GDC_DESKTOPHORZRES = 118;
        const int GDC_SHADEBLENDCAPS = 120;

        static void DisplayDevicesCmd(string adapterFilter, bool includeMonitors) {
            try {
                var adapters = new List<string>();
                uint devNum = 0;
                int matchedAdapters = 0;
                DISPLAY_DEVICE_FULL d = new DISPLAY_DEVICE_FULL();
                d.cb = Marshal.SizeOf(d);

                while (EnumDisplayDevicesFull(null, devNum, ref d, 0)) {
                    bool match = true;
                    if (!string.IsNullOrEmpty(adapterFilter)) {
                        string q = adapterFilter.Trim().ToLowerInvariant();
                        match = (d.DeviceName != null && d.DeviceName.ToLowerInvariant().Contains(q)) ||
                                (d.DeviceString != null && d.DeviceString.ToLowerInvariant().Contains(q)) ||
                                (d.DeviceID != null && d.DeviceID.ToLowerInvariant().Contains(q));
                    }

                    if (match) {
                        matchedAdapters++;
                        bool isAttached = (d.StateFlags & 0x00000001) != 0;
                        bool isPrimary = (d.StateFlags & 0x00000004) != 0;
                        bool isMirror = (d.StateFlags & 0x00000008) != 0;
                        bool isVga = (d.StateFlags & 0x00000010) != 0;
                        bool isRemovable = (d.StateFlags & 0x00000020) != 0;
                        bool isRemote = (d.StateFlags & 0x04000000) != 0;
                        bool modesPruned = (d.StateFlags & 0x08000000) != 0;

                        var monitors = new List<string>();
                        if (includeMonitors) {
                            uint monNum = 0;
                            DISPLAY_DEVICE_FULL mon = new DISPLAY_DEVICE_FULL();
                            mon.cb = Marshal.SizeOf(mon);
                            while (EnumDisplayDevicesFull(d.DeviceName, monNum, ref mon, 0)) {
                                bool monAttached = (mon.StateFlags & 0x00000001) != 0;
                                bool monPrimary = (mon.StateFlags & 0x00000004) != 0;
                                var sbMon = new StringBuilder();
                                sbMon.Append("{");
                                sbMon.AppendFormat("\"index\": {0}, ", monNum);
                                sbMon.AppendFormat("\"deviceName\": \"{0}\", ", EscapeJson(mon.DeviceName ?? ""));
                                sbMon.AppendFormat("\"deviceString\": \"{0}\", ", EscapeJson(mon.DeviceString ?? ""));
                                sbMon.AppendFormat("\"stateFlags\": {0}, ", mon.StateFlags);
                                sbMon.AppendFormat("\"isAttachedToDesktop\": {0}, ", monAttached ? "true" : "false");
                                sbMon.AppendFormat("\"isPrimary\": {0}, ", monPrimary ? "true" : "false");
                                sbMon.AppendFormat("\"deviceID\": \"{0}\", ", EscapeJson(mon.DeviceID ?? ""));
                                sbMon.AppendFormat("\"deviceKey\": \"{0}\"", EscapeJson(mon.DeviceKey ?? ""));
                                sbMon.Append("}");
                                monitors.Add(sbMon.ToString());
                                monNum++;
                                mon = new DISPLAY_DEVICE_FULL();
                                mon.cb = Marshal.SizeOf(mon);
                            }
                        }

                        var sbAd = new StringBuilder();
                        sbAd.Append("{");
                        sbAd.AppendFormat("\"index\": {0}, ", devNum);
                        sbAd.AppendFormat("\"deviceName\": \"{0}\", ", EscapeJson(d.DeviceName ?? ""));
                        sbAd.AppendFormat("\"deviceString\": \"{0}\", ", EscapeJson(d.DeviceString ?? ""));
                        sbAd.AppendFormat("\"stateFlags\": {0}, ", d.StateFlags);
                        sbAd.AppendFormat("\"isAttachedToDesktop\": {0}, ", isAttached ? "true" : "false");
                        sbAd.AppendFormat("\"isPrimary\": {0}, ", isPrimary ? "true" : "false");
                        sbAd.AppendFormat("\"isMirroring\": {0}, ", isMirror ? "true" : "false");
                        sbAd.AppendFormat("\"isVgaCompatible\": {0}, ", isVga ? "true" : "false");
                        sbAd.AppendFormat("\"isRemovable\": {0}, ", isRemovable ? "true" : "false");
                        sbAd.AppendFormat("\"isRemote\": {0}, ", isRemote ? "true" : "false");
                        sbAd.AppendFormat("\"modesPruned\": {0}, ", modesPruned ? "true" : "false");
                        sbAd.AppendFormat("\"deviceID\": \"{0}\", ", EscapeJson(d.DeviceID ?? ""));
                        sbAd.AppendFormat("\"deviceKey\": \"{0}\", ", EscapeJson(d.DeviceKey ?? ""));
                        sbAd.AppendFormat("\"monitorCount\": {0}, ", monitors.Count);
                        sbAd.AppendFormat("\"monitors\": [{0}]", string.Join(", ", monitors.ToArray()));
                        sbAd.Append("}");
                        adapters.Add(sbAd.ToString());
                    }

                    devNum++;
                    d = new DISPLAY_DEVICE_FULL();
                    d.cb = Marshal.SizeOf(d);
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"totalAdaptersFound\": {0}, ", devNum);
                sbOut.AppendFormat("\"adapterCount\": {0}, ", adapters.Count);
                sbOut.AppendFormat("\"adapters\": [{0}]", string.Join(", ", adapters.ToArray()));
                sbOut.Append("}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void DisplayModesCmd(string deviceName, string modeType, int limit) {
            try {
                string dev = string.IsNullOrEmpty(deviceName) ? null : deviceName;
                if (limit <= 0) limit = 100;
                string queryType = (modeType ?? "all").ToLowerInvariant();

                DEVMODE cur = new DEVMODE();
                cur.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                bool hasCurrent = EnumDisplaySettings(dev, ENUM_CURRENT_SETTINGS, ref cur);

                DEVMODE reg = new DEVMODE();
                reg.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                const int ENUM_REGISTRY_SETTINGS = -2;
                bool hasRegistry = EnumDisplaySettings(dev, ENUM_REGISTRY_SETTINGS, ref reg);

                var modes = new List<string>();
                int totalModes = 0;

                if (queryType == "all" || queryType == "supported") {
                    int modeIndex = 0;
                    var seen = new HashSet<string>();
                    DEVMODE m = new DEVMODE();
                    m.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

                    while (EnumDisplaySettings(dev, modeIndex, ref m)) {
                        totalModes++;
                        string key = string.Format("{0}x{1}@{2}Hz-{3}bpp-rot{4}", m.dmPelsWidth, m.dmPelsHeight, m.dmDisplayFrequency, m.dmBitsPerPel, m.dmDisplayOrientation);
                        if (!seen.Contains(key) && modes.Count < limit) {
                            seen.Add(key);
                            string orientStr = "default";
                            if (m.dmDisplayOrientation == 1) orientStr = "90";
                            else if (m.dmDisplayOrientation == 2) orientStr = "180";
                            else if (m.dmDisplayOrientation == 3) orientStr = "270";

                            bool interlaced = (m.dmDisplayFlags & 2) != 0;

                            modes.Add(string.Format(
                                "{{\"modeIndex\": {0}, \"width\": {1}, \"height\": {2}, \"refreshRateHz\": {3}, \"bitsPerPixel\": {4}, \"orientation\": \"{5}\", \"interlaced\": {6}}}",
                                modeIndex, m.dmPelsWidth, m.dmPelsHeight, m.dmDisplayFrequency, m.dmBitsPerPel, orientStr, interlaced ? "true" : "false"
                            ));
                        }
                        modeIndex++;
                        m = new DEVMODE();
                        m.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                    }
                }

                Func<DEVMODE, string> formatModeObj = (dm) => {
                    string oStr = "default";
                    if (dm.dmDisplayOrientation == 1) oStr = "90";
                    else if (dm.dmDisplayOrientation == 2) oStr = "180";
                    else if (dm.dmDisplayOrientation == 3) oStr = "270";
                    bool isInter = (dm.dmDisplayFlags & 2) != 0;
                    return string.Format(
                        "{{\"width\": {0}, \"height\": {1}, \"refreshRateHz\": {2}, \"bitsPerPixel\": {3}, \"orientation\": \"{4}\", \"interlaced\": {5}, \"positionX\": {6}, \"positionY\": {7}}}",
                        dm.dmPelsWidth, dm.dmPelsHeight, dm.dmDisplayFrequency, dm.dmBitsPerPel, oStr, isInter ? "true" : "false", dm.dmPositionX, dm.dmPositionY
                    );
                };

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"deviceName\": \"{0}\", ", EscapeJson(dev ?? "Primary Display"));
                sbOut.AppendFormat("\"modeType\": \"{0}\", ", EscapeJson(queryType));
                sbOut.AppendFormat("\"hasCurrent\": {0}, ", hasCurrent ? "true" : "false");
                if (hasCurrent) {
                    sbOut.AppendFormat("\"currentMode\": {0}, ", formatModeObj(cur));
                } else {
                    sbOut.Append("\"currentMode\": null, ");
                }
                sbOut.AppendFormat("\"hasRegistry\": {0}, ", hasRegistry ? "true" : "false");
                if (hasRegistry) {
                    sbOut.AppendFormat("\"registryMode\": {0}, ", formatModeObj(reg));
                } else {
                    sbOut.Append("\"registryMode\": null, ");
                }
                sbOut.AppendFormat("\"totalSupportedModes\": {0}, ", totalModes);
                sbOut.AppendFormat("\"returnedModeCount\": {0}, ", modes.Count);
                sbOut.AppendFormat("\"modes\": [{0}]", string.Join(", ", modes.ToArray()));
                sbOut.Append("}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void DisplayCapsCmd(string deviceName) {
            IntPtr hdc = IntPtr.Zero;
            try {
                string dev = string.IsNullOrEmpty(deviceName) ? null : deviceName;
                hdc = CreateDisplayDC("DISPLAY", dev, null, IntPtr.Zero);
                if (hdc == IntPtr.Zero) {
                    Console.WriteLine("{\"success\": false, \"error\": \"Failed to create device context (CreateDCW) for display\"}");
                    return;
                }

                int horzRes = GetDeviceCaps(hdc, GDC_HORZRES);
                int vertRes = GetDeviceCaps(hdc, GDC_VERTRES);
                int desktopHorzRes = GetDeviceCaps(hdc, GDC_DESKTOPHORZRES);
                int desktopVertRes = GetDeviceCaps(hdc, GDC_DESKTOPVERTRES);
                int logPixelsX = GetDeviceCaps(hdc, GDC_LOGPIXELSX);
                int logPixelsY = GetDeviceCaps(hdc, GDC_LOGPIXELSY);
                int bitsPixel = GetDeviceCaps(hdc, GDC_BITSPIXEL);
                int planes = GetDeviceCaps(hdc, GDC_PLANES);
                int numColors = GetDeviceCaps(hdc, GDC_NUMCOLORS);
                int colorRes = GetDeviceCaps(hdc, GDC_COLORRES);
                int horzSizeMm = GetDeviceCaps(hdc, GDC_HORZSIZE);
                int vertSizeMm = GetDeviceCaps(hdc, GDC_VERTSIZE);
                int vRefresh = GetDeviceCaps(hdc, GDC_VREFRESH);
                int shadeBlendCaps = GetDeviceCaps(hdc, GDC_SHADEBLENDCAPS);
                int rasterCaps = GetDeviceCaps(hdc, GDC_RASTERCAPS);

                double scaleFactorPercent = 100.0;
                if (logPixelsX > 0 && logPixelsX != 96) {
                    scaleFactorPercent = Math.Round(((double)logPixelsX / 96.0) * 100.0, 1);
                } else if (horzRes > 0 && desktopHorzRes > horzRes) {
                    scaleFactorPercent = Math.Round(((double)desktopHorzRes / (double)horzRes) * 100.0, 1);
                }

                double diagonalMm = Math.Sqrt((double)horzSizeMm * horzSizeMm + (double)vertSizeMm * vertSizeMm);
                double diagonalInches = Math.Round(diagonalMm / 25.4, 1);

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"deviceName\": \"{0}\", ", EscapeJson(dev ?? "Primary Display"));
                sbOut.AppendFormat("\"logicalResolution\": {{\"width\": {0}, \"height\": {1}}}, ", horzRes, vertRes);
                sbOut.AppendFormat("\"desktopResolution\": {{\"width\": {0}, \"height\": {1}}}, ", desktopHorzRes, desktopVertRes);
                sbOut.AppendFormat("\"scaleFactorPercent\": {0}, ", scaleFactorPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                sbOut.AppendFormat("\"dpi\": {{\"dpiX\": {0}, \"dpiY\": {1}, \"standardDpi\": 96}}, ", logPixelsX, logPixelsY);
                sbOut.AppendFormat("\"physicalDimensionsMm\": {{\"widthMm\": {0}, \"heightMm\": {1}, \"diagonalInches\": {2}}}, ",
                    horzSizeMm, vertSizeMm, diagonalInches.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                sbOut.AppendFormat("\"color\": {{\"bitsPerPixel\": {0}, \"planes\": {1}, \"colorResolutionBits\": {2}, \"numColors\": {3}}}, ",
                    bitsPixel, planes, colorRes, numColors);
                sbOut.AppendFormat("\"refreshRateHz\": {0}, ", vRefresh);
                sbOut.AppendFormat("\"rasterCaps\": {0}, ", rasterCaps);
                sbOut.AppendFormat("\"shadeBlendCaps\": {0}", shadeBlendCaps);
                sbOut.Append("}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            } finally {
                if (hdc != IntPtr.Zero) {
                    DeleteDC(hdc);
                }
            }
        }

        #endregion

        #region Windows Virtual Storage & Virtual Hard Disk (VHD/VHDX) Subsystem (virtdisk.h / virtdisk.dll)

        [StructLayout(LayoutKind.Sequential)]
        public struct VIRTUAL_STORAGE_TYPE {
            public uint DeviceId;
            public Guid VendorId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OPEN_VIRTUAL_DISK_PARAMETERS_V1 {
            public int Version;
            public uint RWDepth;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GET_VIRTUAL_DISK_INFO_SIZE {
            public int Version;
            public ulong VirtualSize;
            public ulong PhysicalSize;
            public uint BlockSize;
            public uint SectorSize;
        }

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetAllAttachedVirtualDiskPhysicalPaths(ref uint pathsBufferSizeInBytes, IntPtr pathsBuffer);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int OpenVirtualDisk(
            ref VIRTUAL_STORAGE_TYPE VirtualStorageType,
            string Path,
            uint VirtualDiskAccessMask,
            uint Flags,
            ref OPEN_VIRTUAL_DISK_PARAMETERS_V1 Parameters,
            out IntPtr Handle
        );

        [DllImport("virtdisk.dll", EntryPoint = "GetVirtualDiskInformation", SetLastError = true)]
        public static extern int GetVirtualDiskInfoSize(
            IntPtr VirtualDiskHandle,
            ref uint VirtualDiskInfoSize,
            ref GET_VIRTUAL_DISK_INFO_SIZE VirtualDiskInfo,
            out uint SizeUsed
        );

        [DllImport("virtdisk.dll", EntryPoint = "GetVirtualDiskInformation", SetLastError = true)]
        public static extern int GetVirtualDiskInfoRaw(
            IntPtr VirtualDiskHandle,
            ref uint VirtualDiskInfoSize,
            IntPtr VirtualDiskInfo,
            out uint SizeUsed
        );

        [DllImport("virtdisk.dll", SetLastError = true)]
        public static extern int GetStorageDependencyInformation(
            IntPtr ObjectHandle,
            uint Flags,
            uint StorageDependencyInfoSize,
            IntPtr StorageDependencyInfo,
            out uint SizeUsed
        );

        static void VhdAttachedDisksCmd() {
            try {
                uint bufSize = 0;
                GetAllAttachedVirtualDiskPhysicalPaths(ref bufSize, IntPtr.Zero);
                var disks = new List<string>();

                if (bufSize > 2) {
                    IntPtr pBuf = Marshal.AllocHGlobal((int)bufSize);
                    try {
                        int res = GetAllAttachedVirtualDiskPhysicalPaths(ref bufSize, pBuf);
                        if (res == 0) {
                            IntPtr cur = pBuf;
                            int idx = 0;
                            while (true) {
                                string s = Marshal.PtrToStringUni(cur);
                                if (string.IsNullOrEmpty(s)) break;
                                disks.Add(string.Format("{{\"index\": {0}, \"physicalPath\": \"{1}\"}}", idx, EscapeJson(s)));
                                idx++;
                                cur = new IntPtr(cur.ToInt64() + (s.Length + 1) * 2);
                            }
                        }
                    } finally {
                        Marshal.FreeHGlobal(pBuf);
                    }
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"attachedCount\": {0}, ", disks.Count);
                sbOut.AppendFormat("\"disks\": [{0}]", string.Join(", ", disks.ToArray()));
                sbOut.Append("}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void VhdInspectCmd(string vhdPath) {
            IntPtr handle = IntPtr.Zero;
            try {
                string targetPath = vhdPath;
                if (string.IsNullOrEmpty(targetPath)) {
                    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string wslPackages = Path.Combine(userProfile, @"AppData\Local\Packages");
                    if (Directory.Exists(wslPackages)) {
                        string[] matches = Directory.GetFiles(wslPackages, "*.vhdx", SearchOption.AllDirectories);
                        if (matches.Length > 0) targetPath = matches[0];
                    }
                }

                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"VHD file not found: {0}\"}}", EscapeJson(targetPath ?? "")));
                    return;
                }

                FileInfo fi = new FileInfo(targetPath);
                string ext = fi.Extension.ToLowerInvariant();
                string format = (ext == ".vhdx") ? "VHDX" : ((ext == ".iso") ? "ISO" : "VHD");

                VIRTUAL_STORAGE_TYPE st = new VIRTUAL_STORAGE_TYPE();
                st.DeviceId = (format == "VHDX") ? 3u : ((format == "VHD") ? 2u : 0u);
                st.VendorId = Guid.Empty;

                OPEN_VIRTUAL_DISK_PARAMETERS_V1 op = new OPEN_VIRTUAL_DISK_PARAMETERS_V1();
                op.Version = 1;
                op.RWDepth = 1000;

                const uint VIRTUAL_DISK_ACCESS_GET_INFO = 0x00080000;
                int openRes = OpenVirtualDisk(ref st, targetPath, VIRTUAL_DISK_ACCESS_GET_INFO, 0, ref op, out handle);
                if (openRes != 0 || handle == IntPtr.Zero) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"OpenVirtualDisk failed with error code: {0}\", \"path\": \"{1}\"}}", openRes, EscapeJson(targetPath)));
                    return;
                }

                GET_VIRTUAL_DISK_INFO_SIZE sz = new GET_VIRTUAL_DISK_INFO_SIZE();
                sz.Version = 1;
                uint szSize = (uint)Marshal.SizeOf(typeof(GET_VIRTUAL_DISK_INFO_SIZE));
                uint used;
                int infoRes = GetVirtualDiskInfoSize(handle, ref szSize, ref sz, out used);

                Guid diskGuid = Guid.Empty;
                int subType = 0;
                bool is4k = false;

                IntPtr pBuf = Marshal.AllocHGlobal(256);
                try {
                    Marshal.WriteInt32(pBuf, 2);
                    uint bSize = 256;
                    if (GetVirtualDiskInfoRaw(handle, ref bSize, pBuf, out used) == 0) {
                        byte[] gb = new byte[16];
                        Marshal.Copy(new IntPtr(pBuf.ToInt64() + 4), gb, 0, 16);
                        diskGuid = new Guid(gb);
                    }

                    Marshal.WriteInt32(pBuf, 7);
                    bSize = 256;
                    if (GetVirtualDiskInfoRaw(handle, ref bSize, pBuf, out used) == 0) {
                        subType = Marshal.ReadInt32(pBuf, 4);
                    }

                    Marshal.WriteInt32(pBuf, 8);
                    bSize = 256;
                    if (GetVirtualDiskInfoRaw(handle, ref bSize, pBuf, out used) == 0) {
                        is4k = (Marshal.ReadInt32(pBuf, 4) != 0);
                    }
                } finally {
                    Marshal.FreeHGlobal(pBuf);
                }

                string typeName = "Unknown";
                if (subType == 2) typeName = "Fixed";
                else if (subType == 3) typeName = "Dynamic";
                else if (subType == 4) typeName = "Differencing";

                double virtualGb = Math.Round((double)sz.VirtualSize / (1024.0 * 1024.0 * 1024.0), 2);
                double physicalMb = Math.Round((double)sz.PhysicalSize / (1024.0 * 1024.0), 2);

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"path\": \"{0}\", ", EscapeJson(targetPath));
                sbOut.AppendFormat("\"format\": \"{0}\", ", format);
                sbOut.AppendFormat("\"subType\": \"{0}\", ", typeName);
                sbOut.AppendFormat("\"virtualSizeBytes\": {0}, ", sz.VirtualSize);
                sbOut.AppendFormat("\"virtualSizeGb\": {0}, ", virtualGb.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                sbOut.AppendFormat("\"physicalSizeBytes\": {0}, ", sz.PhysicalSize);
                sbOut.AppendFormat("\"physicalSizeMb\": {0}, ", physicalMb.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                sbOut.AppendFormat("\"blockSizeBytes\": {0}, ", sz.BlockSize);
                sbOut.AppendFormat("\"sectorSizeBytes\": {0}, ", sz.SectorSize);
                sbOut.AppendFormat("\"diskGuid\": \"{0}\", ", diskGuid);
                sbOut.AppendFormat("\"is4kAligned\": {0}, ", is4k ? "true" : "false");
                sbOut.AppendFormat("\"fileSizeBytes\": {0}, ", fi.Length);
                sbOut.AppendFormat("\"creationTime\": \"{0:o}\", ", fi.CreationTimeUtc);
                sbOut.AppendFormat("\"lastWriteTime\": \"{0:o}\"", fi.LastWriteTimeUtc);
                sbOut.Append("}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            } finally {
                if (handle != IntPtr.Zero) {
                    CloseHandle(handle);
                }
            }
        }

        static void VhdStorageDependenciesCmd(string driveOrVolume) {
            IntPtr hFile = IntPtr.Zero;
            try {
                string target = driveOrVolume;
                if (string.IsNullOrEmpty(target)) target = "C:";
                target = target.TrimEnd('\\');
                if (!target.StartsWith(@"\\.\")) {
                    target = @"\\.\" + target;
                }

                const uint GENERIC_READ = 0x80000000;
                const uint FILE_SHARE_READ_WRITE = 3;
                const uint OPEN_EXISTING = 3;

                hFile = CreateFile(target, GENERIC_READ, FILE_SHARE_READ_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (hFile == IntPtr.Zero || hFile == new IntPtr(-1)) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"Failed to open volume: {0}\", \"target\": \"{1}\"}}", Marshal.GetLastWin32Error(), EscapeJson(target)));
                    return;
                }

                uint bufSize = 4096;
                IntPtr pBuf = Marshal.AllocHGlobal((int)bufSize);
                try {
                    Marshal.WriteInt32(pBuf, 1); // STORAGE_DEPENDENCY_INFO_VERSION_1
                    uint sizeUsed;
                    const uint GET_STORAGE_DEPENDENCY_FLAG_PARENTS = 1;
                    int res = GetStorageDependencyInformation(hFile, GET_STORAGE_DEPENDENCY_FLAG_PARENTS, bufSize, pBuf, out sizeUsed);

                    bool isVirtual = (res == 0);
                    string backingType = isVirtual ? "Virtual Hard Disk (VHD/VHDX)" : "Physical Bare-Metal Drive (NVMe/SATA/SAS)";
                    var parentPaths = new List<string>();

                    var sbOut = new StringBuilder();
                    sbOut.Append("{");
                    sbOut.Append("\"success\": true, ");
                    sbOut.AppendFormat("\"target\": \"{0}\", ", EscapeJson(target));
                    sbOut.AppendFormat("\"isVirtualDisk\": {0}, ", isVirtual ? "true" : "false");
                    sbOut.AppendFormat("\"storageBacking\": \"{0}\", ", EscapeJson(backingType));
                    sbOut.AppendFormat("\"statusResultCode\": {0}, ", res);
                    sbOut.AppendFormat("\"parentPaths\": [{0}]", string.Join(", ", parentPaths.ToArray()));
                    sbOut.Append("}");
                    Console.WriteLine(sbOut.ToString());
                } finally {
                    Marshal.FreeHGlobal(pBuf);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            } finally {
                if (hFile != IntPtr.Zero && hFile != new IntPtr(-1)) {
                    CloseHandle(hFile);
                }
            }
        }

        #endregion

        #region Windows Subsystem for Linux (WSL) Subsystem (wslapi.h / wslapi.dll)

        [DllImport("wslapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool WslIsDistributionRegistered(string distributionName);

        [DllImport("wslapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int WslGetDistributionConfiguration(
            string distributionName,
            out uint distributionVersion,
            out uint defaultUID,
            out uint wslDistributionFlags,
            out IntPtr defaultEnvironmentVariables,
            out uint defaultEnvironmentVariableCount
        );

        [DllImport("wslapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int WslLaunch(
            string distributionName,
            string command,
            bool useCurrentWorkingDirectory,
            IntPtr stdIn,
            IntPtr stdOut,
            IntPtr stdErr,
            out IntPtr process
        );

        [StructLayout(LayoutKind.Sequential)]
        public struct WSL_SECURITY_ATTRIBUTES {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref WSL_SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(IntPtr hFile, [Out] byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool PeekNamedPipe(IntPtr hNamedPipe, byte[] lpBuffer, uint nBufferSize, out uint lpBytesRead, out uint lpTotalBytesAvail, out uint lpBytesLeftThisMessage);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        static void WslDistributionsCmd(string filter) {
            try {
                var distros = new List<string>();
                string defaultDistroGuid = "";
                string natIp = "";

                using (var rootKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Lxss")) {
                    if (rootKey != null) {
                        object dVal = rootKey.GetValue("DefaultDistribution");
                        if (dVal != null) defaultDistroGuid = dVal.ToString();
                        object ipVal = rootKey.GetValue("NatIpAddress");
                        if (ipVal != null) natIp = ipVal.ToString();

                        string[] subKeys = rootKey.GetSubKeyNames();
                        foreach (string sub in subKeys) {
                            using (var subKey = rootKey.OpenSubKey(sub)) {
                                if (subKey == null) continue;
                                string name = (subKey.GetValue("DistributionName") ?? "").ToString();
                                if (string.IsNullOrEmpty(name)) continue;

                                if (!string.IsNullOrEmpty(filter)) {
                                    string q = filter.Trim().ToLowerInvariant();
                                    if (!name.ToLowerInvariant().Contains(q) && !sub.ToLowerInvariant().Contains(q)) {
                                        continue;
                                    }
                                }

                                int ver = 2;
                                object vVal = subKey.GetValue("Version");
                                if (vVal != null) int.TryParse(vVal.ToString(), out ver);

                                int state = 1;
                                object sVal = subKey.GetValue("State");
                                if (sVal != null) int.TryParse(sVal.ToString(), out state);

                                string basePath = (subKey.GetValue("BasePath") ?? "").ToString();
                                string vhdFile = (subKey.GetValue("VhdFileName") ?? "ext4.vhdx").ToString();
                                string fullVhdPath = Path.Combine(basePath, vhdFile);

                                int defaultUid = 1000;
                                object uVal = subKey.GetValue("DefaultUid");
                                if (uVal != null) int.TryParse(uVal.ToString(), out defaultUid);

                                int flags = 15;
                                object fVal = subKey.GetValue("Flags");
                                if (fVal != null) int.TryParse(fVal.ToString(), out flags);

                                string flavor = (subKey.GetValue("Flavor") ?? "").ToString();
                                string osVersion = (subKey.GetValue("OsVersion") ?? "").ToString();
                                string packageFamily = (subKey.GetValue("PackageFamilyName") ?? "").ToString();

                                bool isReg = WslIsDistributionRegistered(name);
                                bool isDefault = (sub.Equals(defaultDistroGuid, StringComparison.OrdinalIgnoreCase));

                                bool interop = (flags & 1) != 0;
                                bool appendPath = (flags & 2) != 0;
                                bool driveMounting = (flags & 4) != 0;

                                var sbD = new StringBuilder();
                                sbD.Append("{");
                                sbD.AppendFormat("\"name\": \"{0}\", ", EscapeJson(name));
                                sbD.AppendFormat("\"guid\": \"{0}\", ", EscapeJson(sub));
                                sbD.AppendFormat("\"version\": {0}, ", ver);
                                sbD.AppendFormat("\"isRegistered\": {0}, ", isReg ? "true" : "false");
                                sbD.AppendFormat("\"isDefault\": {0}, ", isDefault ? "true" : "false");
                                sbD.AppendFormat("\"state\": {0}, ", state);
                                sbD.AppendFormat("\"defaultUid\": {0}, ", defaultUid);
                                sbD.AppendFormat("\"flags\": {0}, ", flags);
                                sbD.AppendFormat("\"flagsDecoded\": {{\"enableInterop\": {0}, \"appendNtPath\": {1}, \"enableDriveMounting\": {2}}}, ",
                                    interop ? "true" : "false", appendPath ? "true" : "false", driveMounting ? "true" : "false");
                                sbD.AppendFormat("\"basePath\": \"{0}\", ", EscapeJson(basePath));
                                sbD.AppendFormat("\"vhdPath\": \"{0}\", ", EscapeJson(fullVhdPath));
                                sbD.AppendFormat("\"vhdExists\": {0}, ", File.Exists(fullVhdPath) ? "true" : "false");
                                sbD.AppendFormat("\"flavor\": \"{0}\", ", EscapeJson(flavor));
                                sbD.AppendFormat("\"osVersion\": \"{0}\", ", EscapeJson(osVersion));
                                sbD.AppendFormat("\"packageFamilyName\": \"{0}\"", EscapeJson(packageFamily));
                                sbD.Append("}");
                                distros.Add(sbD.ToString());
                            }
                        }
                    }
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"defaultDistributionGuid\": \"{0}\", ", EscapeJson(defaultDistroGuid));
                sbOut.AppendFormat("\"natIpAddress\": \"{0}\", ", EscapeJson(natIp));
                sbOut.AppendFormat("\"distributionCount\": {0}, ", distros.Count);
                sbOut.AppendFormat("\"distributions\": [{0}]", string.Join(", ", distros.ToArray()));
                sbOut.Append("}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        public class WslExecResult {
            public bool Success;
            public string Distribution = "";
            public string Command = "";
            public uint ExitCode;
            public string Stdout = "";
            public string Stderr = "";
            public long ExecutionTimeMs;
            public string Error = "";
        }

        static WslExecResult WslExecuteInternal(string command, string targetDistro, bool useCurrentDir, int timeoutMs) {
            var res = new WslExecResult { Command = command ?? "", Distribution = targetDistro ?? "" };
            IntPtr hOutRead = IntPtr.Zero, hOutWrite = IntPtr.Zero;
            IntPtr hErrRead = IntPtr.Zero, hErrWrite = IntPtr.Zero;
            IntPtr hProc = IntPtr.Zero;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try {
                if (string.IsNullOrEmpty(command)) {
                    res.Error = "Command must be specified";
                    return res;
                }

                string distro = targetDistro;
                if (string.IsNullOrEmpty(distro)) {
                    using (var rootKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Lxss")) {
                        if (rootKey != null) {
                            string defGuid = (rootKey.GetValue("DefaultDistribution") ?? "").ToString();
                            if (!string.IsNullOrEmpty(defGuid)) {
                                using (var subKey = rootKey.OpenSubKey(defGuid)) {
                                    if (subKey != null) distro = (subKey.GetValue("DistributionName") ?? "").ToString();
                                }
                            }
                        }
                    }
                }
                if (string.IsNullOrEmpty(distro)) distro = "Ubuntu";
                res.Distribution = distro;

                if (timeoutMs <= 0) timeoutMs = 60000;

                WSL_SECURITY_ATTRIBUTES sa = new WSL_SECURITY_ATTRIBUTES();
                sa.nLength = Marshal.SizeOf(sa);
                sa.bInheritHandle = true;
                sa.lpSecurityDescriptor = IntPtr.Zero;

                if (!CreatePipe(out hOutRead, out hOutWrite, ref sa, 0) ||
                    !CreatePipe(out hErrRead, out hErrWrite, ref sa, 0)) {
                    res.Error = "Failed to create IPC pipes for WSL execution";
                    return res;
                }

                string launchCmd = command;
                if (!launchCmd.StartsWith("/bin/sh ") && !launchCmd.StartsWith("/bin/bash ")) {
                    launchCmd = "/bin/sh -c \"" + command.Replace("\"", "\\\"") + "\"";
                }

                int hr = WslLaunch(distro, launchCmd, useCurrentDir, IntPtr.Zero, hOutWrite, hErrWrite, out hProc);

                CloseHandle(hOutWrite); hOutWrite = IntPtr.Zero;
                CloseHandle(hErrWrite); hErrWrite = IntPtr.Zero;

                if (hr != 0 || hProc == IntPtr.Zero) {
                    res.Error = string.Format("WslLaunch failed with HRESULT: 0x{0:X8}", hr);
                    return res;
                }

                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();

                var outThread = new System.Threading.Thread(() => {
                    byte[] b = new byte[4096];
                    uint br;
                    while (ReadFile(hOutRead, b, (uint)b.Length, out br, IntPtr.Zero) && br > 0) {
                        sbOut.Append(Encoding.UTF8.GetString(b, 0, (int)br));
                    }
                });
                var errThread = new System.Threading.Thread(() => {
                    byte[] b = new byte[4096];
                    uint br;
                    while (ReadFile(hErrRead, b, (uint)b.Length, out br, IntPtr.Zero) && br > 0) {
                        sbErr.Append(Encoding.UTF8.GetString(b, 0, (int)br));
                    }
                });

                outThread.IsBackground = true;
                errThread.IsBackground = true;
                outThread.Start();
                errThread.Start();

                uint waitRes = WaitForSingleObject(hProc, (uint)timeoutMs);
                if (waitRes != 0) {
                    TerminateProcess(hProc, 1);
                    res.Error = "Execution timed out after " + timeoutMs + "ms";
                }

                outThread.Join(1500);
                errThread.Join(1500);

                uint exitCode = 0;
                GetExitCodeProcess(hProc, out exitCode);
                sw.Stop();

                res.Success = (waitRes == 0 && exitCode == 0);
                res.ExitCode = exitCode;
                res.Stdout = sbOut.ToString().TrimEnd();
                res.Stderr = sbErr.ToString().TrimEnd();
                res.ExecutionTimeMs = sw.ElapsedMilliseconds;
                return res;
            } catch (Exception ex) {
                res.Error = ex.Message;
                return res;
            } finally {
                if (hOutRead != IntPtr.Zero) CloseHandle(hOutRead);
                if (hOutWrite != IntPtr.Zero) CloseHandle(hOutWrite);
                if (hErrRead != IntPtr.Zero) CloseHandle(hErrRead);
                if (hErrWrite != IntPtr.Zero) CloseHandle(hErrWrite);
                if (hProc != IntPtr.Zero) CloseHandle(hProc);
            }
        }

        static void WslExecuteCmd(string command, string targetDistro, bool useCurrentDir, int timeoutMs) {
            var r = WslExecuteInternal(command, targetDistro, useCurrentDir, timeoutMs);
            if (!string.IsNullOrEmpty(r.Error) && !r.Error.StartsWith("Execution timed out")) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\", \"distribution\": \"{1}\"}}", EscapeJson(r.Error), EscapeJson(r.Distribution)));
            } else {
                var sbJson = new StringBuilder();
                sbJson.Append("{");
                sbJson.AppendFormat("\"success\": {0}, ", r.Success ? "true" : "false");
                sbJson.AppendFormat("\"distribution\": \"{0}\", ", EscapeJson(r.Distribution));
                sbJson.AppendFormat("\"command\": \"{0}\", ", EscapeJson(r.Command));
                sbJson.AppendFormat("\"exitCode\": {0}, ", r.ExitCode);
                sbJson.AppendFormat("\"stdout\": \"{0}\", ", EscapeJson(r.Stdout));
                sbJson.AppendFormat("\"stderr\": \"{0}\", ", EscapeJson(r.Stderr));
                sbJson.AppendFormat("\"executionTimeMs\": {0}, ", r.ExecutionTimeMs);
                sbJson.AppendFormat("\"error\": \"{0}\"", EscapeJson(r.Error ?? ""));
                sbJson.Append("}");
                Console.WriteLine(sbJson.ToString());
            }
        }

        static void WslStatusCmd() {
            try {
                bool wslApiAvailable = File.Exists(Path.Combine(Environment.SystemDirectory, "wslapi.dll"));
                bool wslExeAvailable = File.Exists(Path.Combine(Environment.SystemDirectory, "wsl.exe"));

                string defaultDistro = "";
                int distroCount = 0;
                var distros = new List<string>();

                using (var rootKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Lxss")) {
                    if (rootKey != null) {
                        string defGuid = (rootKey.GetValue("DefaultDistribution") ?? "").ToString();
                        string[] subKeys = rootKey.GetSubKeyNames();
                        distroCount = subKeys.Length;

                        foreach (string sub in subKeys) {
                            using (var subKey = rootKey.OpenSubKey(sub)) {
                                if (subKey == null) continue;
                                string name = (subKey.GetValue("DistributionName") ?? "").ToString();
                                if (!string.IsNullOrEmpty(name)) {
                                    distros.Add(name);
                                    if (sub.Equals(defGuid, StringComparison.OrdinalIgnoreCase)) {
                                        defaultDistro = name;
                                    }
                                }
                            }
                        }
                    }
                }

                string kernelRelease = "";
                if (distros.Count > 0 && !string.IsNullOrEmpty(defaultDistro)) {
                    var r = WslExecuteInternal("uname -r", defaultDistro, false, 5000);
                    if (r.Success) {
                        kernelRelease = r.Stdout.Trim();
                    }
                }

                var sbOut = new StringBuilder();
                sbOut.Append("{");
                sbOut.Append("\"success\": true, ");
                sbOut.AppendFormat("\"wslApiAvailable\": {0}, ", wslApiAvailable ? "true" : "false");
                sbOut.AppendFormat("\"wslExeAvailable\": {0}, ", wslExeAvailable ? "true" : "false");
                sbOut.AppendFormat("\"defaultDistribution\": \"{0}\", ", EscapeJson(defaultDistro));
                sbOut.AppendFormat("\"distroCount\": {0}, ", distroCount);
                sbOut.AppendFormat("\"installedDistros\": [\"{0}\"], ", string.Join("\", \"", distros.ToArray()));
                sbOut.AppendFormat("\"kernelRelease\": \"{0}\", ", EscapeJson(kernelRelease));
                sbOut.Append("\"virtualizationPlatform\": \"Hyper-V / Virtual Machine Platform\"");
                sbOut.Append("}");
                Console.WriteLine(sbOut.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        #region Phase 32: Windows Antimalware Scan Interface (AMSI) Subsystem (amsi.h / amsi.dll)

        public const int AMSI_RESULT_CLEAN = 0;
        public const int AMSI_RESULT_NOT_DETECTED = 1;
        public const int AMSI_RESULT_BLOCKED_BY_ADMIN_START = 0x4000;
        public const int AMSI_RESULT_BLOCKED_BY_ADMIN_END = 0x4FFF;
        public const int AMSI_RESULT_DETECTED = 32768;

        [DllImport("amsi.dll", EntryPoint = "AmsiInitialize", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int AmsiInitialize(
            [MarshalAs(UnmanagedType.LPWStr)] string appName,
            out IntPtr amsiContext);

        [DllImport("amsi.dll", EntryPoint = "AmsiUninitialize", CallingConvention = CallingConvention.StdCall)]
        public static extern void AmsiUninitialize(IntPtr amsiContext);

        [DllImport("amsi.dll", EntryPoint = "AmsiOpenSession", CallingConvention = CallingConvention.StdCall)]
        public static extern int AmsiOpenSession(
            IntPtr amsiContext,
            out IntPtr amsiSession);

        [DllImport("amsi.dll", EntryPoint = "AmsiCloseSession", CallingConvention = CallingConvention.StdCall)]
        public static extern void AmsiCloseSession(
            IntPtr amsiContext,
            IntPtr amsiSession);

        [DllImport("amsi.dll", EntryPoint = "AmsiScanString", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int AmsiScanString(
            IntPtr amsiContext,
            [MarshalAs(UnmanagedType.LPWStr)] string @string,
            [MarshalAs(UnmanagedType.LPWStr)] string contentName,
            IntPtr amsiSession,
            out int result);

        [DllImport("amsi.dll", EntryPoint = "AmsiScanBuffer", CallingConvention = CallingConvention.StdCall)]
        public static extern int AmsiScanBuffer(
            IntPtr amsiContext,
            byte[] buffer,
            uint length,
            [MarshalAs(UnmanagedType.LPWStr)] string contentName,
            IntPtr amsiSession,
            out int result);

        static string GetAmsiResultName(int res) {
            if (res == 0) return "CLEAN";
            if (res == 1) return "NOT_DETECTED";
            if (res >= 0x4000 && res <= 0x4FFF) return "BLOCKED_BY_ADMIN";
            if (res >= 32768) return "DETECTED";
            return "UNKNOWN_" + res;
        }

        static string GetAmsiRiskLevel(int res) {
            if (res >= 32768) return "MALICIOUS";
            if (res >= 0x4000 && res <= 0x4FFF) return "ADMIN_BLOCKED";
            if (res == 0 || res == 1) return "CLEAN";
            return "SUSPICIOUS";
        }

        public class AmsiProviderInfo {
            public string Guid = "";
            public string Name = "";
            public string InprocServer = "";
            public string ThreadingModel = "";
        }

        static List<AmsiProviderInfo> GetRegisteredAmsiProviders() {
            var list = new List<AmsiProviderInfo>();
            try {
                using (var hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)) {
                    using (var provKey = hklm.OpenSubKey(@"SOFTWARE\Microsoft\AMSI\Providers")) {
                        if (provKey != null) {
                            foreach (string sub in provKey.GetSubKeyNames()) {
                                var info = new AmsiProviderInfo { Guid = sub };
                                try {
                                    using (var clsidKey = hklm.OpenSubKey(@"SOFTWARE\Classes\CLSID\" + sub)) {
                                        if (clsidKey != null) {
                                            info.Name = (clsidKey.GetValue(null) ?? "").ToString();
                                            using (var inproc = clsidKey.OpenSubKey("InprocServer32")) {
                                                if (inproc != null) {
                                                    info.InprocServer = (inproc.GetValue(null) ?? "").ToString();
                                                    info.ThreadingModel = (inproc.GetValue("ThreadingModel") ?? "").ToString();
                                                }
                                            }
                                        }
                                    }
                                } catch {}
                                list.Add(info);
                            }
                        }
                    }
                }
            } catch {}
            return list;
        }

        static void AmsiStatusCmd() {
            try {
                string amsiPath = Path.Combine(Environment.SystemDirectory, "amsi.dll");
                bool amsiAvailable = File.Exists(amsiPath);
                bool engineInitialized = false;
                int initHr = -1;

                if (amsiAvailable) {
                    IntPtr ctx = IntPtr.Zero;
                    try {
                        initHr = AmsiInitialize("GeminiSuperDiagnostics", out ctx);
                        if (initHr == 0 && ctx != IntPtr.Zero) {
                            engineInitialized = true;
                            AmsiUninitialize(ctx);
                        }
                    } catch (Exception ex) {
                        initHr = Marshal.GetHRForException(ex);
                    }
                }

                var providers = GetRegisteredAmsiProviders();

                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"success\": true, ");
                sb.AppendFormat("\"amsiAvailable\": {0}, ", amsiAvailable ? "true" : "false");
                sb.AppendFormat("\"amsiDllPath\": \"{0}\", ", EscapeJson(amsiPath));
                sb.AppendFormat("\"engineInitialized\": {0}, ", engineInitialized ? "true" : "false");
                sb.AppendFormat("\"initHResult\": {0}, ", initHr);
                sb.AppendFormat("\"activeProvidersCount\": {0}, ", providers.Count);
                sb.Append("\"providers\": [");
                for (int i = 0; i < providers.Count; i++) {
                    var p = providers[i];
                    if (i > 0) sb.Append(", ");
                    sb.AppendFormat("{{\"guid\": \"{0}\", \"name\": \"{1}\", \"inprocServer\": \"{2}\", \"threadingModel\": \"{3}\"}}",
                        EscapeJson(p.Guid), EscapeJson(p.Name), EscapeJson(p.InprocServer), EscapeJson(p.ThreadingModel));
                }
                sb.Append("], ");
                sb.Append("\"capabilities\": [\"buffer_scan\", \"string_scan\", \"session_isolation\", \"uac_request_eval\"]");
                sb.Append("}");
                Console.WriteLine(sb.ToString());
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AmsiScanStringCmd(string content, string contentName, string appName) {
            try {
                if (content == null) content = "";
                if (string.IsNullOrEmpty(appName)) appName = "GeminiSuperSystem";
                if (string.IsNullOrEmpty(contentName)) contentName = "unnamed_content";

                var sw = System.Diagnostics.Stopwatch.StartNew();
                IntPtr ctx = IntPtr.Zero;
                int hrInit = AmsiInitialize(appName, out ctx);
                if (hrInit != 0 || ctx == IntPtr.Zero) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"AmsiInitialize failed with HRESULT 0x{0:X8}\"}}", hrInit));
                    return;
                }

                try {
                    IntPtr session = IntPtr.Zero;
                    AmsiOpenSession(ctx, out session);

                    try {
                        int resCode = 0;
                        int hrScan = AmsiScanString(ctx, content, contentName, session, out resCode);
                        sw.Stop();

                        if (hrScan != 0) {
                            if (hrScan == unchecked((int)0x80070015) || hrScan == unchecked((int)0x80070032)) {
                                var sbFallback = new StringBuilder();
                                sbFallback.Append("{");
                                sbFallback.Append("\"success\": true, ");
                                sbFallback.Append("\"resultCode\": 1, ");
                                sbFallback.Append("\"resultName\": \"NOT_DETECTED\", ");
                                sbFallback.Append("\"isMalware\": false, ");
                                sbFallback.Append("\"isBlocked\": false, ");
                                sbFallback.Append("\"riskLevel\": \"CLEAN\", ");
                                sbFallback.AppendFormat("\"contentLength\": {0}, ", content.Length);
                                sbFallback.AppendFormat("\"contentName\": \"{0}\", ", EscapeJson(contentName));
                                sbFallback.AppendFormat("\"appName\": \"{0}\", ", EscapeJson(appName));
                                sbFallback.Append("\"serviceReady\": false, ");
                                sbFallback.AppendFormat("\"scanTimeMs\": {0}", sw.ElapsedMilliseconds);
                                sbFallback.Append("}");
                                Console.WriteLine(sbFallback.ToString());
                                return;
                            }
                            Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"AmsiScanString failed with HRESULT 0x{0:X8}\"}}", hrScan));
                            return;
                        }

                        string resName = GetAmsiResultName(resCode);
                        string risk = GetAmsiRiskLevel(resCode);
                        bool isMalware = (resCode >= 32768);
                        bool isBlocked = (resCode >= 0x4000 && resCode <= 0x4FFF);

                        var sb = new StringBuilder();
                        sb.Append("{");
                        sb.Append("\"success\": true, ");
                        sb.AppendFormat("\"resultCode\": {0}, ", resCode);
                        sb.AppendFormat("\"resultName\": \"{0}\", ", EscapeJson(resName));
                        sb.AppendFormat("\"isMalware\": {0}, ", isMalware ? "true" : "false");
                        sb.AppendFormat("\"isBlocked\": {0}, ", isBlocked ? "true" : "false");
                        sb.AppendFormat("\"riskLevel\": \"{0}\", ", EscapeJson(risk));
                        sb.AppendFormat("\"contentLength\": {0}, ", content.Length);
                        sb.AppendFormat("\"contentName\": \"{0}\", ", EscapeJson(contentName));
                        sb.AppendFormat("\"appName\": \"{0}\", ", EscapeJson(appName));
                        sb.Append("\"serviceReady\": true, ");
                        sb.AppendFormat("\"scanTimeMs\": {0}", sw.ElapsedMilliseconds);
                        sb.Append("}");
                        Console.WriteLine(sb.ToString());
                    } finally {
                        if (session != IntPtr.Zero) AmsiCloseSession(ctx, session);
                    }
                } finally {
                    AmsiUninitialize(ctx);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void AmsiScanBufferCmd(string bufferPayload, string encoding, string filePath, string contentName, string appName) {
            try {
                if (string.IsNullOrEmpty(appName)) appName = "GeminiSuperSystem";
                if (string.IsNullOrEmpty(contentName)) {
                    contentName = !string.IsNullOrEmpty(filePath) ? Path.GetFileName(filePath) : "unnamed_buffer";
                }

                byte[] data = null;
                if (!string.IsNullOrEmpty(filePath)) {
                    if (!File.Exists(filePath)) {
                        Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"File not found: {0}\"}}", EscapeJson(filePath)));
                        return;
                    }
                    data = File.ReadAllBytes(filePath);
                } else if (!string.IsNullOrEmpty(bufferPayload)) {
                    string enc = (encoding ?? "base64").ToLowerInvariant();
                    if (enc == "hex") {
                        int len = bufferPayload.Length;
                        data = new byte[len / 2];
                        for (int i = 0; i < len; i += 2) {
                            data[i / 2] = Convert.ToByte(bufferPayload.Substring(i, 2), 16);
                        }
                    } else if (enc == "utf8" || enc == "text") {
                        data = Encoding.UTF8.GetBytes(bufferPayload);
                    } else {
                        try {
                            data = Convert.FromBase64String(bufferPayload);
                        } catch {
                            data = Encoding.UTF8.GetBytes(bufferPayload);
                        }
                    }
                } else {
                    data = new byte[0];
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                IntPtr ctx = IntPtr.Zero;
                int hrInit = AmsiInitialize(appName, out ctx);
                if (hrInit != 0 || ctx == IntPtr.Zero) {
                    Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"AmsiInitialize failed with HRESULT 0x{0:X8}\"}}", hrInit));
                    return;
                }

                try {
                    IntPtr session = IntPtr.Zero;
                    AmsiOpenSession(ctx, out session);

                    try {
                        int resCode = 0;
                        int hrScan = AmsiScanBuffer(ctx, data, (uint)data.Length, contentName, session, out resCode);
                        sw.Stop();

                        if (hrScan != 0) {
                            if (hrScan == unchecked((int)0x80070015) || hrScan == unchecked((int)0x80070032)) {
                                var sbFallback = new StringBuilder();
                                sbFallback.Append("{");
                                sbFallback.Append("\"success\": true, ");
                                sbFallback.Append("\"resultCode\": 1, ");
                                sbFallback.Append("\"resultName\": \"NOT_DETECTED\", ");
                                sbFallback.Append("\"isMalware\": false, ");
                                sbFallback.Append("\"isBlocked\": false, ");
                                sbFallback.Append("\"riskLevel\": \"CLEAN\", ");
                                sbFallback.AppendFormat("\"bufferSizeBytes\": {0}, ", data.Length);
                                sbFallback.AppendFormat("\"contentName\": \"{0}\", ", EscapeJson(contentName));
                                sbFallback.AppendFormat("\"appName\": \"{0}\", ", EscapeJson(appName));
                                sbFallback.Append("\"serviceReady\": false, ");
                                sbFallback.AppendFormat("\"scanTimeMs\": {0}", sw.ElapsedMilliseconds);
                                sbFallback.Append("}");
                                Console.WriteLine(sbFallback.ToString());
                                return;
                            }
                            Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"AmsiScanBuffer failed with HRESULT 0x{0:X8}\"}}", hrScan));
                            return;
                        }

                        string resName = GetAmsiResultName(resCode);
                        string risk = GetAmsiRiskLevel(resCode);
                        bool isMalware = (resCode >= 32768);
                        bool isBlocked = (resCode >= 0x4000 && resCode <= 0x4FFF);

                        var sb = new StringBuilder();
                        sb.Append("{");
                        sb.Append("\"success\": true, ");
                        sb.AppendFormat("\"resultCode\": {0}, ", resCode);
                        sb.AppendFormat("\"resultName\": \"{0}\", ", EscapeJson(resName));
                        sb.AppendFormat("\"isMalware\": {0}, ", isMalware ? "true" : "false");
                        sb.AppendFormat("\"isBlocked\": {0}, ", isBlocked ? "true" : "false");
                        sb.AppendFormat("\"riskLevel\": \"{0}\", ", EscapeJson(risk));
                        sb.AppendFormat("\"bufferSizeBytes\": {0}, ", data.Length);
                        sb.AppendFormat("\"contentName\": \"{0}\", ", EscapeJson(contentName));
                        sb.AppendFormat("\"appName\": \"{0}\", ", EscapeJson(appName));
                        sb.Append("\"serviceReady\": true, ");
                        sb.AppendFormat("\"scanTimeMs\": {0}", sw.ElapsedMilliseconds);
                        sb.Append("}");
                        Console.WriteLine(sb.ToString());
                    } finally {
                        if (session != IntPtr.Zero) AmsiCloseSession(ctx, session);
                    }
                } finally {
                    AmsiUninitialize(ctx);
                }
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        #endregion

        const uint CF_UNICODETEXT = 13;
        const uint GMEM_MOVEABLE = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool CloseClipboard();
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool EmptyClipboard();
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("ole32.dll")]
        static extern int OleFlushClipboard();

        static void ClipboardGetCmd() {
            try {
                string text = "";
                bool hasText = false;
                bool hasImage = false;
                bool hasFiles = false;

                for (int attempt = 0; attempt < 5; attempt++) {
                    if (OpenClipboard(IntPtr.Zero)) {
                        try {
                            if (IsClipboardFormatAvailable(CF_UNICODETEXT)) {
                                IntPtr hData = GetClipboardData(CF_UNICODETEXT);
                                if (hData != IntPtr.Zero) {
                                    IntPtr p = GlobalLock(hData);
                                    if (p != IntPtr.Zero) {
                                        text = Marshal.PtrToStringUni(p) ?? "";
                                        GlobalUnlock(hData);
                                        hasText = !string.IsNullOrEmpty(text);
                                    }
                                }
                            }
                        } finally {
                            CloseClipboard();
                        }
                        break;
                    }
                    Thread.Sleep(50);
                }

                if (!hasText) {
                    RunSta(() => {
                        hasText = Clipboard.ContainsText();
                        hasImage = Clipboard.ContainsImage();
                        hasFiles = Clipboard.ContainsFileDropList();
                        if (hasText) {
                            text = Clipboard.GetText();
                        }
                    });
                } else {
                    RunSta(() => {
                        hasImage = Clipboard.ContainsImage();
                        hasFiles = Clipboard.ContainsFileDropList();
                    });
                }

                Console.WriteLine(string.Format("{{\"success\": true, \"hasText\": {0}, \"hasImage\": {1}, \"hasFiles\": {2}, \"charCount\": {3}, \"text\": \"{4}\"}}",
                    hasText ? "true" : "false", hasImage ? "true" : "false", hasFiles ? "true" : "false",
                    text.Length, EscapeJson(text)));
            } catch (Exception ex) {
                Console.WriteLine(string.Format("{{\"success\": false, \"error\": \"{0}\"}}", EscapeJson(ex.Message)));
            }
        }

        static void ClipboardSetCmd(string text) {
            Exception lastEx = null;
            if (text == null) text = "";
            for (int attempt = 0; attempt < 5; attempt++) {
                try {
                    if (OpenClipboard(IntPtr.Zero)) {
                        try {
                            EmptyClipboard();
                            byte[] bytes = Encoding.Unicode.GetBytes(text + "\0");
                            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
                            if (hGlobal != IntPtr.Zero) {
                                IntPtr pTarget = GlobalLock(hGlobal);
                                if (pTarget != IntPtr.Zero) {
                                    Marshal.Copy(bytes, 0, pTarget, bytes.Length);
                                    GlobalUnlock(hGlobal);
                                    SetClipboardData(CF_UNICODETEXT, hGlobal);
                                }
                            }
                        } finally {
                            CloseClipboard();
                        }

                        Console.WriteLine(string.Format("{{\"success\": true, \"charCount\": {0}}}", text.Length));
                        return;
                    }
                } catch (Exception ex) {
                    lastEx = ex;
                }
                Thread.Sleep(80);
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
                if (OpenClipboard(IntPtr.Zero)) {
                    try {
                        EmptyClipboard();
                    } finally {
                        CloseClipboard();
                    }
                }
                try {
                    RunSta(() => {
                        Clipboard.Clear();
                        try { OleFlushClipboard(); } catch {}
                    });
                } catch {}
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
                        searcher.Options.Timeout = new TimeSpan(0, 0, 3);
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
            } else if (cmd == "dwm_status" || cmd == "dwm") {
                DwmStatusCmd();
            } else if (cmd == "dwm_window" || cmd == "dwm_window_attributes" || cmd == "dwm_attributes") {
                string target = args.Length >= 2 ? args[1] : "active";
                DwmWindowAttributesCmd(target);
            } else if (cmd == "dwm_set_window" || cmd == "dwm_set_attribute" || cmd == "dwm_actuate") {
                string target = args.Length >= 2 ? args[1] : "active";
                string jsonArgs = args.Length >= 3 ? args[2] : "{}";
                DwmSetWindowAttributeCmd(target, jsonArgs);
            } else if (cmd == "sys_arch" || cmd == "system_arch" || cmd == "system_architecture") {
                SystemArchitectureCmd();
            } else if (cmd == "sys_mem" || cmd == "system_memory" || cmd == "system_memory_status") {
                SystemMemoryStatusCmd();
            } else if (cmd == "sys_firmware" || cmd == "system_firmware" || cmd == "system_firmware_tables") {
                string prov = args.Length >= 2 ? args[1] : "ACPI";
                string table = args.Length >= 3 ? args[2] : "";
                SystemFirmwareTablesCmd(prov, table);
            } else if (cmd == "wintrust_verify" || cmd == "verify_file" || cmd == "verify_signature") {
                string p = args.Length >= 2 ? args[1] : "";
                bool allowCat = args.Length >= 3 ? (args[2].ToLowerInvariant() != "false" && args[2] != "0") : true;
                bool checkRev = args.Length >= 4 ? (args[3].ToLowerInvariant() == "true" || args[3] == "1") : false;
                WinTrustVerifyFileCmd(p, allowCat, checkRev);
            } else if (cmd == "wintrust_signer" || cmd == "file_signer" || cmd == "signer_info") {
                string p = args.Length >= 2 ? args[1] : "";
                WinTrustSignerInfoCmd(p);
            } else if (cmd == "wintrust_catalog" || cmd == "file_catalog" || cmd == "catalog_search") {
                string p = args.Length >= 2 ? args[1] : "";
                WinTrustCatalogSearchCmd(p);
            } else if (cmd == "wnet_drives" || cmd == "network_drives" || cmd == "wnet_network_drives") {
                string scope = args.Length >= 2 ? args[1] : "connected";
                string type = args.Length >= 3 ? args[2] : "all";
                WNetNetworkDrivesCmd(scope, type);
            } else if (cmd == "wnet_connection" || cmd == "get_connection" || cmd == "wnet_get_connection") {
                string localName = args.Length >= 2 ? args[1] : null;
                WNetGetConnectionCmd(localName);
            } else if (cmd == "wnet_manage" || cmd == "manage_connection" || cmd == "wnet_manage_connection") {
                string action = args.Length >= 2 ? args[1] : "connect";
                string remote = args.Length >= 3 ? args[2] : null;
                string local = args.Length >= 4 ? args[3] : null;
                string user = args.Length >= 5 ? args[4] : null;
                string pass = args.Length >= 6 ? args[5] : null;
                bool persistent = args.Length >= 7 ? (args[6].ToLowerInvariant() == "true" || args[6] == "1") : false;
                bool force = args.Length >= 8 ? (args[7].ToLowerInvariant() == "true" || args[7] == "1") : false;
                WNetManageConnectionCmd(action, remote, local, user, pass, persistent, force);
            } else if (cmd == "toolhelp_modules" || cmd == "process_modules" || cmd == "list_modules") {
                string target = args.Length >= 2 ? args[1] : "current";
                string search = args.Length >= 3 ? args[2] : "";
                int limit = args.Length >= 4 ? int.Parse(args[3]) : 100;
                ToolHelpModulesCmd(target, search, limit);
            } else if (cmd == "toolhelp_threads" || cmd == "process_threads" || cmd == "list_threads") {
                string target = args.Length >= 2 ? args[1] : "current";
                int limit = args.Length >= 3 ? int.Parse(args[2]) : 100;
                ToolHelpThreadsCmd(target, limit);
            } else if (cmd == "toolhelp_process_tree" || cmd == "process_tree" || cmd == "toolhelp_tree") {
                uint rootPid = args.Length >= 2 && !string.IsNullOrEmpty(args[1]) ? uint.Parse(args[1]) : 0;
                string search = args.Length >= 3 ? args[2] : "";
                int limit = args.Length >= 4 ? int.Parse(args[3]) : 150;
                ToolHelpProcessTreeCmd(rootPid, search, limit);
            } else if (cmd == "sens_alive" || cmd == "network_alive" || cmd == "sens_network_alive") {
                SensNetworkAliveCmd();
            } else if (cmd == "sens_reachable" || cmd == "destination_reachable" || cmd == "sens_destination_reachable") {
                string dest = args.Length >= 2 ? args[1] : "8.8.8.8";
                int timeoutMs = args.Length >= 3 ? int.Parse(args[2]) : 3000;
                SensDestinationReachableCmd(dest, timeoutMs);
            } else if (cmd == "sens_connectivity" || cmd == "network_connectivity" || cmd == "sens_network_connectivity") {
                bool includeProfiles = args.Length >= 2 ? (args[1].ToLowerInvariant() != "false" && args[1] != "0") : true;
                bool includeAdapters = args.Length >= 3 ? (args[2].ToLowerInvariant() != "false" && args[2] != "0") : true;
                SensNetworkConnectivityCmd(includeProfiles, includeAdapters);
            } else if (cmd == "time_zone_info" || cmd == "timezone_info" || cmd == "time_zone") {
                bool enumAll = args.Length >= 2 ? (args[1].ToLowerInvariant() == "true" || args[1] == "1") : false;
                string filter = args.Length >= 3 ? args[2] : "";
                string utcTs = args.Length >= 4 ? args[3] : "";
                TimeGetZoneInfoCmd(enumAll, filter, utcTs);
            } else if (cmd == "time_chronometry" || cmd == "chronometry" || cmd == "time_clocks") {
                TimeGetChronometryCmd();
            } else if (cmd == "time_adjustment" || cmd == "time_drift" || cmd == "time_sync") {
                TimeGetAdjustmentCmd();
            } else if (cmd == "power_schemes" || cmd == "power_schemes_list" || cmd == "list_power_schemes") {
                PowerSchemesListCmd();
            } else if (cmd == "power_execution_state" || cmd == "keep_awake" || cmd == "set_execution_state") {
                bool sysReq = args.Length >= 2 ? (args[1].ToLowerInvariant() != "false" && args[1] != "0") : true;
                bool dispReq = args.Length >= 3 ? (args[2].ToLowerInvariant() == "true" || args[2] == "1") : false;
                bool away = args.Length >= 4 ? (args[3].ToLowerInvariant() == "true" || args[3] == "1") : false;
                bool cont = args.Length >= 5 ? (args[4].ToLowerInvariant() != "false" && args[4] != "0") : true;
                bool rest = args.Length >= 6 ? (args[5].ToLowerInvariant() == "true" || args[5] == "1") : false;
                PowerExecutionStateCmd(sysReq, dispReq, away, cont, rest);
            } else if (cmd == "power_hardware_telemetry" || cmd == "power_telemetry" || cmd == "battery_state") {
                PowerHardwareTelemetryCmd();
            } else if (cmd == "net_shares" || cmd == "net_share" || cmd == "smb_shares") {
                string shareName = args.Length >= 2 ? args[1] : "";
                string typeFilter = args.Length >= 3 ? args[2] : "all";
                NetSharesCmd(shareName, typeFilter);
            } else if (cmd == "net_sessions" || cmd == "net_session" || cmd == "smb_sessions") {
                string clientFilter = args.Length >= 2 ? args[1] : "";
                string userFilter = args.Length >= 3 ? args[2] : "";
                bool includeFiles = args.Length >= 4 ? (args[3].ToLowerInvariant() != "false" && args[3] != "0") : true;
                NetSessionsCmd(clientFilter, userFilter, includeFiles);
            } else if (cmd == "net_accounts" || cmd == "net_users" || cmd == "net_groups") {
                bool incUsers = args.Length >= 2 ? (args[1].ToLowerInvariant() != "false" && args[1] != "0") : true;
                bool incGroups = args.Length >= 3 ? (args[2].ToLowerInvariant() != "false" && args[2] != "0") : true;
                string targetGroup = args.Length >= 4 ? args[3] : "Administrators";
                NetAccountsCmd(incUsers, incGroups, targetGroup);
            } else if (cmd == "memory_virtual_query" || cmd == "virtual_query" || cmd == "vm_query") {
                int pid = 0;
                if (args.Length >= 2 && !string.IsNullOrEmpty(args[1])) int.TryParse(args[1], out pid);
                int maxReg = 50;
                if (args.Length >= 3 && !string.IsNullOrEmpty(args[2])) int.TryParse(args[2], out maxReg);
                string filter = args.Length >= 4 ? args[3] : "commit";
                MemoryVirtualQueryCmd(pid, maxReg, filter);
            } else if (cmd == "memory_heap_summary" || cmd == "heap_summary" || cmd == "heaps") {
                MemoryHeapSummaryCmd();
            } else if (cmd == "memory_working_set_tune" || cmd == "working_set_tune" || cmd == "ws_tune") {
                int pid = 0;
                if (args.Length >= 2 && !string.IsNullOrEmpty(args[1])) int.TryParse(args[1], out pid);
                int minWs = 0;
                if (args.Length >= 3 && !string.IsNullOrEmpty(args[2])) int.TryParse(args[2], out minWs);
                int maxWs = 0;
                if (args.Length >= 4 && !string.IsNullOrEmpty(args[3])) int.TryParse(args[3], out maxWs);
                bool emptyWs = args.Length >= 5 ? (args[4].ToLowerInvariant() == "true" || args[4] == "1") : false;
                MemoryWorkingSetTuneCmd(pid, minWs, maxWs, emptyWs);
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
            } else if (cmd == "console_info" || cmd == "con_info" || cmd == "console") {
                bool incProcs = args.Length >= 2 ? (args[1].ToLowerInvariant() != "false" && args[1] != "0") : true;
                ConsoleInfoCmd(incProcs);
            } else if (cmd == "console_mode" || cmd == "con_mode") {
                string vtOpt = args.Length >= 2 ? args[1] : null;
                string qeOpt = args.Length >= 3 ? args[2] : null;
                string miOpt = args.Length >= 4 ? args[3] : null;
                string extOpt = args.Length >= 5 ? args[4] : null;
                ConsoleModeCmd(vtOpt, qeOpt, miOpt, extOpt);
            } else if (cmd == "console_control" || cmd == "con_control" || cmd == "console_title") {
                string title = args.Length >= 2 ? args[1] : null;
                string curVis = args.Length >= 3 ? args[2] : null;
                string curSize = args.Length >= 4 ? args[3] : null;
                string activate = args.Length >= 5 ? args[4] : null;
                ConsoleControlCmd(title, curVis, curSize, activate);
            } else if (cmd == "wts_sessions" || cmd == "terminal_sessions" || cmd == "rdp_sessions") {
                bool incDetails = args.Length >= 2 ? (args[1].ToLowerInvariant() != "false" && args[1] != "0") : true;
                WtsSessionsCmd(incDetails);
            } else if (cmd == "wts_processes" || cmd == "terminal_processes" || cmd == "session_processes") {
                int sId = -1;
                if (args.Length >= 2 && !string.IsNullOrEmpty(args[1])) int.TryParse(args[1], out sId);
                string filter = args.Length >= 3 ? args[2] : "";
                int limit = 50;
                if (args.Length >= 4 && !string.IsNullOrEmpty(args[3])) int.TryParse(args[3], out limit);
                WtsProcessesCmd(sId, filter, limit);
            } else if (cmd == "wts_session_message" || cmd == "wts_message" || cmd == "session_message") {
                int sId = -1;
                if (args.Length >= 2 && !string.IsNullOrEmpty(args[1])) int.TryParse(args[1], out sId);
                string title = args.Length >= 3 ? args[2] : "Gemini Super System";
                string message = args.Length >= 4 ? args[3] : "Notice from Gemini Super System";
                uint style = 0x40;
                if (args.Length >= 5 && !string.IsNullOrEmpty(args[4])) uint.TryParse(args[4], out style);
                uint timeout = 10;
                if (args.Length >= 6 && !string.IsNullOrEmpty(args[5])) uint.TryParse(args[5], out timeout);
                bool wait = args.Length >= 7 ? (args[6].ToLowerInvariant() == "true" || args[6] == "1") : false;
                WtsSessionMessageCmd(sId, title, message, style, timeout, wait);
            } else if (cmd == "psapi_performance" || cmd == "performance_info" || cmd == "perf_info") {
                PsapiPerformanceCmd();
            } else if (cmd == "psapi_device_drivers" || cmd == "device_drivers" || cmd == "kernel_device_drivers") {
                string filter = args.Length >= 2 ? args[1] : "";
                int limit = 100;
                if (args.Length >= 3 && !string.IsNullOrEmpty(args[2])) int.TryParse(args[2], out limit);
                PsapiDeviceDriversCmd(filter, limit);
            } else if (cmd == "psapi_process_memory" || cmd == "proc_memory" || cmd == "process_memory_info") {
                int pid = 0;
                if (args.Length >= 2 && !string.IsNullOrEmpty(args[1])) int.TryParse(args[1], out pid);
                bool includeMapped = args.Length >= 3 ? (args[2].ToLowerInvariant() != "false" && args[2] != "0") : true;
                PsapiProcessMemoryCmd(pid, includeMapped);
            } else if (cmd == "cred_enumerate" || cmd == "credentials_list" || cmd == "cred_list") {
                string filter = args.Length >= 2 ? args[1] : null;
                int limit = 50;
                if (args.Length >= 3 && !string.IsNullOrEmpty(args[2])) int.TryParse(args[2], out limit);
                CredEnumerateCmd(filter, limit);
            } else if (cmd == "cred_read" || cmd == "credential_read" || cmd == "cred_get") {
                string targetName = args.Length >= 2 ? args[1] : "";
                uint credType = 1;
                if (args.Length >= 3 && !string.IsNullOrEmpty(args[2])) uint.TryParse(args[2], out credType);
                bool includeSecret = args.Length >= 4 ? (args[3].ToLowerInvariant() == "true" || args[3] == "1") : false;
                CredReadCmd(targetName, credType, includeSecret);
            } else if (cmd == "cred_manage" || cmd == "credential_manage" || cmd == "cred_set") {
                string action = args.Length >= 2 ? args[1] : "write";
                string targetName = args.Length >= 3 ? args[2] : "";
                string userName = args.Length >= 4 ? args[3] : "";
                string secret = args.Length >= 5 ? args[4] : "";
                string comment = args.Length >= 6 ? args[5] : "";
                uint credType = 1;
                if (args.Length >= 7 && !string.IsNullOrEmpty(args[6])) uint.TryParse(args[6], out credType);
                uint persist = 2;
                if (args.Length >= 8 && !string.IsNullOrEmpty(args[7])) uint.TryParse(args[7], out persist);
                CredManageCmd(action, targetName, userName, secret, comment, credType, persist);
            } else if (cmd == "dns_query" || cmd == "dns" || cmd == "dns_lookup") {
                string host = args.Length >= 2 ? args[1] : "";
                string typeStr = args.Length >= 3 ? args[2] : "A";
                bool bypassCache = args.Length >= 4 ? (args[3].ToLowerInvariant() == "true" || args[3] == "1") : false;
                DnsQueryCmd(host, typeStr, bypassCache);
            } else if (cmd == "dns_flush" || cmd == "dns_cache_flush" || cmd == "flush_dns") {
                DnsFlushCmd();
            } else if (cmd == "dns_resolve" || cmd == "resolve_host" || cmd == "dns_resolve_host") {
                string host = args.Length >= 2 ? args[1] : "";
                bool bypassCache = args.Length >= 3 ? (args[2].ToLowerInvariant() == "true" || args[2] == "1") : false;
                DnsResolveCmd(host, bypassCache);
            } else if (cmd == "dpapi_protect" || cmd == "protect_data") {
                string data = args.Length >= 2 ? args[1] : "";
                string desc = args.Length >= 3 ? args[2] : "Gemini Secret";
                string scope = args.Length >= 4 ? args[3] : "CurrentUser";
                string entropy = args.Length >= 5 ? args[4] : null;
                DpapiProtectCmd(data, desc, scope, entropy);
            } else if (cmd == "dpapi_unprotect" || cmd == "unprotect_data") {
                string cipher = args.Length >= 2 ? args[1] : "";
                string entropy = args.Length >= 3 ? args[2] : null;
                DpapiUnprotectCmd(cipher, entropy);
            } else if (cmd == "dpapi_file" || cmd == "dpapi_protect_file") {
                string action = args.Length >= 2 ? args[1] : "encrypt";
                string src = args.Length >= 3 ? args[2] : "";
                string dst = args.Length >= 4 ? args[3] : "";
                string scope = args.Length >= 5 ? args[4] : "CurrentUser";
                string desc = args.Length >= 6 ? args[5] : "";
                string entropy = args.Length >= 7 ? args[6] : null;
                DpapiProtectFileCmd(action, src, dst, scope, desc, entropy);
            } else if (cmd == "fs_volumes" || cmd == "fs-volumes") {
                FsVolumesCmd();
            } else if (cmd == "fs_mount_points" || cmd == "fs-mount-points" || cmd == "fs_mounts") {
                string root = args.Length >= 2 ? args[1] : "C:\\";
                FsVolumeMountPointsCmd(root);
            } else if (cmd == "fs_drives" || cmd == "fs-drives") {
                string filter = args.Length >= 2 ? args[1] : null;
                FsDrivesCmd(filter);
            } else if (cmd == "spooler_printers" || cmd == "spooler-printers" || cmd == "printers") {
                SpoolerPrintersCmd();
            } else if (cmd == "spooler_jobs" || cmd == "spooler-jobs" || cmd == "print_jobs") {
                string printerName = args.Length >= 2 ? args[1] : null;
                SpoolerJobsCmd(printerName);
            } else if (cmd == "spooler_default_printer" || cmd == "spooler-default-printer" || cmd == "default_printer") {
                string newDefault = args.Length >= 2 ? args[1] : null;
                SpoolerDefaultPrinterCmd(newDefault);
            } else if (cmd == "intl_locales" || cmd == "locales" || cmd == "nls_locales") {
                string filter = args.Length >= 2 ? args[1] : "";
                int limit = 50;
                if (args.Length >= 3 && !string.IsNullOrEmpty(args[2])) int.TryParse(args[2], out limit);
                bool detailed = args.Length >= 4 ? (args[3].ToLowerInvariant() == "true" || args[3] == "1") : false;
                IntlLocalesCmd(filter, limit, detailed);
            } else if (cmd == "intl_codepages" || cmd == "codepages" || cmd == "nls_codepages") {
                string query = args.Length >= 2 ? args[1] : "";
                IntlCodePagesCmd(query);
            } else if (cmd == "intl_ui_languages" || cmd == "ui_languages" || cmd == "preferred_languages") {
                IntlUiLanguagesCmd();
            } else if (cmd == "iphlp_routing_table" || cmd == "routing_table" || cmd == "routes") {
                string filter = args.Length >= 2 ? args[1] : "";
                int limit = 100;
                if (args.Length >= 3 && !string.IsNullOrEmpty(args[2])) int.TryParse(args[2], out limit);
                IpHlpRoutingTableCmd(filter, limit);
            } else if (cmd == "iphlp_arp_table" || cmd == "arp_table" || cmd == "arp") {
                string filter = args.Length >= 2 ? args[1] : "";
                int limit = 100;
                if (args.Length >= 3 && !string.IsNullOrEmpty(args[2])) int.TryParse(args[2], out limit);
                IpHlpArpTableCmd(filter, limit);
            } else if (cmd == "iphlp_interfaces" || cmd == "network_interfaces" || cmd == "interfaces") {
                string filter = args.Length >= 2 ? args[1] : "";
                IpHlpInterfacesCmd(filter);
            } else if (cmd == "display_devices" || cmd == "enum_display_devices" || cmd == "graphics_adapters") {
                string adapterFilter = args.Length >= 2 ? args[1] : "";
                bool includeMonitors = args.Length >= 3 ? (args[2].Equals("true", StringComparison.OrdinalIgnoreCase) || args[2] == "1") : true;
                DisplayDevicesCmd(adapterFilter, includeMonitors);
            } else if (cmd == "display_modes" || cmd == "enum_display_settings" || cmd == "graphics_modes") {
                string deviceName = args.Length >= 2 ? args[1] : "";
                string modeType = args.Length >= 3 ? args[2] : "all";
                int limit = 100;
                if (args.Length >= 4 && !string.IsNullOrEmpty(args[3])) int.TryParse(args[3], out limit);
                DisplayModesCmd(deviceName, modeType, limit);
            } else if (cmd == "display_capabilities" || cmd == "device_caps" || cmd == "display_caps") {
                string deviceName = args.Length >= 2 ? args[1] : "";
                DisplayCapsCmd(deviceName);
            } else if (cmd == "vhd_attached_disks" || cmd == "attached_vhds" || cmd == "virtual_disks") {
                VhdAttachedDisksCmd();
            } else if (cmd == "vhd_inspect" || cmd == "inspect_vhd" || cmd == "vhdx_info") {
                string vhdPath = args.Length >= 2 ? args[1] : "";
                VhdInspectCmd(vhdPath);
            } else if (cmd == "vhd_storage_dependencies" || cmd == "storage_dependencies" || cmd == "vhd_dependencies") {
                string drive = args.Length >= 2 ? args[1] : "C:";
                VhdStorageDependenciesCmd(drive);
            } else if (cmd == "wsl_distributions" || cmd == "wsl-distributions" || cmd == "wsl_distros") {
                string filter = args.Length >= 2 ? args[1] : "";
                WslDistributionsCmd(filter);
            } else if (cmd == "wsl_execute" || cmd == "wsl-execute" || cmd == "wsl_exec" || cmd == "wsl_launch") {
                string command = args.Length >= 2 ? args[1] : "";
                string distro = args.Length >= 3 ? args[2] : "";
                bool useCurrentDir = args.Length >= 4 ? (args[3].ToLowerInvariant() == "true" || args[3] == "1") : false;
                int timeoutMs = 60000;
                if (args.Length >= 5 && !string.IsNullOrEmpty(args[4])) int.TryParse(args[4], out timeoutMs);
                WslExecuteCmd(command, distro, useCurrentDir, timeoutMs);
            } else if (cmd == "wsl_status" || cmd == "wsl-status") {
                WslStatusCmd();
            } else if (cmd == "amsi_status" || cmd == "amsi-status" || cmd == "antimalware_status") {
                AmsiStatusCmd();
            } else if (cmd == "amsi_scan_string" || cmd == "amsi-scan-string" || cmd == "scan_string") {
                string content = args.Length >= 2 ? args[1] : "";
                string contentName = args.Length >= 3 ? args[2] : "unnamed_content";
                string appName = args.Length >= 4 ? args[3] : "GeminiSuperSystem";
                AmsiScanStringCmd(content, contentName, appName);
            } else if (cmd == "amsi_scan_buffer" || cmd == "amsi-scan-buffer" || cmd == "scan_buffer") {
                string bufferPayload = args.Length >= 2 ? args[1] : "";
                string encoding = args.Length >= 3 ? args[2] : "base64";
                string filePath = args.Length >= 4 ? args[3] : "";
                string contentName = args.Length >= 5 ? args[4] : "";
                string appName = args.Length >= 6 ? args[5] : "GeminiSuperSystem";
                AmsiScanBufferCmd(bufferPayload, encoding, filePath, contentName, appName);
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

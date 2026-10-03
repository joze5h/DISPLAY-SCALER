using System;
using System.Runtime.InteropServices;

namespace DISPLAY_SCALER.Native
{
    internal static class Win32Display
    {
        public const uint DM_PELSWIDTH = 0x00080000;

        public const uint DM_PELSHEIGHT = 0x00010000;
        public const uint DM_BITSPERPEL = 0x00040000;
        public const uint DM_DISPLAYFREQUENCY = 0x00400000;
        public const uint DM_DISPLAYFLAGS = 0x00200000;
        public const uint DM_POSITION = 0x00000020;
        public const uint DM_DISPLAYORIENTATION = 0x00000080;

        public const int DISP_CHANGE_SUCCESSFUL = 0;

        public const int DISP_CHANGE_RESTART = 1;
        public const int DISP_CHANGE_NEWMODE = 2;
        public const int DISP_CHANGE_FAILED = -1;
        public const int DISP_CHANGE_BADMODE = -2;
        public const int DISP_CHANGE_NOTUPDATED = -3;
        public const int DISP_CHANGE_BADFLAGS = -4;
        public const int DISP_CHANGE_BADPARAM = -5;
        public const int DISP_CHANGE_BADDUALVIEW = -6;

        public const uint CDS_UPDATEREGISTRY = 0x00000001;

        public const uint CDS_TEST = 0x00000002;
        public const uint CDS_FULLSCREEN = 0x00000004;
        public const uint CDS_GLOBAL = 0x00000008;
        public const uint CDS_SET_PRIMARY = 0x00000010;
        public const uint CDS_VIDEOPARAMETERS = 0x00000020;
        public const uint CDS_ENABLE_UNSAFE_MODES = 0x00000100;
        public const uint CDS_DISABLE_UNSAFE_MODES = 0x00000200;
        public const uint CDS_RESET = 0x20000000;
        public const uint CDS_NORESET = 0x10000000;


        public const int ENUM_CURRENT_SETTINGS = unchecked((int)0xFFFFFFFF);
        public const int ENUM_REGISTRY_SETTINGS = unchecked((int)0xFFFFFFFE);

        public const uint EDS_RAWMODE = 0x00000002;

        public const uint EDS_ROTATEDMODE = 0x00000004;

        public const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;

        public const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
        public const uint SDC_VALIDATE = 0x00000040;
        public const uint SDC_APPLY = 0x00000080;
        public const uint SDC_NO_OPTIMIZATION = 0x00000100;
        public const uint SDC_SAVE_TO_DATABASE = 0x00000200;
        public const uint SDC_ALLOW_CHANGES = 0x00000400;
        public const uint SDC_FORCE_MODE_ENUMERATION = 0x00001000;


        public const int ERROR_SUCCESS = 0;
        public const int ERROR_INSUFFICIENT_BUFFER = 122;

        public const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;

            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;

            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
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

        public const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;

        public const uint DISPLAY_DEVICE_MULTI_DRIVER = 0x00000002;
        public const uint DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;
        public const uint DISPLAY_DEVICE_MIRRORING_DRIVER = 0x00000008;
        public const uint DISPLAY_DEVICE_VGA_COMPATIBLE = 0x00000010;
        public const uint DISPLAY_DEVICE_REMOVABLE = 0x00000020;
        public const uint DISPLAY_DEVICE_MODESPRUNED = 0x08000000;
        public const uint DISPLAY_DEVICE_REMOTE = 0x04000000;
        public const uint DISPLAY_DEVICE_DISCONNECT = 0x02000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public uint type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_2DREGION
        {
            public uint cx;
            public uint cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINTL
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECTL
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
        {
            public ulong pixelRate;
            public DISPLAYCONFIG_RATIONAL hSyncFreq;
            public DISPLAYCONFIG_RATIONAL vSyncFreq;
            public DISPLAYCONFIG_2DREGION activeSize;
            public DISPLAYCONFIG_2DREGION totalSize;
            public uint videoStandard;
            public uint scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_TARGET_MODE
        {
            public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SOURCE_MODE
        {
            public uint width;
            public uint height;
            public uint pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
        {
            public POINTL PathSourceSize;
            public RECTL DesktopImageRegion;
            public RECTL DesktopImageClip;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct DISPLAYCONFIG_MODE_INFO
        {
            [FieldOffset(0)] public uint infoType;
            [FieldOffset(4)] public uint id;
            [FieldOffset(8)] public LUID adapterId;
            [FieldOffset(16)] public DISPLAYCONFIG_TARGET_MODE targetMode;
            [FieldOffset(16)] public DISPLAYCONFIG_SOURCE_MODE sourceMode;
            [FieldOffset(16)] public DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate;
            public uint scanLineOrdering;
            public int targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int QueryDisplayConfig(
            uint flags,
            ref uint numPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
            ref uint numModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetDisplayConfig(
            uint numPathArrayElements,
            DISPLAYCONFIG_PATH_INFO[] pathArray,
            uint numModeInfoArrayElements,
            DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int EnumDisplayDevices(string lpDevice, uint iDeviceNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int EnumDisplaySettingsEx(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFOEX info);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        public const int MONITORINFOF_PRIMARY = 0x00000001;

        public static string ChangeResultToString(int result)
        {
            switch (result)
            {
                case DISP_CHANGE_SUCCESSFUL: return "Успешно применено";
                case DISP_CHANGE_RESTART: return "Требуется перезагрузка Windows";
                case DISP_CHANGE_NEWMODE: return "Применён новый режим";
                case DISP_CHANGE_FAILED: return "Сбой применения настроек";
                case DISP_CHANGE_BADMODE: return "Неподдерживаемый режим графики";
                case DISP_CHANGE_NOTUPDATED: return "Применено, но не сохранено в реестре";
                case DISP_CHANGE_BADFLAGS: return "Некорректные флаги";
                case DISP_CHANGE_BADPARAM: return "Некорректный параметр";
                case DISP_CHANGE_BADDUALVIEW: return "Ошибка в режиме dual-view";
                default: return $"Код {result}";
            }
        }
    }
}

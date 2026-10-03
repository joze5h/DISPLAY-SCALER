using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Infrastructure.Timing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DISPLAY_SCALER.Native
{
    internal static class NvApi
    {
        public const int NVAPI_OK = 0;
        public const int NVAPI_ERROR = -1;
        public const int NVAPI_LIBRARY_NOT_FOUND = -2;
        public const int NVAPI_NO_IMPLEMENTATION = -3;
        public const int NVAPI_API_NOT_INITIALIZED = -4;
        public const int NVAPI_INVALID_ARGUMENT = -5;
        public const int NVAPI_NVIDIA_DEVICE_NOT_FOUND = -6;
        public const int NVAPI_END_ENUMERATION = -7;
        public const int NVAPI_INVALID_HANDLE = -8;
        public const int NVAPI_INCOMPATIBLE_STRUCT_VERSION = -9;
        public const int NVAPI_HANDLE_INVALIDATED = -10;
        public const int NVAPI_FUNCTION_NOT_FOUND = -136;

        private const uint ID_INITIALIZE = 0x0150E828;
        private const uint ID_GET_ERROR_MESSAGE = 0x6C2D048C;
        private const uint ID_GET_INTERFACE_VERSION_STRING = 0x0105FA66;
        private const uint ID_ENUM_PHYSICAL_GPUS = 0xE5AC921F;
        private const uint ID_GPU_GET_FULL_NAME = 0xCEEE8E9F;
        private const uint ID_ENUM_NVIDIA_DISPLAY_HANDLE = 0x9ABDD40D;
        private const uint ID_GET_ASSOCIATED_NVIDIA_DISPLAY_HANDLE = 0x35C29134;
        private const uint ID_DISP_GET_DISPLAY_ID_BY_DISPLAY_NAME = 0xAE457190;
        private const uint ID_DISP_GET_TIMING = 0x175167E9;
        private const uint ID_DISP_ENUM_CUSTOM_DISPLAY = 0xA2072D59;
        private const uint ID_DISP_TRY_CUSTOM_DISPLAY = 0x1F7DB630;
        private const uint ID_DISP_SAVE_CUSTOM_DISPLAY = 0x49882876;
        private const uint ID_DISP_REVERT_CUSTOM_DISPLAY_TRIAL = 0xCBBD40F0;
        private const uint ID_DISP_DELETE_CUSTOM_DISPLAY = 0x552E5B9B;
        private const uint ID_DISP_GET_DISPLAY_CONFIG = 0x11ABCCF8;
        private const uint ID_DISP_SET_DISPLAY_CONFIG = 0x5D8CF8DE;
        private const uint ID_DISP_GET_EDID_DATA = 0x436CED76;

        private const uint ID_GET_DVC_INFO = 0x4085DE45;

        private const uint ID_SET_DVC_LEVEL = 0x172409B4;
        private const uint ID_GET_DVC_INFO_EX = 0x0E45002D;
        private const uint ID_SET_DVC_LEVEL_EX = 0x4A82C2B1;

        internal const int NVAPI_SHORT_STRING_MAX = 64;
        internal const int NVAPI_MAX_PHYSICAL_GPUS = 64;
        internal const uint NV_DISPLAY_DVC_INFO_VER = 0x00010010;
        internal const uint NV_DISPLAY_DVC_INFO_EX_VER = 0x00010014;
        internal const int NV_FORMAT_A8R8G8B8 = 21;
        internal const int NV_TIMING_OVERRIDE_CURRENT = 0;
        internal const int NV_TIMING_OVERRIDE_AUTO = 1;
        internal const int NV_TIMING_OVERRIDE_EDID = 2;
        internal const int NV_TIMING_OVERRIDE_DMT = 3;
        internal const int NV_TIMING_OVERRIDE_DMT_RB = 4;
        internal const int NV_TIMING_OVERRIDE_CVT = 5;
        internal const int NV_TIMING_OVERRIDE_CVT_RB = 6;
        internal const int NV_TIMING_OVERRIDE_GTF = 7;
        internal const uint NV_TIMING_INPUT_VER = 0x00010020;
        internal const uint NV_CUSTOM_DISPLAY_VER = 0x00010090;
        internal const uint NV_EDID_DATA_VER = 0x00020038;
        internal const uint NV_DISPLAYCONFIG_FORCE_MODE_ENUMERATION = 0x00000008;
        internal const uint NV_DISPLAYCONFIG_DRIVER_RELOAD_ALLOWED = 0x00000004;

        public enum NvStatus : int
        { }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct NvShortString
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = NVAPI_SHORT_STRING_MAX)]
            public byte[] Value;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvDisplayDvcInfo
        {
            public uint version;
            public int currentLevel;
            public int minLevel;
            public int maxLevel;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvDisplayDvcInfoEx
        {
            public uint version;
            public int currentLevel;
            public int minLevel;
            public int maxLevel;
            public int defaultLevel;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvViewportF
        {
            public float x;
            public float y;
            public float w;
            public float h;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvTimingExt
        {
            public uint flag;
            public ushort rr;
            private ushort _pad0;
            public uint rrx1k;
            public uint aspect;
            public ushort rep;
            private ushort _pad1;
            public uint status;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
            public byte[] name;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvTiming
        {
            public ushort HVisible;
            public ushort HBorder;
            public ushort HFrontPorch;
            public ushort HSyncWidth;
            public ushort HTotal;
            public byte HSyncPol;
            public ushort VVisible;
            public ushort VBorder;
            public ushort VFrontPorch;
            public ushort VSyncWidth;
            public ushort VTotal;
            public byte VSyncPol;
            public ushort interlaced;
            public uint pclk;
            public NvTimingExt etc;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvTimingFlag
        {
            public uint interlacedAndReserved;

            public uint timingFormat;
            public uint scaling;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvTimingInput
        {
            public uint version;
            public uint width;
            public uint height;
            public float rr;
            public NvTimingFlag flag;
            public int type;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvCustomDisplay
        {
            public uint version;
            public uint width;
            public uint height;
            public uint depth;
            public int colorFormat;
            public NvViewportF srcPartition;
            public float xRatio;
            public float yRatio;
            public NvTiming timing;
            public uint hwModeSetOnly;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvEdidData
        {
            public uint version;
            public IntPtr pEDID;
            public uint sizeOfEDID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public uint[] reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvPosition
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvResolution
        {
            public uint width;
            public uint height;
            public uint colorDepth;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvDisplayConfigSourceModeInfo
        {
            public NvResolution resolution;
            public int colorFormat;
            public NvPosition position;
            public int spanningOrientation;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvDisplayConfigPathTargetInfo
        {
            public uint displayId;
            public IntPtr details;
            public uint targetId;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct NvDisplayConfigPathInfo
        {
            public uint version;
            public uint sourceId;
            public uint targetInfoCount;
            public IntPtr targetInfo;
            public IntPtr sourceModeInfo;
            public uint flags;
            public IntPtr pOSAdapterID;
        }

        private static uint NvDisplayConfigPathInfoVer2
        {
            get { return (uint)Marshal.SizeOf(typeof(NvDisplayConfigPathInfo)) | (2U << 16); }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiInitializeDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiGetErrorMessageDelegate(NvStatus status, ref NvShortString message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiGetInterfaceVersionStringDelegate(ref NvShortString version);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiEnumPhysicalGpusDelegate(
            [Out, MarshalAs(UnmanagedType.LPArray, SizeConst = NVAPI_MAX_PHYSICAL_GPUS)] IntPtr[] gpuHandles,
            out uint gpuCount);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiGpuGetFullNameDelegate(IntPtr gpuHandle, ref NvShortString name);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiEnumNvidiaDisplayHandleDelegate(uint displayEnumId, out IntPtr displayHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiGetAssociatedNvidiaDisplayHandleDelegate(
            [MarshalAs(UnmanagedType.LPStr)] string displayName, out IntPtr displayHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiGetDvcInfoDelegate(IntPtr displayHandle, uint outputId, ref NvDisplayDvcInfo info);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiSetDvcLevelDelegate(IntPtr displayHandle, uint outputId, int level);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiGetDvcInfoExDelegate(IntPtr displayHandle, uint outputId, ref NvDisplayDvcInfoEx info);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiSetDvcLevelExDelegate(IntPtr displayHandle, uint outputId, ref NvDisplayDvcInfoEx info);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiDispGetDisplayIdByDisplayNameDelegate(
            [MarshalAs(UnmanagedType.LPStr)] string displayName, out uint displayId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiDispGetEdidDataDelegate(uint displayId, ref NvEdidData edidData, ref uint edidFlags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiDispGetTimingDelegate(uint displayId, ref NvTimingInput timingInput, ref NvTiming timing);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiDispEnumCustomDisplayDelegate(uint displayId, uint index, ref NvCustomDisplay customDisplay);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiDispTryCustomDisplayDelegate(ref uint displayIds, uint count, ref NvCustomDisplay customDisplay);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiDispSaveCustomDisplayDelegate(ref uint displayIds, uint count, uint isThisOutputIdOnly, uint isThisMonitorIdOnly);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiDispRevertCustomDisplayTrialDelegate(ref uint displayIds, uint count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiDispDeleteCustomDisplayDelegate(ref uint displayIds, uint count, ref NvCustomDisplay customDisplay);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiDispGetDisplayConfigDelegate(ref uint pathInfoCount, IntPtr pathInfo);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NvStatus NvapiDispSetDisplayConfigDelegate(uint pathInfoCount, IntPtr pathInfo, uint flags);

        public static NvapiInitializeDelegate Initialize;
        public static NvapiGetErrorMessageDelegate GetErrorMessage;
        public static NvapiGetInterfaceVersionStringDelegate GetInterfaceVersionString;
        public static NvapiEnumPhysicalGpusDelegate EnumPhysicalGpus;
        public static NvapiGpuGetFullNameDelegate GpuGetFullName;
        public static NvapiEnumNvidiaDisplayHandleDelegate EnumNvidiaDisplayHandle;
        public static NvapiGetAssociatedNvidiaDisplayHandleDelegate GetAssociatedNvidiaDisplayHandle;
        public static NvapiGetDvcInfoDelegate GetDvcInfo;
        public static NvapiSetDvcLevelDelegate SetDvcLevel;
        public static NvapiGetDvcInfoExDelegate GetDvcInfoEx;
        public static NvapiSetDvcLevelExDelegate SetDvcLevelEx;
        public static NvapiDispGetDisplayIdByDisplayNameDelegate DispGetDisplayIdByDisplayName;
        public static NvapiDispGetEdidDataDelegate DispGetEdidData;
        public static NvapiDispGetTimingDelegate DispGetTiming;
        public static NvapiDispEnumCustomDisplayDelegate DispEnumCustomDisplay;
        public static NvapiDispTryCustomDisplayDelegate DispTryCustomDisplay;
        public static NvapiDispSaveCustomDisplayDelegate DispSaveCustomDisplay;
        public static NvapiDispRevertCustomDisplayTrialDelegate DispRevertCustomDisplayTrial;
        public static NvapiDispDeleteCustomDisplayDelegate DispDeleteCustomDisplay;
        public static NvapiDispGetDisplayConfigDelegate DispGetDisplayConfig;
        public static NvapiDispSetDisplayConfigDelegate DispSetDisplayConfig;

        private static bool _loaded;
        private static bool _initialized;
        private static IntPtr _nvapiModule = IntPtr.Zero;
        private static NvapiQueryInterfaceDelegate _queryInterface;

        public static string LastLoadError { get; private set; }
        public static string LoadedPath { get; private set; }
        public static string LastCustomDisplayStep { get; private set; }
        public static string LastCustomDisplayDiagnostics { get; private set; }

        public sealed class CustomDisplayTimingBase
        {
            public uint Width { get; private set; }
            public uint Height { get; private set; }
            public uint RefreshRate { get; private set; }
            public uint RefreshRateMilliHz { get; private set; }
            public int TimingOverrideType { get; private set; }
            public bool UseDriverDefaultTiming { get; private set; }
            public bool HardwareModeSetOnly { get; private set; }
            public string Label { get; private set; }

            public CustomDisplayTimingBase(uint width, uint height, uint refreshRate, string label)
                : this(width, height, refreshRate, NormalizeRefreshMilliHz(refreshRate, 0), NV_TIMING_OVERRIDE_AUTO, false, false, label)
            {
            }

            public CustomDisplayTimingBase(uint width, uint height, uint refreshRate, uint refreshRateMilliHz, string label)
                : this(width, height, refreshRate, refreshRateMilliHz, NV_TIMING_OVERRIDE_AUTO, false, false, label)
            {
            }

            public CustomDisplayTimingBase(uint width, uint height, uint refreshRate, uint refreshRateMilliHz, int timingOverrideType, string label)
                : this(width, height, refreshRate, refreshRateMilliHz, timingOverrideType, false, false, label)
            {
            }

            public CustomDisplayTimingBase(uint width, uint height, uint refreshRate, uint refreshRateMilliHz, int timingOverrideType, bool useDriverDefaultTiming, bool hardwareModeSetOnly, string label)
            {
                Width = width;
                Height = height;
                RefreshRate = refreshRate;
                RefreshRateMilliHz = NormalizeRefreshMilliHz(refreshRate, refreshRateMilliHz);
                TimingOverrideType = timingOverrideType;
                UseDriverDefaultTiming = useDriverDefaultTiming;
                HardwareModeSetOnly = hardwareModeSetOnly;
                Label = label ?? string.Empty;
            }

            public CustomDisplayTimingBase WithHardwareModeSetOnly(bool hardwareModeSetOnly, string suffix)
            {
                string nextLabel = Label ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(suffix))
                    nextLabel = string.IsNullOrWhiteSpace(nextLabel) ? suffix : nextLabel + suffix;
                return new CustomDisplayTimingBase(Width, Height, RefreshRate, RefreshRateMilliHz, TimingOverrideType, UseDriverDefaultTiming, hardwareModeSetOnly, nextLabel);
            }

            public static CustomDisplayTimingBase DriverPolicyTiming(uint width, uint height, uint refreshRate, uint refreshRateMilliHz, bool hardwareModeSetOnly, string label)
            {
                return new CustomDisplayTimingBase(width, height, refreshRate, refreshRateMilliHz, NV_TIMING_OVERRIDE_AUTO, true, hardwareModeSetOnly, label);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr NvapiQueryInterfaceDelegate(uint functionId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string fileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);

        public static bool Load()
        {
            if (_loaded) return true;

            var errors = new List<string>(4);
            foreach (string path in GetNvApiCandidates())
            {
                IntPtr module = LoadLibraryW(path);
                if (module == IntPtr.Zero)
                {
                    errors.Add(path + " -> Win32 " + Marshal.GetLastWin32Error());
                    continue;
                }

                IntPtr qi = GetProcAddress(module, "nvapi_QueryInterface");
                if (qi == IntPtr.Zero)
                    qi = GetProcAddress(module, "NvAPI_QueryInterface");

                if (qi == IntPtr.Zero)
                {
                    errors.Add(path + " -> NvAPI_QueryInterface not found");
                    FreeLibrary(module);
                    continue;
                }

                _nvapiModule = module;
                _queryInterface = (NvapiQueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(
                    qi, typeof(NvapiQueryInterfaceDelegate));

                Initialize = Resolve<NvapiInitializeDelegate>(ID_INITIALIZE);
                GetErrorMessage = Resolve<NvapiGetErrorMessageDelegate>(ID_GET_ERROR_MESSAGE);
                GetInterfaceVersionString = Resolve<NvapiGetInterfaceVersionStringDelegate>(ID_GET_INTERFACE_VERSION_STRING);
                EnumPhysicalGpus = Resolve<NvapiEnumPhysicalGpusDelegate>(ID_ENUM_PHYSICAL_GPUS);
                GpuGetFullName = Resolve<NvapiGpuGetFullNameDelegate>(ID_GPU_GET_FULL_NAME);
                EnumNvidiaDisplayHandle = Resolve<NvapiEnumNvidiaDisplayHandleDelegate>(ID_ENUM_NVIDIA_DISPLAY_HANDLE);
                GetAssociatedNvidiaDisplayHandle = Resolve<NvapiGetAssociatedNvidiaDisplayHandleDelegate>(ID_GET_ASSOCIATED_NVIDIA_DISPLAY_HANDLE);
                GetDvcInfo = Resolve<NvapiGetDvcInfoDelegate>(ID_GET_DVC_INFO);
                SetDvcLevel = Resolve<NvapiSetDvcLevelDelegate>(ID_SET_DVC_LEVEL);
                GetDvcInfoEx = Resolve<NvapiGetDvcInfoExDelegate>(ID_GET_DVC_INFO_EX);
                SetDvcLevelEx = Resolve<NvapiSetDvcLevelExDelegate>(ID_SET_DVC_LEVEL_EX);
                DispGetDisplayIdByDisplayName = Resolve<NvapiDispGetDisplayIdByDisplayNameDelegate>(ID_DISP_GET_DISPLAY_ID_BY_DISPLAY_NAME);
                DispGetEdidData = Resolve<NvapiDispGetEdidDataDelegate>(ID_DISP_GET_EDID_DATA);
                DispGetTiming = Resolve<NvapiDispGetTimingDelegate>(ID_DISP_GET_TIMING);
                DispEnumCustomDisplay = Resolve<NvapiDispEnumCustomDisplayDelegate>(ID_DISP_ENUM_CUSTOM_DISPLAY);
                DispTryCustomDisplay = Resolve<NvapiDispTryCustomDisplayDelegate>(ID_DISP_TRY_CUSTOM_DISPLAY);
                DispSaveCustomDisplay = Resolve<NvapiDispSaveCustomDisplayDelegate>(ID_DISP_SAVE_CUSTOM_DISPLAY);
                DispRevertCustomDisplayTrial = Resolve<NvapiDispRevertCustomDisplayTrialDelegate>(ID_DISP_REVERT_CUSTOM_DISPLAY_TRIAL);
                DispDeleteCustomDisplay = Resolve<NvapiDispDeleteCustomDisplayDelegate>(ID_DISP_DELETE_CUSTOM_DISPLAY);
                DispGetDisplayConfig = Resolve<NvapiDispGetDisplayConfigDelegate>(ID_DISP_GET_DISPLAY_CONFIG);
                DispSetDisplayConfig = Resolve<NvapiDispSetDisplayConfigDelegate>(ID_DISP_SET_DISPLAY_CONFIG);

                if (Initialize == null)
                {
                    errors.Add(path + " -> NvAPI_Initialize entry point was not resolved");
                    ClearResolvedFunctions();
                    FreeLibrary(module);
                    _nvapiModule = IntPtr.Zero;
                    _queryInterface = null;
                    continue;
                }

                if (!ValidateStructLayout(out var layoutError))
                {
                    errors.Add(path + " -> " + layoutError);
                    ClearResolvedFunctions();
                    FreeLibrary(module);
                    _nvapiModule = IntPtr.Zero;
                    _queryInterface = null;
                    continue;
                }

                _loaded = true;
                LoadedPath = path;
                LastLoadError = null;
                return true;
            }

            LastLoadError = "NVAPI DLL not loaded. Tried: " + string.Join("; ", errors);
            return false;
        }

        public static int InitializeApi()
        {
            if (!_loaded || Initialize == null) return NVAPI_LIBRARY_NOT_FOUND;
            int status = (int)Initialize();
            _initialized = status == NVAPI_OK;
            return status;
        }

        public static bool IsInitialized
        {
            get { return _initialized; }
        }

        public static bool HasDvcSupport
        {
            get { return ((GetDvcInfoEx != null && SetDvcLevelEx != null) || (GetDvcInfo != null && SetDvcLevel != null)) && EnumNvidiaDisplayHandle != null; }
        }

        public static bool HasCustomDisplaySupport
        {
            get { return DispGetDisplayIdByDisplayName != null && DispGetTiming != null && DispTryCustomDisplay != null && DispSaveCustomDisplay != null; }
        }

        public static bool HasCustomDisplayEnumeration
        {
            get { return DispGetDisplayIdByDisplayName != null && DispEnumCustomDisplay != null; }
        }

        public static bool HasEdidDataSupport
        {
            get { return DispGetDisplayIdByDisplayName != null && DispGetEdidData != null; }
        }

        public static bool HasCustomDisplayDeletion
        {
            get { return DispGetDisplayIdByDisplayName != null && DispDeleteCustomDisplay != null; }
        }

        public static bool HasDisplayConfigSupport
        {
            get { return DispGetDisplayConfig != null && DispSetDisplayConfig != null; }
        }

        public static bool TryGetDisplayHandle(string deviceName, out IntPtr displayHandle)
        {
            displayHandle = IntPtr.Zero;
            if (!IsInitialized || GetAssociatedNvidiaDisplayHandle == null) return false;

            string[] names = BuildDisplayNameCandidates(deviceName);
            for (int i = 0; i < names.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(names[i])) continue;
                int status = (int)GetAssociatedNvidiaDisplayHandle(names[i], out displayHandle);
                if (status == NVAPI_OK && displayHandle != IntPtr.Zero) return true;
            }

            if (!string.IsNullOrWhiteSpace(deviceName))
                return false;

            return TryGetFirstDisplayHandle(out displayHandle);
        }

        public static bool TryGetFirstDisplayHandle(out IntPtr displayHandle)
        {
            displayHandle = IntPtr.Zero;
            if (!IsInitialized || EnumNvidiaDisplayHandle == null) return false;

            int status = (int)EnumNvidiaDisplayHandle(0, out displayHandle);
            return status == NVAPI_OK && displayHandle != IntPtr.Zero;
        }

        public static bool TryGetDvcInfo(IntPtr displayHandle, out NvDisplayDvcInfo info, out int status)
        {
            info = new NvDisplayDvcInfo { version = NV_DISPLAY_DVC_INFO_VER };
            status = NVAPI_NO_IMPLEMENTATION;
            if (!IsInitialized || displayHandle == IntPtr.Zero) return false;

            if (GetDvcInfoEx != null)
            {
                var ex = new NvDisplayDvcInfoEx { version = NV_DISPLAY_DVC_INFO_EX_VER };
                status = (int)GetDvcInfoEx(displayHandle, 0, ref ex);
                if (status == NVAPI_OK)
                {
                    info.currentLevel = ex.currentLevel;
                    info.minLevel = ex.minLevel;
                    info.maxLevel = ex.maxLevel;
                    return true;
                }
            }

            if (GetDvcInfo == null) return false;

            status = (int)GetDvcInfo(displayHandle, 0, ref info);
            return status == NVAPI_OK;
        }

        public static bool TryGetDvcInfoEx(IntPtr displayHandle, out NvDisplayDvcInfoEx info, out int status)
        {
            info = new NvDisplayDvcInfoEx { version = NV_DISPLAY_DVC_INFO_EX_VER };
            status = NVAPI_NO_IMPLEMENTATION;
            if (!IsInitialized || GetDvcInfoEx == null || displayHandle == IntPtr.Zero) return false;

            status = (int)GetDvcInfoEx(displayHandle, 0, ref info);
            return status == NVAPI_OK;
        }

        public static bool TrySetDvcLevel(IntPtr displayHandle, int level, out int status)
        {
            status = NVAPI_NO_IMPLEMENTATION;
            if (!IsInitialized || displayHandle == IntPtr.Zero) return false;

            if (level < 0) level = 0;
            if (level > 100) level = 100;

            if (SetDvcLevelEx != null)
            {
                var info = new NvDisplayDvcInfoEx
                {
                    version = NV_DISPLAY_DVC_INFO_EX_VER,
                    currentLevel = level
                };
                status = (int)SetDvcLevelEx(displayHandle, 0, ref info);
                if (status == NVAPI_OK) return true;
            }

            if (SetDvcLevel == null) return false;

            status = (int)SetDvcLevel(displayHandle, 0, level);
            return status == NVAPI_OK;
        }

        public static bool TryForceNvidiaModeEnumeration(out int status, out string detail)
        {
            status = NVAPI_NO_IMPLEMENTATION;
            detail = null;

            if (!IsInitialized || !HasDisplayConfigSupport)
            {
                detail = "NvAPI_DISP_GetDisplayConfig/NvAPI_DISP_SetDisplayConfig недоступны в загруженном драйвере.";
                return false;
            }

            IntPtr pathPtr = IntPtr.Zero;
            NvDisplayConfigPathInfo[] managedPaths = null;
            int pathSize = Marshal.SizeOf(typeof(NvDisplayConfigPathInfo));
            int targetSize = Marshal.SizeOf(typeof(NvDisplayConfigPathTargetInfo));
            int sourceSize = Marshal.SizeOf(typeof(NvDisplayConfigSourceModeInfo));

            try
            {
                uint pathCount = 0;
                status = (int)DispGetDisplayConfig(ref pathCount, IntPtr.Zero);
                if (status != NVAPI_OK || pathCount == 0)
                {
                    detail = "NvAPI_DISP_GetDisplayConfig pass #1: " + StatusToString(status);
                    return false;
                }

                if (pathCount > int.MaxValue)
                {
                    detail = "NvAPI_DISP_GetDisplayConfig returned too many display paths: " + pathCount;
                    status = NVAPI_INVALID_ARGUMENT;
                    return false;
                }

                int pathCountInt = (int)pathCount;
                pathPtr = Marshal.AllocHGlobal(checked(pathCountInt * pathSize));
                ZeroMemory(pathPtr, checked(pathCountInt * pathSize));

                for (int i = 0; i < pathCountInt; i++)
                {
                    var path = new NvDisplayConfigPathInfo { version = NvDisplayConfigPathInfoVer2 };
                    Marshal.StructureToPtr(path, Add(pathPtr, i * pathSize), false);
                }

                uint secondPathCount = pathCount;
                status = (int)DispGetDisplayConfig(ref secondPathCount, pathPtr);
                if (status != NVAPI_OK)
                {
                    detail = "NvAPI_DISP_GetDisplayConfig pass #2: " + StatusToString(status);
                    return false;
                }

                if (secondPathCount > pathCount || secondPathCount > int.MaxValue)
                {
                    detail = "NvAPI_DISP_GetDisplayConfig returned unexpected path count on pass #2: " + secondPathCount;
                    status = NVAPI_INVALID_ARGUMENT;
                    return false;
                }

                pathCount = secondPathCount;
                pathCountInt = (int)pathCount;
                managedPaths = new NvDisplayConfigPathInfo[pathCountInt];
                for (int i = 0; i < pathCountInt; i++)
                {
                    managedPaths[i] = (NvDisplayConfigPathInfo)Marshal.PtrToStructure(Add(pathPtr, i * pathSize), typeof(NvDisplayConfigPathInfo));
                    managedPaths[i].version = NvDisplayConfigPathInfoVer2;
                    managedPaths[i].sourceModeInfo = Marshal.AllocHGlobal(sourceSize);
                    ZeroMemory(managedPaths[i].sourceModeInfo, sourceSize);

                    if (managedPaths[i].targetInfoCount > 0)
                    {
                        managedPaths[i].targetInfo = Marshal.AllocHGlobal(checked((int)managedPaths[i].targetInfoCount * targetSize));
                        ZeroMemory(managedPaths[i].targetInfo, checked((int)managedPaths[i].targetInfoCount * targetSize));
                    }

                    Marshal.StructureToPtr(managedPaths[i], Add(pathPtr, i * pathSize), false);
                }

                uint thirdPathCount = pathCount;
                status = (int)DispGetDisplayConfig(ref thirdPathCount, pathPtr);
                if (status != NVAPI_OK)
                {
                    detail = "NvAPI_DISP_GetDisplayConfig pass #3: " + StatusToString(status);
                    return false;
                }

                if (thirdPathCount > pathCount)
                {
                    detail = "NvAPI_DISP_GetDisplayConfig topology changed during pass #3; required path count " + thirdPathCount + " exceeds allocated count " + pathCount + ".";
                    status = NVAPI_INVALID_ARGUMENT;
                    return false;
                }

                uint flags = NV_DISPLAYCONFIG_FORCE_MODE_ENUMERATION | NV_DISPLAYCONFIG_DRIVER_RELOAD_ALLOWED;
                status = (int)DispSetDisplayConfig(thirdPathCount, pathPtr, flags);
                if (status == NVAPI_OK)
                {
                    detail = "NvAPI_DISP_SetDisplayConfig/NV_DISPLAYCONFIG_FORCE_MODE_ENUMERATION: OS mode list refresh OK.";
                    return true;
                }

                int firstStatus = status;
                status = (int)DispSetDisplayConfig(thirdPathCount, pathPtr, NV_DISPLAYCONFIG_FORCE_MODE_ENUMERATION);
                if (status == NVAPI_OK)
                {
                    detail = "NvAPI_DISP_SetDisplayConfig/NV_DISPLAYCONFIG_FORCE_MODE_ENUMERATION: OS mode list refresh OK after retry without DRIVER_RELOAD_ALLOWED.";
                    return true;
                }

                detail = "NvAPI_DISP_SetDisplayConfig/NV_DISPLAYCONFIG_FORCE_MODE_ENUMERATION: " + StatusToString(firstStatus) + "; retry: " + StatusToString(status);
                return false;
            }
            catch (Exception ex)
            {
                status = NVAPI_ERROR;
                detail = "NvAPI display-config refresh exception: " + ex.Message;
                return false;
            }
            finally
            {
                if (managedPaths != null)
                {
                    for (int i = 0; i < managedPaths.Length; i++)
                    {
                        if (managedPaths[i].targetInfo != IntPtr.Zero)
                            Marshal.FreeHGlobal(managedPaths[i].targetInfo);
                        if (managedPaths[i].sourceModeInfo != IntPtr.Zero)
                            Marshal.FreeHGlobal(managedPaths[i].sourceModeInfo);
                    }
                }

                if (pathPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(pathPtr);
            }
        }

        public static bool TryRegisterCustomDisplay(string deviceName, uint width, uint height, uint refresh, uint depth, out int status)
        {
            return TryRegisterCustomDisplay(deviceName, width, height, refresh, 0, depth, null, out status);
        }

        public static bool TryRegisterCustomDisplay(string deviceName, uint width, uint height, uint refresh, uint depth, IEnumerable<CustomDisplayTimingBase> timingBases, out int status)
        {
            return TryRegisterCustomDisplay(deviceName, width, height, refresh, 0, depth, timingBases, out status);
        }

        public static bool TryRegisterCustomDisplay(string deviceName, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth, IEnumerable<CustomDisplayTimingBase> timingBases, out int status)
        {
            status = NVAPI_NO_IMPLEMENTATION;
            LastCustomDisplayStep = null;
            LastCustomDisplayDiagnostics = null;

            if (!IsInitialized || !HasCustomDisplaySupport)
            {
                LastCustomDisplayStep = "NVAPI custom-display entry points";
                LastCustomDisplayDiagnostics = "NvAPI custom-display functions are not available in the loaded NVIDIA driver.";
                return false;
            }

            if (!TryGetDisplayId(deviceName, out uint displayId))
            {
                LastCustomDisplayStep = "NvAPI_DISP_GetDisplayIdByDisplayName";
                status = NVAPI_INVALID_ARGUMENT;
                LastCustomDisplayDiagnostics = "Display name was not resolved to an active NVIDIA displayId: " + (deviceName ?? "<null>");
                return false;
            }

            uint sourceRefreshMilliHz = NormalizeRefreshMilliHz(refresh, refreshMilliHz);
            List<CustomDisplayTimingBase> attempts = BuildTimingAttemptList(width, height, refresh, sourceRefreshMilliHz, timingBases);
            var diagnostics = new StringBuilder();
            int lastStatus = NVAPI_ERROR;

            for (int i = 0; i < attempts.Count; i++)
            {
                CustomDisplayTimingBase attempt = attempts[i];
                if (!TryBuildCustomDisplay(displayId, width, height, refresh, sourceRefreshMilliHz, depth, attempt.Width, attempt.Height, attempt.RefreshRate, attempt.RefreshRateMilliHz, attempt.TimingOverrideType, attempt.UseDriverDefaultTiming, attempt.HardwareModeSetOnly, out NvCustomDisplay custom, out status))
                {
                    lastStatus = status;
                    AppendCustomDisplayAttemptDiagnostic(diagnostics, attempt, LastCustomDisplayStep, status);
                    continue;
                }

                LastCustomDisplayStep = "NvAPI_DISP_TryCustomDisplay";
                status = (int)DispTryCustomDisplay(ref displayId, 1, ref custom);
                if (status != NVAPI_OK)
                {
                    lastStatus = status;
                    AppendCustomDisplayAttemptDiagnostic(diagnostics, attempt, LastCustomDisplayStep, status);
                    continue;
                }

                NativeWait.Sleep(500);

                LastCustomDisplayStep = "NvAPI_DISP_SaveCustomDisplay";

                status = (int)DispSaveCustomDisplay(ref displayId, 1, 1, 1);
                NativeWait.Sleep(300);
                TryRevertCustomDisplay(displayId);

                if (status == NVAPI_OK)
                {
                    LastCustomDisplayStep = null;
                    LastCustomDisplayDiagnostics = diagnostics.Length == 0
                        ? "NVAPI custom-display save succeeded with timing base " + FormatTimingBase(attempt) + "."
                        : diagnostics.ToString() + " Success with timing base " + FormatTimingBase(attempt) + ".";
                    return true;
                }

                lastStatus = status;
                AppendCustomDisplayAttemptDiagnostic(diagnostics, attempt, LastCustomDisplayStep, status);
            }

            status = lastStatus;
            LastCustomDisplayDiagnostics = diagnostics.Length == 0
                ? "No NVAPI custom-display timing attempts were produced."
                : diagnostics.ToString();
            if (string.IsNullOrWhiteSpace(LastCustomDisplayStep))
                LastCustomDisplayStep = "NvAPI_DISP_TryCustomDisplay";
            return false;
        }

        public static bool TryBeginCustomDisplayTrial(string deviceName, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth, IEnumerable<CustomDisplayTimingBase> timingBases, out int status)
        {
            return TryBeginCustomDisplayTrial(deviceName, width, height, refresh, refreshMilliHz, depth, timingBases, true, true, out status);
        }

        public static bool TryBeginCustomDisplayTrial(string deviceName, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth, IEnumerable<CustomDisplayTimingBase> timingBases, bool includeRequestedSourceTiming, bool includeDriverPolicyFallback, out int status)
        {
            status = NVAPI_NO_IMPLEMENTATION;
            LastCustomDisplayStep = null;
            LastCustomDisplayDiagnostics = null;

            if (!IsInitialized || !HasCustomDisplaySupport)
            {
                LastCustomDisplayStep = "NVAPI custom-display entry points";
                LastCustomDisplayDiagnostics = "NvAPI custom-display functions are not available in the loaded NVIDIA driver.";
                return false;
            }

            if (!TryGetDisplayId(deviceName, out uint displayId))
            {
                LastCustomDisplayStep = "NvAPI_DISP_GetDisplayIdByDisplayName";
                status = NVAPI_INVALID_ARGUMENT;
                LastCustomDisplayDiagnostics = "Display name was not resolved to an active NVIDIA displayId: " + (deviceName ?? "<null>");
                return false;
            }

            uint sourceRefreshMilliHz = NormalizeRefreshMilliHz(refresh, refreshMilliHz);
            List<CustomDisplayTimingBase> attempts = BuildTimingAttemptList(width, height, refresh, sourceRefreshMilliHz, timingBases, includeRequestedSourceTiming, includeDriverPolicyFallback);
            var diagnostics = new StringBuilder();
            int lastStatus = NVAPI_ERROR;

            for (int i = 0; i < attempts.Count; i++)
            {
                CustomDisplayTimingBase attempt = attempts[i];
                if (!TryBuildCustomDisplay(displayId, width, height, refresh, sourceRefreshMilliHz, depth, attempt.Width, attempt.Height, attempt.RefreshRate, attempt.RefreshRateMilliHz, attempt.TimingOverrideType, attempt.UseDriverDefaultTiming, attempt.HardwareModeSetOnly, out NvCustomDisplay custom, out status))
                {
                    lastStatus = status;
                    AppendCustomDisplayAttemptDiagnostic(diagnostics, attempt, LastCustomDisplayStep, status);
                    continue;
                }

                LastCustomDisplayStep = "NvAPI_DISP_TryCustomDisplay";
                status = (int)DispTryCustomDisplay(ref displayId, 1, ref custom);
                if (status == NVAPI_OK)
                {
                    LastCustomDisplayStep = null;
                    LastCustomDisplayDiagnostics = diagnostics.Length == 0
                        ? "NVAPI custom-display trial succeeded with timing base " + FormatTimingBase(attempt) + "."
                        : diagnostics.ToString() + " Trial succeeded with timing base " + FormatTimingBase(attempt) + ".";
                    return true;
                }

                lastStatus = status;
                AppendCustomDisplayAttemptDiagnostic(diagnostics, attempt, LastCustomDisplayStep, status);
            }

            status = lastStatus;
            LastCustomDisplayDiagnostics = diagnostics.Length == 0
                ? "No NVAPI custom-display timing attempts were produced."
                : diagnostics.ToString();
            if (string.IsNullOrWhiteSpace(LastCustomDisplayStep))
                LastCustomDisplayStep = "NvAPI_DISP_TryCustomDisplay";
            return false;
        }

        public static bool TrySaveCustomDisplayTrial(string deviceName, out int status)
        {
            status = NVAPI_NO_IMPLEMENTATION;
            LastCustomDisplayStep = null;
            LastCustomDisplayDiagnostics = null;

            if (!IsInitialized || DispSaveCustomDisplay == null)
            {
                LastCustomDisplayStep = "NvAPI_DISP_SaveCustomDisplay";
                LastCustomDisplayDiagnostics = "NvAPI_DISP_SaveCustomDisplay is not available in the loaded NVIDIA driver.";
                return false;
            }

            if (!TryGetDisplayId(deviceName, out uint displayId))
            {
                LastCustomDisplayStep = "NvAPI_DISP_GetDisplayIdByDisplayName";
                status = NVAPI_INVALID_ARGUMENT;
                LastCustomDisplayDiagnostics = "Display name was not resolved to an active NVIDIA displayId: " + (deviceName ?? "<null>");
                return false;
            }

            LastCustomDisplayStep = "NvAPI_DISP_SaveCustomDisplay";

            status = (int)DispSaveCustomDisplay(ref displayId, 1, 1, 1);
            if (status == NVAPI_OK)
            {
                LastCustomDisplayStep = null;
                LastCustomDisplayDiagnostics = "NVAPI custom-display trial was saved for the selected output and monitor EDID.";
                return true;
            }

            LastCustomDisplayDiagnostics = "NvAPI_DISP_SaveCustomDisplay failed: " + StatusToString(status) + ".";
            return false;
        }

        public static bool TryRevertCustomDisplayTrial(string deviceName, out int status)
        {
            status = NVAPI_NO_IMPLEMENTATION;
            LastCustomDisplayStep = null;
            LastCustomDisplayDiagnostics = null;

            if (!IsInitialized || DispRevertCustomDisplayTrial == null)
            {
                LastCustomDisplayStep = "NvAPI_DISP_RevertCustomDisplayTrial";
                LastCustomDisplayDiagnostics = "NvAPI_DISP_RevertCustomDisplayTrial is not available in the loaded NVIDIA driver.";
                return false;
            }

            if (!TryGetDisplayId(deviceName, out uint displayId))
            {
                LastCustomDisplayStep = "NvAPI_DISP_GetDisplayIdByDisplayName";
                status = NVAPI_INVALID_ARGUMENT;
                LastCustomDisplayDiagnostics = "Display name was not resolved to an active NVIDIA displayId: " + (deviceName ?? "<null>");
                return false;
            }

            LastCustomDisplayStep = "NvAPI_DISP_RevertCustomDisplayTrial";
            status = (int)DispRevertCustomDisplayTrial(ref displayId, 1);
            if (status == NVAPI_OK)
            {
                LastCustomDisplayStep = null;
                LastCustomDisplayDiagnostics = "NVAPI custom-display trial was reverted.";
                return true;
            }

            LastCustomDisplayDiagnostics = "NvAPI_DISP_RevertCustomDisplayTrial failed: " + StatusToString(status) + ".";
            return false;
        }

        public static bool TryDeleteCustomDisplay(string deviceName, uint width, uint height, uint refresh, uint depth, out int status)
        {
            return TryDeleteCustomDisplay(deviceName, width, height, refresh, 0, depth, out status);
        }

        public static bool TryDeleteCustomDisplay(string deviceName, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth, out int status)
        {
            status = NVAPI_NO_IMPLEMENTATION;
            LastCustomDisplayStep = null;
            LastCustomDisplayDiagnostics = null;
            if (!IsInitialized || !HasCustomDisplayDeletion)
            {
                LastCustomDisplayStep = "NvAPI_DISP_DeleteCustomDisplay";
                return false;
            }

            if (!TryGetDisplayId(deviceName, out uint displayId))
            {
                LastCustomDisplayStep = "NvAPI_DISP_GetDisplayIdByDisplayName";
                status = NVAPI_INVALID_ARGUMENT;
                return false;
            }

            NvCustomDisplay custom;
            if (DispEnumCustomDisplay != null)
            {
                if (!TryFindCustomDisplay(displayId, width, height, refresh, refreshMilliHz, depth, out custom, out status))
                {
                    LastCustomDisplayStep = "NvAPI_DISP_EnumCustomDisplay";
                    if (status == NVAPI_OK)
                        status = NVAPI_END_ENUMERATION;
                    return false;
                }
            }
            else
            {
                uint normalizedMilliHz = NormalizeRefreshMilliHz(refresh, refreshMilliHz);
                if (!TryBuildCustomDisplay(displayId, width, height, refresh, normalizedMilliHz, depth, width, height, refresh, normalizedMilliHz, NV_TIMING_OVERRIDE_AUTO, false, false, out custom, out status))
                    return false;
            }

            LastCustomDisplayStep = "NvAPI_DISP_DeleteCustomDisplay";
            status = (int)DispDeleteCustomDisplay(ref displayId, 1, ref custom);
            if (status == NVAPI_OK)
            {
                LastCustomDisplayStep = null;
                return true;
            }

            return false;
        }

        private static bool TryBuildCustomDisplay(uint displayId, uint width, uint height, uint refresh, uint depth, out NvCustomDisplay custom, out int status)
        {
            uint refreshMilliHz = NormalizeRefreshMilliHz(refresh, 0);
            return TryBuildCustomDisplay(displayId, width, height, refresh, refreshMilliHz, depth, width, height, refresh, refreshMilliHz, NV_TIMING_OVERRIDE_AUTO, false, false, out custom, out status);
        }

        private static bool TryBuildCustomDisplay(uint displayId, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth, uint timingWidth, uint timingHeight, uint timingRefresh, uint timingRefreshMilliHz, int timingOverrideType, bool useDriverDefaultTiming, bool hardwareModeSetOnly, out NvCustomDisplay custom, out int status)
        {
            custom = CreateEmptyCustomDisplay();
            status = NVAPI_OK;

            NvTiming timing = CreateEmptyTiming();
            if (!useDriverDefaultTiming)
            {
                if (!TryCalculateCustomTiming(displayId, timingWidth, timingHeight, timingRefresh, timingRefreshMilliHz, timingOverrideType, out timing, out status))
                    return false;

                EnsureTimingRefreshFields(ref timing, refresh, refreshMilliHz);
            }

            custom = new NvCustomDisplay
            {
                version = NV_CUSTOM_DISPLAY_VER,
                width = width,
                height = height,
                depth = depth == 0 ? 32U : depth,
                colorFormat = NV_FORMAT_A8R8G8B8,
                srcPartition = new NvViewportF { x = 0f, y = 0f, w = 1f, h = 1f },
                xRatio = 1f,
                yRatio = 1f,
                timing = timing,
                hwModeSetOnly = hardwareModeSetOnly ? 1U : 0U
            };
            return true;
        }

        private static List<CustomDisplayTimingBase> BuildTimingAttemptList(uint width, uint height, uint refresh, uint refreshMilliHz, IEnumerable<CustomDisplayTimingBase> timingBases)
        {
            return BuildTimingAttemptList(width, height, refresh, refreshMilliHz, timingBases, true, true);
        }

        private static List<CustomDisplayTimingBase> BuildTimingAttemptList(uint width, uint height, uint refresh, uint refreshMilliHz, IEnumerable<CustomDisplayTimingBase> timingBases, bool includeRequestedSourceTiming, bool includeDriverPolicyFallback)
        {
            var result = new List<CustomDisplayTimingBase>(20);
            uint normalizedMilliHz = NormalizeRefreshMilliHz(refresh, refreshMilliHz);

            if (includeRequestedSourceTiming)
                AddExactTimingStandards(result, width, height, refresh, normalizedMilliHz, "requested visible source mode");

            if (timingBases != null)
            {
                foreach (CustomDisplayTimingBase timingBase in timingBases)
                    AddTimingAttemptWithHardwareVariant(result, timingBase);
            }

            if (includeDriverPolicyFallback)
            {
                AddTimingAttemptWithHardwareVariant(
                    result,
                    CustomDisplayTimingBase.DriverPolicyTiming(width, height, refresh, normalizedMilliHz, false, "NVIDIA driver-policy timing fallback; no explicit NV_TIMING transport"));
            }

            return result;
        }

        private static void AddExactTimingStandards(List<CustomDisplayTimingBase> result, uint width, uint height, uint refresh, uint refreshMilliHz, string label)
        {
            AddTimingAttemptWithHardwareVariant(result, new CustomDisplayTimingBase(width, height, refresh, refreshMilliHz, NV_TIMING_OVERRIDE_AUTO, label + " / Auto"));
        }

        private static void AddTimingAttemptWithHardwareVariant(List<CustomDisplayTimingBase> result, CustomDisplayTimingBase timingBase)
        {
            if (timingBase == null) return;
            AddTimingAttempt(result, timingBase);
            if (!timingBase.HardwareModeSetOnly)
                AddTimingAttempt(result, timingBase.WithHardwareModeSetOnly(true, " / hwModeSetOnly trial"));
        }

        private static void AddTimingAttempt(List<CustomDisplayTimingBase> result, CustomDisplayTimingBase timingBase)
        {
            if (result == null || timingBase == null) return;
            if (timingBase.Width == 0 || timingBase.Height == 0 || timingBase.RefreshRate == 0) return;

            for (int i = 0; i < result.Count; i++)
            {
                CustomDisplayTimingBase existing = result[i];
                if (existing.Width == timingBase.Width &&
                    existing.Height == timingBase.Height &&
                    Math.Abs((long)existing.RefreshRate - (long)timingBase.RefreshRate) <= 1 &&
                    existing.RefreshRateMilliHz == timingBase.RefreshRateMilliHz &&
                    existing.TimingOverrideType == timingBase.TimingOverrideType &&
                    existing.UseDriverDefaultTiming == timingBase.UseDriverDefaultTiming &&
                    existing.HardwareModeSetOnly == timingBase.HardwareModeSetOnly)
                {
                    return;
                }
            }

            result.Add(timingBase);
        }

        private static void AppendCustomDisplayAttemptDiagnostic(StringBuilder diagnostics, CustomDisplayTimingBase timingBase, string step, int status)
        {
            if (diagnostics == null) return;
            if (diagnostics.Length > 0) diagnostics.Append(" ");
            diagnostics.Append("[Timing ")
                       .Append(FormatTimingBase(timingBase))
                       .Append("] ")
                       .Append(string.IsNullOrWhiteSpace(step) ? "NVAPI" : step)
                       .Append(": ")
                       .Append(StatusToString(status))
                       .Append(".");
        }

        private static string FormatTimingBase(CustomDisplayTimingBase timingBase)
        {
            if (timingBase == null) return "<null>";
            string label = string.IsNullOrWhiteSpace(timingBase.Label) ? string.Empty : " / " + timingBase.Label;
            string timingText = timingBase.UseDriverDefaultTiming ? "DRIVER_POLICY" : FormatTimingOverrideType(timingBase.TimingOverrideType);
            return timingBase.Width + "x" + timingBase.Height + "@" + FormatMilliHz(timingBase.RefreshRateMilliHz) +
                " / timing=" + timingText +
                " / hwModeSetOnly=" + (timingBase.HardwareModeSetOnly ? "1" : "0") + label;
        }

        private static string FormatTimingOverrideType(int type)
        {
            switch (type)
            {
                case NV_TIMING_OVERRIDE_CURRENT: return "CURRENT";
                case NV_TIMING_OVERRIDE_AUTO: return "AUTO";
                case NV_TIMING_OVERRIDE_EDID: return "EDID";
                case NV_TIMING_OVERRIDE_DMT: return "DMT";
                case NV_TIMING_OVERRIDE_DMT_RB: return "DMT_RB";
                case NV_TIMING_OVERRIDE_CVT: return "CVT";
                case NV_TIMING_OVERRIDE_CVT_RB: return "CVT_RB";
                case NV_TIMING_OVERRIDE_GTF: return "GTF";
                default: return type.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static float GetTimingInputRefreshRate(uint refresh, uint refreshMilliHz)
        {
            uint targetMilliHz = NormalizeRefreshMilliHz(refresh, refreshMilliHz);
            if (targetMilliHz > 0)
                return (float)(targetMilliHz / 1000.0);
            return refresh;
        }

        private static uint NormalizeRefreshMilliHz(uint refresh, uint refreshMilliHz)
        {
            return RefreshRateMath.NormalizeDisplayMilliHz(refresh, refreshMilliHz);
        }

        private static string FormatMilliHz(uint milliHz)
        {
            if (milliHz == 0)
                return "unknown";
            return (milliHz / 1000U) + "." + (milliHz % 1000U).ToString("D3") + " Hz";
        }

        private static bool TryCalculateCustomTiming(uint displayId, uint width, uint height, uint refresh, uint refreshMilliHz, int timingOverrideType, out NvTiming timing, out int status)
        {
            timing = CreateEmptyTiming();
            status = NVAPI_NO_IMPLEMENTATION;
            if (DispGetTiming == null)
            {
                LastCustomDisplayStep = "NvAPI_DISP_GetTiming";
                return false;
            }

            var timingInput = new NvTimingInput
            {
                version = NV_TIMING_INPUT_VER,
                width = width,
                height = height,
                rr = GetTimingInputRefreshRate(refresh, refreshMilliHz),
                flag = new NvTimingFlag(),
                type = timingOverrideType
            };

            var candidate = CreateEmptyTiming();
            LastCustomDisplayStep = "NvAPI_DISP_GetTiming";
            status = (int)DispGetTiming(displayId, ref timingInput, ref candidate);
            if (status != NVAPI_OK)
                return false;

            EnsureTimingRefreshFields(ref candidate, refresh, refreshMilliHz);
            timing = candidate;
            LastCustomDisplayStep = null;
            return true;
        }

        private static void EnsureTimingRefreshFields(ref NvTiming timing, uint refresh, uint refreshMilliHz)
        {
            if (timing.etc.name == null)
                timing.etc.name = new byte[40];

            uint targetMilliHz = NormalizeRefreshMilliHz(refresh, refreshMilliHz);

            if (refresh <= ushort.MaxValue)
                timing.etc.rr = (ushort)refresh;
            if (targetMilliHz > 0)
                timing.etc.rrx1k = targetMilliHz;
        }

        private static bool TryFindCustomDisplay(uint displayId, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth, out NvCustomDisplay custom, out int status)
        {
            custom = CreateEmptyCustomDisplay();
            status = NVAPI_NO_IMPLEMENTATION;
            if (DispEnumCustomDisplay == null)
                return false;

            for (uint i = 0; i < 128; i++)
            {
                var candidate = CreateEmptyCustomDisplay();
                status = (int)DispEnumCustomDisplay(displayId, i, ref candidate);
                if (status == NVAPI_END_ENUMERATION)
                {
                    status = NVAPI_OK;
                    return false;
                }

                if (status != NVAPI_OK)
                    return false;

                if (CustomDisplayMatches(candidate, width, height, refresh, refreshMilliHz, depth))
                {
                    custom = candidate;
                    return true;
                }
            }

            status = NVAPI_OK;
            return false;
        }

        private static bool CustomDisplayMatches(NvCustomDisplay custom, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth)
        {
            uint customWidth = custom.width > 0 ? custom.width : custom.timing.HVisible;
            uint customHeight = custom.height > 0 ? custom.height : custom.timing.VVisible;
            uint customDepth = custom.depth > 0 ? custom.depth : 32U;
            uint wantedDepth = depth > 0 ? depth : 32U;

            if (customWidth != width || customHeight != height || customDepth != wantedDepth)
                return false;

            uint customMilliHz = GetCustomDisplayRefreshMilliHz(custom);
            uint wantedMilliHz = NormalizeRefreshMilliHz(refresh, refreshMilliHz);
            if (customMilliHz > 0 && wantedMilliHz > 0)
                return Math.Abs((long)customMilliHz - (long)wantedMilliHz) <= 1;

            uint customRefresh = GetCustomDisplayRefresh(custom);
            return customRefresh > 0 && customRefresh == refresh;
        }

        private static uint GetCustomDisplayRefreshMilliHz(NvCustomDisplay custom)
        {
            if (custom.timing.etc.rrx1k > 0)
                return custom.timing.etc.rrx1k;
            if (custom.timing.etc.rr > 0)
                return custom.timing.etc.rr * 1000U;
            if (custom.timing.pclk > 0 && custom.timing.HTotal > 0 && custom.timing.VTotal > 0)
                return (uint)Math.Round(((custom.timing.pclk * 10000.0) / custom.timing.HTotal / custom.timing.VTotal) * 1000.0, MidpointRounding.AwayFromZero);
            return 0;
        }

        private static uint GetCustomDisplayRefresh(NvCustomDisplay custom)
        {
            uint milliHz = GetCustomDisplayRefreshMilliHz(custom);
            if (milliHz > 0)
                return (uint)Math.Round(milliHz / 1000.0, MidpointRounding.AwayFromZero);
            return 0;
        }

        private static NvTiming CreateEmptyTiming()
        {
            return new NvTiming { etc = new NvTimingExt { name = new byte[40] } };
        }

        private static NvCustomDisplay CreateEmptyCustomDisplay()
        {
            return new NvCustomDisplay
            {
                version = NV_CUSTOM_DISPLAY_VER,
                timing = CreateEmptyTiming()
            };
        }

        private static void TryRevertCustomDisplay(uint displayId)
        {
            if (DispRevertCustomDisplayTrial == null) return;
            try { DispRevertCustomDisplayTrial(ref displayId, 1); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("NvAPI revert custom display trial failed: " + ex.Message); }
        }

        public static List<NvCustomDisplay> EnumCustomDisplays(string deviceName, out int status)
        {
            var result = new List<NvCustomDisplay>(16);
            status = NVAPI_NO_IMPLEMENTATION;
            if (!IsInitialized || !HasCustomDisplayEnumeration) return result;
            if (!TryGetDisplayId(deviceName, out uint displayId))
            {
                status = NVAPI_INVALID_ARGUMENT;
                return result;
            }

            for (uint i = 0; i < 128; i++)
            {
                var custom = new NvCustomDisplay
                {
                    version = NV_CUSTOM_DISPLAY_VER,
                    timing = new NvTiming { etc = new NvTimingExt { name = new byte[40] } }
                };

                status = (int)DispEnumCustomDisplay(displayId, i, ref custom);
                if (status == NVAPI_END_ENUMERATION)
                {
                    status = NVAPI_OK;
                    break;
                }

                if (status != NVAPI_OK)
                    break;

                result.Add(custom);
            }

            return result;
        }

        public static List<string> EnumerateResolvableDisplayNames()
        {
            var result = new List<string>(4);
            if (!IsInitialized || DispGetDisplayIdByDisplayName == null)
                return result;

            for (int index = 1; index <= 64; index++)
            {
                string deviceName = @"\\.\DISPLAY" + index;
                if (TryGetDisplayId(deviceName, out _))
                    result.Add(deviceName);
            }

            return result;
        }

        public static bool TryGetEdid(string deviceName, out byte[] edid, out int status)
        {
            edid = null;
            status = NVAPI_NO_IMPLEMENTATION;
            if (!IsInitialized || !HasEdidDataSupport)
                return false;
            if (!TryGetDisplayId(deviceName, out uint displayId))
            {
                status = NVAPI_INVALID_ARGUMENT;
                return false;
            }

            var data = new NvEdidData
            {
                version = NV_EDID_DATA_VER,
                reserved = new uint[8]
            };
            uint flags = 0;
            status = (int)DispGetEdidData(displayId, ref data, ref flags);
            if (status != NVAPI_OK || data.sizeOfEDID == 0 || data.sizeOfEDID > 65536)
                return false;

            IntPtr buffer = IntPtr.Zero;
            try
            {
                buffer = Marshal.AllocHGlobal((int)data.sizeOfEDID);
                data.pEDID = buffer;
                data.reserved = new uint[8];
                status = (int)DispGetEdidData(displayId, ref data, ref flags);
                if (status != NVAPI_OK || data.sizeOfEDID == 0 || data.sizeOfEDID > 65536)
                    return false;

                edid = new byte[(int)data.sizeOfEDID];
                Marshal.Copy(buffer, edid, 0, (int)data.sizeOfEDID);
                return true;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool TryGetDisplayId(string deviceName, out uint displayId)
        {
            displayId = 0;
            if (DispGetDisplayIdByDisplayName == null) return false;

            string[] names = BuildDisplayNameCandidates(deviceName);
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (string.IsNullOrWhiteSpace(name)) continue;
                int status = (int)DispGetDisplayIdByDisplayName(name, out displayId);
                if (status == NVAPI_OK && displayId != 0) return true;
            }

            return false;
        }

        private static void ClearResolvedFunctions()
        {
            Initialize = null;
            GetErrorMessage = null;
            GetInterfaceVersionString = null;
            EnumPhysicalGpus = null;
            GpuGetFullName = null;
            EnumNvidiaDisplayHandle = null;
            GetAssociatedNvidiaDisplayHandle = null;
            GetDvcInfo = null;
            SetDvcLevel = null;
            GetDvcInfoEx = null;
            SetDvcLevelEx = null;
            DispGetDisplayIdByDisplayName = null;
            DispGetEdidData = null;
            DispGetTiming = null;
            DispEnumCustomDisplay = null;
            DispTryCustomDisplay = null;
            DispSaveCustomDisplay = null;
            DispRevertCustomDisplayTrial = null;
            DispDeleteCustomDisplay = null;
            DispGetDisplayConfig = null;
            DispSetDisplayConfig = null;
        }

        private static bool ValidateStructLayout(out string error)
        {
            error = null;
            if (Marshal.SizeOf(typeof(NvTimingFlag)) != 12)
            {
                error = "NV_TIMING_FLAG managed size mismatch: " + Marshal.SizeOf(typeof(NvTimingFlag));
                return false;
            }

            if (Marshal.SizeOf(typeof(NvTimingInput)) != 32)
            {
                error = "NV_TIMING_INPUT managed size mismatch: " + Marshal.SizeOf(typeof(NvTimingInput));
                return false;
            }

            if (Marshal.SizeOf(typeof(NvTiming)) != 96)
            {
                error = "NV_TIMING managed size mismatch: " + Marshal.SizeOf(typeof(NvTiming));
                return false;
            }

            if (Marshal.SizeOf(typeof(NvCustomDisplay)) != 144)
            {
                error = "NV_CUSTOM_DISPLAY managed size mismatch: " + Marshal.SizeOf(typeof(NvCustomDisplay));
                return false;
            }

            return true;
        }

        private static T Resolve<T>(uint id) where T : class
        {
            if (_queryInterface == null) return null;
            IntPtr ptr = _queryInterface(id);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T;
        }

        private static IEnumerable<string> GetNvApiCandidates()
        {
            string lib = Environment.Is64BitProcess ? "nvapi64.dll" : "nvapi.dll";
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (Environment.Is64BitProcess)
            {
                yield return Path.Combine(windows, "System32", "nvapi64.dll");
            }
            else
            {
                yield return Path.Combine(windows, "SysWOW64", "nvapi.dll");
                yield return Path.Combine(windows, "System32", "nvapi.dll");
            }

            yield return lib;
        }

        private static string[] BuildDisplayNameCandidates(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
                return new string[0];

            string noDot = deviceName.Replace(@"\\.\", @"\\");
            return new[] { deviceName, noDot };
        }

        private static IntPtr Add(IntPtr ptr, int offset)
        {
            return new IntPtr(ptr.ToInt64() + offset);
        }

        private static void ZeroMemory(IntPtr ptr, int bytes)
        {
            if (ptr == IntPtr.Zero || bytes <= 0) return;
            byte[] zero = new byte[bytes];
            Marshal.Copy(zero, 0, ptr, bytes);
        }

        public static string StatusToString(int status)
        {
            if (GetErrorMessage != null)
            {
                try
                {
                    var message = new NvShortString { Value = new byte[NVAPI_SHORT_STRING_MAX] };
                    if ((int)GetErrorMessage((NvStatus)status, ref message) == NVAPI_OK)
                    {
                        string text = ShortStringToString(ref message);
                        if (!string.IsNullOrWhiteSpace(text)) return text;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("NvAPI status text lookup failed: " + ex.Message);
                }
            }

            switch (status)
            {
                case NVAPI_OK: return "OK";
                case NVAPI_ERROR: return "General NVAPI error";
                case NVAPI_LIBRARY_NOT_FOUND: return "NVAPI library not found";
                case NVAPI_NO_IMPLEMENTATION: return "NVAPI function is not implemented by this driver";
                case NVAPI_API_NOT_INITIALIZED: return "NVAPI is not initialized";
                case NVAPI_INVALID_ARGUMENT: return "Invalid argument";
                case NVAPI_NVIDIA_DEVICE_NOT_FOUND: return "NVIDIA device not found";
                case NVAPI_END_ENUMERATION: return "End of enumeration";
                case NVAPI_INVALID_HANDLE: return "Invalid NVAPI handle";
                case NVAPI_INCOMPATIBLE_STRUCT_VERSION: return "Incompatible NVAPI structure version";
                case NVAPI_HANDLE_INVALIDATED: return "NVAPI handle invalidated";
                case NVAPI_FUNCTION_NOT_FOUND: return "NVAPI function not found";
                default: return "NVAPI status " + status;
            }
        }

        public static string ShortStringToString(ref NvShortString s)
        {
            if (s.Value == null) return string.Empty;
            int len = Array.FindIndex(s.Value, b => b == 0);
            if (len < 0) len = s.Value.Length;
            return Encoding.ASCII.GetString(s.Value, 0, len);
        }
    }
}

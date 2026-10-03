using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Native;
using System;
using System.Collections.Generic;

namespace DISPLAY_SCALER.Services
{
    public sealed class NvApiService
    {
        public static NvApiService Instance { get; } = new NvApiService();

        public bool IsAvailable { get; private set; }
        public bool IsInitialized { get; private set; }
        public string ApiVersion { get; private set; }
        public string LastError { get; private set; }

        private NvApiService()
        { }

        public void Initialize()
        {
            if (IsInitialized) return;

            if (!NvApi.Load())
            {
                IsAvailable = false;
                LastError = string.IsNullOrWhiteSpace(NvApi.LastLoadError)
                    ? "Не удалось загрузить NVAPI DLL. Убедитесь, что установлен драйвер NVIDIA."
                    : NvApi.LastLoadError;
                return;
            }
            IsAvailable = true;

            int status = NvApi.InitializeApi();
            if (status != NvApi.NVAPI_OK)
            {
                IsInitialized = false;
                LastError = $"NvAPI_Initialize failed: {NvApi.StatusToString(status)}";
                return;
            }
            IsInitialized = true;
            LastError = null;

            if (NvApi.GetInterfaceVersionString != null)
            {
                try
                {
                    var ver = new NvApi.NvShortString { Value = new byte[NvApi.NVAPI_SHORT_STRING_MAX] };
                    int s = (int)NvApi.GetInterfaceVersionString(ref ver);
                    if (s == NvApi.NVAPI_OK)
                        ApiVersion = NvApi.ShortStringToString(ref ver);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("NVAPI version read failed: " + ex.Message); }
            }
        }

        public string ResolveDisplayNameForEdid(string preferredDeviceName, byte[] expectedEdid, out string detail)
        {
            detail = null;
            if (!IsInitialized || !NvApi.HasEdidDataSupport)
            {
                detail = "NVAPI EDID lookup недоступен.";
                return null;
            }

            if (expectedEdid == null || expectedEdid.Length < 18)
            {
                detail = "EDID выбранного монитора недоступен.";
                return null;
            }

            var matches = new List<string>(2);
            List<string> candidates = NvApi.EnumerateResolvableDisplayNames();
            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (!NvApi.TryGetEdid(candidate, out var candidateEdid, out var status))
                    continue;

                if (EdidIdentityMatches(expectedEdid, candidateEdid))
                    matches.Add(candidate);
            }

            if (matches.Count == 1)
            {
                detail = "NVAPI output resolved by EDID: " + matches[0] + ".";
                return matches[0];
            }

            if (matches.Count > 1)
            {
                detail = "Несколько NVAPI outputs имеют одинаковый EDID: " + string.Join(", ", matches) + ".";
                return null;
            }

            if (!string.IsNullOrWhiteSpace(preferredDeviceName) &&
                NvApi.TryGetEdid(preferredDeviceName, out var preferredEdid, out var preferredStatus))
            {
                detail = "NVAPI output uses direct Windows display name: " + preferredDeviceName + ".";
                return preferredDeviceName;
            }

            detail = "NVAPI output с EDID выбранного монитора не найден среди " + candidates.Count + " display names.";
            return null;
        }

        public string ResolveDisplayNameForMonitor(string preferredDeviceName, string monitorDeviceId, byte[] expectedEdid, out string detail)
        {
            string resolvedByEdid = ResolveDisplayNameForEdid(preferredDeviceName, expectedEdid, out detail);
            if (!string.IsNullOrWhiteSpace(resolvedByEdid))
                return resolvedByEdid;

            string monitorPnpIdentity = ExtractMonitorPnpIdentity(monitorDeviceId);
            if (string.IsNullOrWhiteSpace(monitorPnpIdentity))
                return null;

            var matches = new List<string>(2);
            List<string> candidates = NvApi.EnumerateResolvableDisplayNames();
            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (!NvApi.TryGetEdid(candidate, out var candidateEdid, out var status))
                    continue;

                EdidInfo candidateInfo = Win32Monitor.ParseEdid(candidateEdid);
                if (!candidateInfo.IsValid || !MonitorPnpIdentityMatches(monitorPnpIdentity, candidateInfo))
                    continue;

                matches.Add(candidate);
            }

            if (matches.Count == 1)
            {
                detail = "NVAPI output resolved by monitor PNP identity: " + matches[0] + ".";
                return matches[0];
            }

            if (matches.Count > 1)
            {
                detail = "Несколько NVAPI outputs совпадают с PNP identity монитора: " + string.Join(", ", matches) + ".";
                return null;
            }

            detail = "NVAPI output с PNP identity " + monitorPnpIdentity + " не найден среди " + candidates.Count + " display names.";
            return null;
        }

        private static bool EdidIdentityMatches(byte[] expected, byte[] candidate)
        {
            if (expected == null || candidate == null || expected.Length < 18 || candidate.Length < 18)
                return false;

            for (int i = 8; i < 18; i++)
            {
                if (expected[i] != candidate[i])
                    return false;
            }

            return true;
        }

        private static string ExtractMonitorPnpIdentity(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return null;

            string[] parts = deviceId.Split('\\');
            if (parts.Length < 2 || !string.Equals(parts[0], "MONITOR", StringComparison.OrdinalIgnoreCase))
                return null;

            return parts[1].Trim().ToUpperInvariant();
        }

        private static bool MonitorPnpIdentityMatches(string monitorPnpIdentity, EdidInfo candidate)
        {
            if (string.IsNullOrWhiteSpace(monitorPnpIdentity) || candidate == null || string.IsNullOrWhiteSpace(candidate.PnpVendorCode))
                return false;

            string candidateIdentity = candidate.PnpVendorCode.Trim().ToUpperInvariant() + candidate.ProductCode.ToString("X4");
            return string.Equals(monitorPnpIdentity, candidateIdentity, StringComparison.OrdinalIgnoreCase);
        }

        public List<GpuInfo> EnumerateGpus()
        {
            var list = new List<GpuInfo>(NvApi.NVAPI_MAX_PHYSICAL_GPUS);
            if (!IsInitialized || NvApi.EnumPhysicalGpus == null) return list;

            var handles = new IntPtr[NvApi.NVAPI_MAX_PHYSICAL_GPUS];
            int status = (int)NvApi.EnumPhysicalGpus(handles, out uint count);
            if (status != NvApi.NVAPI_OK)
            {
                LastError = $"EnumPhysicalGPUs: {NvApi.StatusToString(status)}";
                return list;
            }

            for (uint i = 0; i < count; i++)
            {
                var gpu = new GpuInfo { NvHandle = handles[i], NvApiVersion = ApiVersion };
                if (NvApi.GpuGetFullName != null)
                {
                    var name = new NvApi.NvShortString { Value = new byte[NvApi.NVAPI_SHORT_STRING_MAX] };
                    int s = (int)NvApi.GpuGetFullName(handles[i], ref name);
                    if (s == NvApi.NVAPI_OK)
                        gpu.FullName = NvApi.ShortStringToString(ref name);
                }
                if (string.IsNullOrEmpty(gpu.FullName))
                    gpu.FullName = $"NVIDIA GPU #{i + 1}";

                gpu.DvcSupported = NvApi.HasDvcSupport;
                gpu.IsReady = true;

                list.Add(gpu);
            }
            return list;
        }

        public bool CanControlSaturation(string deviceName)
        {
            if (!IsInitialized || !NvApi.HasDvcSupport) return false;
            return NvApi.TryGetDisplayHandle(deviceName, out IntPtr handle) && handle != IntPtr.Zero;
        }

        public int GetSaturationForDisplay(string deviceName, out int defaultLevel)
        {
            defaultLevel = 50;
            if (!IsInitialized || !NvApi.HasDvcSupport)
            {
                LastError = "NVAPI DVC functions are not available in the loaded driver.";
                return -1;
            }

            if (!NvApi.TryGetDisplayHandle(deviceName, out IntPtr displayHandle))
            {
                LastError = "Не удалось получить NVIDIA display handle для " + (deviceName ?? "первого дисплея") + ".";
                return -1;
            }

            if (NvApi.TryGetDvcInfoEx(displayHandle, out NvApi.NvDisplayDvcInfoEx ex, out int status))
            {
                defaultLevel = ex.defaultLevel > 0 ? ex.defaultLevel : 50;
                LastError = null;
                return ex.currentLevel;
            }

            if (NvApi.TryGetDvcInfo(displayHandle, out NvApi.NvDisplayDvcInfo info, out status))
            {
                LastError = null;
                return info.currentLevel;
            }

            LastError = "GetDVCInfo: " + NvApi.StatusToString(status);
            return -1;
        }

        public bool SetSaturationForDisplay(string deviceName, int level)
        {
            if (!IsInitialized || !NvApi.HasDvcSupport)
            {
                LastError = "NVAPI DVC functions are not available in the loaded driver.";
                return false;
            }

            if (!NvApi.TryGetDisplayHandle(deviceName, out IntPtr displayHandle))
            {
                LastError = "Не удалось получить NVIDIA display handle для " + (deviceName ?? "первого дисплея") + ".";
                return false;
            }

            if (!NvApi.TrySetDvcLevel(displayHandle, level, out int status))
            {
                LastError = "SetDVCLevel: " + NvApi.StatusToString(status);
                return false;
            }

            LastError = null;
            return true;
        }

        public bool RegisterCustomDisplay(string deviceName, uint width, uint height, uint refresh, uint depth, out string error)
        {
            return RegisterCustomDisplay(deviceName, width, height, refresh, 0, depth, null, out error);
        }

        internal bool RegisterCustomDisplay(string deviceName, uint width, uint height, uint refresh, uint depth, IEnumerable<NvApi.CustomDisplayTimingBase> timingBases, out string error)
        {
            return RegisterCustomDisplay(deviceName, width, height, refresh, 0, depth, timingBases, out error);
        }

        internal bool RegisterCustomDisplay(string deviceName, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth, IEnumerable<NvApi.CustomDisplayTimingBase> timingBases, out string error)
        {
            error = null;
            if (!IsInitialized || !NvApi.HasCustomDisplaySupport)
            {
                error = "NVAPI custom display API недоступен в загруженном драйвере.";
                return false;
            }

            if (!NvApi.TryRegisterCustomDisplay(deviceName, width, height, refresh, refreshMilliHz, depth, timingBases, out int status))
            {
                error = BuildCustomDisplayError("NVAPI custom display", status);
                return false;
            }

            return true;
        }

        internal bool BeginCustomDisplayTrial(string deviceName, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth, IEnumerable<NvApi.CustomDisplayTimingBase> timingBases, out string error)
        {
            return BeginCustomDisplayTrial(deviceName, width, height, refresh, refreshMilliHz, depth, timingBases, true, true, out error);
        }

        internal bool BeginCustomDisplayTrial(string deviceName, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth, IEnumerable<NvApi.CustomDisplayTimingBase> timingBases, bool includeRequestedSourceTiming, bool includeDriverPolicyFallback, out string error)
        {
            error = null;
            if (!IsInitialized || !NvApi.HasCustomDisplaySupport)
            {
                error = "NVAPI custom display API недоступен в загруженном драйвере.";
                return false;
            }

            if (!NvApi.TryBeginCustomDisplayTrial(deviceName, width, height, refresh, refreshMilliHz, depth, timingBases, includeRequestedSourceTiming, includeDriverPolicyFallback, out int status))
            {
                error = BuildCustomDisplayError("NVAPI custom display trial", status);
                return false;
            }

            return true;
        }

        internal bool SaveCustomDisplayTrial(string deviceName, out string error)
        {
            error = null;
            if (!IsInitialized || !NvApi.HasCustomDisplaySupport)
            {
                error = "NVAPI custom display API недоступен в загруженном драйвере.";
                return false;
            }

            if (!NvApi.TrySaveCustomDisplayTrial(deviceName, out int status))
            {
                error = BuildCustomDisplayError("NVAPI save custom display", status);
                return false;
            }

            return true;
        }

        internal bool RevertCustomDisplayTrial(string deviceName, out string error)
        {
            error = null;
            if (!IsInitialized)
            {
                error = "NVAPI не инициализирован.";
                return false;
            }

            if (!NvApi.TryRevertCustomDisplayTrial(deviceName, out int status))
            {
                error = BuildCustomDisplayError("NVAPI revert custom display trial", status);
                return false;
            }

            return true;
        }

        public bool RefreshOsModeList(out string detail)
        {
            detail = null;
            if (!IsInitialized || !NvApi.HasDisplayConfigSupport)
            {
                detail = "NVAPI display-config refresh недоступен в загруженном драйвере.";
                return false;
            }

            bool ok = NvApi.TryForceNvidiaModeEnumeration(out var status, out detail);
            if (!ok && string.IsNullOrWhiteSpace(detail))
                detail = "NvAPI_DISP_SetDisplayConfig: " + NvApi.StatusToString(status);
            return ok;
        }

        public List<CustomResolution> EnumerateCustomDisplays(string deviceName, out string error)
        {
            error = null;
            var list = new List<CustomResolution>(16);
            if (!IsInitialized || !NvApi.HasCustomDisplayEnumeration)
            {
                error = "NVAPI custom display enumeration недоступен.";
                return list;
            }

            List<NvApi.NvCustomDisplay> displays = NvApi.EnumCustomDisplays(deviceName, out int status);
            if (status != NvApi.NVAPI_OK)
            {
                error = "NvAPI_DISP_EnumCustomDisplay: " + NvApi.StatusToString(status);
                return list;
            }

            for (int i = 0; i < displays.Count; i++)
            {
                NvApi.NvCustomDisplay display = displays[i];
                uint refresh = GetRefreshRate(display);
                uint refreshMilliHz = GetRefreshRateMilliHz(display);

                uint width = display.width > 0 ? display.width : display.timing.HVisible;
                uint height = display.height > 0 ? display.height : display.timing.VVisible;
                if (width == 0 || height == 0 || refresh == 0) continue;

                list.Add(new CustomResolution
                {
                    Width = width,
                    Height = height,
                    RefreshRate = refresh,
                    RefreshRateMilliHz = refreshMilliHz,
                    BitsPerPixel = display.depth == 0 ? 32 : display.depth
                });
            }

            return list;
        }

        public bool TryGetCustomDisplayRefreshPrecision(string deviceName, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth, out bool targetRefreshMatches, out string detail)
        {
            targetRefreshMatches = false;
            detail = "NVIDIA custom display не найден.";

            if (!IsInitialized || !NvApi.HasCustomDisplayEnumeration)
            {
                detail = "NVAPI custom display enumeration недоступен.";
                return false;
            }

            List<NvApi.NvCustomDisplay> displays = NvApi.EnumCustomDisplays(deviceName, out int status);
            if (status != NvApi.NVAPI_OK)
            {
                detail = "NvAPI_DISP_EnumCustomDisplay: " + NvApi.StatusToString(status);
                return false;
            }

            uint wantedDepth = depth == 0 ? 32U : depth;
            uint wantedMilliHz = NormalizeTargetMilliHz(refresh, refreshMilliHz);
            bool found = false;
            uint bestMilliHz = 0;
            uint bestRefresh = 0;
            long bestDelta = long.MaxValue;

            for (int i = 0; i < displays.Count; i++)
            {
                NvApi.NvCustomDisplay display = displays[i];
                uint displayWidth = display.width > 0 ? display.width : display.timing.HVisible;
                uint displayHeight = display.height > 0 ? display.height : display.timing.VVisible;
                uint displayDepth = display.depth == 0 ? 32U : display.depth;
                if (displayWidth != width || displayHeight != height || displayDepth != wantedDepth)
                    continue;

                uint milliHz = GetRefreshRateMilliHz(display);
                uint displayRefresh = GetRefreshRate(display);
                if (milliHz == 0 && displayRefresh == 0)
                    continue;

                long delta = wantedMilliHz > 0 && milliHz > 0
                    ? Math.Abs((long)milliHz - (long)wantedMilliHz)
                    : Math.Abs((long)displayRefresh - (long)refresh) * 1000L;

                if (!found || delta < bestDelta)
                {
                    found = true;
                    bestDelta = delta;
                    bestMilliHz = milliHz;
                    bestRefresh = displayRefresh;
                }
            }

            if (!found)
                return false;

            targetRefreshMatches = wantedMilliHz > 0 && bestMilliHz > 0 && bestDelta <= 1;
            detail = "NVIDIA custom timing refresh: nominal=" + bestRefresh + " Hz, exact=" + FormatMilliHz(bestMilliHz) + ", target=" + FormatMilliHz(wantedMilliHz) + ", delta=" + bestDelta + " mHz.";
            return true;
        }

        public bool DeleteCustomDisplay(string deviceName, uint width, uint height, uint refresh, uint depth, out string error)
        {
            return DeleteCustomDisplay(deviceName, width, height, refresh, 0, depth, out error);
        }

        public bool DeleteCustomDisplay(string deviceName, uint width, uint height, uint refresh, uint refreshMilliHz, uint depth, out string error)
        {
            error = null;
            if (!IsInitialized || !NvApi.HasCustomDisplayDeletion)
            {
                error = "NVAPI custom display deletion API недоступен в загруженном драйвере.";
                return false;
            }

            if (!NvApi.TryDeleteCustomDisplay(deviceName, width, height, refresh, refreshMilliHz, depth, out int status))
            {
                error = BuildCustomDisplayError("NVAPI delete custom display", status);
                return false;
            }

            return true;
        }

        private static string BuildCustomDisplayError(string prefix, int status)
        {
            string step = string.IsNullOrWhiteSpace(NvApi.LastCustomDisplayStep)
                ? "unknown step"
                : NvApi.LastCustomDisplayStep;
            string diagnostics = NvApi.LastCustomDisplayDiagnostics;
            string error = prefix + " (" + step + "): " + NvApi.StatusToString(status);
            return string.IsNullOrWhiteSpace(diagnostics)
                ? error
                : string.Concat(error, ". Diagnostics: ", diagnostics);
        }

        private static uint GetRefreshRate(NvApi.NvCustomDisplay display)
        {
            uint milliHz = GetRefreshRateMilliHz(display);
            if (milliHz > 0)
                return (uint)Math.Round(milliHz / 1000.0, MidpointRounding.AwayFromZero);
            if (display.timing.etc.rr > 0)
                return display.timing.etc.rr;
            return 0;
        }

        private static uint GetRefreshRateMilliHz(NvApi.NvCustomDisplay display)
        {
            if (display.timing.etc.rrx1k > 0)
                return display.timing.etc.rrx1k;
            if (display.timing.etc.rr > 0)
                return display.timing.etc.rr * 1000U;
            if (display.timing.pclk > 0 && display.timing.HTotal > 0 && display.timing.VTotal > 0)
                return (uint)Math.Round(((display.timing.pclk * 10000.0) / display.timing.HTotal / display.timing.VTotal) * 1000.0, MidpointRounding.AwayFromZero);
            return 0;
        }

        private static uint NormalizeTargetMilliHz(uint refresh, uint refreshMilliHz)
        {
            if (refresh == 0)
                return 0;
            uint nominalMilliHz = refresh <= uint.MaxValue / 1000U ? refresh * 1000U : 0;
            if (refreshMilliHz == 0)
                return nominalMilliHz;
            uint roundedNominal = (uint)Math.Round(refreshMilliHz / 1000.0, MidpointRounding.AwayFromZero);
            if (roundedNominal != refresh)
                return nominalMilliHz;
            uint diff = refreshMilliHz > nominalMilliHz ? refreshMilliHz - nominalMilliHz : nominalMilliHz - refreshMilliHz;
            return diff < 1U ? nominalMilliHz : refreshMilliHz;
        }

        private static string FormatMilliHz(uint milliHz)
        {
            if (milliHz == 0)
                return "unknown";
            return (milliHz / 1000U) + "." + (milliHz % 1000U).ToString("D3") + " Hz";
        }
    }
}

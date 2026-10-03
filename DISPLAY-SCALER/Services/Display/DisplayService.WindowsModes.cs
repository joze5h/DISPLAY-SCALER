using DISPLAY_SCALER.Infrastructure.Timing;
using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Native;
using System;
using System.Collections.Generic;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class DisplayService
    {
        public bool IsNvidiaCustomModeVisible(MonitorInfo monitor, CustomResolution res, out string error)
        {
            if (monitor == null || res == null)
            {
                error = "Монитор или разрешение не определены.";
                return false;
            }

            return IsNvapiModeVisible(monitor, NormalizeResolution(res), out error);
        }

        public bool IsNvidiaCustomModeTargetRefresh(MonitorInfo monitor, CustomResolution res, out string detail)
        {
            detail = null;
            if (monitor == null || res == null)
            {
                detail = "Монитор или разрешение не определены.";
                return false;
            }
            if (_nv == null || !_nv.IsInitialized)
            {
                detail = "NVAPI не инициализирован.";
                return false;
            }

            CustomResolution normalized = NormalizeResolution(res);
            bool found = _nv.TryGetCustomDisplayRefreshPrecision(
                GetNvapiDeviceName(monitor),
                normalized.Width,
                normalized.Height,
                normalized.RefreshRate,
                normalized.RefreshRateMilliHz,
                normalized.BitsPerPixel,
                out var exact,
                out detail);

            return found && exact;
        }

        public bool IsWindowsModeEnumerated(MonitorInfo monitor, CustomResolution res)
        {
            return IsWindowsModeEnumeratedWithFlags(monitor, res, 0);
        }

        public bool IsWindowsRawModeEnumerated(MonitorInfo monitor, CustomResolution res)
        {
            return IsWindowsModeEnumeratedWithFlags(monitor, res, Win32Display.EDS_RAWMODE);
        }

        public bool TryFindBestWindowsModeForResolution(MonitorInfo monitor, uint width, uint height, out CustomResolution bestMode)
        {
            bestMode = null;
            if (monitor == null || string.IsNullOrWhiteSpace(monitor.DeviceName) || width == 0 || height == 0)
                return false;

            Win32Display.DEVMODE best = new Win32Display.DEVMODE();
            uint bestRefresh = 0;
            uint bestBpp = 0;

            for (int i = 0; i < MaxDisplayModeEnumeration; i++)
            {
                var dm = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, i, ref dm, 0) == 0)
                    break;

                if (dm.dmPelsWidth != width || dm.dmPelsHeight != height)
                    continue;

                uint bpp = dm.dmBitsPerPel == 0 ? DefaultBitsPerPixel : dm.dmBitsPerPel;
                if (bpp < DefaultBitsPerPixel)
                    continue;

                bool interlaced = (dm.dmDisplayFlags & 0x2) != 0;
                if (interlaced)
                    continue;

                uint refresh = dm.dmDisplayFrequency;
                if (refresh == 0)
                    continue;

                if (refresh > bestRefresh || (refresh == bestRefresh && bpp > bestBpp))
                {
                    best = dm;
                    bestRefresh = refresh;
                    bestBpp = bpp;
                }
            }

            if (bestRefresh == 0)
                return false;

            bestMode = new CustomResolution
            {
                Width = width,
                Height = height,
                RefreshRate = bestRefresh,
                RefreshRateMilliHz = ResolveNativeRefreshRateMilliHz(monitor, bestRefresh),
                BitsPerPixel = bestBpp == 0 ? DefaultBitsPerPixel : bestBpp,
                Origin = ResolutionOrigin.System,
                AddedByDisplayScaler = false,
                IsApplied = false
            };

            return true;
        }

        public string GetWindowsModeVisibilityText(MonitorInfo monitor, CustomResolution res)
        {
            bool normal = IsWindowsModeEnumerated(monitor, res);
            bool raw = IsWindowsRawModeEnumerated(monitor, res);

            if (normal) return "найден в обычном EnumDisplaySettingsEx";
            if (raw) return "найден только через EnumDisplaySettingsEx + EDS_RAWMODE";
            return "не найден в списке EnumDisplaySettingsEx";
        }

        private bool IsWindowsModeEnumeratedWithFlags(MonitorInfo monitor, CustomResolution res, uint enumFlags)
        {
            if (monitor == null || res == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return false;

            var normalized = NormalizeResolution(res);
            return TryGetMatchingDevMode(monitor, normalized, enumFlags, out var dm);
        }

        public ApplyResult TestDisplayMode(MonitorInfo monitor, CustomResolution res)
        {
            ApplyResult validation = ValidateCustomResolutionRequest(monitor, res);
            if (!validation.Success)
                return validation;

            var normalized = NormalizeResolution(res);
            bool fromWindowsList = TryGetMatchingDevMode(monitor, normalized, 0, out var dm);
            bool fromRawList = false;
            if (!fromWindowsList)
                fromRawList = TryGetMatchingDevMode(monitor, normalized, Win32Display.EDS_RAWMODE, out dm);

            if (!fromWindowsList && !fromRawList)
                return ApplyResult.Fail("CDS_TEST пропущен: режим не найден ни в обычном EnumDisplaySettingsEx, ни через EDS_RAWMODE. Проверять вручную собранный DEVMODE нельзя надёжно, Windows всё равно его не отдаёт как поддерживаемый mode.");

            int result = Win32Display.ChangeDisplaySettingsEx(monitor.DeviceName, ref dm, IntPtr.Zero, Win32Display.CDS_TEST, IntPtr.Zero);
            if (result == Win32Display.DISP_CHANGE_SUCCESSFUL)
            {
                string source = fromWindowsList ? "обычного EnumDisplaySettingsEx" : "EDS_RAWMODE";
                return ApplyResult.Ok("Windows подтвердил режим через ChangeDisplaySettingsEx/CDS_TEST с DEVMODE из " + source + ".");
            }

            return ApplyResult.Fail("CDS_TEST: " + Win32Display.ChangeResultToString(result));
        }

        private static bool TryGetMatchingDevMode(MonitorInfo monitor, CustomResolution normalized, uint enumFlags, out Win32Display.DEVMODE match)
        {
            match = new Win32Display.DEVMODE();
            if (monitor == null || normalized == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return false;

            for (int i = 0; i < MaxDisplayModeEnumeration; i++)
            {
                var dm = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, i, ref dm, enumFlags) == 0)
                    break;

                if (dm.dmPelsWidth == 0 || dm.dmPelsHeight == 0)
                    continue;

                uint bpp = dm.dmBitsPerPel == 0 ? DefaultBitsPerPixel : dm.dmBitsPerPel;
                if (bpp < DefaultBitsPerPixel)
                    continue;

                uint frequency = dm.dmDisplayFrequency == 0 ? normalized.RefreshRate : dm.dmDisplayFrequency;
                if (dm.dmPelsWidth == normalized.Width &&
                    dm.dmPelsHeight == normalized.Height &&
                    Math.Abs((long)frequency - (long)normalized.RefreshRate) <= 1)
                {
                    if ((dm.dmFields & Win32Display.DM_BITSPERPEL) == 0)
                        dm.dmBitsPerPel = normalized.BitsPerPixel;
                    if ((dm.dmFields & Win32Display.DM_DISPLAYFREQUENCY) == 0)
                        dm.dmDisplayFrequency = normalized.RefreshRate;

                    dm.dmFields |= Win32Display.DM_PELSWIDTH | Win32Display.DM_PELSHEIGHT |
                                   Win32Display.DM_DISPLAYFREQUENCY | Win32Display.DM_BITSPERPEL;
                    match = dm;
                    return true;
                }
            }

            return false;
        }

        private bool IsNvapiModeVisible(MonitorInfo monitor, CustomResolution res, out string error)
        {
            error = null;
            if (_nv == null || !_nv.IsInitialized)
            {
                error = "NVAPI не инициализирован.";
                return false;
            }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                List<CustomResolution> modes = _nv.EnumerateCustomDisplays(GetNvapiDeviceName(monitor), out error);
                for (int i = 0; i < modes.Count; i++)
                {
                    if (ModesMatch(modes[i], res))
                        return true;
                }

                NativeWait.Sleep(150);
            }

            return false;
        }

        public ApplyResult RefreshWindowsModeList(MonitorInfo monitor)
        {
            PrimeWindowsModeEnumeration(monitor);
            return ApplyResult.Ok("Non-mutating mode-list refresh: EnumDisplaySettingsEx cache primed for target display only; global SetDisplayConfig/NVAPI SetDisplayConfig intentionally skipped.");
        }

        private static string FormatApplyResult(ApplyResult result)
        {
            if (result == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(result.Detail)) return result.Detail;
            return result.Message ?? string.Empty;
        }

        private static ApplyResult ForceWindowsModeEnumeration()
        {
            try
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    int sizes = Win32Display.GetDisplayConfigBufferSizes(Win32Display.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
                    if (sizes != Win32Display.ERROR_SUCCESS)
                        return ApplyResult.Fail("GetDisplayConfigBufferSizes/QDC_ONLY_ACTIVE_PATHS: Win32 " + sizes);

                    if (pathCount == 0 || modeCount == 0)
                        return ApplyResult.Fail("QueryDisplayConfig: активные display paths не найдены.");

                    var paths = new Win32Display.DISPLAYCONFIG_PATH_INFO[(int)pathCount];
                    var modes = new Win32Display.DISPLAYCONFIG_MODE_INFO[(int)modeCount];
                    uint queryPathCount = pathCount;
                    uint queryModeCount = modeCount;

                    int query = Win32Display.QueryDisplayConfig(
                        Win32Display.QDC_ONLY_ACTIVE_PATHS,
                        ref queryPathCount,
                        paths,
                        ref queryModeCount,
                        modes,
                        IntPtr.Zero);

                    if (query == Win32Display.ERROR_INSUFFICIENT_BUFFER)
                    {
                        NativeWait.Sleep(100);
                        continue;
                    }

                    if (query != Win32Display.ERROR_SUCCESS)
                        return ApplyResult.Fail("QueryDisplayConfig/QDC_ONLY_ACTIVE_PATHS: Win32 " + query);

                    uint flags = Win32Display.SDC_APPLY |
                                 Win32Display.SDC_USE_SUPPLIED_DISPLAY_CONFIG |
                                 Win32Display.SDC_FORCE_MODE_ENUMERATION |
                                 Win32Display.SDC_NO_OPTIMIZATION;

                    int set = Win32Display.SetDisplayConfig(queryPathCount, paths, queryModeCount, modes, flags);
                    if (set == Win32Display.ERROR_SUCCESS)
                        return ApplyResult.Ok("SetDisplayConfig/SDC_FORCE_MODE_ENUMERATION: Windows mode list refresh OK; SDC_ALLOW_CHANGES intentionally not used.");

                    return ApplyResult.Fail("SetDisplayConfig/SDC_FORCE_MODE_ENUMERATION: Win32 " + set + "; SDC_ALLOW_CHANGES intentionally not used to avoid changing other monitors.");
                }

                return ApplyResult.Fail("QueryDisplayConfig: не удалось получить стабильный список active paths.");
            }
            catch (Exception ex)
            {
                return ApplyResult.Fail("SetDisplayConfig refresh exception: " + ex.Message);
            }
        }

        private static void PrimeWindowsModeEnumeration(MonitorInfo monitor)
        {
            try
            {
                if (monitor == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                    return;

                var dm = CreateDevMode();
                Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, 0, ref dm, 0);
                dm = CreateDevMode();
                Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, 0, ref dm, Win32Display.EDS_RAWMODE);
                dm = CreateDevMode();
                Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, Win32Display.ENUM_CURRENT_SETTINGS, ref dm, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PrimeWindowsModeEnumeration failed: " + ex.Message);
            }
        }

        private static bool ModesMatch(CustomResolution mode, CustomResolution target)
        {
            if (mode == null || target == null) return false;

            uint modeBpp = mode.BitsPerPixel == 0 ? DefaultBitsPerPixel : mode.BitsPerPixel;
            uint targetBpp = target.BitsPerPixel == 0 ? DefaultBitsPerPixel : target.BitsPerPixel;
            if (mode.Width != target.Width || mode.Height != target.Height || modeBpp != targetBpp)
                return false;

            if (mode.RefreshRateMilliHz > 0 && target.RefreshRateMilliHz > 0)
                return Math.Abs((long)mode.RefreshRateMilliHz - (long)target.RefreshRateMilliHz) <= 1;

            return Math.Abs((long)mode.RefreshRate - (long)target.RefreshRate) <= 1;
        }
    }
}

using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Native;
using System;
using System.Collections.Generic;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class DisplayService
    {
        public List<uint> GetAvailableRefreshRates(MonitorInfo monitor)
        {
            var values = new List<uint>();
            if (!IsValidCurrentSettingsTarget(monitor))
                return values;

            uint currentBpp = monitor.CurrentBitsPerPixel == 0 ? DefaultBitsPerPixel : monitor.CurrentBitsPerPixel;

            CollectRefreshRatesFromSnapshot(monitor, currentBpp, values);
            if (values.Count == 0 && currentBpp != DefaultBitsPerPixel)
                CollectRefreshRatesFromSnapshot(monitor, DefaultBitsPerPixel, values);
            if (values.Count == 0)
                CollectRefreshRatesFromSnapshot(monitor, 0, values);

            if (values.Count == 0)
            {
                CollectRefreshRatesNative(monitor, currentBpp, values);
                if (values.Count == 0 && currentBpp != DefaultBitsPerPixel)
                    CollectRefreshRatesNative(monitor, DefaultBitsPerPixel, values);
                if (values.Count == 0)
                    CollectRefreshRatesNative(monitor, 0, values);
            }

            values.Sort();
            return values;
        }

        public List<uint> GetAvailableColorDepths(MonitorInfo monitor)
        {
            var values = new List<uint>();
            if (!IsValidCurrentSettingsTarget(monitor))
                return values;

            uint currentRefresh = monitor.CurrentRefreshRate;

            CollectColorDepthsFromSnapshot(monitor, currentRefresh, values);
            if (values.Count == 0)
                CollectColorDepthsFromSnapshot(monitor, 0, values);

            if (values.Count == 0)
            {
                CollectColorDepthsNative(monitor, currentRefresh, values);
                if (values.Count == 0)
                    CollectColorDepthsNative(monitor, 0, values);
            }

            values.Sort();
            values.Reverse();
            return values;
        }

        public ApplyResult ApplyCurrentRefreshRate(MonitorInfo monitor, uint refreshRate)
        {
            if (!IsValidCurrentSettingsTarget(monitor))
                return ApplyResult.Fail(ApplyErrorCode.MonitorNotSelected, "Монитор не выбран или не подключён.");
            if (refreshRate == 0)
                return ApplyResult.Fail(ApplyErrorCode.InvalidResolution, "Частота не выбрана.");

            uint preferredBpp = monitor.CurrentBitsPerPixel == 0 ? DefaultBitsPerPixel : monitor.CurrentBitsPerPixel;
            if (!TryFindCurrentGeometryDevModeForRefresh(monitor, refreshRate, preferredBpp, out var mode))
            {
                return ApplyResult.Fail(ApplyErrorCode.UnsupportedMode, "Windows не вернул режим " + monitor.CurrentResolutionText + " @ " + refreshRate + " Гц для выбранного монитора.");
            }

            return ApplyEnumeratedDisplayMode(monitor, mode, "Частота применена: " + refreshRate + " Гц.");
        }

        public ApplyResult ApplyCurrentColorDepth(MonitorInfo monitor, uint bitsPerPixel)
        {
            if (!IsValidCurrentSettingsTarget(monitor))
                return ApplyResult.Fail(ApplyErrorCode.MonitorNotSelected, "Монитор не выбран или не подключён.");
            if (bitsPerPixel == 0)
                return ApplyResult.Fail(ApplyErrorCode.InvalidBitsPerPixel, "Битность цвета не выбрана.");

            uint preferredRefresh = monitor.CurrentRefreshRate;
            if (!TryFindCurrentGeometryDevModeForColorDepth(monitor, preferredRefresh, bitsPerPixel, out var mode))
            {
                return ApplyResult.Fail(ApplyErrorCode.UnsupportedMode, "Windows не вернул режим " + monitor.CurrentResolutionText + " с битностью " + bitsPerPixel + " bpp для выбранного монитора.");
            }

            return ApplyEnumeratedDisplayMode(monitor, mode, "Битность цвета применена: " + bitsPerPixel + " bpp.");
        }

        private static bool IsValidCurrentSettingsTarget(MonitorInfo monitor)
        {
            return monitor != null && monitor.IsAttached && !string.IsNullOrWhiteSpace(monitor.DeviceName) && monitor.CurrentWidth > 0 && monitor.CurrentHeight > 0;
        }

        private static void CollectRefreshRatesFromSnapshot(MonitorInfo monitor, uint bitsPerPixel, List<uint> result)
        {
            if (monitor == null || monitor.SupportedModes == null)
                return;

            for (int i = 0; i < monitor.SupportedModes.Count; i++)
            {
                ResolutionMode mode = monitor.SupportedModes[i];
                if (mode == null || mode.Width != monitor.CurrentWidth || mode.Height != monitor.CurrentHeight)
                    continue;
                if (mode.IsInterlaced)
                    continue;
                if (bitsPerPixel > 0 && NormalizeBpp(mode.BitsPerPixel) != bitsPerPixel)
                    continue;
                if (mode.RefreshRate == 0)
                    continue;
                if (!ContainsUInt(result, mode.RefreshRate))
                    result.Add(mode.RefreshRate);
            }
        }

        private static void CollectColorDepthsFromSnapshot(MonitorInfo monitor, uint refreshRate, List<uint> result)
        {
            if (monitor == null || monitor.SupportedModes == null)
                return;

            for (int i = 0; i < monitor.SupportedModes.Count; i++)
            {
                ResolutionMode mode = monitor.SupportedModes[i];
                if (mode == null || mode.Width != monitor.CurrentWidth || mode.Height != monitor.CurrentHeight)
                    continue;
                if (mode.IsInterlaced)
                    continue;
                if (refreshRate > 0 && Math.Abs((long)mode.RefreshRate - (long)refreshRate) > 1)
                    continue;

                uint bpp = NormalizeBpp(mode.BitsPerPixel);
                if (bpp == 0)
                    continue;
                if (!ContainsUInt(result, bpp))
                    result.Add(bpp);
            }
        }

        private static void CollectRefreshRatesNative(MonitorInfo monitor, uint bitsPerPixel, List<uint> result)
        {
            for (int i = 0; i < MaxDisplayModeEnumeration; i++)
            {
                var dm = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, i, ref dm, 0) == 0)
                    break;

                if (!MatchesCurrentGeometry(monitor, dm))
                    continue;
                if ((dm.dmDisplayFlags & 0x2) != 0)
                    continue;
                if (bitsPerPixel > 0 && NormalizeBpp(dm.dmBitsPerPel) != bitsPerPixel)
                    continue;
                if (dm.dmDisplayFrequency == 0)
                    continue;
                if (!ContainsUInt(result, dm.dmDisplayFrequency))
                    result.Add(dm.dmDisplayFrequency);
            }
        }

        private static void CollectColorDepthsNative(MonitorInfo monitor, uint refreshRate, List<uint> result)
        {
            for (int i = 0; i < MaxDisplayModeEnumeration; i++)
            {
                var dm = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, i, ref dm, 0) == 0)
                    break;

                if (!MatchesCurrentGeometry(monitor, dm))
                    continue;
                if ((dm.dmDisplayFlags & 0x2) != 0)
                    continue;
                if (refreshRate > 0 && Math.Abs((long)dm.dmDisplayFrequency - (long)refreshRate) > 1)
                    continue;

                uint bpp = NormalizeBpp(dm.dmBitsPerPel);
                if (bpp == 0)
                    continue;
                if (!ContainsUInt(result, bpp))
                    result.Add(bpp);
            }
        }

        private static bool TryFindCurrentGeometryDevModeForRefresh(MonitorInfo monitor, uint refreshRate, uint preferredBitsPerPixel, out Win32Display.DEVMODE mode)
        {
            mode = new Win32Display.DEVMODE();
            if (!IsValidCurrentSettingsTarget(monitor))
                return false;

            Win32Display.DEVMODE fallback = new Win32Display.DEVMODE();
            bool hasFallback = false;

            for (int i = 0; i < MaxDisplayModeEnumeration; i++)
            {
                var dm = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, i, ref dm, 0) == 0)
                    break;

                if (!MatchesCurrentGeometry(monitor, dm))
                    continue;
                if ((dm.dmDisplayFlags & 0x2) != 0)
                    continue;
                if (Math.Abs((long)dm.dmDisplayFrequency - (long)refreshRate) > 1)
                    continue;

                if (!hasFallback)
                {
                    fallback = dm;
                    hasFallback = true;
                }

                if (preferredBitsPerPixel == 0 || NormalizeBpp(dm.dmBitsPerPel) == preferredBitsPerPixel)
                {
                    PrepareCurrentPositionMode(monitor, ref dm);
                    mode = dm;
                    return true;
                }
            }

            if (!hasFallback)
                return false;

            PrepareCurrentPositionMode(monitor, ref fallback);
            mode = fallback;
            return true;
        }

        private static bool TryFindCurrentGeometryDevModeForColorDepth(MonitorInfo monitor, uint preferredRefreshRate, uint bitsPerPixel, out Win32Display.DEVMODE mode)
        {
            mode = new Win32Display.DEVMODE();
            if (!IsValidCurrentSettingsTarget(monitor))
                return false;

            Win32Display.DEVMODE fallback = new Win32Display.DEVMODE();
            bool hasFallback = false;

            for (int i = 0; i < MaxDisplayModeEnumeration; i++)
            {
                var dm = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, i, ref dm, 0) == 0)
                    break;

                if (!MatchesCurrentGeometry(monitor, dm))
                    continue;
                if ((dm.dmDisplayFlags & 0x2) != 0)
                    continue;
                if (NormalizeBpp(dm.dmBitsPerPel) != bitsPerPixel)
                    continue;

                if (!hasFallback)
                {
                    fallback = dm;
                    hasFallback = true;
                }

                if (preferredRefreshRate == 0 || Math.Abs((long)dm.dmDisplayFrequency - (long)preferredRefreshRate) <= 1)
                {
                    PrepareCurrentPositionMode(monitor, ref dm);
                    mode = dm;
                    return true;
                }
            }

            if (!hasFallback)
                return false;

            PrepareCurrentPositionMode(monitor, ref fallback);
            mode = fallback;
            return true;
        }

        private static ApplyResult ApplyEnumeratedDisplayMode(MonitorInfo monitor, Win32Display.DEVMODE mode, string successMessage)
        {
            PrepareCurrentPositionMode(monitor, ref mode);

            int test = Win32Display.ChangeDisplaySettingsEx(monitor.DeviceName, ref mode, IntPtr.Zero, Win32Display.CDS_TEST, IntPtr.Zero);
            if (test != Win32Display.DISP_CHANGE_SUCCESSFUL)
                return ApplyResult.Fail(ApplyErrorCode.UnsupportedMode, "Windows отклонил режим при CDS_TEST.", Win32Display.ChangeResultToString(test));

            int apply = Win32Display.ChangeDisplaySettingsEx(monitor.DeviceName, ref mode, IntPtr.Zero, Win32Display.CDS_UPDATEREGISTRY, IntPtr.Zero);
            if (apply == Win32Display.DISP_CHANGE_SUCCESSFUL)
                return ApplyResult.Ok(successMessage, Win32Display.ChangeResultToString(apply));
            if (apply == Win32Display.DISP_CHANGE_RESTART)
                return ApplyResult.Fail(ApplyErrorCode.UnsupportedMode, "Windows требует перезагрузку для применения режима.", Win32Display.ChangeResultToString(apply));

            return ApplyResult.Fail(ApplyErrorCode.UnsupportedMode, "Windows не применил выбранный режим.", Win32Display.ChangeResultToString(apply));
        }

        private static void PrepareCurrentPositionMode(MonitorInfo monitor, ref Win32Display.DEVMODE mode)
        {
            mode.dmFields |= Win32Display.DM_PELSWIDTH |
                             Win32Display.DM_PELSHEIGHT |
                             Win32Display.DM_BITSPERPEL |
                             Win32Display.DM_DISPLAYFREQUENCY |
                             Win32Display.DM_POSITION;
            mode.dmPositionX = monitor.PositionX;
            mode.dmPositionY = monitor.PositionY;
            if (mode.dmBitsPerPel == 0)
                mode.dmBitsPerPel = DefaultBitsPerPixel;
        }

        private static bool MatchesCurrentGeometry(MonitorInfo monitor, Win32Display.DEVMODE dm)
        {
            return dm.dmPelsWidth == monitor.CurrentWidth && dm.dmPelsHeight == monitor.CurrentHeight;
        }

        private static uint NormalizeBpp(uint bpp)
        {
            return bpp == 0 ? DefaultBitsPerPixel : bpp;
        }

        private static bool ContainsUInt(List<uint> values, uint value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }
            return false;
        }
    }
}
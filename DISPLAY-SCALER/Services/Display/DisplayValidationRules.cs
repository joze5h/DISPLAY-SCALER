using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Models;

namespace DISPLAY_SCALER.Services.Display
{
    public sealed class DisplayValidationResult
    {
        public bool Success { get; private set; }
        public ApplyErrorCode ErrorCode { get; private set; }
        public string Message { get; private set; }

        public static DisplayValidationResult Ok()
        {
            return new DisplayValidationResult { Success = true, ErrorCode = ApplyErrorCode.None };
        }

        public static DisplayValidationResult Fail(ApplyErrorCode code, string message)
        {
            return new DisplayValidationResult { Success = false, ErrorCode = code, Message = message };
        }
    }

    public static class DisplayValidationRules
    {
        public static DisplayValidationResult ValidateCustomResolutionRequest(MonitorInfo monitor, CustomResolution res)
        {
            if (monitor == null)
                return DisplayValidationResult.Fail(ApplyErrorCode.MonitorNotSelected, "Монитор не определён.");
            if (!monitor.IsAttached || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return DisplayValidationResult.Fail(ApplyErrorCode.MonitorNotAttached, "Монитор не подключён к активному ПК.");
            if (res == null)
                return DisplayValidationResult.Fail(ApplyErrorCode.ResolutionNotSpecified, "Разрешение не определено.");

            if (!ResolutionRules.TryValidate(res.Width, res.Height, res.RefreshRate, out var validationMessage))
                return DisplayValidationResult.Fail(ApplyErrorCode.InvalidResolution, validationMessage);
            if (res.BitsPerPixel != 0 && res.BitsPerPixel != ResolutionRules.DefaultBitsPerPixel)
                return DisplayValidationResult.Fail(ApplyErrorCode.InvalidBitsPerPixel, "Для Windows 10/11 корректен только 32 bpp display mode.");

            string hardBlockReason = ValidateHardCustomResolutionForMonitor(monitor, res);
            if (!string.IsNullOrWhiteSpace(hardBlockReason))
                return DisplayValidationResult.Fail(ApplyErrorCode.MonitorRangeViolation, hardBlockReason);

            return DisplayValidationResult.Ok();
        }

        public static string ValidateHardCustomResolutionForMonitor(MonitorInfo monitor, CustomResolution res)
        {
            if (monitor == null || res == null || res.Width == 0 || res.Height == 0)
                return null;

            double aspect = (double)res.Width / res.Height;
            if (aspect < 0.35 || aspect > 4.10)
                return "Соотношение сторон " + aspect.ToString("F2") + ":1 выглядит ошибочным для игрового custom-resolution. Проверьте ширину и высоту.";

            if (monitor.MinVerticalRate > 0 && res.RefreshRate + 1 < monitor.MinVerticalRate)
                return "Частота " + res.RefreshRate + " Гц ниже EDID-диапазона монитора.";

            return null;
        }

        public static void GetReferenceResolution(MonitorInfo monitor, out uint width, out uint height)
        {
            width = 0;
            height = 0;
            if (monitor == null) return;

            if (monitor.NativeWidth > 0 && monitor.NativeHeight > 0)
            {
                width = monitor.NativeWidth;
                height = monitor.NativeHeight;
                return;
            }

            if (monitor.CurrentWidth > 0 && monitor.CurrentHeight > 0)
            {
                width = monitor.CurrentWidth;
                height = monitor.CurrentHeight;
                return;
            }

            long bestArea = 0;
            if (monitor.SupportedModes != null)
            {
                for (int i = 0; i < monitor.SupportedModes.Count; i++)
                {
                    ResolutionMode mode = monitor.SupportedModes[i];
                    if (mode == null || mode.Width == 0 || mode.Height == 0) continue;
                    long area = (long)mode.Width * mode.Height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        width = mode.Width;
                        height = mode.Height;
                    }
                }
            }
        }

        public static uint GetReferenceRefresh(MonitorInfo monitor)
        {
            if (monitor == null) return 0;

            uint maxRefresh = monitor.CurrentRefreshRate;
            if (monitor.NativeRefreshRateHz > maxRefresh)
                maxRefresh = monitor.NativeRefreshRateHz;
            if (monitor.MaxVerticalRate > maxRefresh)
                maxRefresh = monitor.MaxVerticalRate;

            if (monitor.SupportedModes != null)
            {
                for (int i = 0; i < monitor.SupportedModes.Count; i++)
                {
                    ResolutionMode mode = monitor.SupportedModes[i];
                    if (mode == null || mode.IsInterlaced)
                        continue;
                    if (mode.BitsPerPixel != 0 && mode.BitsPerPixel < ResolutionRules.DefaultBitsPerPixel)
                        continue;
                    if (mode.RefreshRate > maxRefresh)
                        maxRefresh = mode.RefreshRate;
                }
            }
            return maxRefresh;
        }
    }
}
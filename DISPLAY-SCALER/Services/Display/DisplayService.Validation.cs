using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Models;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class DisplayService
    {
        private static ApplyResult ValidateCustomResolutionRequest(MonitorInfo monitor, CustomResolution res)
        {
            DISPLAY_SCALER.Services.Display.DisplayValidationResult validation = DISPLAY_SCALER.Services.Display.DisplayValidationRules.ValidateCustomResolutionRequest(monitor, res);
            return validation.Success
                ? ApplyResult.Ok()
                : ApplyResult.Fail(validation.ErrorCode, validation.Message);
        }

        private static void GetReferenceResolution(MonitorInfo monitor, out uint width, out uint height)
        {
            DISPLAY_SCALER.Services.Display.DisplayValidationRules.GetReferenceResolution(monitor, out width, out height);
        }

        private static uint GetReferenceRefresh(MonitorInfo monitor)
        {
            return DISPLAY_SCALER.Services.Display.DisplayValidationRules.GetReferenceRefresh(monitor);
        }

        private static CustomResolution NormalizeResolution(CustomResolution res)
        {
            return new CustomResolution
            {
                Width = res.Width,
                Height = res.Height,
                RefreshRate = res.RefreshRate,
                RefreshRateMilliHz = NormalizeRefreshMilliHz(res.RefreshRate, res.RefreshRateMilliHz),
                BitsPerPixel = DefaultBitsPerPixel,
                Origin = res.Origin,
                AddedByDisplayScaler = res.AddedByDisplayScaler
            };
        }

        public uint ResolveNativeRefreshRateMilliHz(MonitorInfo monitor, uint nominalRefreshRate)
        {
            return ResolveNativeRefreshRateMilliHzStatic(monitor, nominalRefreshRate);
        }

        internal static uint ResolveNativeRefreshRateMilliHzStatic(MonitorInfo monitor, uint nominalRefreshRate)
        {
            return RefreshRateMath.ResolveNativeMilliHz(nominalRefreshRate,
                monitor == null ? 0 : monitor.NativeRefreshRateMilliHz,
                monitor == null ? 0 : monitor.CurrentRefreshRateMilliHz);
        }

        private static uint NormalizeRefreshMilliHz(uint nominalRefreshRate, uint exactMilliHz)
        {
            return RefreshRateMath.NormalizeDisplayMilliHz(nominalRefreshRate, exactMilliHz);
        }
    }
}
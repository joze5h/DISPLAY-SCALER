using DISPLAY_SCALER.Models;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class ResolutionProfileService
    {
        private static DisplayScalerProfile CloneProfileWithResolution(DisplayScalerProfile profile, CustomResolution resolution)
        {
            if (profile == null)
                return null;

            CustomResolution normalized = Normalize(resolution);
            var clone = new DisplayScalerProfile
            {
                SchemaVersion = profile.SchemaVersion,
                Application = profile.Application,
                ExportKind = string.IsNullOrWhiteSpace(profile.ExportKind) ? "PortableResolution" : profile.ExportKind,
                PortableAcrossDisplays = profile.PortableAcrossDisplays || profile.SchemaVersion >= 2,
                ProfileName = normalized == null ? profile.ProfileName : normalized.Label,
                ExportedUtc = profile.ExportedUtc,
                ExportedByAppVersion = profile.ExportedByAppVersion,
                CreationHints = CloneCreationHints(profile.CreationHints),
                SourceDisplay = CloneProfileMonitor(profile.SourceDisplay),
                Resolution = normalized == null ? null : new DisplayProfileResolution
                {
                    Width = normalized.Width,
                    Height = normalized.Height,
                    RefreshRate = normalized.RefreshRate,
                    RefreshRateHz = normalized.RefreshRate,
                    NominalRefreshRateHz = normalized.RefreshRate,
                    ExactRefreshRateMilliHz = NormalizeMilliHz(normalized.RefreshRate, normalized.RefreshRateMilliHz),
                    SourceNativeRefreshRateMilliHz = profile.Resolution == null ? 0 : profile.Resolution.SourceNativeRefreshRateMilliHz,
                    SourceNativeRefreshRateNumerator = profile.Resolution == null ? 0 : profile.Resolution.SourceNativeRefreshRateNumerator,
                    SourceNativeRefreshRateDenominator = profile.Resolution == null ? 0 : profile.Resolution.SourceNativeRefreshRateDenominator,
                    ExactRefreshRatePolicy = PreserveUserEditedRefreshPolicy,
                    IntegerRefreshRate = NormalizeMilliHz(normalized.RefreshRate, normalized.RefreshRateMilliHz) == NormalizeMilliHz(normalized.RefreshRate, 0),
                    PreserveLegitimateFractionalRefreshRates = true,
                    BitsPerPixel = 32,
                    AspectRatio = normalized.AspectRatio,
                    TimingMode = profile.Resolution == null || string.IsNullOrWhiteSpace(profile.Resolution.TimingMode) ? "NVIDIA_AUTO_TARGET_EQUIVALENT_REFRESH" : profile.Resolution.TimingMode,
                    EstimatedPixelClockMhz = EstimatePixelClockMhz(normalized)
                },
                Diagnostics = CloneDiagnostics(profile.Diagnostics)
            };

            if (clone.Diagnostics != null && normalized != null)
                clone.Diagnostics.Notes.Add("Пользователь изменил импортируемое разрешение перед применением: " + normalized.Label + ".");

            return clone;
        }

        private static DisplayProfileCreationHints CloneCreationHints(DisplayProfileCreationHints source)
        {
            if (source == null) return new DisplayProfileCreationHints();
            return new DisplayProfileCreationHints
            {
                TargetApi = source.TargetApi,
                TimingMode = string.IsNullOrWhiteSpace(source.TimingMode) ? "NVIDIA_AUTO_TARGET_EQUIVALENT_REFRESH" : source.TimingMode,
                RecalculateTimingOnImport = source.RecalculateTimingOnImport,
                SourceMonitorIsBinding = source.SourceMonitorIsBinding,
                ExactRefreshRatePolicy = PreserveUserEditedRefreshPolicy,
                PreferTargetNativeRefreshRate = false,
                PreserveLegitimateFractionalRefreshRates = source.PreserveLegitimateFractionalRefreshRates,
                SizeTransferPolicy = string.IsNullOrWhiteSpace(source.SizeTransferPolicy) ? "TryExactTargetModeNoAutomaticFit" : source.SizeTransferPolicy,
                FitToTargetNativeWhenOversized = false,
                PreserveExistingTargetNativeModes = source.PreserveExistingTargetNativeModes,
                Notes = source.Notes
            };
        }

        private static DisplayProfileMonitor CloneProfileMonitor(DisplayProfileMonitor source)
        {
            if (source == null) return null;
            return new DisplayProfileMonitor
            {
                DeviceName = source.DeviceName,
                FriendlyName = source.FriendlyName,
                Manufacturer = source.Manufacturer,
                ProductCode = source.ProductCode,
                SerialNumber = source.SerialNumber,
                DeviceId = source.DeviceId,
                AdapterName = source.AdapterName,
                AdapterDeviceId = source.AdapterDeviceId,
                NativeWidth = source.NativeWidth,
                NativeHeight = source.NativeHeight,
                CurrentWidth = source.CurrentWidth,
                CurrentHeight = source.CurrentHeight,
                CurrentRefreshRate = source.CurrentRefreshRate,
                CurrentRefreshRateMilliHz = source.CurrentRefreshRateMilliHz,
                CurrentRefreshRateNumerator = source.CurrentRefreshRateNumerator,
                CurrentRefreshRateDenominator = source.CurrentRefreshRateDenominator,
                NativeRefreshRateHz = source.NativeRefreshRateHz,
                NativeRefreshRateMilliHz = source.NativeRefreshRateMilliHz,
                NativeRefreshRateNumerator = source.NativeRefreshRateNumerator,
                NativeRefreshRateDenominator = source.NativeRefreshRateDenominator,
                MinVerticalRate = source.MinVerticalRate,
                MaxVerticalRate = source.MaxVerticalRate,
                MaxPixelClockMhz = source.MaxPixelClockMhz,
                Fingerprint = source.Fingerprint
            };
        }

        private static DisplayProfileDiagnostics CloneDiagnostics(DisplayProfileDiagnostics source)
        {
            var clone = new DisplayProfileDiagnostics();
            if (source == null)
                return clone;

            clone.SourceModeWasAddedByDisplayScaler = source.SourceModeWasAddedByDisplayScaler;
            clone.SourceModeMatchedNvidiaCustomList = source.SourceModeMatchedNvidiaCustomList;
            clone.SourceModeMatchedWindowsModeList = source.SourceModeMatchedWindowsModeList;
            if (source.Notes != null)
            {
                foreach (string note in source.Notes)
                    clone.Notes.Add(note);
            }
            return clone;
        }

        private static CustomResolution Normalize(CustomResolution res)
        {
            if (res == null) return null;
            return new CustomResolution
            {
                Width = res.Width,
                Height = res.Height,
                RefreshRate = res.RefreshRate,
                RefreshRateMilliHz = NormalizeMilliHz(res.RefreshRate, res.RefreshRateMilliHz),
                BitsPerPixel = 32,
                AddedByDisplayScaler = true
            };
        }

        private static double EstimatePixelClockMhz(CustomResolution res)
        {
            if (res == null || res.Width == 0 || res.Height == 0 || res.RefreshRate == 0) return 0;

            return (double)res.Width * res.Height * res.RefreshRate * 1.08 / 1000000.0;
        }
    }
}
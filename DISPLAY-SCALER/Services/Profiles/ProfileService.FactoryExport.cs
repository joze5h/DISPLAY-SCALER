using DISPLAY_SCALER.Models;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class ResolutionProfileService
    {
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        public string BuildDefaultFileName(CustomResolution resolution)
        {
            string name = resolution == null ? "resolution" : resolution.Width + "x" + resolution.Height + "_" + resolution.RefreshRate + "Hz";

            return SanitizeFileName(AppName + "_" + name + "_CRU.bin");
        }

        public DisplayScalerProfile CreateProfile(MonitorInfo monitor, CustomResolution resolution)
        {
            if (monitor == null) throw new ArgumentNullException(nameof(monitor));
            if (resolution == null) throw new ArgumentNullException(nameof(resolution));

            var normalized = Normalize(resolution);
            uint exportExactMilliHz = ResolveExportExactRefreshMilliHz(monitor, normalized);
            uint nominalRefreshMilliHz = NormalizeMilliHz(normalized.RefreshRate, 0);

            var profile = new DisplayScalerProfile
            {
                SchemaVersion = CurrentSchemaVersion,
                Application = AppName,
                ProfileName = normalized.Label,
                ExportedUtc = DateTime.UtcNow.ToString("o"),
                ExportedByAppVersion = GetAppVersion(),
                ExportKind = "PortableResolution",
                PortableAcrossDisplays = true,
                CreationHints = new DisplayProfileCreationHints
                {
                    TargetApi = "NVIDIA_NVAPI_CUSTOM_DISPLAY",
                    TimingMode = "NVIDIA_AUTO_TARGET_EQUIVALENT_REFRESH",
                    RecalculateTimingOnImport = true,
                    SourceMonitorIsBinding = false,
                    ExactRefreshRatePolicy = "PreferTargetNativeFractionalWhenNominalMatches",
                    SizeTransferPolicy = "TryExactTargetModeNoAutomaticFit",
                    PreferTargetNativeRefreshRate = true,
                    PreserveLegitimateFractionalRefreshRates = true,
                    FitToTargetNativeWhenOversized = false,
                    PreserveExistingTargetNativeModes = true,
                    Notes = "Portable profile keeps the exported width, height, refresh rate and bpp. NVAPI trial decides whether the selected monitor accepts the mode."
                },
                SourceDisplay = CreatePortableSourceDisplayTiming(monitor),
                Resolution = new DisplayProfileResolution
                {
                    Width = normalized.Width,
                    Height = normalized.Height,
                    RefreshRate = normalized.RefreshRate,
                    RefreshRateHz = normalized.RefreshRate,
                    NominalRefreshRateHz = normalized.RefreshRate,
                    ExactRefreshRateMilliHz = exportExactMilliHz,
                    SourceNativeRefreshRateMilliHz = GetSourceNativeRefreshMilliHz(monitor),
                    SourceNativeRefreshRateNumerator = GetSourceNativeRefreshNumerator(monitor),
                    SourceNativeRefreshRateDenominator = GetSourceNativeRefreshDenominator(monitor),
                    ExactRefreshRatePolicy = "PreferTargetNativeFractionalWhenNominalMatches",
                    IntegerRefreshRate = exportExactMilliHz == nominalRefreshMilliHz,
                    PreserveLegitimateFractionalRefreshRates = true,
                    BitsPerPixel = normalized.BitsPerPixel,
                    AspectRatio = normalized.AspectRatio,
                    TimingMode = "NVIDIA_AUTO_TARGET_EQUIVALENT_REFRESH",
                    EstimatedPixelClockMhz = EstimatePixelClockMhz(normalized)
                },
                Diagnostics = new DisplayProfileDiagnostics
                {
                    SourceModeWasAddedByDisplayScaler = resolution.AddedByDisplayScaler,
                    SourceModeMatchedWindowsModeList = false,
                    SourceModeMatchedNvidiaCustomList = false
                }
            };

            return profile;
        }

        private static DisplayProfileMonitor CreatePortableSourceDisplayTiming(MonitorInfo monitor)
        {
            if (monitor == null)
                return null;

            return new DisplayProfileMonitor
            {
                FriendlyName = monitor.FriendlyName,
                Manufacturer = monitor.Manufacturer,
                ProductCode = monitor.ProductCode,
                AdapterName = monitor.AdapterName,
                NativeWidth = monitor.NativeWidth,
                NativeHeight = monitor.NativeHeight,
                NativeRefreshRateHz = monitor.NativeRefreshRateHz,
                NativeRefreshRateMilliHz = monitor.NativeRefreshRateMilliHz,
                NativeRefreshRateNumerator = monitor.NativeRefreshRateNumerator,
                NativeRefreshRateDenominator = monitor.NativeRefreshRateDenominator,
                CurrentWidth = monitor.CurrentWidth,
                CurrentHeight = monitor.CurrentHeight,
                CurrentRefreshRate = monitor.CurrentRefreshRate,
                CurrentRefreshRateMilliHz = monitor.CurrentRefreshRateMilliHz,
                CurrentRefreshRateNumerator = monitor.CurrentRefreshRateNumerator,
                CurrentRefreshRateDenominator = monitor.CurrentRefreshRateDenominator,
                MinVerticalRate = monitor.MinVerticalRate,
                MaxVerticalRate = monitor.MaxVerticalRate,
                MaxPixelClockMhz = monitor.MaxPixelClockMhz
            };
        }

        private static uint ResolveExportExactRefreshMilliHz(MonitorInfo monitor, CustomResolution normalized)
        {
            if (normalized == null)
                return 0;
            uint fromResolution = NormalizeMilliHz(normalized.RefreshRate, normalized.RefreshRateMilliHz);
            uint nominalMilliHz = NormalizeMilliHz(normalized.RefreshRate, 0);
            if (fromResolution != 0 && fromResolution != nominalMilliHz)
                return fromResolution;

            uint nativeMilliHz = DisplayService.ResolveNativeRefreshRateMilliHzStatic(monitor, normalized.RefreshRate);
            return NormalizeMilliHz(normalized.RefreshRate, nativeMilliHz);
        }

        private static uint GetSourceNativeRefreshMilliHz(MonitorInfo monitor)
        {
            if (monitor == null)
                return 0;
            if (monitor.NativeRefreshRateMilliHz > 0)
                return monitor.NativeRefreshRateMilliHz;
            return monitor.CurrentRefreshRateMilliHz;
        }

        private static uint GetSourceNativeRefreshNumerator(MonitorInfo monitor)
        {
            if (monitor == null)
                return 0;
            if (monitor.NativeRefreshRateNumerator > 0)
                return monitor.NativeRefreshRateNumerator;
            return monitor.CurrentRefreshRateNumerator;
        }

        private static uint GetSourceNativeRefreshDenominator(MonitorInfo monitor)
        {
            if (monitor == null)
                return 0;
            if (monitor.NativeRefreshRateDenominator > 0)
                return monitor.NativeRefreshRateDenominator;
            return monitor.CurrentRefreshRateDenominator;
        }

        public void ExportProfile(string path, MonitorInfo monitor, CustomResolution resolution)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Путь экспорта не задан.", nameof(path));
            CruEdidBinProfileCodec.Export(path, monitor, resolution);
        }

        private static string GetAppVersion()
        {
            return CachedAppVersion;
        }

        private static string ResolveAppVersion()
        {
            try
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "1.0.0.0" : v.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetAppVersion failed: " + ex.Message);
                return "1.0.0.0";
            }
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "DISPLAY-SCALER_profile.bin";

            string trimmed = value.Trim();
            var sb = new StringBuilder(trimmed.Length);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                sb.Append(c == ' ' || ContainsChar(InvalidFileNameChars, c) ? '_' : c);
            }

            return sb.Length == 0 ? "DISPLAY-SCALER_profile.bin" : sb.ToString();
        }

        private static bool ContainsChar(char[] values, char value)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                    return true;
            }

            return false;
        }
    }
}
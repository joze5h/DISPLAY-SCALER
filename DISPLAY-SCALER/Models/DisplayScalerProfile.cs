using System.Collections.Generic;

namespace DISPLAY_SCALER.Models
{
    public sealed class DisplayScalerProfile
    {
        public int SchemaVersion { get; set; }
        public string Application { get; set; }
        public string ExportKind { get; set; }
        public bool PortableAcrossDisplays { get; set; }
        public string ProfileName { get; set; }
        public string ExportedUtc { get; set; }
        public string ExportedByAppVersion { get; set; }
        public DisplayProfileResolution Resolution { get; set; }
        public DisplayProfileCreationHints CreationHints { get; set; }

        public DisplayProfileMonitor SourceDisplay { get; set; }

        public DisplayProfileDiagnostics Diagnostics { get; set; }

        public DisplayScalerProfile()
        {
            SchemaVersion = 10;
            Application = "DISPLAY-SCALER";
            ExportKind = "PortableResolution";
            PortableAcrossDisplays = true;
            Resolution = new DisplayProfileResolution();
            CreationHints = new DisplayProfileCreationHints();
            SourceDisplay = null;
            Diagnostics = new DisplayProfileDiagnostics();
        }
    }

    public sealed class DisplayProfileCreationHints
    {
        public string TargetApi { get; set; }
        public string TimingMode { get; set; }
        public bool RecalculateTimingOnImport { get; set; }
        public bool SourceMonitorIsBinding { get; set; }
        public string Notes { get; set; }
        public string ExactRefreshRatePolicy { get; set; }
        public string SizeTransferPolicy { get; set; }
        public bool PreferTargetNativeRefreshRate { get; set; }
        public bool PreserveLegitimateFractionalRefreshRates { get; set; }
        public bool FitToTargetNativeWhenOversized { get; set; }
        public bool PreserveExistingTargetNativeModes { get; set; }

        public DisplayProfileCreationHints()
        {
            TargetApi = "NVIDIA_NVAPI_CUSTOM_DISPLAY";
            TimingMode = "NVIDIA_AUTO_TARGET_EQUIVALENT_REFRESH";
            RecalculateTimingOnImport = true;
            SourceMonitorIsBinding = false;
            ExactRefreshRatePolicy = "PreferTargetNativeFractionalWhenNominalMatches";
            SizeTransferPolicy = "TryExactTargetModeNoAutomaticFit";
            PreferTargetNativeRefreshRate = true;
            PreserveLegitimateFractionalRefreshRates = true;
            FitToTargetNativeWhenOversized = false;
            PreserveExistingTargetNativeModes = true;
            Notes = "Portable profile keeps the exported width, height, refresh rate and bpp. NVAPI trial decides whether the selected monitor accepts the mode.";
        }
    }

    public sealed class DisplayProfileMonitor
    {
        public string DeviceName { get; set; }
        public string FriendlyName { get; set; }
        public string Manufacturer { get; set; }
        public string ProductCode { get; set; }
        public string SerialNumber { get; set; }
        public string DeviceId { get; set; }
        public string AdapterName { get; set; }
        public string AdapterDeviceId { get; set; }
        public uint NativeWidth { get; set; }
        public uint NativeHeight { get; set; }
        public uint CurrentWidth { get; set; }
        public uint CurrentHeight { get; set; }
        public uint CurrentRefreshRate { get; set; }
        public uint CurrentRefreshRateMilliHz { get; set; }
        public uint CurrentRefreshRateNumerator { get; set; }
        public uint CurrentRefreshRateDenominator { get; set; }
        public uint NativeRefreshRateHz { get; set; }
        public uint NativeRefreshRateMilliHz { get; set; }
        public uint NativeRefreshRateNumerator { get; set; }
        public uint NativeRefreshRateDenominator { get; set; }
        public uint MinVerticalRate { get; set; }
        public uint MaxVerticalRate { get; set; }
        public uint MaxPixelClockMhz { get; set; }
        public string Fingerprint { get; set; }
    }

    public sealed class DisplayProfileResolution
    {
        public uint Width { get; set; }
        public uint Height { get; set; }

        public uint RefreshRate { get; set; }

        public uint RefreshRateHz { get; set; }

        public uint NominalRefreshRateHz { get; set; }

        public uint ExactRefreshRateMilliHz { get; set; }

        public uint SourceNativeRefreshRateMilliHz { get; set; }

        public uint SourceNativeRefreshRateNumerator { get; set; }
        public uint SourceNativeRefreshRateDenominator { get; set; }
        public string ExactRefreshRatePolicy { get; set; }

        public bool IntegerRefreshRate { get; set; }

        public bool PreserveLegitimateFractionalRefreshRates { get; set; }
        public uint BitsPerPixel { get; set; }
        public string AspectRatio { get; set; }
        public string TimingMode { get; set; }
        public double EstimatedPixelClockMhz { get; set; }
    }

    public sealed class DisplayProfileDiagnostics
    {
        public bool SourceModeWasAddedByDisplayScaler { get; set; }
        public bool SourceModeMatchedNvidiaCustomList { get; set; }
        public bool SourceModeMatchedWindowsModeList { get; set; }
        public List<string> Notes { get; set; }

        public DisplayProfileDiagnostics()
        {
            Notes = new List<string>();
        }
    }

    public sealed class ProfileImportPlan
    {
        public string FilePath { get; set; }
        public DisplayScalerProfile Profile { get; set; }
        public CustomResolution Resolution { get; set; }
        public CustomResolution RequestedResolution { get; set; }
        public bool WasResolutionAdaptedToTarget { get; set; }
        public string ResolutionAdaptationReason { get; set; }
        public bool WasRefreshAdaptedToTarget { get; set; }
        public string RefreshAdaptationReason { get; set; }
        public bool HasOversizedFitFallback { get; set; }
        public CustomResolution FallbackResolution { get; set; }
        public bool CanImport { get; set; }
        public bool AlreadyAvailableInWindows { get; set; }
        public bool RequiresDriverCustomSave { get; set; }
        public bool ExactMonitorMatch { get; set; }
        public bool SameNativeResolution { get; set; }
        public string RiskLevel { get; set; }
        public int RiskScore { get; set; }
        public string Summary { get; set; }
        public double EstimatedPixelClockMhz { get; set; }
        public List<string> BlockingIssues { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> Checks { get; set; }

        public ProfileImportPlan()
        {
            BlockingIssues = new List<string>();
            Warnings = new List<string>();
            Checks = new List<string>();
            RiskLevel = "UNKNOWN";
            RiskScore = 0;
        }
    }

    public enum ProfileApplyErrorCode
    {
        None = 0,
        Unknown = 1,
        MissingPlan = 10,
        CompatibilityBlocked = 20,
        TargetMonitorMissing = 30,
        DisplayApplyFailed = 40,
        DriverRecoveryFailed = 50,
        UnsupportedMode = 60,
        UserCancelled = 70
    }

    public sealed class ProfileApplyResult
    {
        public bool Success { get; set; }
        public bool PartialSuccess { get; set; }
        public bool DriverRestartAttempted { get; set; }
        public bool DriverRestartSucceeded { get; set; }
        public bool NeedsDeveloperDiagnostics { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
        public string DiagnosticDirectory { get; set; }
        public string DiagnosticFilePath { get; set; }
        public ProfileApplyErrorCode ErrorCode { get; set; }
        public CustomResolution FailedResolution { get; set; }
        public List<string> Log { get; set; }

        public ProfileApplyResult()
        {
            Log = new List<string>();
        }

        public static ProfileApplyResult Ok(string message, string detail = null)
        {
            return new ProfileApplyResult { Success = true, Message = message, Detail = detail, ErrorCode = ProfileApplyErrorCode.None };
        }

        public static ProfileApplyResult Fail(string message, string detail = null)
        {
            return new ProfileApplyResult { Success = false, Message = message, Detail = detail, ErrorCode = ProfileApplyErrorCode.Unknown };
        }

        public static ProfileApplyResult Fail(ProfileApplyErrorCode errorCode, string message, string detail = null)
        {
            return new ProfileApplyResult { Success = false, Message = message, Detail = detail, ErrorCode = errorCode };
        }
    }

    public sealed class DriverRestartResult
    {
        public bool Success { get; set; }
        public bool Attempted { get; set; }
        public string CommandLine { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool NvApiRefreshAttempted { get; set; }
        public bool NvApiRefreshSucceeded { get; set; }
        public string NvApiRefreshDetail { get; set; }

        public static DriverRestartResult NotAttempted(string reason)
        {
            return new DriverRestartResult { Attempted = false, Success = false, Error = reason };
        }
    }
}
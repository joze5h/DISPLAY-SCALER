using DISPLAY_SCALER.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class ResolutionProfileService
    {
        public void LogDeveloperProblem(string category, MonitorInfo monitor, CustomResolution resolution, string message, string detail, Exception exception)
        {
            try
            {
                string dir = GetDiagnosticsDirectory();
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "LOG.log");
                string entry = BuildLogEntrySeparator() + BuildGenericDiagnosticText(category, monitor, resolution, message, detail, exception) + Environment.NewLine;
                File.AppendAllText(file, entry, Utf8NoBom);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Diagnostic logging failed: " + ex.Message);
            }
        }

        private static string BuildGenericDiagnosticText(string category, MonitorInfo monitor, CustomResolution resolution, string message, string detail, Exception exception)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("DISPLAY-SCALER");
            sb.AppendLine("Category: " + (string.IsNullOrWhiteSpace(category) ? "General" : category));
            sb.AppendLine("Time local: " + DateTime.Now.ToString("O"));
            sb.AppendLine("Time UTC:   " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine("App version: " + GetAppVersion());
            sb.AppendLine();
            sb.AppendLine("Message: " + (message ?? string.Empty));
            sb.AppendLine("Detail: " + (detail ?? string.Empty));
            if (resolution != null)
            {
                sb.AppendLine("Resolution: " + resolution.Label + " / bpp=" + resolution.BitsPerPixel + " / exactMilliHz=" + resolution.RefreshRateMilliHz);
            }
            AppendMonitor(sb, "Monitor", monitor);
            if (exception != null)
            {
                sb.AppendLine();
                sb.AppendLine("Exception");
                sb.AppendLine(exception.ToString());
            }
            return sb.ToString();
        }

        public ProfileApplyResult LogImportProblem(ProfileApplyResult result, ProfileImportPlan plan, MonitorInfo targetMonitor, Exception exception)
        {
            if (result == null)
                result = ProfileApplyResult.Fail("Ошибка импорта.");

            if (result.Success)
                return result;
            if (result.ErrorCode == ProfileApplyErrorCode.UnsupportedMode)
                return result;

            result.NeedsDeveloperDiagnostics = true;

            try
            {
                string dir = GetDiagnosticsDirectory();
                Directory.CreateDirectory(dir);

                string file = Path.Combine(dir, "LOG.log");
                string text = BuildImportDiagnosticText(result, plan, targetMonitor, exception);
                string entry = BuildLogEntrySeparator() + text + Environment.NewLine;

                File.AppendAllText(file, entry, Utf8NoBom);

                result.DiagnosticDirectory = dir;
                result.DiagnosticFilePath = file;
                result.Log.Add("Diagnostic file: " + file);
            }
            catch (Exception logException)
            {
                result.Log.Add("Diagnostic log write failed: " + logException.Message);
            }

            return result;
        }

        private static string GetDiagnosticsDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
                appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
                appData = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appData, AppName);
        }

        private static string BuildLogEntrySeparator()
        {
            return LogEntrySeparator;
        }

        private static string BuildImportDiagnosticText(ProfileApplyResult result, ProfileImportPlan plan, MonitorInfo targetMonitor, Exception exception)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("DISPLAY-SCALER");
            sb.AppendLine("Time local: " + DateTime.Now.ToString("O"));
            sb.AppendLine("Time UTC:   " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine("App version: " + GetAppVersion());
            sb.AppendLine();

            sb.AppendLine("Result");
            sb.AppendLine("Success: " + (result != null && result.Success));
            sb.AppendLine("PartialSuccess: " + (result != null && result.PartialSuccess));
            sb.AppendLine("Message: " + (result == null ? string.Empty : result.Message));
            sb.AppendLine("Detail: " + (result == null ? string.Empty : result.Detail));
            sb.AppendLine("DriverRestartAttempted: " + (result != null && result.DriverRestartAttempted));
            sb.AppendLine("DriverRestartSucceeded: " + (result != null && result.DriverRestartSucceeded));
            sb.AppendLine();

            if (plan != null)
            {
                sb.AppendLine("Import plan");
                sb.AppendLine("FilePath: " + plan.FilePath);
                sb.AppendLine("CanImport: " + plan.CanImport);
                sb.AppendLine("RiskLevel: " + plan.RiskLevel);
                sb.AppendLine("RiskScore: " + plan.RiskScore);
                sb.AppendLine("Summary: " + plan.Summary);
                sb.AppendLine("AlreadyAvailableInWindows: " + plan.AlreadyAvailableInWindows);
                sb.AppendLine("ExactMonitorMatch: " + plan.ExactMonitorMatch);
                sb.AppendLine("SameNativeResolution: " + plan.SameNativeResolution);
                if (plan.RequestedResolution != null)
                    sb.AppendLine("RequestedResolution: " + plan.RequestedResolution.Label + " / bpp=" + plan.RequestedResolution.BitsPerPixel);
                if (plan.Resolution != null)
                    sb.AppendLine("Resolution: " + plan.Resolution.Label + " / bpp=" + plan.Resolution.BitsPerPixel);
                sb.AppendLine("WasResolutionAdaptedToTarget: " + plan.WasResolutionAdaptedToTarget);
                sb.AppendLine("ResolutionAdaptationReason: " + plan.ResolutionAdaptationReason);
                sb.AppendLine("WasRefreshAdaptedToTarget: " + plan.WasRefreshAdaptedToTarget);
                sb.AppendLine("RefreshAdaptationReason: " + plan.RefreshAdaptationReason);
                sb.AppendLine("HasOversizedFitFallback: " + plan.HasOversizedFitFallback);
                if (plan.FallbackResolution != null)
                    sb.AppendLine("FallbackResolution: " + plan.FallbackResolution.Label + " / bpp=" + plan.FallbackResolution.BitsPerPixel);
                sb.AppendLine("EstimatedPixelClockMhz: " + plan.EstimatedPixelClockMhz.ToString("F2"));
                AppendList(sb, "BlockingIssues", plan.BlockingIssues);
                AppendList(sb, "Warnings", plan.Warnings);
                AppendList(sb, "Checks", plan.Checks);
                sb.AppendLine();

                if (plan.Profile != null)
                {
                    sb.AppendLine("Profile");
                    sb.AppendLine("Application: " + plan.Profile.Application);
                    sb.AppendLine("SchemaVersion: " + plan.Profile.SchemaVersion);
                    sb.AppendLine("ProfileName: " + plan.Profile.ProfileName);
                    sb.AppendLine("ExportedUtc: " + plan.Profile.ExportedUtc);
                    sb.AppendLine("ExportedByAppVersion: " + plan.Profile.ExportedByAppVersion);
                    if (plan.Profile.Resolution != null)
                    {
                        sb.AppendLine("Profile resolution: " + plan.Profile.Resolution.Width + "x" + plan.Profile.Resolution.Height + "@" + plan.Profile.Resolution.RefreshRate + " bpp=" + plan.Profile.Resolution.BitsPerPixel);
                        sb.AppendLine("Profile exact refresh: " + FormatMilliHzInvariant(plan.Profile.Resolution.ExactRefreshRateMilliHz) + " Hz / policy=" + plan.Profile.Resolution.ExactRefreshRatePolicy);
                        sb.AppendLine("Profile source native refresh: " + FormatMilliHzInvariant(plan.Profile.Resolution.SourceNativeRefreshRateMilliHz) + " Hz");
                    }
                    AppendProfileMonitor(sb, "Source display", plan.Profile.SourceDisplay);
                    sb.AppendLine();
                }
            }

            AppendMonitor(sb, "Target monitor", targetMonitor);

            if (result != null)
                AppendList(sb, "Runtime log", result.Log);

            if (exception != null)
            {
                sb.AppendLine();
                sb.AppendLine("Exception");
                sb.AppendLine(exception.ToString());
            }

            return sb.ToString();
        }

        private static string FormatMilliHzInvariant(uint milliHz)
        {
            if (milliHz == 0)
                return "unknown";

            uint whole = milliHz / 1000U;
            uint fraction = milliHz % 1000U;
            if (fraction == 0U)
                return whole.ToString(System.Globalization.CultureInfo.InvariantCulture);

            return whole.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + fraction.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void AppendMonitor(StringBuilder sb, string title, MonitorInfo monitor)
        {
            sb.AppendLine(title);
            if (monitor == null)
            {
                sb.AppendLine("<null>");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("DeviceName: " + monitor.DeviceName);
            sb.AppendLine("NvApiDeviceName: " + monitor.NvApiDeviceName);
            sb.AppendLine("FriendlyName: " + monitor.FriendlyName);
            sb.AppendLine("Manufacturer: " + monitor.Manufacturer);
            sb.AppendLine("ProductCode: " + monitor.ProductCode);
            sb.AppendLine("SerialNumber: " + monitor.SerialNumber);
            sb.AppendLine("DeviceId: " + monitor.DeviceId);
            sb.AppendLine("AdapterName: " + monitor.AdapterName);
            sb.AppendLine("AdapterDeviceId: " + monitor.AdapterDeviceId);
            sb.AppendLine("IsAttached: " + monitor.IsAttached);
            sb.AppendLine("Native: " + monitor.NativeWidth + "x" + monitor.NativeHeight + " @ " + FormatMilliHzInvariant(monitor.NativeRefreshRateMilliHz) + " Hz");
            sb.AppendLine("NativeRefreshRational: " + monitor.NativeRefreshRateNumerator + "/" + monitor.NativeRefreshRateDenominator);
            sb.AppendLine("Current: " + monitor.CurrentWidth + "x" + monitor.CurrentHeight + "@" + monitor.CurrentRefreshRate + " exact=" + FormatMilliHzInvariant(monitor.CurrentRefreshRateMilliHz) + " Hz");
            sb.AppendLine("CurrentRefreshRational: " + monitor.CurrentRefreshRateNumerator + "/" + monitor.CurrentRefreshRateDenominator);
            sb.AppendLine("VerticalRange: " + monitor.MinVerticalRate + "-" + monitor.MaxVerticalRate);
            sb.AppendLine("MaxPixelClockMhz: " + monitor.MaxPixelClockMhz);
            sb.AppendLine();
        }

        private static void AppendProfileMonitor(StringBuilder sb, string title, DisplayProfileMonitor monitor)
        {
            sb.AppendLine(title);
            if (monitor == null)
            {
                sb.AppendLine("<null>");
                return;
            }

            sb.AppendLine("DeviceName: " + monitor.DeviceName);
            sb.AppendLine("FriendlyName: " + monitor.FriendlyName);
            sb.AppendLine("Manufacturer: " + monitor.Manufacturer);
            sb.AppendLine("ProductCode: " + monitor.ProductCode);
            sb.AppendLine("SerialNumber: " + monitor.SerialNumber);
            sb.AppendLine("DeviceId: " + monitor.DeviceId);
            sb.AppendLine("AdapterName: " + monitor.AdapterName);
            sb.AppendLine("AdapterDeviceId: " + monitor.AdapterDeviceId);
            sb.AppendLine("Native: " + monitor.NativeWidth + "x" + monitor.NativeHeight + " @ " + FormatMilliHzInvariant(monitor.NativeRefreshRateMilliHz) + " Hz");
            sb.AppendLine("NativeRefreshRational: " + monitor.NativeRefreshRateNumerator + "/" + monitor.NativeRefreshRateDenominator);
            sb.AppendLine("Current: " + monitor.CurrentWidth + "x" + monitor.CurrentHeight + "@" + monitor.CurrentRefreshRate + " exact=" + FormatMilliHzInvariant(monitor.CurrentRefreshRateMilliHz) + " Hz");
            sb.AppendLine("CurrentRefreshRational: " + monitor.CurrentRefreshRateNumerator + "/" + monitor.CurrentRefreshRateDenominator);
            sb.AppendLine("VerticalRange: " + monitor.MinVerticalRate + "-" + monitor.MaxVerticalRate);
            sb.AppendLine("MaxPixelClockMhz: " + monitor.MaxPixelClockMhz);
            sb.AppendLine("Fingerprint: " + monitor.Fingerprint);
        }

        private static void AppendList(StringBuilder sb, string title, IList<string> list)
        {
            sb.AppendLine(title);
            if (list == null || list.Count == 0)
            {
                sb.AppendLine("  <empty>");
                return;
            }

            for (int i = 0; i < list.Count; i++)
                sb.AppendLine("  - " + list[i]);
        }

        private static string FormatApplyResult(ApplyResult result)
        {
            if (result == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(result.Detail))
                return result.Detail;
            return result.Message ?? string.Empty;
        }
    }
}

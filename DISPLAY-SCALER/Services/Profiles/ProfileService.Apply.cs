using DISPLAY_SCALER.Infrastructure.Timing;
using DISPLAY_SCALER.Models;
using System;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class ResolutionProfileService
    {
        public ProfileApplyResult ApplyImport(ProfileImportPlan plan, MonitorInfo targetMonitor)
        {
            return ApplyImport(plan, targetMonitor, null);
        }

        public ProfileApplyResult ApplyImport(ProfileImportPlan plan, MonitorInfo targetMonitor, Func<CustomResolution, int, bool> confirmTrial)
        {
            if (plan == null || plan.Profile == null)
                return LogImportProblem(ProfileApplyResult.Fail(ProfileApplyErrorCode.MissingPlan, "План импорта отсутствует."), plan, targetMonitor, null);
            if (!plan.CanImport)
                return LogImportProblem(ProfileApplyResult.Fail(ProfileApplyErrorCode.CompatibilityBlocked, "Импорт заблокирован проверками совместимости.", BuildIssueText(plan.BlockingIssues)), plan, targetMonitor, null);
            if (targetMonitor == null)
                return LogImportProblem(ProfileApplyResult.Fail(ProfileApplyErrorCode.TargetMonitorMissing, "Целевой монитор не выбран."), plan, targetMonitor, null);

            var res = Normalize(plan.Resolution);
            var result = new ProfileApplyResult();
            if (res == null)
                return LogImportProblem(ProfileApplyResult.Fail(ProfileApplyErrorCode.MissingPlan, "Разрешение для импорта отсутствует."), plan, targetMonitor, null);

            bool forceDriverCustomSave = RequiresDriverCustomSave(plan.Profile);

            ApplyResult preflightRefresh = _display.RefreshWindowsModeList(targetMonitor);
            result.Log.Add("Preflight Windows/NVIDIA mode-list refresh before import apply: " + FormatApplyResult(preflightRefresh));
            NativeWait.Sleep(300);

            bool alreadyInWindows = plan.AlreadyAvailableInWindows || _display.IsWindowsModeEnumerated(targetMonitor, res);
            if (alreadyInWindows && !forceDriverCustomSave)
            {
                bool exactTransfer = ShouldPreserveUserEditedRefresh(plan.Profile);
                bool targetRefreshMatches = _display.IsNvidiaCustomModeTargetRefresh(targetMonitor, res, out var precisionDetail);
                bool nvidiaVisible = _display.IsNvidiaCustomModeVisible(targetMonitor, res, out _);
                bool targetMonitorExactRefreshKnown = TryVerifyKnownTargetExactRefresh(targetMonitor, res, out var knownExactDetail);

                bool exactRefreshVerified = !exactTransfer || targetRefreshMatches || targetMonitorExactRefreshKnown;
                if ((!nvidiaVisible || targetRefreshMatches) && exactRefreshVerified)
                {
                    _display.MarkDisplayScalerResolution(targetMonitor, res);
                    result.Success = true;
                    result.Message = "Импорт успешен!";
                    result.Detail = string.Empty;
                    result.Log.Add("Mode already exists in Windows mode list with verified target refresh: " + res.Label + ". " + precisionDetail + " " + knownExactDetail);
                    return result;
                }

                if (exactTransfer && !exactRefreshVerified)
                    result.Log.Add("Windows reports a nominally matching mode, but exact milli-Hz refresh cannot be verified for exact-transfer import; NVAPI custom trial/save will be performed. " + precisionDetail + " " + knownExactDetail);
                else
                    result.Log.Add("Mode already exists, but NVIDIA custom timing has несовпадающую refresh metadata and will be recreated: " + precisionDetail);
            }

            if (alreadyInWindows && forceDriverCustomSave)
            {
                result.Log.Add("Импорт CRU/EDID .bin требует реального NVAPI trial/save, поэтому существующий Windows mode не считается завершённым импортом.");
            }

            if (_display.HasNativeRefreshHijackRisk(targetMonitor, res, out var pendingHijackReason))
            {
                result.Log.Add("Native/current mode preflight warning: " + pendingHijackReason + " Operation is not blocked; the real NVAPI trial and post-save native/current guard will decide.");
            }

            ApplyResult add = _display.AddCustomResolution(targetMonitor, res, confirmTrial, forceDriverCustomSave);
            result.Log.Add("AddCustomResolution: " + FormatApplyResult(add));
            if (!add.Success)
            {
                if (add.ErrorCode == ApplyErrorCode.UserCancelled)
                    return BuildUserCancelledResult(res, add, result);

                if (IsUnsupportedModeFailure(add))
                    return BuildUnsupportedModeResult(res, add, result);

                if (ShouldAttemptRecoveryAfterAddFailure(add.Message))
                {
                    result.DriverRestartAttempted = true;
                    result.Log.Add("NVAPI verification failed through EnumCustomDisplay. Starting display-adapter recovery restart.");
                    DriverRestartResult recovery = _driverRestart.RestartDisplayAdapter(targetMonitor);
                    result.DriverRestartSucceeded = recovery.Success;
                    result.Log.Add("Driver restart: " + (recovery.Success ? "OK" : "FAILED") + ". " + (recovery.CommandLine ?? string.Empty));
                    if (!string.IsNullOrWhiteSpace(recovery.Output))
                        result.Log.Add("Driver restart stdout: " + recovery.Output.Trim());
                    if (!string.IsNullOrWhiteSpace(recovery.Error))
                        result.Log.Add("Driver restart stderr: " + recovery.Error.Trim());

                    NativeWait.Sleep(1500);
                    ApplyResult refreshAfterRestart = _display.RefreshWindowsModeList(targetMonitor);
                    result.Log.Add("Windows mode-list refresh after restart: " + FormatApplyResult(refreshAfterRestart));
                    bool visibleAfterRestart = _display.IsNvidiaCustomModeVisible(targetMonitor, res, out var enumError);
                    result.Log.Add("EnumCustomDisplay after restart: " + (visibleAfterRestart ? "FOUND" : (enumError ?? "NOT FOUND")));

                    if (visibleAfterRestart)
                        return LogIfFailed(VerifyWindowsSideAfterNvidiaSave(result, targetMonitor, res), plan, targetMonitor);
                }

                result.Success = false;
                result.ErrorCode = ProfileApplyErrorCode.DisplayApplyFailed;
                result.Message = "Импорт не выполнен.";
                result.Detail = add.Message;
                return LogImportProblem(result, plan, targetMonitor, null);
            }

            NativeWait.Sleep(500);
            ProfileApplyResult verified = VerifyWindowsSideAfterNvidiaSave(result, targetMonitor, res);
            if (!verified.Success && ShouldAttemptRecoveryAfterVerificationFailure(verified))
                verified = TryRecoverAfterPostSaveVerificationFailure(verified, targetMonitor, res);
            return LogIfFailed(verified, plan, targetMonitor);
        }

        private static bool TryVerifyKnownTargetExactRefresh(MonitorInfo targetMonitor, CustomResolution resolution, out string detail)
        {
            detail = "Known target exact refresh is unavailable.";
            if (targetMonitor == null || resolution == null || resolution.RefreshRateMilliHz == 0)
                return false;

            if (targetMonitor.CurrentWidth == resolution.Width &&
                targetMonitor.CurrentHeight == resolution.Height &&
                targetMonitor.CurrentRefreshRateMilliHz > 0)
            {
                long delta = Math.Abs((long)targetMonitor.CurrentRefreshRateMilliHz - (long)resolution.RefreshRateMilliHz);
                detail = "Current target exact refresh delta=" + delta + " mHz.";
                if (delta <= 1)
                    return true;
            }

            if (targetMonitor.NativeWidth == resolution.Width &&
                targetMonitor.NativeHeight == resolution.Height &&
                targetMonitor.NativeRefreshRateMilliHz > 0)
            {
                long delta = Math.Abs((long)targetMonitor.NativeRefreshRateMilliHz - (long)resolution.RefreshRateMilliHz);
                detail = "Native target exact refresh delta=" + delta + " mHz.";
                if (delta <= 1)
                    return true;
            }

            return false;
        }

        private static bool RequiresDriverCustomSave(DisplayScalerProfile profile)
        {
            if (profile == null)
                return false;

            if (string.Equals(profile.ExportKind, "CRU_EDID_BIN", StringComparison.OrdinalIgnoreCase))
                return true;

            string resolutionTiming = profile.Resolution == null ? null : profile.Resolution.TimingMode;
            if (!string.IsNullOrWhiteSpace(resolutionTiming) &&
                resolutionTiming.IndexOf("CRU", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string hintTiming = profile.CreationHints == null ? null : profile.CreationHints.TimingMode;
            return !string.IsNullOrWhiteSpace(hintTiming) &&
                   hintTiming.IndexOf("CRU", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private ProfileApplyResult TryRecoverAfterPostSaveVerificationFailure(ProfileApplyResult result, MonitorInfo targetMonitor, CustomResolution res)
        {
            if (result == null)
                result = new ProfileApplyResult();

            result.DriverRestartAttempted = true;
            result.Log.Add("Post-save verification failed. Starting display-adapter recovery restart.");

            DriverRestartResult recovery = _driverRestart.RestartDisplayAdapter(targetMonitor);
            result.DriverRestartSucceeded = recovery.Success;
            result.Log.Add("Driver restart: " + (recovery.Success ? "OK" : "FAILED") + ". " + (recovery.CommandLine ?? string.Empty));
            if (!string.IsNullOrWhiteSpace(recovery.Output))
                result.Log.Add("Driver restart stdout: " + recovery.Output.Trim());
            if (!string.IsNullOrWhiteSpace(recovery.Error))
                result.Log.Add("Driver restart stderr: " + recovery.Error.Trim());

            NativeWait.Sleep(1500);
            ApplyResult refreshAfterRestart = _display.RefreshWindowsModeList(targetMonitor);
            result.Log.Add("Windows mode-list refresh after restart: " + FormatApplyResult(refreshAfterRestart));

            result.Success = false;
            result.ErrorCode = ProfileApplyErrorCode.DriverRecoveryFailed;
            result.PartialSuccess = false;
            result.Message = null;
            result.Detail = null;
            return VerifyWindowsSideAfterNvidiaSave(result, targetMonitor, res);
        }

        private static bool ShouldAttemptRecoveryAfterVerificationFailure(ProfileApplyResult result)
        {
            if (result == null || result.Success || result.DriverRestartAttempted)
                return false;
            if (result.ErrorCode == ProfileApplyErrorCode.UnsupportedMode)
                return false;

            string text = ((result.Message ?? string.Empty) + " " + (result.Detail ?? string.Empty)).Trim();
            return text.IndexOf("не подтвержд", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("не выполнен", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("mode list", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("EnumDisplaySettingsEx", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("EnumCustomDisplay", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private ProfileApplyResult VerifyWindowsSideAfterNvidiaSave(ProfileApplyResult result, MonitorInfo targetMonitor, CustomResolution res)
        {
            string visibility = _display.GetWindowsModeVisibilityText(targetMonitor, res);
            result.Log.Add("Windows mode list: " + visibility);

            if (_display.IsWindowsModeEnumerated(targetMonitor, res))
            {
                _display.MarkDisplayScalerResolution(targetMonitor, res);
                result.Success = true;
                result.Message = "Импорт успешен!";
                result.Detail = string.Empty;
                result.Log.Add("Import verified: mode exists in normal Windows EnumDisplaySettingsEx list.");
                return result;
            }

            bool rawVisible = _display.IsWindowsRawModeEnumerated(targetMonitor, res);
            bool nvVisible = _display.IsNvidiaCustomModeVisible(targetMonitor, res, out var enumError);
            ApplyResult winTest = _display.TestDisplayMode(targetMonitor, res);
            result.Log.Add("CDS_TEST: " + FormatApplyResult(winTest));

            result.Success = false;
            result.ErrorCode = ProfileApplyErrorCode.DisplayApplyFailed;
            result.FailedResolution = res;
            result.PartialSuccess = false;
            result.Message = "Разрешение сохранено в NVIDIA, но не появилось в обычном списке разрешений Windows.";
            result.Detail = "Для игр нужен normal EnumDisplaySettingsEx mode list. Состояние: RAW=" + rawVisible +
                ", NVIDIA custom-list=" + nvVisible +
                ", CDS_TEST=" + FormatApplyResult(winTest) +
                (string.IsNullOrWhiteSpace(enumError) ? "." : "; EnumCustomDisplay: " + enumError);
            result.Log.Add("Import verification failed: normal Windows EnumDisplaySettingsEx list does not contain the mode. RAW=" + rawVisible + "; NVIDIA custom-list=" + nvVisible + ".");
            return result;
        }

        private ProfileApplyResult LogIfFailed(ProfileApplyResult result, ProfileImportPlan plan, MonitorInfo targetMonitor)
        {
            if (result != null && !result.Success && result.ErrorCode != ProfileApplyErrorCode.UnsupportedMode)
                return LogImportProblem(result, plan, targetMonitor, null);
            return result;
        }

        private static bool IsUnsupportedModeFailure(ApplyResult add)
        {
            if (add == null || add.Success)
                return false;

            return add.ErrorCode == ApplyErrorCode.NvApiRejectedMode ||
                   add.ErrorCode == ApplyErrorCode.MonitorRangeViolation;
        }

        private static ProfileApplyResult BuildUnsupportedModeResult(CustomResolution resolution, ApplyResult add, ProfileApplyResult existing)
        {
            ProfileApplyResult result = existing ?? new ProfileApplyResult();
            result.Success = false;
            result.PartialSuccess = false;
            result.ErrorCode = ProfileApplyErrorCode.UnsupportedMode;
            result.FailedResolution = resolution;
            result.Message = "Монитор не поддерживает данное разрешение с этой частотой.";
            result.Detail = add == null ? string.Empty : ((add.Message ?? string.Empty) + (string.IsNullOrWhiteSpace(add.Detail) ? string.Empty : " " + add.Detail));
            if (resolution != null)
                result.Log.Add("Unsupported mode rejected by NVAPI/Windows test: " + resolution.Label + ".");
            if (add != null)
                result.Log.Add("ApplyResult: " + FormatApplyResult(add));
            return result;
        }

        private static ProfileApplyResult BuildUserCancelledResult(CustomResolution resolution, ApplyResult add, ProfileApplyResult existing)
        {
            ProfileApplyResult result = existing ?? new ProfileApplyResult();
            result.Success = false;
            result.PartialSuccess = false;
            result.ErrorCode = ProfileApplyErrorCode.UserCancelled;
            result.FailedResolution = resolution;
            result.Message = "Операция отменена.";
            result.Detail = add == null ? string.Empty : ((add.Message ?? string.Empty) + (string.IsNullOrWhiteSpace(add.Detail) ? string.Empty : " " + add.Detail));
            if (resolution != null)
                result.Log.Add("User cancelled trial confirmation for " + resolution.Label + ".");
            return result;
        }

        private static bool ShouldAttemptRecoveryAfterAddFailure(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            return message.IndexOf("не появился", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("EnumCustomDisplay", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("вернул OK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("Windows mode list", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("не показывает", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
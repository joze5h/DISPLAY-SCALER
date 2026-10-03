using DISPLAY_SCALER.Infrastructure.Timing;
using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Native;
using System;
using System.Collections.Generic;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class DisplayService
    {
        private const int CustomResolutionTrialConfirmationSeconds = 20;

        public ApplyResult AddCustomResolution(MonitorInfo monitor, CustomResolution res)
        {
            return AddCustomResolution(monitor, res, null);
        }

        public ApplyResult AddCustomResolution(MonitorInfo monitor, CustomResolution res, Func<CustomResolution, int, bool> confirmTrial)
        {
            return AddCustomResolution(monitor, res, confirmTrial, false);
        }

        public ApplyResult AddCustomResolution(MonitorInfo monitor, CustomResolution res, Func<CustomResolution, int, bool> confirmTrial, bool forceRecreateExisting)
        {
            ApplyResult validation = ValidateCustomResolutionRequest(monitor, res);
            if (!validation.Success)
                return validation;

            var normalized = NormalizeResolution(res);
            List<CustomResolution> preExistingDisplayScalerModes = CapturePreExistingDisplayScalerModes(monitor, normalized);

            string nativeHijackPreflightText = string.Empty;
            if (HasNativeRefreshHijackRisk(monitor, normalized, out var nativeHijackRisk))
            {
                nativeHijackPreflightText = "Native/current mode preflight warning: " + nativeHijackRisk + " Operation is not blocked; real NVAPI trial and post-save guard will decide. ";
            }

            ProtectedNativeModeSnapshot nativeModeGuard = CaptureProtectedNativeModes(monitor, normalized);

            if (_nv == null || !_nv.IsInitialized)
                return ApplyResult.Fail(ApplyErrorCode.NvApiNotInitialized, "NVAPI не инициализирован. Кастомное разрешение не было добавлено в NVIDIA-драйвер.");

            string staleNvidiaModeText = string.Empty;
            CustomResolution replacedNvidiaModeBackup = null;
            if (IsNvapiModeVisible(monitor, normalized, out var enumError))
            {
                ApplyResult existingModeRefreshResult = RefreshWindowsModeList(monitor);
                NativeWait.Sleep(500);

                bool targetRefreshMatches = IsNvidiaCustomModeTargetRefresh(monitor, normalized, out var precisionDetail);
                bool existingModeAlreadyRolledBackByGuard = false;

                if (!forceRecreateExisting && IsWindowsModeEnumerated(monitor, normalized) && targetRefreshMatches)
                {
                    ApplyResult existingGuardResult = VerifyProtectedNativeModesAfterCustomSave(monitor, normalized, nativeModeGuard);
                    if (existingGuardResult.Success)
                    {
                        RestorePreExistingDisplayScalerModes(monitor, preExistingDisplayScalerModes);
                        RegisterDisplayScalerMode(monitor, normalized);
                        res.AddedByDisplayScaler = true;
                        string existingModeRefreshText = existingModeRefreshResult.Success ? existingModeRefreshResult.Detail : existingModeRefreshResult.Message;
                        return ApplyResult.Ok(FormatMode(normalized) + " уже существует в NVIDIA custom display list, имеет корректную целевую частоту, виден обычному Windows mode list и не перехватывает native/current режимы; marker DISPLAY-SCALER обновлён. " + precisionDetail + " " + existingModeRefreshText + " " + existingGuardResult.Detail);
                    }

                    staleNvidiaModeText = "Существующий NVIDIA custom mode перехватывал или скрывал native/current режим и был отправлен в rollback guard. " + existingGuardResult.Detail + " ";
                    existingModeAlreadyRolledBackByGuard = true;
                }

                if (existingModeAlreadyRolledBackByGuard)
                {
                    ApplyResult refreshAfterGuardRollback = RefreshWindowsModeList(monitor);
                    NativeWait.Sleep(300);
                    staleNvidiaModeText = string.Concat(
                        staleNvidiaModeText,
                        "Обновление mode list после guard rollback: ",
                        FormatApplyResult(refreshAfterGuardRollback),
                        " ");
                }
                else
                {
                    replacedNvidiaModeBackup = CaptureNvidiaModeBackup(monitor, normalized);
                    ApplyResult deleteExisting = DeleteCustomDisplayVariants(monitor, normalized, true);
                    if (deleteExisting.Success)
                    {
                        ApplyResult refreshAfterDelete = RefreshWindowsModeList(monitor);
                        NativeWait.Sleep(300);
                        string refreshAfterDeleteText = refreshAfterDelete.Success ? refreshAfterDelete.Detail : refreshAfterDelete.Message;
                        staleNvidiaModeText = "Существующий NVIDIA custom mode удалён и создаётся заново через target-refresh TryCustomDisplay -> проверка active mode -> SaveCustomDisplay. " + precisionDetail + " " + FormatApplyResult(deleteExisting) + " " + refreshAfterDeleteText + " ";
                    }
                    else
                    {
                        return ApplyResult.Fail("Режим уже есть в NVIDIA custom display list, но его нужно пересоздать для корректного exact-refresh импорта. Удаление не удалось: " + FormatApplyResult(deleteExisting) + ". " + precisionDetail);
                    }
                }
            }

            ApplyResult trialSave = TryNvidiaTrialThenSave(monitor, normalized, confirmTrial);
            if (!trialSave.Success)
            {
                string rollbackText = TryRestoreReplacedNvidiaMode(monitor, replacedNvidiaModeBackup);
                return ApplyResult.Fail(
                    trialSave.ErrorCode == ApplyErrorCode.None ? ApplyErrorCode.Unknown : trialSave.ErrorCode,
                    staleNvidiaModeText + trialSave.Message +
                    (string.IsNullOrWhiteSpace(trialSave.Detail) ? string.Empty : " " + trialSave.Detail) +
                    rollbackText);
            }

            if (!IsNvapiModeVisible(monitor, normalized, out enumError))
            {
                string detail = string.IsNullOrWhiteSpace(enumError) ? string.Empty : " " + enumError;
                return ApplyResult.Fail(ApplyErrorCode.ModeNotVisibleInWindows, staleNvidiaModeText + "NvAPI_DISP_SaveCustomDisplay вернул OK после успешного trial-теста, но режим не появился в NvAPI_DISP_EnumCustomDisplay." + detail + " " + trialSave.Detail);
            }

            if (!IsNvidiaCustomModeTargetRefresh(monitor, normalized, out var savedPrecisionDetail))
                return ApplyResult.Fail(ApplyErrorCode.ModeNotVisibleInWindows, staleNvidiaModeText + "NvAPI сохранил режим, но частота в custom timing не совпадает с целевой native/nominal частотой. " + savedPrecisionDetail + " " + trialSave.Detail);

            ApplyResult refreshResult = RefreshWindowsModeList(monitor);
            NativeWait.Sleep(900);

            string refreshText = refreshResult.Success
                ? refreshResult.Detail
                : refreshResult.Message;

            ApplyResult nativeGuardResult = VerifyProtectedNativeModesAfterCustomSave(monitor, normalized, nativeModeGuard);
            if (!nativeGuardResult.Success)
            {
                string rollbackText = TryRestoreReplacedNvidiaMode(monitor, replacedNvidiaModeBackup);
                return ApplyResult.Fail(
                    nativeGuardResult.ErrorCode == ApplyErrorCode.None ? ApplyErrorCode.Unknown : nativeGuardResult.ErrorCode,
                    staleNvidiaModeText + nativeGuardResult.Message,
                    nativeGuardResult.Detail + " " + trialSave.Detail + " " + refreshText + rollbackText);
            }

            if (IsWindowsModeEnumerated(monitor, normalized))
            {
                RestorePreExistingDisplayScalerModes(monitor, preExistingDisplayScalerModes);
                RegisterDisplayScalerMode(monitor, normalized);
                res.AddedByDisplayScaler = true;
                return ApplyResult.Ok(staleNvidiaModeText + nativeHijackPreflightText + FormatMode(normalized) + " прошёл NVAPI trial-тест, сохранён через NvAPI_DISP_SaveCustomDisplay и появился в обычном Windows mode list. Предыдущие marker DISPLAY-SCALER сохранены. " + trialSave.Detail + " " + refreshText);
            }

            bool rawVisibleAfterSave = IsWindowsRawModeEnumerated(monitor, normalized);
            ApplyResult winTestAfterSave = TestDisplayMode(monitor, normalized);
            string postSaveVisibility = rawVisibleAfterSave
                ? "Режим уже виден через EDS_RAWMODE."
                : "Режим пока не виден через EDS_RAWMODE.";

            RestorePreExistingDisplayScalerModes(monitor, preExistingDisplayScalerModes);
            RegisterDisplayScalerMode(monitor, normalized);
            res.AddedByDisplayScaler = true;

            return ApplyResult.Ok(
                staleNvidiaModeText + nativeHijackPreflightText + FormatMode(normalized) +
                " прошёл NVAPI trial-тест, был подтверждён пользователем и сохранён через NvAPI_DISP_SaveCustomDisplay. " +
                "Обычный Windows mode list ещё не показал режим сразу после сохранения, но NVIDIA custom display list уже подтвердил режим; DISPLAY-SCALER не откатывает такой успешный save. " +
                postSaveVisibility + " CDS_TEST после save: " + FormatApplyResult(winTestAfterSave) + ". Предыдущие marker DISPLAY-SCALER сохранены. " +
                trialSave.Detail + " " + refreshText);
        }

        private CustomResolution CaptureNvidiaModeBackup(MonitorInfo monitor, CustomResolution target)
        {
            if (monitor == null || target == null || _nv == null || !_nv.IsInitialized)
                return null;

            List<CustomResolution> modes = _nv.EnumerateCustomDisplays(GetNvapiDeviceName(monitor), out var enumError);
            if (!string.IsNullOrWhiteSpace(enumError))
                System.Diagnostics.Debug.WriteLine("NVIDIA mode backup enumeration failed: " + enumError);

            for (int i = 0; i < modes.Count; i++)
            {
                CustomResolution mode = modes[i];
                if (ModesMatch(mode, target))
                    return NormalizeResolution(mode);
            }

            return null;
        }

        private string TryRestoreReplacedNvidiaMode(MonitorInfo monitor, CustomResolution backup)
        {
            if (monitor == null || backup == null || _nv == null || !_nv.IsInitialized)
                return string.Empty;

            if (IsNvapiModeVisible(monitor, backup, out var enumError))
                return " Rollback: исходный NVIDIA custom mode уже присутствует; повторное создание не требуется.";

            if (_nv.RegisterCustomDisplay(
                    GetNvapiDeviceName(monitor),
                    backup.Width,
                    backup.Height,
                    backup.RefreshRate,
                    backup.RefreshRateMilliHz,
                    backup.BitsPerPixel,
                    null,
                    out var restoreError))
            {
                ApplyResult refresh = RefreshWindowsModeList(monitor);
                NativeWait.Sleep(500);
                return " Rollback: исходный NVIDIA custom mode " + backup.Label + " восстановлен после неудачного пересоздания. " + FormatApplyResult(refresh);
            }

            string enumerationText = string.IsNullOrWhiteSpace(enumError) ? string.Empty : " Enum before rollback: " + enumError + ".";
            return " Rollback warning: исходный NVIDIA custom mode " + backup.Label + " не удалось восстановить: " + (restoreError ?? "unknown error") + "." + enumerationText;
        }

        private static List<CustomResolution> CapturePreExistingDisplayScalerModes(MonitorInfo monitor, CustomResolution excludingMode)
        {
            var result = new List<CustomResolution>();
            if (monitor == null || monitor.CustomResolutions == null)
                return result;

            for (int i = 0; i < monitor.CustomResolutions.Count; i++)
            {
                CustomResolution existing = monitor.CustomResolutions[i];
                if (existing == null || !existing.AddedByDisplayScaler)
                    continue;
                if (ModesMatch(existing, excludingMode))
                    continue;

                result.Add(new CustomResolution
                {
                    Width = existing.Width,
                    Height = existing.Height,
                    RefreshRate = existing.RefreshRate,
                    RefreshRateMilliHz = existing.RefreshRateMilliHz,
                    BitsPerPixel = existing.BitsPerPixel == 0 ? DefaultBitsPerPixel : existing.BitsPerPixel,
                    AddedByDisplayScaler = true
                });
            }

            return result;
        }

        private void RestorePreExistingDisplayScalerModes(MonitorInfo monitor, List<CustomResolution> modes)
        {
            if (monitor == null || modes == null || modes.Count == 0)
                return;

            for (int i = 0; i < modes.Count; i++)
            {
                CustomResolution mode = modes[i];
                if (mode != null)
                    RegisterDisplayScalerMode(monitor, mode);
            }
        }

        private ApplyResult TryNvidiaTrialThenSave(MonitorInfo monitor, CustomResolution normalized, Func<CustomResolution, int, bool> confirmTrial)
        {
            string currentBeforeText = "unknown";
            if (TryGetCurrentDevMode(monitor, out var currentBefore))
                currentBeforeText = FormatDevMode(currentBefore);

            ActiveDisplayModeSnapshot desktopModeGuard = CaptureActiveDisplayModeSnapshot(monitor == null ? null : monitor.DeviceName);

            ApplyResult unsafePreflight = TryEnableUnsafeWindowsModesForTargetDisplay(monitor);
            NativeWait.Sleep(300);

            bool trialStarted = TryBeginCustomDisplayTrialUsingDocumentedPlan(
                monitor,
                normalized,
                out var nvapiError,
                out var selectedTrialPlanText,
                out var rejectedTrialPlanText);

            if (!trialStarted)
                return ApplyResult.Fail(ApplyErrorCode.NvApiRejectedMode,
                    string.IsNullOrWhiteSpace(nvapiError)
                    ? "NvAPI_DISP_TryCustomDisplay отклонил кастомное разрешение."
                    : nvapiError,
                    "Pre-trial unsafe-mode publish: " + FormatApplyResult(unsafePreflight) + " " + rejectedTrialPlanText);

            bool trialApplied = WaitForCurrentDisplayMode(monitor, normalized, 5000, out var currentAfterTrialText);
            string trialDetectionText = (string.IsNullOrWhiteSpace(selectedTrialPlanText) ? string.Empty : selectedTrialPlanText + " ") +
                (trialApplied
                ? "Windows current mode confirmed the tested custom mode: " + currentAfterTrialText + "."
                : "Windows current mode did not report the tested custom mode before the confirmation dialog: " + currentAfterTrialText + ". DISPLAY-SCALER still opens the NVIDIA-style countdown because NvAPI_DISP_TryCustomDisplay already returned OK; this avoids false rejection on GPU-scaled/secondary-monitor trials where EnumDisplaySettingsEx can keep reporting the scan-out/native timing.");

            if (confirmTrial != null)
            {
                bool userConfirmed;
                try
                {
                    userConfirmed = confirmTrial(normalized, CustomResolutionTrialConfirmationSeconds);
                }
                catch (Exception ex)
                {
                    _nv.RevertCustomDisplayTrial(GetNvapiDeviceName(monitor), out var revertAfterCallbackError);
                    string desktopRestore = RestoreNonTargetDisplayModes(desktopModeGuard);
                    return ApplyResult.Fail(
                        "Окно подтверждения custom mode завершилось ошибкой, trial-режим отменён.",
                        "До trial: " + currentBeforeText + "; " + trialDetectionText + " UI exception: " + ex.Message + ". " +
                        (string.IsNullOrWhiteSpace(revertAfterCallbackError) ? "Trial был отменён." : "RevertCustomDisplayTrial: " + revertAfterCallbackError) +
                        " " + desktopRestore);
                }

                if (!userConfirmed)
                {
                    _nv.RevertCustomDisplayTrial(GetNvapiDeviceName(monitor), out var revertError);
                    string desktopRestore = RestoreNonTargetDisplayModes(desktopModeGuard);
                    return ApplyResult.Fail(
                        ApplyErrorCode.UserCancelled,
                        "Сохранение custom resolution отменено пользователем или таймером.",
                        "До trial: " + currentBeforeText + "; " + trialDetectionText + " " +
                        (string.IsNullOrWhiteSpace(revertError) ? "Trial был отменён." : "RevertCustomDisplayTrial: " + revertError) +
                        " " + desktopRestore);
                }
            }

            bool saved = _nv.SaveCustomDisplayTrial(GetNvapiDeviceName(monitor), out nvapiError);
            string saveText = string.IsNullOrWhiteSpace(nvapiError) ? "NvAPI_DISP_SaveCustomDisplay: OK." : nvapiError;
            if (!saved)
            {
                _nv.RevertCustomDisplayTrial(GetNvapiDeviceName(monitor), out var revertError);
                string desktopRestore = RestoreNonTargetDisplayModes(desktopModeGuard);
                return ApplyResult.Fail(
                    ApplyErrorCode.NvApiRejectedMode,
                    string.IsNullOrWhiteSpace(nvapiError) ? "NvAPI_DISP_SaveCustomDisplay не сохранил режим после успешного trial-теста." : nvapiError,
                    "Trial был подтверждён пользователем. Pre-trial unsafe-mode publish: " + FormatApplyResult(unsafePreflight) + " " + trialDetectionText + " " +
                    (string.IsNullOrWhiteSpace(revertError) ? "Trial был отменён." : "RevertCustomDisplayTrial: " + revertError) +
                    " " + desktopRestore);
            }

            NativeWait.Sleep(500);

            ApplyResult publicationWhileTrialActive = EnsureSavedCustomModeIsPublishedWhileTrialModeIsActive(monitor, normalized, desktopModeGuard);

            string targetRestoreAfterSave = RestoreTargetDisplayModeAfterConfirmedSave(monitor, currentBefore, currentBeforeText);
            string desktopRestoreAfterSave = RestoreNonTargetDisplayModes(desktopModeGuard);

            ApplyResult publicationAfterRestore = EnsureSavedCustomModeIsPublishedToNormalWindowsList(monitor, normalized, desktopModeGuard);
            ApplyResult publication = publicationAfterRestore;
            if (!publication.Success)
            {
                bool savedNvapiVisible = IsNvapiModeVisible(monitor, normalized, out var savedNvapiEnumError);
                string savedNvapiRefreshDetail = savedNvapiVisible ? string.Empty : "NvAPI custom mode was not visible after save.";
                bool savedNvapiRefreshOk = savedNvapiVisible && IsNvidiaCustomModeTargetRefresh(monitor, normalized, out savedNvapiRefreshDetail);
                if (savedNvapiRefreshOk)
                {
                    return ApplyResult.Ok("До trial: " + currentBeforeText + "; " + trialDetectionText + " " + saveText +
                        " Trial-режим сохранён в NVIDIA custom display list с точной целевой частотой, но обычный Windows EnumDisplaySettingsEx ещё не показал режим после refresh. " +
                        "DISPLAY-SCALER не откатывает успешный NVIDIA save как ошибку: режим подтверждён через NvAPI_DISP_EnumCustomDisplay. " +
                        "Pre-trial unsafe-mode publish: " + FormatApplyResult(unsafePreflight) + " " +
                        "Publication while trial active: " + FormatApplyResult(publicationWhileTrialActive) + " " +
                        targetRestoreAfterSave + " " + desktopRestoreAfterSave + " Publication after restore: " + FormatApplyResult(publicationAfterRestore) +
                        " NvAPI saved mode check: " + savedNvapiRefreshDetail +
                        (string.IsNullOrWhiteSpace(savedNvapiEnumError) ? string.Empty : " EnumCustomDisplay: " + savedNvapiEnumError));
                }

                return ApplyResult.Fail(
                    ApplyErrorCode.ModeNotVisibleInWindows,
                    "NvAPI_DISP_SaveCustomDisplay сохранил режим после успешного trial-теста, но Windows не опубликовал его в обычный список разрешений целевого монитора.",
                    "До trial: " + currentBeforeText + "; Pre-trial unsafe-mode publish: " + FormatApplyResult(unsafePreflight) + " " + trialDetectionText + " " + saveText + " " +
                    "Publication while trial active: " + FormatApplyResult(publicationWhileTrialActive) + " " +
                    targetRestoreAfterSave + " " + desktopRestoreAfterSave + " Publication after restore: " + FormatApplyResult(publicationAfterRestore) +
                    " NvAPI saved mode visible: " + savedNvapiVisible + ". " + savedNvapiRefreshDetail +
                    (string.IsNullOrWhiteSpace(savedNvapiEnumError) ? string.Empty : " EnumCustomDisplay: " + savedNvapiEnumError));
            }

            return ApplyResult.Ok("До trial: " + currentBeforeText + "; Pre-trial unsafe-mode publish: " + FormatApplyResult(unsafePreflight) + " " + trialDetectionText + " " + saveText +
                " Trial-режим сохранён в NVIDIA custom display list; normal Windows mode list был проверен сначала при активном trial-режиме, затем target откатан к исходному активному режиму. " +
                "Publication while trial active: " + FormatApplyResult(publicationWhileTrialActive) + " " +
                targetRestoreAfterSave + " " + desktopRestoreAfterSave + " Publication after restore: " + FormatApplyResult(publicationAfterRestore));
        }

        private ApplyResult EnsureSavedCustomModeIsPublishedWhileTrialModeIsActive(MonitorInfo monitor, CustomResolution normalized, ActiveDisplayModeSnapshot desktopModeGuard)
        {
            if (monitor == null || normalized == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return ApplyResult.Fail(ApplyErrorCode.ModeNotVisibleInWindows, "Trial-active publication skipped: target display is not defined.");

            if (WaitForNormalWindowsModeEnumeration(monitor, normalized, 6500, out var firstWaitText))
                return ApplyResult.Ok("Trial-active publication: normal EnumDisplaySettingsEx contains " + FormatMode(normalized) + " while target is still in tested mode. " + firstWaitText);

            ApplyResult forceEnumeration = ForceWindowsModeEnumeration();
            string desktopRestore = RestoreNonTargetDisplayModes(desktopModeGuard);
            NativeWait.Sleep(900);

            if (WaitForNormalWindowsModeEnumeration(monitor, normalized, 6500, out var secondWaitText))
                return ApplyResult.Ok("Trial-active publication: normal EnumDisplaySettingsEx contains " + FormatMode(normalized) + " after SDC_FORCE_MODE_ENUMERATION while target is still in tested mode. " + FormatApplyResult(forceEnumeration) + " " + desktopRestore + " " + secondWaitText);

            ApplyResult unsafeModes = TryEnableUnsafeWindowsModesForTargetDisplay(monitor);
            NativeWait.Sleep(900);

            if (WaitForNormalWindowsModeEnumeration(monitor, normalized, 6500, out var unsafeWaitText))
                return ApplyResult.Ok("Trial-active publication: normal EnumDisplaySettingsEx contains " + FormatMode(normalized) + " after CDS_ENABLE_UNSAFE_MODES while target is still in tested mode. " + FormatApplyResult(forceEnumeration) + " " + desktopRestore + " " + FormatApplyResult(unsafeModes) + " " + unsafeWaitText);

            bool rawVisible = IsWindowsRawModeEnumerated(monitor, normalized);
            bool nvVisible = IsNvapiModeVisible(monitor, normalized, out var enumError);
            return ApplyResult.Fail(
                ApplyErrorCode.ModeNotVisibleInWindows,
                "Trial-active publication failed: normal EnumDisplaySettingsEx still does not contain " + FormatMode(normalized) + ".",
                "Initial wait while trial active: " + firstWaitText + " Force enumeration: " + FormatApplyResult(forceEnumeration) + " " + desktopRestore +
                " Second wait while trial active: " + secondWaitText + " Unsafe modes: " + FormatApplyResult(unsafeModes) +
                " Unsafe wait while trial active: " + unsafeWaitText + " Raw mode visible: " + rawVisible + ". NVIDIA custom-list visible: " + nvVisible +
                (string.IsNullOrWhiteSpace(enumError) ? "." : "; EnumCustomDisplay: " + enumError));
        }

        private string RestoreTargetDisplayModeAfterConfirmedSave(MonitorInfo monitor, Win32Display.DEVMODE currentBefore, string currentBeforeText)
        {
            if (monitor == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return "Target restore skipped: target display is not defined.";

            string revertText;
            if (_nv != null && _nv.IsInitialized)
            {
                bool reverted = _nv.RevertCustomDisplayTrial(GetNvapiDeviceName(monitor), out var revertError);
                revertText = reverted
                    ? "RevertCustomDisplayTrial restored the temporary target trial mode."
                    : "RevertCustomDisplayTrial did not restore target trial mode: " + (revertError ?? "unknown error") + ".";
                NativeWait.Sleep(650);
            }
            else
            {
                revertText = "RevertCustomDisplayTrial skipped: NVAPI is not initialized.";
            }

            if (TryGetCurrentDevMode(monitor, out var afterRevert) && DevModesSameDesktopModeWithoutPosition(afterRevert, currentBefore))
                return revertText + " Target active mode after save/revert: " + FormatDevMode(afterRevert) + ".";

            if (currentBefore.dmPelsWidth == 0 || currentBefore.dmPelsHeight == 0)
                return revertText + " Target fallback restore skipped: original mode is unknown.";

            Win32Display.DEVMODE restore = currentBefore;
            PrepareEnumeratedModeForRestore(ref restore);

            int test = Win32Display.ChangeDisplaySettingsEx(monitor.DeviceName, ref restore, IntPtr.Zero, Win32Display.CDS_TEST, IntPtr.Zero);
            if (test != Win32Display.DISP_CHANGE_SUCCESSFUL)
                return revertText + " Target fallback restore to " + currentBeforeText + " skipped: CDS_TEST " + Win32Display.ChangeResultToString(test) + ".";

            int apply = Win32Display.ChangeDisplaySettingsEx(monitor.DeviceName, ref restore, IntPtr.Zero, Win32Display.CDS_FULLSCREEN, IntPtr.Zero);
            NativeWait.Sleep(650);

            return revertText + " Target fallback restore to " + currentBeforeText + ": " + Win32Display.ChangeResultToString(apply) + ".";
        }

        private ApplyResult EnsureSavedCustomModeIsPublishedToNormalWindowsList(MonitorInfo monitor, CustomResolution normalized, ActiveDisplayModeSnapshot desktopModeGuard)
        {
            if (monitor == null || normalized == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return ApplyResult.Fail(ApplyErrorCode.ModeNotVisibleInWindows, "Windows publication skipped: target display is not defined.");

            if (WaitForNormalWindowsModeEnumeration(monitor, normalized, 3500, out var firstWaitText))
                return ApplyResult.Ok("Windows mode publication: normal EnumDisplaySettingsEx contains " + FormatMode(normalized) + " after Save/Revert. " + firstWaitText);

            ApplyResult forceEnumeration = ForceWindowsModeEnumeration();
            string desktopRestore = RestoreNonTargetDisplayModes(desktopModeGuard);
            NativeWait.Sleep(700);

            if (WaitForNormalWindowsModeEnumeration(monitor, normalized, 4500, out var secondWaitText))
                return ApplyResult.Ok("Windows mode publication: normal EnumDisplaySettingsEx contains " + FormatMode(normalized) + " after SDC_FORCE_MODE_ENUMERATION. " + FormatApplyResult(forceEnumeration) + " " + desktopRestore + " " + secondWaitText);

            ApplyResult unsafeModes = TryEnableUnsafeWindowsModesForTargetDisplay(monitor);
            NativeWait.Sleep(700);

            if (WaitForNormalWindowsModeEnumeration(monitor, normalized, 4500, out var unsafeWaitText))
                return ApplyResult.Ok("Windows mode publication: normal EnumDisplaySettingsEx contains " + FormatMode(normalized) + " after CDS_ENABLE_UNSAFE_MODES target publish. " + FormatApplyResult(forceEnumeration) + " " + desktopRestore + " " + FormatApplyResult(unsafeModes) + " " + unsafeWaitText);

            ApplyResult registryPublish = TryPublishRawCustomModeToWindowsUserRegistryWithoutActivating(monitor, normalized, desktopModeGuard);
            NativeWait.Sleep(700);

            if (WaitForNormalWindowsModeEnumeration(monitor, normalized, 6500, out var thirdWaitText))
                return ApplyResult.Ok("Windows mode publication: normal EnumDisplaySettingsEx contains " + FormatMode(normalized) + " after unsafe-mode publish and CDS_UPDATEREGISTRY|CDS_NORESET staging from RAW DEVMODE. " + FormatApplyResult(forceEnumeration) + " " + desktopRestore + " " + FormatApplyResult(unsafeModes) + " " + FormatApplyResult(registryPublish) + " " + thirdWaitText);

            bool rawVisible = IsWindowsRawModeEnumerated(monitor, normalized);
            bool nvVisible = IsNvapiModeVisible(monitor, normalized, out var enumError);
            ApplyResult registrySettings = CheckWindowsRegistrySettings(monitor, normalized);
            return ApplyResult.Fail(
                ApplyErrorCode.ModeNotVisibleInWindows,
                "Windows mode publication failed: normal EnumDisplaySettingsEx still does not contain " + FormatMode(normalized) + ".",
                "Initial wait: " + firstWaitText + " Force enumeration: " + FormatApplyResult(forceEnumeration) + " " + desktopRestore +
                " Second wait: " + secondWaitText + " Unsafe modes: " + FormatApplyResult(unsafeModes) +
                " Unsafe wait: " + unsafeWaitText + " Registry staging: " + FormatApplyResult(registryPublish) +
                " Third wait: " + thirdWaitText + " Registry settings: " + FormatApplyResult(registrySettings) +
                " Raw mode visible: " + rawVisible + ". NVIDIA custom-list visible: " + nvVisible +
                (string.IsNullOrWhiteSpace(enumError) ? "." : "; EnumCustomDisplay: " + enumError));
        }

        private ApplyResult TryEnableUnsafeWindowsModesForTargetDisplay(MonitorInfo monitor)
        {
            if (monitor == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return ApplyResult.Fail("CDS_ENABLE_UNSAFE_MODES skipped: target display is not defined.");

            int result = Win32Display.ChangeDisplaySettingsEx(
                monitor.DeviceName,
                IntPtr.Zero,
                IntPtr.Zero,
                Win32Display.CDS_ENABLE_UNSAFE_MODES,
                IntPtr.Zero);

            PrimeWindowsModeEnumeration(monitor);

            if (result == Win32Display.DISP_CHANGE_SUCCESSFUL)
                return ApplyResult.Ok("CDS_ENABLE_UNSAFE_MODES accepted for target display; Windows should stop pruning driver-reported custom modes by monitor capability filter.");

            int globalResult = Win32Display.ChangeDisplaySettingsEx(
                null,
                IntPtr.Zero,
                IntPtr.Zero,
                Win32Display.CDS_ENABLE_UNSAFE_MODES,
                IntPtr.Zero);

            PrimeWindowsModeEnumeration(monitor);

            if (globalResult == Win32Display.DISP_CHANGE_SUCCESSFUL)
                return ApplyResult.Ok("CDS_ENABLE_UNSAFE_MODES target call returned " + Win32Display.ChangeResultToString(result) + ", but global unsafe-mode enable was accepted; Windows should stop pruning driver-reported custom modes by monitor capability filter.");

            return ApplyResult.Fail("CDS_ENABLE_UNSAFE_MODES failed: target=" + Win32Display.ChangeResultToString(result) + ", global=" + Win32Display.ChangeResultToString(globalResult) + ".");
        }

        private ApplyResult TryPublishRawCustomModeToWindowsUserRegistryWithoutActivating(MonitorInfo monitor, CustomResolution normalized, ActiveDisplayModeSnapshot desktopModeGuard)
        {
            if (monitor == null || normalized == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return ApplyResult.Fail("Registry staging skipped: target display is not defined.");

            if (!TryGetMatchingDevMode(monitor, normalized, Win32Display.EDS_RAWMODE, out var rawMode))
                return ApplyResult.Fail("Registry staging skipped: RAW DEVMODE for " + FormatMode(normalized) + " was not found.");

            string currentBeforeText = "unknown";
            Win32Display.DEVMODE currentBefore = new Win32Display.DEVMODE();
            bool hasCurrentBefore = TryGetCurrentDevMode(monitor, out currentBefore);
            if (hasCurrentBefore)
                currentBeforeText = FormatDevMode(currentBefore);

            rawMode.dmFields |= Win32Display.DM_PELSWIDTH |
                                Win32Display.DM_PELSHEIGHT |
                                Win32Display.DM_BITSPERPEL |
                                Win32Display.DM_DISPLAYFREQUENCY;

            if (hasCurrentBefore && (currentBefore.dmFields & Win32Display.DM_POSITION) != 0)
            {
                rawMode.dmPositionX = currentBefore.dmPositionX;
                rawMode.dmPositionY = currentBefore.dmPositionY;
                rawMode.dmFields |= Win32Display.DM_POSITION;
            }

            int test = Win32Display.ChangeDisplaySettingsEx(
                monitor.DeviceName,
                ref rawMode,
                IntPtr.Zero,
                Win32Display.CDS_TEST,
                IntPtr.Zero);

            if (test != Win32Display.DISP_CHANGE_SUCCESSFUL)
                return ApplyResult.Fail("Registry staging CDS_TEST failed: " + Win32Display.ChangeResultToString(test) + ".");

            int stage = Win32Display.ChangeDisplaySettingsEx(
                monitor.DeviceName,
                ref rawMode,
                IntPtr.Zero,
                Win32Display.CDS_UPDATEREGISTRY | Win32Display.CDS_NORESET,
                IntPtr.Zero);

            string stageFlagsText = "CDS_UPDATEREGISTRY|CDS_NORESET";
            if (stage != Win32Display.DISP_CHANGE_SUCCESSFUL && stage != Win32Display.DISP_CHANGE_RESTART)
            {
                int stageUnsafe = Win32Display.ChangeDisplaySettingsEx(
                    monitor.DeviceName,
                    ref rawMode,
                    IntPtr.Zero,
                    Win32Display.CDS_UPDATEREGISTRY | Win32Display.CDS_NORESET | Win32Display.CDS_ENABLE_UNSAFE_MODES,
                    IntPtr.Zero);

                if (stageUnsafe == Win32Display.DISP_CHANGE_SUCCESSFUL || stageUnsafe == Win32Display.DISP_CHANGE_RESTART)
                {
                    stage = stageUnsafe;
                    stageFlagsText = "CDS_UPDATEREGISTRY|CDS_NORESET|CDS_ENABLE_UNSAFE_MODES";
                }
            }

            string activeModeGuardText = EnsureTargetModeStillActiveAfterRegistryStaging(monitor, currentBefore, currentBeforeText, hasCurrentBefore);
            string nonTargetRestore = RestoreNonTargetDisplayModes(desktopModeGuard);
            PrimeWindowsModeEnumeration(monitor);

            bool stageAccepted = stage == Win32Display.DISP_CHANGE_SUCCESSFUL ||
                                 stage == Win32Display.DISP_CHANGE_RESTART;

            if (!stageAccepted)
            {
                return ApplyResult.Fail(
                    "Registry staging failed: " + Win32Display.ChangeResultToString(stage) + ".",
                    "CDS_TEST succeeded, but registry staging did not. Last attempted flags: " + stageFlagsText + ". " + activeModeGuardText + " " + nonTargetRestore);
            }

            string stageText = stage == Win32Display.DISP_CHANGE_RESTART
                ? "Registry staging returned DISP_CHANGE_RESTART; mode was stored but Windows may need an additional display-settings refresh/restart before it appears everywhere."
                : "Registry staging returned DISP_CHANGE_SUCCESSFUL.";

            return ApplyResult.Ok(
                "Registry staging OK",
                "CDS_TEST accepted RAW DEVMODE; " + stageText + " Flags: " + stageFlagsText + ". The mode was staged with CDS_NORESET, so DISPLAY-SCALER did not activate it. " + activeModeGuardText + " " + nonTargetRestore);
        }

        private string EnsureTargetModeStillActiveAfterRegistryStaging(MonitorInfo monitor, Win32Display.DEVMODE currentBefore, string currentBeforeText, bool hasCurrentBefore)
        {
            if (!hasCurrentBefore || monitor == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return "Target active-mode guard skipped: original mode is unknown.";

            if (TryGetCurrentDevMode(monitor, out var currentAfter) && DevModesSameDesktopModeWithoutPosition(currentAfter, currentBefore))
                return "Target active mode unchanged after registry staging: " + FormatDevMode(currentAfter) + ".";

            Win32Display.DEVMODE restore = currentBefore;
            PrepareEnumeratedModeForRestore(ref restore);

            int test = Win32Display.ChangeDisplaySettingsEx(monitor.DeviceName, ref restore, IntPtr.Zero, Win32Display.CDS_TEST, IntPtr.Zero);
            if (test != Win32Display.DISP_CHANGE_SUCCESSFUL)
                return "Target active-mode guard could not restore " + currentBeforeText + ": CDS_TEST " + Win32Display.ChangeResultToString(test) + ".";

            int apply = Win32Display.ChangeDisplaySettingsEx(monitor.DeviceName, ref restore, IntPtr.Zero, Win32Display.CDS_FULLSCREEN, IntPtr.Zero);
            NativeWait.Sleep(500);
            return "Target active-mode guard restored " + currentBeforeText + " after registry staging: " + Win32Display.ChangeResultToString(apply) + ".";
        }

        private static ApplyResult CheckWindowsRegistrySettings(MonitorInfo monitor, CustomResolution normalized)
        {
            if (monitor == null || normalized == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return ApplyResult.Fail("Registry settings check skipped: target display is not defined.");

            try
            {
                var dm = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, Win32Display.ENUM_REGISTRY_SETTINGS, ref dm, 0) == 0)
                    return ApplyResult.Fail("EnumDisplaySettingsEx/ENUM_REGISTRY_SETTINGS returned no mode.");

                uint bpp = dm.dmBitsPerPel == 0 ? DefaultBitsPerPixel : dm.dmBitsPerPel;
                uint refresh = dm.dmDisplayFrequency == 0 ? normalized.RefreshRate : dm.dmDisplayFrequency;
                bool matches = dm.dmPelsWidth == normalized.Width &&
                               dm.dmPelsHeight == normalized.Height &&
                               Math.Abs((long)refresh - (long)normalized.RefreshRate) <= 1 &&
                               bpp >= DefaultBitsPerPixel;

                return matches
                    ? ApplyResult.Ok("ENUM_REGISTRY_SETTINGS contains " + FormatDevMode(dm) + ".")
                    : ApplyResult.Fail("ENUM_REGISTRY_SETTINGS is " + FormatDevMode(dm) + ", not " + FormatMode(normalized) + ".");
            }
            catch (Exception ex)
            {
                return ApplyResult.Fail("Registry settings check exception: " + ex.Message);
            }
        }

        private static bool WaitForNormalWindowsModeEnumeration(MonitorInfo monitor, CustomResolution normalized, int timeoutMs, out string detail)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs <= 0 ? 1 : timeoutMs);
            int attempts = 0;
            do
            {
                attempts++;
                PrimeWindowsModeEnumeration(monitor);
                if (TryGetMatchingDevMode(monitor, normalized, 0, out var match))
                {
                    detail = "Found after " + attempts + " attempt(s): " + FormatDevMode(match) + ".";
                    return true;
                }

                NativeWait.Sleep(250);
            }
            while (DateTime.UtcNow < deadline);

            detail = "Not found after " + attempts + " attempt(s) / " + timeoutMs + " ms.";
            return false;
        }

        private static bool DevModesSameDesktopModeWithoutPosition(Win32Display.DEVMODE a, Win32Display.DEVMODE b)
        {
            uint aRefresh = a.dmDisplayFrequency;
            uint bRefresh = b.dmDisplayFrequency;
            if (aRefresh == 0 || bRefresh == 0)
                aRefresh = bRefresh = Math.Max(aRefresh, bRefresh);

            uint aBpp = a.dmBitsPerPel == 0 ? DefaultBitsPerPixel : a.dmBitsPerPel;
            uint bBpp = b.dmBitsPerPel == 0 ? DefaultBitsPerPixel : b.dmBitsPerPel;

            return a.dmPelsWidth == b.dmPelsWidth &&
                   a.dmPelsHeight == b.dmPelsHeight &&
                   Math.Abs((long)aRefresh - (long)bRefresh) <= 1 &&
                   aBpp == bBpp;
        }

        private bool TryBeginCustomDisplayTrialUsingDocumentedPlan(MonitorInfo monitor, CustomResolution target, out string error, out string selectedPlanText, out string rejectedPlanText)
        {
            error = null;
            selectedPlanText = string.Empty;
            rejectedPlanText = string.Empty;

            if (monitor == null || target == null)
            {
                error = "Target monitor or custom resolution is not defined.";
                return false;
            }

            bool exactStarted = _nv.BeginCustomDisplayTrial(
                GetNvapiDeviceName(monitor),
                target.Width,
                target.Height,
                target.RefreshRate,
                target.RefreshRateMilliHz,
                target.BitsPerPixel,
                null,
                true,
                true,
                out var exactError);

            if (exactStarted)
            {
                selectedPlanText = "Trial strategy: exact requested-source timing first. No GPU-scaled surrogate timing was needed.";
                return true;
            }

            rejectedPlanText = "Exact requested-source timing was rejected before countdown: " + (string.IsNullOrWhiteSpace(exactError) ? "unknown NVAPI rejection" : exactError) + ".";

            List<NvApi.CustomDisplayTimingBase> gpuScaledTimingBases = BuildGpuScaledFallbackTimingBases(monitor, target);
            if (gpuScaledTimingBases.Count == 0)
            {
                error = exactError;
                rejectedPlanText += " GPU-scaled fallback was not attempted because no safe target-monitor timing base was available.";
                return false;
            }

            bool fallbackStarted = _nv.BeginCustomDisplayTrial(
                GetNvapiDeviceName(monitor),
                target.Width,
                target.Height,
                target.RefreshRate,
                target.RefreshRateMilliHz,
                target.BitsPerPixel,
                gpuScaledTimingBases,
                false,
                false,
                out var fallbackError);

            if (fallbackStarted)
            {
                selectedPlanText = "Trial strategy: exact requested-source timing was rejected, then GPU-scaled source-mode fallback was accepted. Visible custom mode stays " + FormatMode(target) + "; transport timing uses a target-monitor geometry at the same requested refresh.";
                return true;
            }

            error = fallbackError;
            rejectedPlanText += " GPU-scaled source-mode fallback was also rejected before countdown: " + (string.IsNullOrWhiteSpace(fallbackError) ? "unknown NVAPI rejection" : fallbackError) + ".";
            return false;
        }

        private static void PrepareEnumeratedModeForRestore(ref Win32Display.DEVMODE mode)
        {
            uint fields = mode.dmFields;
            if (mode.dmPelsWidth > 0)
                fields |= Win32Display.DM_PELSWIDTH;
            if (mode.dmPelsHeight > 0)
                fields |= Win32Display.DM_PELSHEIGHT;
            if (mode.dmBitsPerPel > 0)
                fields |= Win32Display.DM_BITSPERPEL;
            if (mode.dmDisplayFrequency > 0)
                fields |= Win32Display.DM_DISPLAYFREQUENCY;

            mode.dmFields = fields;
        }

        private static List<NvApi.CustomDisplayTimingBase> BuildGpuScaledFallbackTimingBases(MonitorInfo monitor, CustomResolution target)
        {
            var result = new List<NvApi.CustomDisplayTimingBase>(8);
            if (monitor == null || target == null)
                return result;

            AddGpuScaledTimingBase(result, target, monitor.CurrentWidth, monitor.CurrentHeight, "GPU-scaled fallback: current target geometry at requested refresh");
            AddGpuScaledTimingBase(result, target, monitor.NativeWidth, monitor.NativeHeight, "GPU-scaled fallback: native target geometry at requested refresh");

            ResolutionMode largestSameRefresh = null;
            ResolutionMode largestKnownMode = null;
            if (monitor.SupportedModes != null)
            {
                for (int i = 0; i < monitor.SupportedModes.Count; i++)
                {
                    ResolutionMode mode = monitor.SupportedModes[i];
                    if (mode == null || mode.Width == 0 || mode.Height == 0)
                        continue;
                    if (mode.BitsPerPixel != 0 && mode.BitsPerPixel < DefaultBitsPerPixel)
                        continue;
                    if (mode.IsInterlaced)
                        continue;

                    if (largestKnownMode == null || ((long)mode.Width * mode.Height) > ((long)largestKnownMode.Width * largestKnownMode.Height))
                        largestKnownMode = mode;

                    if (Math.Abs((long)mode.RefreshRate - (long)target.RefreshRate) <= 1 &&
                        (largestSameRefresh == null || ((long)mode.Width * mode.Height) > ((long)largestSameRefresh.Width * largestSameRefresh.Height)))
                    {
                        largestSameRefresh = mode;
                    }
                }
            }

            if (largestSameRefresh != null)
                AddGpuScaledTimingBase(result, target, largestSameRefresh.Width, largestSameRefresh.Height, "GPU-scaled fallback: largest enumerated target geometry already available at requested refresh");

            if (largestKnownMode != null)
                AddGpuScaledTimingBase(result, target, largestKnownMode.Width, largestKnownMode.Height, "GPU-scaled fallback: largest enumerated target geometry recalculated at requested refresh");

            return result;
        }

        private static void AddGpuScaledTimingBase(List<NvApi.CustomDisplayTimingBase> result, CustomResolution target, uint timingWidth, uint timingHeight, string label)
        {
            if (result == null || target == null || timingWidth == 0 || timingHeight == 0 || target.RefreshRate == 0)
                return;

            if (timingWidth == target.Width && timingHeight == target.Height)
                return;

            AddTimingBase(result, timingWidth, timingHeight, target.RefreshRate, target.RefreshRateMilliHz, label);
        }

        private static void AddTimingBase(List<NvApi.CustomDisplayTimingBase> result, uint width, uint height, uint refresh, uint refreshMilliHz, string label)
        {
            if (result == null || width == 0 || height == 0 || refresh == 0) return;
            uint normalizedMilliHz = NormalizeRefreshMilliHz(refresh, refreshMilliHz);
            for (int i = 0; i < result.Count; i++)
            {
                NvApi.CustomDisplayTimingBase existing = result[i];
                if (existing.Width == width &&
                    existing.Height == height &&
                    Math.Abs((long)existing.RefreshRate - (long)refresh) <= 1 &&
                    existing.RefreshRateMilliHz == normalizedMilliHz)
                {
                    return;
                }
            }

            result.Add(new NvApi.CustomDisplayTimingBase(width, height, refresh, normalizedMilliHz, label));
        }

        public ApplyResult RemoveCustomResolution(MonitorInfo monitor, CustomResolution res)
        {
            if (monitor == null || res == null)
                return ApplyResult.Fail("Монитор или разрешение не определены.");

            if (!res.CanDelete || res.Origin == ResolutionOrigin.System)
            {
                return ApplyResult.Fail(
                    "Системное или штатное разрешение защищено от удаления.",
                    "Удалять можно только режимы из разделов «КАСТОМНЫЕ» и «ДОБАВЛЕНО ЧЕРЕЗ DISPLAY-SCALER». Источник: " + res.SourceLabel + ".");
            }

            var normalized = NormalizeResolution(res);
            if (_nv == null || !_nv.IsInitialized)
                return ApplyResult.Fail("NVAPI не инициализирован. Удаление NVIDIA custom display mode невозможно выполнить корректно.");

            // Удаляем только выбранный custom mode. Удаление всех вариантов той же геометрии
            // опасно: оно может затронуть другой кастомный режим с иной частотой.
            ApplyResult deleteResult = DeleteCustomDisplayVariants(monitor, normalized, true);

            bool nonStandardChanged = Win32Monitor.RemoveNonStandardMode(
                monitor.DeviceId,
                normalized.Width,
                normalized.Height,
                normalized.RefreshRate,
                out var nonStandardDetail);
            ApplyResult refresh = RefreshWindowsModeList(monitor);
            NativeWait.Sleep(600);

            bool stillVisible = IsNvapiModeVisible(monitor, normalized, out var enumError);
            if (stillVisible)
            {
                return ApplyResult.Fail(
                    "Удаление не завершено: выбранный NVIDIA custom mode всё ещё виден после DeleteCustomDisplay.",
                    FormatApplyResult(deleteResult) + " Refresh: " + FormatApplyResult(refresh));
            }

            bool deletedFromDriver = ApplyResultHasUsefulDeletion(deleteResult) || nonStandardChanged;
            bool stillVisibleInWindows = IsWindowsModeEnumerated(monitor, normalized);
            if (deletedFromDriver && stillVisibleInWindows)
            {
                for (int attempt = 0; attempt < 4 && stillVisibleInWindows; attempt++)
                {
                    ApplyResult retryRefresh = RefreshWindowsModeList(monitor);
                    NativeWait.Sleep(400);
                    stillVisibleInWindows = IsWindowsModeEnumerated(monitor, normalized);
                    if (!stillVisibleInWindows)
                    {
                        refresh = retryRefresh;
                        break;
                    }
                }
            }

            bool markerChanged = false;
            if (deletedFromDriver || !stillVisibleInWindows)
                markerChanged = RemoveDisplayScalerMode(monitor, normalized);

            bool deletedOrOwnedByDisplayScaler = deletedFromDriver || markerChanged;

            if (deletedOrOwnedByDisplayScaler && stillVisibleInWindows)
            {
                return ApplyResult.Ok(
                    "Удалено из хранилищ DISPLAY-SCALER/NVIDIA, но обычный Windows mode list ещё может показывать режим до обновления драйвера/перечисления.",
                    FormatApplyResult(deleteResult) + " Marker removed: " + markerChanged + ". NonStandardModes changed: " + nonStandardChanged + ". " +
                    (string.IsNullOrWhiteSpace(nonStandardDetail) ? string.Empty : "NonStandardModes: " + nonStandardDetail + " ") +
                    "Refresh: " + FormatApplyResult(refresh) + " EnumCustomDisplay: " + (enumError ?? string.Empty));
            }

            if (!deleteResult.Success)
            {
                if (markerChanged || nonStandardChanged)
                    return ApplyResult.Ok("NVAPI custom mode не был удалён полностью, но локальные записи DISPLAY-SCALER/DAL очищены. " + FormatApplyResult(deleteResult) + " Marker removed: " + markerChanged + ". NonStandardModes changed: " + nonStandardChanged + ". " + (nonStandardDetail ?? string.Empty) + " Refresh: " + FormatApplyResult(refresh));
                return deleteResult;
            }

            if (!ApplyResultHasUsefulDeletion(deleteResult) && !markerChanged && !nonStandardChanged)
            {
                if (IsWindowsModeEnumerated(monitor, normalized))
                {
                    return ApplyResult.Fail(
                        "Режим не удалён: он есть в Windows mode list, но не найден в NVIDIA custom display list.",
                        "Такой режим может быть EDID/driver/native mode либо stale-записью вне NvAPI. " +
                        "Если это конфликтующий NVIDIA custom mode, удали его в ручную. Refresh: " + FormatApplyResult(refresh));
                }

                return ApplyResult.Ok("Запись не найдена в NVIDIA custom display list и marker DISPLAY-SCALER не найден. Refresh: " + FormatApplyResult(refresh));
            }

            return ApplyResult.Ok((markerChanged
                ? "Удалено через NvAPI_DISP_DeleteCustomDisplay; marker DISPLAY-SCALER очищен. "
                : "Удалено через NvAPI_DISP_DeleteCustomDisplay. ") +
                "NonStandardModes changed: " + nonStandardChanged + ". " + (nonStandardDetail ?? string.Empty) + " " +
                FormatApplyResult(deleteResult) + " Refresh: " + FormatApplyResult(refresh));
        }

        private ApplyResult DeleteCustomDisplayVariants(MonitorInfo monitor, CustomResolution normalized, bool switchAwayFirst)
        {
            if (monitor == null || normalized == null)
                return ApplyResult.Fail("Монитор или разрешение не определены.");
            if (_nv == null || !_nv.IsInitialized)
                return ApplyResult.Fail("NVAPI не инициализирован.");

            int deletedCount = 0;
            var details = new List<string>(8);
            string lastEnumError = null;

            for (int pass = 0; pass < 16; pass++)
            {
                List<CustomResolution> modes = _nv.EnumerateCustomDisplays(GetNvapiDeviceName(monitor), out lastEnumError);
                CustomResolution deleteCandidate = null;
                for (int i = 0; i < modes.Count; i++)
                {
                    CustomResolution mode = modes[i];
                    if (mode == null)
                        continue;

                    bool exactMatch = ModesMatch(mode, normalized);
                    if (exactMatch)
                    {
                        deleteCandidate = NormalizeResolution(mode);
                        break;
                    }
                }

                if (deleteCandidate == null)
                    break;

                if (switchAwayFirst)
                {
                    if (!TrySwitchAwayFromModeBeforeDelete(monitor, deleteCandidate, out var switchDetail))
                    {
                        return ApplyResult.Fail(
                            "Удаление NVIDIA custom mode остановлено: не удалось безопасно переключиться с потенциально активного удаляемого режима.",
                            (switchDetail ?? "Current desktop mode could not be verified or switched safely.") +
                            " Deleted before failure: " + deletedCount + ". " + string.Join(" ", details));
                    }

                    if (!string.IsNullOrWhiteSpace(switchDetail))
                        details.Add(switchDetail);
                }

                if (!_nv.DeleteCustomDisplay(
                        GetNvapiDeviceName(monitor),
                        deleteCandidate.Width,
                        deleteCandidate.Height,
                        deleteCandidate.RefreshRate,
                        deleteCandidate.RefreshRateMilliHz,
                        deleteCandidate.BitsPerPixel,
                        out var deleteError))
                {
                    return ApplyResult.Fail(
                        "NvAPI_DISP_DeleteCustomDisplay отклонил удаление режима " + deleteCandidate.Label + ".",
                        (deleteError ?? string.Empty) + " Deleted before failure: " + deletedCount + ". " + string.Join(" ", details));
                }

                deletedCount++;
                details.Add("Deleted " + deleteCandidate.Label + " from NVIDIA custom display list.");
                NativeWait.Sleep(350);
            }

            if (deletedCount == 0)
            {
                if (!string.IsNullOrWhiteSpace(lastEnumError))
                    return ApplyResult.Fail("Не удалось перечислить NVIDIA custom display list перед удалением.", lastEnumError);
                return ApplyResult.Ok("NVIDIA custom display entry was not found.");
            }

            return ApplyResult.Ok("Deleted NVIDIA custom display entries: " + deletedCount + ". " + string.Join(" ", details));
        }

        private bool TrySwitchAwayFromModeBeforeDelete(MonitorInfo monitor, CustomResolution deleting, out string detail)
        {
            detail = null;
            if (monitor == null || deleting == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return false;
            if (!TryGetCurrentDevMode(monitor, out var current))
                return false;

            var currentRes = new CustomResolution
            {
                Width = current.dmPelsWidth,
                Height = current.dmPelsHeight,
                RefreshRate = current.dmDisplayFrequency == 0 ? deleting.RefreshRate : current.dmDisplayFrequency,
                BitsPerPixel = current.dmBitsPerPel == 0 ? DefaultBitsPerPixel : current.dmBitsPerPel
            };

            if (!WindowsModeCouldMatchDeletingMode(currentRes, deleting))
                return true;
            if (!TryGetSafeDevModeForDeletion(monitor, deleting, out var safeMode))
            {
                detail = "Deletion pre-switch skipped: safe non-matching desktop mode was not found.";
                return false;
            }

            int result = Win32Display.ChangeDisplaySettingsEx(monitor.DeviceName, ref safeMode, IntPtr.Zero, Win32Display.CDS_FULLSCREEN, IntPtr.Zero);
            NativeWait.Sleep(700);
            detail = result == Win32Display.DISP_CHANGE_SUCCESSFUL
                ? "Switched away from active custom mode before delete to " + FormatDevMode(safeMode) + "."
                : "Attempted to switch away before delete, result: " + Win32Display.ChangeResultToString(result) + ".";
            return result == Win32Display.DISP_CHANGE_SUCCESSFUL;
        }

        private static bool TryGetSafeDevModeForDeletion(MonitorInfo monitor, CustomResolution deleting, out Win32Display.DEVMODE safeMode)
        {
            safeMode = new Win32Display.DEVMODE();
            if (monitor == null || deleting == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return false;

            Win32Display.DEVMODE bestDifferentRefresh = new Win32Display.DEVMODE();
            Win32Display.DEVMODE bestAnyRefresh = new Win32Display.DEVMODE();
            int bestDifferentScore = int.MinValue;
            int bestAnyScore = int.MinValue;

            uint nativeWidth = monitor.NativeWidth > 0 ? monitor.NativeWidth : monitor.CurrentWidth;
            uint nativeHeight = monitor.NativeHeight > 0 ? monitor.NativeHeight : monitor.CurrentHeight;
            uint currentWidth = monitor.CurrentWidth;
            uint currentHeight = monitor.CurrentHeight;

            for (int i = 0; i < MaxDisplayModeEnumeration; i++)
            {
                var dm = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, i, ref dm, 0) == 0)
                    break;

                if (dm.dmPelsWidth == 0 || dm.dmPelsHeight == 0)
                    continue;
                uint bpp = dm.dmBitsPerPel == 0 ? DefaultBitsPerPixel : dm.dmBitsPerPel;
                if (bpp < DefaultBitsPerPixel)
                    continue;

                var candidate = new CustomResolution
                {
                    Width = dm.dmPelsWidth,
                    Height = dm.dmPelsHeight,
                    RefreshRate = dm.dmDisplayFrequency == 0 ? 60U : dm.dmDisplayFrequency,
                    BitsPerPixel = bpp
                };
                if (WindowsModeCouldMatchDeletingMode(candidate, deleting))
                    continue;

                bool nativeSize = nativeWidth > 0 && nativeHeight > 0 && candidate.Width == nativeWidth && candidate.Height == nativeHeight;
                bool currentSize = currentWidth > 0 && currentHeight > 0 && candidate.Width == currentWidth && candidate.Height == currentHeight;
                int score = 0;
                if (nativeSize) score += 1000000;
                if (currentSize) score += 500000;
                if (!RefreshEquals(candidate.RefreshRate, deleting.RefreshRate)) score += 100000;
                score += (int)Math.Min(candidate.RefreshRate, 10000);

                dm.dmFields |= Win32Display.DM_PELSWIDTH | Win32Display.DM_PELSHEIGHT |
                               Win32Display.DM_DISPLAYFREQUENCY | Win32Display.DM_BITSPERPEL;

                if (!RefreshEquals(candidate.RefreshRate, deleting.RefreshRate) && score > bestDifferentScore)
                {
                    bestDifferentScore = score;
                    bestDifferentRefresh = dm;
                }
                if (score > bestAnyScore)
                {
                    bestAnyScore = score;
                    bestAnyRefresh = dm;
                }
            }

            if (bestDifferentScore != int.MinValue)
            {
                safeMode = bestDifferentRefresh;
                return true;
            }
            if (bestAnyScore != int.MinValue)
            {
                safeMode = bestAnyRefresh;
                return true;
            }
            return false;
        }

        private static bool WindowsModeCouldMatchDeletingMode(CustomResolution windowsMode, CustomResolution deleting)
        {
            if (windowsMode == null || deleting == null)
                return false;

            uint windowsBpp = windowsMode.BitsPerPixel == 0 ? DefaultBitsPerPixel : windowsMode.BitsPerPixel;
            uint deletingBpp = deleting.BitsPerPixel == 0 ? DefaultBitsPerPixel : deleting.BitsPerPixel;
            if (windowsMode.Width != deleting.Width || windowsMode.Height != deleting.Height || windowsBpp != deletingBpp)
                return false;

            return windowsMode.RefreshRate == 0 || deleting.RefreshRate == 0 ||
                   Math.Abs((long)windowsMode.RefreshRate - (long)deleting.RefreshRate) <= 1;
        }

        private static bool ApplyResultHasUsefulDeletion(ApplyResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Detail))
                return false;
            return result.Detail.IndexOf("Deleted NVIDIA custom display entries:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   result.Detail.IndexOf("Deleted ", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

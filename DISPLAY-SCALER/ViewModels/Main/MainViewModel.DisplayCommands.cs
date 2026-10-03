using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DISPLAY_SCALER.ViewModels
{
    public sealed partial class MainViewModel
    {
        public async Task RefreshAsync()
        {
            IsBusy = true;
            StatusMessage = "Опрос мониторов и GPU..";
            try
            {
                bool selectedMonitorLoaded = await RefreshDisplayCatalogAsync();
                if (selectedMonitorLoaded)
                    StatusMessage = $"Найдено мониторов: {Monitors.Count}. {NvidiaStatus}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка: " + ex.Message;
                _profiles.LogDeveloperProblem("Refresh monitors exception", SelectedMonitor, null, ex.Message, null, ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private sealed class FullDisplayResetOperationResult
        {
            public ApplyResult Reset { get; set; }
            public DriverRestartResult Restart { get; set; }
        }

        public async Task RestartGpuDriverAsync()
        {
            IsBusy = true;
            StatusMessage = "Перезапуск графического драйвера через PnP disable/enable..";

            try
            {
                DriverRestartResult result = await RunDisplayOperationAsync(() =>
                {
                    DriverRestartResult restart = _driverRestart.RestartAllPresentDisplayAdapters();
                    if (!restart.Success)
                        return restart;

                    if (_nv.IsInitialized)
                    {
                        bool nvapiOk = _nv.RefreshOsModeList(out string nvapiDetail);
                        restart.NvApiRefreshAttempted = true;
                        restart.NvApiRefreshSucceeded = nvapiOk;
                        restart.NvApiRefreshDetail = nvapiDetail;
                    }

                    return restart;
                });

                await RefreshDisplayCatalogAsync();

                if (result.Success)
                {
                    string detail = string.IsNullOrWhiteSpace(result.Output)
                        ? string.Empty
                        : " " + result.Output.Trim();
                    string nvapiDetail = string.IsNullOrWhiteSpace(result.NvApiRefreshDetail)
                        ? string.Empty
                        : " NVAPI: " + result.NvApiRefreshDetail.Trim();

                    StatusMessage = "Графический драйвер перезапущен через PnP disable/enable." + detail + nvapiDetail;
                }
                else
                {
                    string detail = BuildDriverRestartDetail(result);
                    StatusMessage = "Не удалось перезагрузить драйвер GPU: " + detail;
                    _profiles.LogDeveloperProblem("GPU driver restart failed", SelectedMonitor, null, detail, result == null ? null : result.Output, null);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task FullDisplayResetAsync()
        {
            IsBusy = true;
            StatusMessage = "Полный сброс конфигурации дисплеев и подготовка перезапуска драйвера..";
            try
            {
                FullDisplayResetOperationResult operation = await RunDisplayOperationAsync(() =>
                {
                    ApplyResult reset = _driverRestart.ResetAllDisplayConfiguration();
                    DriverRestartResult restart = _driverRestart.RestartAllPresentDisplayAdapters();

                    return new FullDisplayResetOperationResult
                    {
                        Reset = reset,
                        Restart = restart
                    };
                });

                ApplyResult resetResult = operation.Reset;
                DriverRestartResult restartResult = operation.Restart;

                if (restartResult != null && restartResult.Success)
                {
                    ClearResolutionSelection();
                    await RefreshDisplayCatalogAsync();
                }

                if (resetResult == null || !resetResult.Success)
                {
                    string message = resetResult == null ? "операция не вернула результат." : resetResult.Message;
                    string resetFailureDetail = resetResult == null || string.IsNullOrWhiteSpace(resetResult.Detail)
                        ? message
                        : message + " (" + resetResult.Detail + ")";
                    string restartDetail = restartResult != null && restartResult.Success
                        ? " Драйвер при этом автоматически перезапущен. " + (restartResult.Output ?? string.Empty).Trim()
                        : " Автоматический перезапуск драйвера также завершился ошибкой: " + BuildDriverRestartDetail(restartResult);

                    StatusMessage = "Полный сброс выполнен не полностью: " + resetFailureDetail + restartDetail;
                    _profiles.LogDeveloperProblem(
                        "Full display reset partial failure",
                        null,
                        null,
                        resetResult == null ? "No result" : resetResult.Message,
                        (resetResult == null ? null : resetResult.Detail) + " | Driver restart: " + BuildDriverRestartDetail(restartResult),
                        null);
                    return;
                }

                if (restartResult == null || !restartResult.Success)
                {
                    string restartDetail = BuildDriverRestartDetail(restartResult);
                    StatusMessage = "Полный сброс выполнен, но драйвер автоматически не перезапустился: " + restartDetail;
                    _profiles.LogDeveloperProblem(
                        "Full display reset driver restart failed",
                        null,
                        null,
                        restartDetail,
                        restartResult == null ? null : restartResult.Output,
                        null);
                    return;
                }

                string resetDetail = string.IsNullOrWhiteSpace(resetResult.Detail)
                    ? string.Empty
                    : " " + resetResult.Detail.Trim();
                string driverDetail = string.IsNullOrWhiteSpace(restartResult.Output)
                    ? string.Empty
                    : " " + restartResult.Output.Trim();

                StatusMessage = "Полный сброс выполнен, графический драйвер автоматически перезапущен." + resetDetail + driverDetail;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string BuildDriverRestartDetail(DriverRestartResult result)
        {
            if (result == null)
                return "операция не вернула результат.";

            if (!string.IsNullOrWhiteSpace(result.Error))
                return result.Error.Trim();

            if (!string.IsNullOrWhiteSpace(result.Output))
                return result.Output.Trim();

            if (!result.Attempted)
                return "операция не была запущена.";

            return "SetupAPI не выполнил PnP disable/enable ни для одного display-адаптера.";
        }

        private static MonitorInfo SelectMonitorAfterRefresh(IList<MonitorInfo> monitors, string selectedDevice)
        {
            if (monitors == null || monitors.Count == 0)
                return null;

            if (!string.IsNullOrEmpty(selectedDevice))
            {
                for (int i = 0; i < monitors.Count; i++)
                {
                    MonitorInfo monitor = monitors[i];
                    if (monitor != null && string.Equals(monitor.DeviceName, selectedDevice, StringComparison.OrdinalIgnoreCase))
                        return monitor;
                }
            }

            for (int i = 0; i < monitors.Count; i++)
            {
                MonitorInfo monitor = monitors[i];
                if (monitor != null && monitor.IsPrimary)
                    return monitor;
            }

            return monitors[0];
        }

        public async Task OpenAddDialogAsync()
        {
            if (SelectedMonitor == null) return;

            MonitorInfo monitor = SelectedMonitor;
            CustomResolution newResolution = _customResolutionDialog.ShowAddDialog(monitor);
            if (newResolution == null) return;

            IsBusy = true;
            StatusMessage = $"Регистрация: {newResolution.Label} в Windows..";
            try
            {
                var addResult = await RunDisplayOperationAsync(() => _display.AddCustomResolution(monitor, newResolution, CreateTrialConfirmationCallback(monitor)));
                if (!addResult.Success)
                {
                    if (TryShowUnsupportedMode(addResult, newResolution))
                        return;

                    StatusMessage = "Не удалось добавить режим: " + addResult.Message;
                    _profiles.LogDeveloperProblem("Manual add custom resolution failed", monitor, newResolution, addResult.Message, addResult.Detail, null);
                    return;
                }

                await RefreshDisplayCatalogAsync();
                SelectResolutionIfPresent(newResolution);
                StatusMessage = $"Новый режим добавлен в NVIDIA/Windows: {newResolution.Label}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task EditCustomResolutionAsync()
        {
            if (SelectedMonitor == null || SelectedCustomResolution == null) return;
            if (!SelectedCustomResolution.CanEdit)
            {
                StatusMessage = "Системные разрешения нельзя изменять. Для изменения создай отдельное кастомное разрешение.";
                return;
            }

            MonitorInfo monitor = SelectedMonitor;
            CustomResolution oldResolution = SelectedCustomResolution;
            CustomResolution newResolution = _customResolutionDialog.ShowEditDialog(monitor, oldResolution);
            if (newResolution == null) return;

            if (ResolutionIdentityEquals(newResolution, oldResolution))
            {
                StatusMessage = "Изменений нет.";
                return;
            }

            IsBusy = true;
            StatusMessage = $"Изменение {oldResolution.Label} → {newResolution.Label}..";
            try
            {
                var addResult = await RunDisplayOperationAsync(() => _display.AddCustomResolution(monitor, newResolution, CreateTrialConfirmationCallback(monitor)));
                if (!addResult.Success)
                {
                    if (TryShowUnsupportedMode(addResult, newResolution))
                    {
                        StatusMessage = "Новый режим не поддерживается, старый режим оставлен без изменений.";
                        return;
                    }

                    StatusMessage = "Новый режим не прошёл тест/сохранение, старый режим оставлен без изменений: " + addResult.Message;
                    _profiles.LogDeveloperProblem("Edit custom resolution failed: new mode add before old removal", monitor, newResolution, addResult.Message, addResult.Detail, null);
                    return;
                }

                var removeResult = await RunDisplayOperationAsync(() => _display.RemoveCustomResolution(monitor, oldResolution));
                if (!removeResult.Success)
                {
                    await RefreshDisplayCatalogAsync();
                    SelectResolutionIfPresent(newResolution);
                    StatusMessage = "Новый режим добавлен, но старый режим удалить не удалось: " + removeResult.Message;
                    _profiles.LogDeveloperProblem("Edit custom resolution partial success: old mode removal failed", monitor, oldResolution, removeResult.Message, removeResult.Detail, null);
                    return;
                }

                await RefreshDisplayCatalogAsync();
                SelectResolutionIfPresent(newResolution);
                StatusMessage = $"Режим изменён: {oldResolution.Label} → {newResolution.Label}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task RemoveCustomResolutionAsync()
        {
            if (SelectedMonitor == null || SelectedCustomResolution == null) return;
            if (!SelectedCustomResolution.CanDelete)
            {
                StatusMessage = "Системные и штатные разрешения монитора защищены от удаления.";
                return;
            }

            MonitorInfo monitor = SelectedMonitor;
            CustomResolution resolution = SelectedCustomResolution;
            IsBusy = true;
            StatusMessage = $"Удаление '{resolution.Label}'..";
            try
            {
                var result = await RunDisplayOperationAsync(() => _display.RemoveCustomResolution(monitor, resolution));
                if (result.Success)
                {
                    await RefreshDisplayCatalogAsync();
                    ClearResolutionSelection();
                    StatusMessage = $"Удалено: {resolution.Label}. {result.Detail}";
                }
                else
                {
                    StatusMessage = "Ошибка удаления: " + result.Message;
                    _profiles.LogDeveloperProblem("Remove custom resolution failed", monitor, resolution, result.Message, result.Detail, null);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task ApplyPresetAsync(EsportsPreset preset)
        {
            if (SelectedMonitor == null || preset == null) return;

            MonitorInfo monitor = SelectedMonitor;
            IsBusy = true;
            StatusMessage = $"Добавление пресета '{preset.Name}'..";
            try
            {
                uint refresh = monitor.CurrentRefreshRate > 0
                    ? monitor.CurrentRefreshRate
                    : (preset.RefreshRate > 0 ? preset.RefreshRate : 60U);

                var resolution = new CustomResolution
                {
                    Width = preset.Width,
                    Height = preset.Height,
                    RefreshRate = refresh,
                    RefreshRateMilliHz = RefreshRateMath.ToMilliHz(refresh),
                    BitsPerPixel = 32
                };

                var addResult = await RunDisplayOperationAsync(() => _display.AddCustomResolution(monitor, resolution, CreateTrialConfirmationCallback(monitor)));
                if (addResult.Success)
                {
                    await RefreshDisplayCatalogAsync();
                    SelectResolutionIfPresent(resolution);
                    StatusMessage = $"Пресет «{preset.Name}» добавлен в Windows/NVIDIA.";
                }
                else
                {
                    if (TryShowUnsupportedMode(addResult, resolution))
                        return;

                    StatusMessage = "Не удалось добавить: " + addResult.Message;
                    _profiles.LogDeveloperProblem("Apply preset failed: " + preset.Name, monitor, resolution, addResult.Message, addResult.Detail, null);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool TryShowUnsupportedMode(ApplyResult result, CustomResolution resolution)
        {
            if (result == null || result.Success)
                return false;

            bool unsupported = result.ErrorCode == ApplyErrorCode.NvApiRejectedMode ||
                               result.ErrorCode == ApplyErrorCode.MonitorRangeViolation;
            if (!unsupported)
                return false;

            StatusMessage = "Монитор не поддерживает данное разрешение с этой частотой.";
            _customResolutionDialog.ShowUnsupportedMode(resolution);
            return true;
        }

        private Func<CustomResolution, int, bool> CreateTrialConfirmationCallback(MonitorInfo targetMonitor)
        {
            return (resolution, seconds) => _customResolutionDialog.ShowTrialConfirmation(resolution, seconds, targetMonitor);
        }

        private void CopySelectedResolution()
        {
            if (SelectedCustomResolution == null) return;

            _clipboardResolution = CloneResolution(SelectedCustomResolution, SelectedCustomResolution.AddedByDisplayScaler);
            _clipboardSourceMonitor = SelectedMonitor;

            StatusMessage = "Скопировано: " + _clipboardResolution.Label;
            OnPropertyChanged(nameof(ClipboardResolutionText));
            OnPropertyChanged(nameof(HasClipboardResolution));
        }

        public async Task PasteResolutionAsync()
        {
            if (SelectedMonitor == null || _clipboardResolution == null) return;

            MonitorInfo targetMonitor = SelectedMonitor;
            MonitorInfo sourceMonitor = _clipboardSourceMonitor ?? targetMonitor;
            CustomResolution clipboardResolution = CloneResolution(_clipboardResolution, _clipboardResolution.AddedByDisplayScaler);

            IsBusy = true;
            StatusMessage = "Проверка скопированного режима..";
            ProfileImportPlan plan = null;
            try
            {
                plan = await RunDisplayOperationAsync(() => CreateClipboardImportPlan(sourceMonitor, targetMonitor, clipboardResolution));
                if (plan == null || plan.Profile == null || plan.Resolution == null)
                {
                    string reason = plan == null ? "План вставки не создан." : (plan.Summary ?? "Скопированный режим не прошёл проверку.");
                    StatusMessage = "Ошибка вставки. Диагностика сохранена.";
                    ProfileApplyResult invalid = ProfileApplyResult.Fail("Вставка не выполнена.", reason);
                    _profiles.LogImportProblem(invalid, plan, targetMonitor, null);
                    _profileDialog.ShowProfileApplyResult(invalid);
                    return;
                }

                IsBusy = false;
                ProfileImportPlan confirmedPlan = _profileDialog.ShowImportDialog(
                    plan,
                    targetMonitor,
                    resolution => CreateClipboardImportPlan(sourceMonitor, targetMonitor, resolution));
                if (confirmedPlan == null)
                {
                    StatusMessage = "Вставка отменена пользователем.";
                    return;
                }

                plan = confirmedPlan;
                if (!plan.CanImport)
                {
                    string reason = plan.Summary ?? "Скопированный режим не прошёл проверку совместимости.";
                    StatusMessage = "Ошибка вставки. Диагностика сохранена.";
                    ProfileApplyResult blocked = ProfileApplyResult.Fail("Вставка не выполнена.", reason);
                    _profiles.LogImportProblem(blocked, plan, targetMonitor, null);
                    _profileDialog.ShowProfileApplyResult(blocked);
                    return;
                }

                IsBusy = true;
                StatusMessage = "Вставка и проверка режима " + plan.Resolution.Label + "..";
                ProfileApplyResult result = await RunDisplayOperationAsync(() => _profiles.ApplyImport(plan, targetMonitor, CreateTrialConfirmationCallback(targetMonitor)));

                await RefreshDisplayCatalogAsync();
                ClearResolutionSelection();

                if (result.Success)
                    result.Message = "Вставка успешна!";

                StatusMessage = result.Success
                    ? "Вставка успешна!"
                    : (result.ErrorCode == ProfileApplyErrorCode.UserCancelled
                        ? "Вставка отменена пользователем или таймером."
                        : (result.ErrorCode == ProfileApplyErrorCode.UnsupportedMode ? "Монитор не поддерживает данное разрешение с этой частотой." : "Ошибка вставки. Диагностика сохранена."));
                _profileDialog.ShowProfileApplyResult(result);
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка вставки. Диагностика сохранена.";
                ProfileApplyResult failure = ProfileApplyResult.Fail("Вставка не выполнена.", ex.Message);
                _profiles.LogImportProblem(failure, plan, targetMonitor, ex);
                _profileDialog.ShowProfileApplyResult(failure);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private ProfileImportPlan CreateClipboardImportPlan(MonitorInfo sourceMonitor, MonitorInfo targetMonitor, CustomResolution resolution)
        {
            var profile = _profiles.CreateProfile(sourceMonitor, resolution);
            profile.ProfileName = "Clipboard: " + resolution.Label;
            PreserveClipboardExactRefresh(profile, resolution);
            MarkClipboardProfileAsExactTransfer(profile);
            profile.Diagnostics.Notes.Add("Скопировано из внутреннего буфера DISPLAY-SCALER.");
            return _profiles.ValidateProfile(profile, targetMonitor);
        }

        private static void PreserveClipboardExactRefresh(DisplayScalerProfile profile, CustomResolution source)
        {
            if (profile == null || profile.Resolution == null || source == null || source.RefreshRate == 0)
                return;

            uint exactMilliHz = source.RefreshRateMilliHz;
            if (exactMilliHz == 0 && source.RefreshRate <= uint.MaxValue / 1000U)
                exactMilliHz = source.RefreshRate * 1000U;

            profile.Resolution.RefreshRate = source.RefreshRate;
            profile.Resolution.RefreshRateHz = source.RefreshRate;
            profile.Resolution.NominalRefreshRateHz = source.RefreshRate;
            profile.Resolution.ExactRefreshRateMilliHz = exactMilliHz;
            uint nominalMilliHz = source.RefreshRate <= uint.MaxValue / 1000U ? source.RefreshRate * 1000U : 0U;
            profile.Resolution.IntegerRefreshRate = nominalMilliHz > 0 && exactMilliHz == nominalMilliHz;
        }

        private static void MarkClipboardProfileAsExactTransfer(DisplayScalerProfile profile)
        {
            if (profile == null)
                return;

            if (profile.CreationHints != null)
            {
                profile.CreationHints.ExactRefreshRatePolicy = "PreserveUserEditedRefreshRate";
                profile.CreationHints.TimingMode = "NVIDIA_AUTO_TARGET_EXACT_CLIPBOARD_TRANSFER";
                profile.CreationHints.PreferTargetNativeRefreshRate = false;
                profile.CreationHints.Notes = "Clipboard import keeps the copied width, height, refresh and bpp unless edited in the import dialog.";
            }

            if (profile.Resolution != null)
            {
                profile.Resolution.ExactRefreshRatePolicy = "PreserveUserEditedRefreshRate";
                profile.Resolution.TimingMode = "NVIDIA_AUTO_TARGET_EXACT_CLIPBOARD_TRANSFER";
            }
        }

        private static CustomResolution CloneResolution(CustomResolution source, bool addedByDisplayScaler)
        {
            if (source == null) return null;
            return new CustomResolution
            {
                Width = source.Width,
                Height = source.Height,
                RefreshRate = source.RefreshRate,
                RefreshRateMilliHz = source.RefreshRateMilliHz,
                BitsPerPixel = source.BitsPerPixel == 0 ? 32U : source.BitsPerPixel,
                Origin = addedByDisplayScaler ? ResolutionOrigin.DisplayScaler : source.Origin,
                AddedByDisplayScaler = addedByDisplayScaler,
                IsApplied = false
            };
        }
    }
}

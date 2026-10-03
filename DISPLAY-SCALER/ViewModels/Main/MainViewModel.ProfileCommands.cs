using DISPLAY_SCALER.Models;
using System;
using System.Threading.Tasks;

namespace DISPLAY_SCALER.ViewModels
{
    public sealed partial class MainViewModel
    {
        public async Task ExportProfileAsync()
        {
            if (SelectedMonitor == null || SelectedCustomResolution == null) return;

            MonitorInfo monitor = SelectedMonitor;
            CustomResolution resolution = CloneResolution(SelectedCustomResolution, SelectedCustomResolution.AddedByDisplayScaler);
            string defaultName = _profiles.BuildDefaultFileName(resolution);
            string path = _profileDialog.AskExportPath(defaultName);
            if (string.IsNullOrWhiteSpace(path))
            {
                StatusMessage = "Экспорт отменён.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Экспорт CRU/EDID .bin " + resolution.Label + "..";
            try
            {
                await RunDisplayOperationAsync(() => _profiles.ExportProfile(path, monitor, resolution));
                StatusMessage = "CRU/EDID .bin экспортирован: " + path;
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка экспорта: " + ex.Message;
                _profiles.LogDeveloperProblem("Export profile exception", monitor, resolution, ex.Message, null, ex);
                _profileDialog.ShowError("DISPLAY-SCALER · Экспорт", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task ImportProfileAsync()
        {
            if (SelectedMonitor == null) return;

            MonitorInfo monitor = SelectedMonitor;
            string path = _profileDialog.AskImportPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                StatusMessage = "Импорт отменён.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Проверка CRU/EDID .bin..";
            ProfileImportPlan plan = null;
            try
            {
                plan = await RunDisplayOperationAsync(() => _profiles.LoadAndValidate(path, monitor));
                if (plan == null || plan.Profile == null || plan.Resolution == null)
                {
                    string reason = plan == null ? "Профиль не загружен." : (plan.Summary ?? "Файл не является корректным CRU/EDID .bin или legacy DISPLAY-SCALER профилем.");
                    StatusMessage = "Ошибка импорта. Диагностика сохранена.";
                    ProfileApplyResult invalid = ProfileApplyResult.Fail("Импорт не выполнен.", reason);
                    _profiles.LogImportProblem(invalid, plan, monitor, null);
                    _profileDialog.ShowProfileApplyResult(invalid);
                    return;
                }

                IsBusy = false;
                ProfileImportPlan confirmedPlan = _profileDialog.ShowImportDialog(
                    plan,
                    monitor,
                    resolution => _profiles.RevalidateEditedImport(plan, monitor, resolution));
                if (confirmedPlan == null)
                {
                    StatusMessage = "Импорт отменён пользователем.";
                    return;
                }

                plan = confirmedPlan;
                if (!plan.CanImport)
                {
                    string reason = plan.Summary ?? "Профиль не прошёл проверку совместимости.";
                    StatusMessage = "Ошибка импорта. Диагностика сохранена.";
                    ProfileApplyResult blocked = ProfileApplyResult.Fail("Импорт не выполнен.", reason);
                    _profiles.LogImportProblem(blocked, plan, monitor, null);
                    _profileDialog.ShowProfileApplyResult(blocked);
                    return;
                }

                IsBusy = true;
                StatusMessage = "Импорт и проверка режима " + plan.Resolution.Label + "..";
                ProfileApplyResult result = await RunDisplayOperationAsync(() => _profiles.ApplyImport(plan, monitor, CreateTrialConfirmationCallback(monitor)));

                await RefreshDisplayCatalogAsync();
                ClearResolutionSelection();

                StatusMessage = result.Success
                    ? "Импорт успешен!"
                    : (result.ErrorCode == ProfileApplyErrorCode.UserCancelled
                        ? "Импорт отменён пользователем или таймером."
                        : (result.ErrorCode == ProfileApplyErrorCode.UnsupportedMode ? "Монитор не поддерживает данное разрешение с этой частотой." : "Ошибка импорта. Диагностика сохранена."));
                _profileDialog.ShowProfileApplyResult(result);
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка импорта. Диагностика сохранена.";
                ProfileApplyResult failure = ProfileApplyResult.Fail("Импорт не выполнен.", ex.Message);
                _profiles.LogImportProblem(failure, plan, monitor, ex);
                _profileDialog.ShowProfileApplyResult(failure);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
using DISPLAY_SCALER.Models;

namespace DISPLAY_SCALER.Services.Dialogs
{
    public interface ICustomResolutionDialogService
    {
        CustomResolution ShowAddDialog(MonitorInfo monitor);

        CustomResolution ShowEditDialog(MonitorInfo monitor, CustomResolution initialResolution);

        bool ShowTrialConfirmation(CustomResolution resolution, int seconds);

        bool ShowTrialConfirmation(CustomResolution resolution, int seconds, MonitorInfo targetMonitor);

        void ShowUnsupportedMode(CustomResolution resolution);
    }
}
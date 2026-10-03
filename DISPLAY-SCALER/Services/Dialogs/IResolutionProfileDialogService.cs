using DISPLAY_SCALER.Models;
using System;

namespace DISPLAY_SCALER.Services.Dialogs
{
    public interface IResolutionProfileDialogService
    {
        string AskExportPath(string suggestedFileName);

        string AskImportPath();

        ProfileImportPlan ShowImportDialog(ProfileImportPlan plan, MonitorInfo targetMonitor, Func<CustomResolution, ProfileImportPlan> revalidate);

        void ShowProfileApplyResult(ProfileApplyResult result);

        void ShowError(string title, string message);
    }
}
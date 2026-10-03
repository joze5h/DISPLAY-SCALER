using DISPLAY_SCALER.Models;
using System;

namespace DISPLAY_SCALER.Services.Profiles
{
    public interface IResolutionProfileService
    {
        string BuildDefaultFileName(CustomResolution resolution);

        DisplayScalerProfile CreateProfile(MonitorInfo monitor, CustomResolution resolution);

        void ExportProfile(string path, MonitorInfo monitor, CustomResolution resolution);

        ProfileImportPlan LoadAndValidate(string path, MonitorInfo targetMonitor);

        ProfileImportPlan ValidateProfile(DisplayScalerProfile profile, MonitorInfo targetMonitor);

        ProfileImportPlan RevalidateEditedImport(ProfileImportPlan basePlan, MonitorInfo targetMonitor, CustomResolution editedResolution);

        ProfileApplyResult ApplyImport(ProfileImportPlan plan, MonitorInfo targetMonitor);

        ProfileApplyResult ApplyImport(ProfileImportPlan plan, MonitorInfo targetMonitor, Func<CustomResolution, int, bool> confirmTrial);

        void LogDeveloperProblem(string category, MonitorInfo monitor, CustomResolution resolution, string message, string detail, Exception exception);

        ProfileApplyResult LogImportProblem(ProfileApplyResult result, ProfileImportPlan plan, MonitorInfo targetMonitor, Exception exception);
    }
}
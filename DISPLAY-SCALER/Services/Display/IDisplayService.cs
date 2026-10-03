using DISPLAY_SCALER.Models;
using System;
using System.Collections.Generic;

namespace DISPLAY_SCALER.Services.Display
{
    public interface IDisplayService
    {
        List<MonitorInfo> EnumerateMonitors();

        ApplyResult AddCustomResolution(MonitorInfo monitor, CustomResolution res);

        ApplyResult AddCustomResolution(MonitorInfo monitor, CustomResolution res, Func<CustomResolution, int, bool> confirmTrial);

        ApplyResult AddCustomResolution(MonitorInfo monitor, CustomResolution res, Func<CustomResolution, int, bool> confirmTrial, bool forceRecreateExisting);

        ApplyResult RemoveCustomResolution(MonitorInfo monitor, CustomResolution res);

        ApplyResult RefreshWindowsModeList(MonitorInfo monitor);

        List<uint> GetAvailableRefreshRates(MonitorInfo monitor);

        List<uint> GetAvailableColorDepths(MonitorInfo monitor);

        ApplyResult ApplyCurrentRefreshRate(MonitorInfo monitor, uint refreshRate);

        ApplyResult ApplyCurrentColorDepth(MonitorInfo monitor, uint bitsPerPixel);


        ApplyResult TestDisplayMode(MonitorInfo monitor, CustomResolution res);

        uint ResolveNativeRefreshRateMilliHz(MonitorInfo monitor, uint nominalRefreshRate);

        void LoadCustomResolutions(MonitorInfo monitor);

        void MarkDisplayScalerResolution(MonitorInfo monitor, CustomResolution res);

        bool HasNativeRefreshHijackRisk(MonitorInfo monitor, CustomResolution res, out string reason);

        bool IsNvidiaCustomModeVisible(MonitorInfo monitor, CustomResolution res, out string error);

        bool IsNvidiaCustomModeTargetRefresh(MonitorInfo monitor, CustomResolution res, out string detail);

        bool IsWindowsModeEnumerated(MonitorInfo monitor, CustomResolution res);

        bool IsWindowsRawModeEnumerated(MonitorInfo monitor, CustomResolution res);

        bool TryFindBestWindowsModeForResolution(MonitorInfo monitor, uint width, uint height, out CustomResolution bestMode);

        string GetWindowsModeVisibilityText(MonitorInfo monitor, CustomResolution res);
    }
}

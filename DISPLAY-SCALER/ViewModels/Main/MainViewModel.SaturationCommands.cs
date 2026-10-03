using DISPLAY_SCALER.Models;
using System;
using System.Threading.Tasks;

namespace DISPLAY_SCALER.ViewModels
{
    public sealed partial class MainViewModel
    {
        public async Task ApplySaturationAsync(int level)
        {
            if (SelectedMonitor == null) return;

            MonitorInfo monitor = SelectedMonitor;
            IsBusy = true;
            StatusMessage = $"Установка насыщенности {level}%..";
            try
            {
                string nvapiDeviceName = string.IsNullOrWhiteSpace(monitor.NvApiDeviceName) ? monitor.DeviceName : monitor.NvApiDeviceName;
                bool ok = await RunDisplayOperationAsync(() => _nv.SetSaturationForDisplay(nvapiDeviceName, level));
                if (ok)
                {
                    if (SelectedMonitor != null) SelectedMonitor.Saturation = level;
                    StatusMessage = $"Насыщенность: {level}%";
                }
                else
                {
                    StatusMessage = "Ошибка NVAPI: " + _nv.LastError;
                    SaturationSupported = false;
                    _profiles.LogDeveloperProblem("Saturation apply failed", monitor, null, _nv.LastError, null, null);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task ResetSaturationAsync()
        {
            int defaultValue = SelectedMonitor?.SaturationDefault > 0 ? SelectedMonitor.SaturationDefault : 50;
            Saturation = defaultValue;
            await ApplySaturationAsync(defaultValue);
        }
    }
}

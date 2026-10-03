using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DISPLAY_SCALER.ViewModels
{
    public sealed partial class MainViewModel
    {
        private void RebuildDisplaySettingOptions()
        {
            RefreshRateOptions.Clear();
            ColorDepthOptions.Clear();

            MonitorInfo monitor = SelectedMonitor;
            if (monitor == null)
            {
                SelectedRefreshRateOption = null;
                SelectedColorDepthOption = null;
                RaiseDisplaySettingOptionProperties();
                return;
            }

            DisplaySettingOption selectedRefresh = null;
            List<uint> refreshRates = _display.GetAvailableRefreshRates(monitor);
            for (int i = 0; i < refreshRates.Count; i++)
            {
                uint value = refreshRates[i];
                var option = new DisplaySettingOption(value, RefreshRateMath.Format(value, value * 1000U));
                RefreshRateOptions.Add(option);
                if (selectedRefresh == null && monitor.CurrentRefreshRate > 0 && System.Math.Abs((long)value - (long)monitor.CurrentRefreshRate) <= 1)
                    selectedRefresh = option;
            }

            DisplaySettingOption selectedColorDepth = null;
            List<uint> colorDepths = _display.GetAvailableColorDepths(monitor);
            for (int i = 0; i < colorDepths.Count; i++)
            {
                uint value = colorDepths[i];
                var option = new DisplaySettingOption(value, FormatColorDepth(value));
                ColorDepthOptions.Add(option);
                if (selectedColorDepth == null && monitor.CurrentBitsPerPixel > 0 && value == monitor.CurrentBitsPerPixel)
                    selectedColorDepth = option;
            }

            SelectedRefreshRateOption = selectedRefresh;
            SelectedColorDepthOption = selectedColorDepth;
            RaiseDisplaySettingOptionProperties();
        }

        public async Task ApplyRefreshRateAsync()
        {
            if (SelectedMonitor == null || SelectedRefreshRateOption == null)
                return;

            MonitorInfo monitor = SelectedMonitor;
            uint refresh = SelectedRefreshRateOption.Value;
            IsBusy = true;
            StatusMessage = "Применение частоты " + refresh + " Гц..";
            try
            {
                ApplyResult result = await RunDisplayOperationAsync(() => _display.ApplyCurrentRefreshRate(monitor, refresh));
                StatusMessage = result.Success ? result.Message : "Частота не применена: " + result.Message;
                if (result.Success)
                    await RefreshDisplayCatalogAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task ApplyColorDepthAsync()
        {
            if (SelectedMonitor == null || SelectedColorDepthOption == null)
                return;

            MonitorInfo monitor = SelectedMonitor;
            uint bits = SelectedColorDepthOption.Value;
            IsBusy = true;
            StatusMessage = "Применение битности цвета " + bits + " bpp..";
            try
            {
                ApplyResult result = await RunDisplayOperationAsync(() => _display.ApplyCurrentColorDepth(monitor, bits));
                StatusMessage = result.Success ? result.Message : "Битность цвета не применена: " + result.Message;
                if (result.Success)
                    await RefreshDisplayCatalogAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string FormatColorDepth(uint bitsPerPixel)
        {
            return bitsPerPixel == 0 ? "—" : bitsPerPixel + " bpp";
        }

        private void RaiseDisplaySettingOptionProperties()
        {
            OnPropertyChanged(nameof(HasRefreshRateOptions));
            OnPropertyChanged(nameof(HasColorDepthOptions));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
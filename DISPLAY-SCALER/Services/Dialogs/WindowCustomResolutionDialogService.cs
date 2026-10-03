using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.ViewModels;
using DISPLAY_SCALER.Views;
using System;
using System.Windows;

namespace DISPLAY_SCALER.Services.Dialogs
{
    public sealed class WindowCustomResolutionDialogService : ICustomResolutionDialogService
    {
        public CustomResolution ShowAddDialog(MonitorInfo monitor)
        {
            return ShowDialog(monitor, null, false);
        }

        public CustomResolution ShowEditDialog(MonitorInfo monitor, CustomResolution initialResolution)
        {
            return ShowDialog(monitor, initialResolution, true);
        }

        public bool ShowTrialConfirmation(CustomResolution resolution, int seconds)
        {
            return ShowTrialConfirmation(resolution, seconds, null);
        }

        public bool ShowTrialConfirmation(CustomResolution resolution, int seconds, MonitorInfo targetMonitor)
        {
            var dispatcher = Application.Current == null ? null : Application.Current.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                return (bool)dispatcher.Invoke(new Func<bool>(() => ShowTrialConfirmation(resolution, seconds, targetMonitor)));

            var dialog = new TimedApplyDialog(resolution, seconds, targetMonitor);

            return dialog.ShowDialog() == true;
        }

        public void ShowUnsupportedMode(CustomResolution resolution)
        {
            var dispatcher = Application.Current == null ? null : Application.Current.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(new Action(() => ShowUnsupportedMode(resolution)));
                return;
            }

            var dialog = new UnsupportedModeDialog(resolution)
            {
                Owner = Application.Current?.MainWindow
            };

            dialog.ShowDialog();
        }

        private static CustomResolution ShowDialog(MonitorInfo monitor, CustomResolution initialResolution, bool editMode)
        {
            var viewModel = new CustomResViewModel(monitor, initialResolution, editMode);
            var dialog = new AddCustomResolutionDialog(viewModel)
            {
                Owner = Application.Current?.MainWindow
            };

            return dialog.ShowDialog() == true ? viewModel.Result : null;
        }
    }
}
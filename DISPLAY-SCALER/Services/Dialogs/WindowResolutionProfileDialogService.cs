using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.ViewModels;
using DISPLAY_SCALER.Views;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace DISPLAY_SCALER.Services.Dialogs
{
    public sealed class WindowResolutionProfileDialogService : IResolutionProfileDialogService
    {
        public string AskExportPath(string suggestedFileName)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Экспорт CRU/EDID BIN",
                FileName = string.IsNullOrWhiteSpace(suggestedFileName) ? "DISPLAY-SCALER_profile.bin" : suggestedFileName,
                DefaultExt = ".bin",
                Filter = "CRU / EDID BIN (*.bin)|*.bin|All files (*.*)|*.*",
                AddExtension = true,
                OverwritePrompt = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string AskImportPath()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Импорт CRU/EDID BIN",
                DefaultExt = ".bin",
                Filter = "CRU / EDID BIN (*.bin)|*.bin|Legacy DISPLAY-SCALER JSON (*.dscaler.json;*.json)|*.dscaler.json;*.json|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public ProfileImportPlan ShowImportDialog(ProfileImportPlan plan, MonitorInfo targetMonitor, Func<CustomResolution, ProfileImportPlan> revalidate)
        {
            if (plan == null || revalidate == null) return null;

            var viewModel = new ImportProfileViewModel(targetMonitor, plan, revalidate);
            var dialog = new ImportProfileDialog(viewModel)
            {
                Owner = Application.Current?.MainWindow
            };

            return dialog.ShowDialog() == true ? viewModel.ResultPlan : null;
        }

        public void ShowProfileApplyResult(ProfileApplyResult result)
        {
            if (result == null) return;

            if (result.Success)
            {
                MessageBox.Show("Импорт успешен!", "DISPLAY-SCALER", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (result.ErrorCode == ProfileApplyErrorCode.UserCancelled)
            {
                MessageBox.Show(result.Message ?? "Операция отменена.", "DISPLAY-SCALER", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (result.ErrorCode == ProfileApplyErrorCode.UnsupportedMode)
            {
                var unsupported = new UnsupportedModeDialog(result.FailedResolution)
                {
                    Owner = Application.Current?.MainWindow
                };
                unsupported.ShowDialog();
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.DiagnosticDirectory))
                TryOpenFolder(result.DiagnosticDirectory);

            var sb = new StringBuilder();
            sb.AppendLine("Произошла ошибка импорта.");
            sb.AppendLine();
            sb.AppendLine("Диагностика сохранена в:");
            sb.AppendLine(string.IsNullOrWhiteSpace(result.DiagnosticDirectory) ? "%AppData%\\DISPLAY-SCALER" : result.DiagnosticDirectory);

            if (!string.IsNullOrWhiteSpace(result.DiagnosticFilePath))
            {
                sb.AppendLine();
                sb.AppendLine("Файл:");
                sb.AppendLine("LOG.log");
            }

            sb.AppendLine();
            sb.AppendLine("Скиньте разработчику файл LOG.log из этой папки.");

            MessageBox.Show(sb.ToString(), "DISPLAY-SCALER · Импорт", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static void TryOpenFolder(string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("OpenDiagnosticsFolder failed: " + ex.Message);
            }
        }

        public void ShowError(string title, string message)
        {
            string diagnosticsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DISPLAY-SCALER");
            string text = (message ?? "Ошибка") + Environment.NewLine + Environment.NewLine +
                          "Если ошибка повторяется, приложите разработчику LOG.log из папки:" + Environment.NewLine +
                          diagnosticsDir;
            MessageBox.Show(text, string.IsNullOrWhiteSpace(title) ? "DISPLAY-SCALER" : title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
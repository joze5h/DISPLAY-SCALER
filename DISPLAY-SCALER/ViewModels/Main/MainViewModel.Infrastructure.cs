using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DISPLAY_SCALER.ViewModels
{
    public sealed partial class MainViewModel
    {
        private static readonly Comparison<CustomResolution> CustomResolutionViewComparison = CompareCustomResolutionViews;
        private static readonly Comparison<CustomResolution> ExternalCustomResolutionViewComparison = CompareExternalCustomResolutionViews;

        private sealed class RefreshSnapshot
        {
            public List<MonitorInfo> Monitors { get; set; }
            public List<GpuInfo> Gpus { get; set; }
        }

        private void OnCommandException(Exception ex)
        {
            StatusMessage = "Ошибка команды: " + ex.Message;
            _logger.Error("Unhandled command exception", ex);
            if (_profiles != null)
                _profiles.LogDeveloperProblem("Command exception", SelectedMonitor, SelectedCustomResolution, ex.Message, null, ex);
        }

        private Task<T> RunDisplayOperationAsync<T>(Func<T> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return _displayOperations.RunExclusiveAsync(() => Task.Run(action));
        }

        private Task RunDisplayOperationAsync(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return _displayOperations.RunExclusiveAsync(() => Task.Run(action));
        }

        private async Task<bool> RefreshDisplayCatalogAsync()
        {
            string selectedDevice = SelectedMonitor?.DeviceName;
            RefreshSnapshot snapshot = await RunDisplayOperationAsync(() => new RefreshSnapshot
            {
                Gpus = _nv.IsInitialized ? _nv.EnumerateGpus() : new List<GpuInfo>(0),
                Monitors = _display.EnumerateMonitors()
            });

            UpdateNvidiaStatus(snapshot.Gpus);

            Monitors.Clear();
            for (int i = 0; i < snapshot.Monitors.Count; i++)
                Monitors.Add(snapshot.Monitors[i]);

            SelectedMonitor = SelectMonitorAfterRefresh(Monitors, selectedDevice);

            Task<bool> selectedMonitorLoadTask = _selectedMonitorLoadTask;
            return selectedMonitorLoadTask == null || await selectedMonitorLoadTask;
        }

        private async Task<bool> LoadSelectedMonitorDetailsAsync(MonitorInfo monitor, int generation)
        {
            if (monitor == null)
                return true;

            try
            {
                await RunDisplayOperationAsync(() => _display.LoadCustomResolutions(monitor));

                if (generation != _selectedMonitorLoadGeneration || !ReferenceEquals(SelectedMonitor, monitor))
                    return true;

                RebuildCustomResolutionViews();
                return true;
            }
            catch (Exception ex)
            {
                bool isCurrentSelection = generation == _selectedMonitorLoadGeneration && ReferenceEquals(SelectedMonitor, monitor);
                if (!isCurrentSelection)
                    return true;

                StatusMessage = "Не удалось загрузить пользовательские режимы выбранного монитора: " + ex.Message;
                _logger.Error("Failed to load selected monitor custom resolutions", ex);
                _profiles.LogDeveloperProblem("Selected monitor custom-resolution load failed", monitor, null, ex.Message, null, ex);
                return false;
            }
        }

        private void OpenDwtLink()
        {
            try
            {
                _externalLinks.Open(DwtTelegramUrl);
            }
            catch (Exception ex)
            {
                StatusMessage = "Не удалось открыть ссылку: " + ex.Message;
                _logger.Error("Failed to open external link", ex);
            }
        }

        private void ClearResolutionSelection()
        {
            _syncingResolutionSelection = true;
            try
            {
                SelectedDisplayScalerResolution = null;
                SelectedExternalCustomResolution = null;
                SelectedSystemResolution = null;
                SelectedCustomResolution = null;
            }
            finally
            {
                _syncingResolutionSelection = false;
            }
        }

        private void SelectResolutionIfPresent(CustomResolution target)
        {
            if (target == null)
            {
                ClearResolutionSelection();
                return;
            }

            for (int i = 0; i < DisplayScalerCustomResolutions.Count; i++)
            {
                CustomResolution candidate = DisplayScalerCustomResolutions[i];
                if (ResolutionIdentityEquals(candidate, target))
                {
                    SelectedDisplayScalerResolution = candidate;
                    return;
                }
            }

            for (int i = 0; i < ExternalCustomResolutions.Count; i++)
            {
                CustomResolution candidate = ExternalCustomResolutions[i];
                if (ResolutionIdentityEquals(candidate, target))
                {
                    SelectedExternalCustomResolution = candidate;
                    return;
                }
            }

            for (int i = 0; i < SystemResolutions.Count; i++)
            {
                CustomResolution candidate = SystemResolutions[i];
                if (ResolutionIdentityEquals(candidate, target))
                {
                    SelectedSystemResolution = candidate;
                    return;
                }
            }

            ClearResolutionSelection();
        }

        private static bool ResolutionIdentityEquals(CustomResolution a, CustomResolution b)
        {
            if (a == null || b == null)
                return false;

            uint aBpp = a.BitsPerPixel == 0 ? 32U : a.BitsPerPixel;
            uint bBpp = b.BitsPerPixel == 0 ? 32U : b.BitsPerPixel;
            if (a.Width != b.Width || a.Height != b.Height || aBpp != bBpp || a.RefreshRate != b.RefreshRate)
                return false;

            uint aExact = RefreshRateMath.NormalizeDisplayMilliHz(a.RefreshRate, a.RefreshRateMilliHz);
            uint bExact = RefreshRateMath.NormalizeDisplayMilliHz(b.RefreshRate, b.RefreshRateMilliHz);
            return Math.Abs((long)aExact - (long)bExact) <= 1L;
        }

        private void RebuildCustomResolutionViews()
        {
            DisplayScalerCustomResolutions.Clear();
            ExternalCustomResolutions.Clear();
            SystemResolutions.Clear();

            if (SelectedMonitor == null)
            {
                OnPropertyChanged(nameof(HasCustomResolutions));
                OnPropertyChanged(nameof(HasSelectedCustomResolution));
                OnPropertyChanged(nameof(SelectedCustomResolutionText));
                return;
            }

            var knownCustomModes = SelectedMonitor.CustomResolutions;
            var displayScalerModes = new List<CustomResolution>(knownCustomModes.Count);
            var externalCustomModes = new List<CustomResolution>(knownCustomModes.Count);
            var systemModes = new List<CustomResolution>(SelectedMonitor.SupportedModes.Count);

            for (int i = 0; i < knownCustomModes.Count; i++)
            {
                CustomResolution mode = knownCustomModes[i];
                if (mode == null)
                    continue;

                if (mode.AddedByDisplayScaler || mode.Origin == ResolutionOrigin.DisplayScaler)
                {
                    mode.Origin = ResolutionOrigin.DisplayScaler;
                    AddResolutionIfMissing(displayScalerModes, mode);
                }
                else
                {
                    mode.Origin = ResolutionOrigin.Custom;
                    AddResolutionIfMissing(externalCustomModes, mode);
                }
            }

            for (int i = 0; i < SelectedMonitor.SupportedModes.Count; i++)
            {
                ResolutionMode mode = SelectedMonitor.SupportedModes[i];
                if (mode == null || MatchesKnownCustomMode(mode, knownCustomModes))
                    continue;

                AddBestSystemMode(systemModes, new CustomResolution
                {
                    Width = mode.Width,
                    Height = mode.Height,
                    RefreshRate = mode.RefreshRate,
                    BitsPerPixel = mode.BitsPerPixel,
                    Origin = ResolutionOrigin.System,
                    AddedByDisplayScaler = false
                });
            }

            displayScalerModes.Sort(CustomResolutionViewComparison);
            externalCustomModes.Sort(ExternalCustomResolutionViewComparison);
            systemModes.Sort(CustomResolutionViewComparison);

            for (int i = 0; i < displayScalerModes.Count; i++)
                DisplayScalerCustomResolutions.Add(displayScalerModes[i]);

            for (int i = 0; i < externalCustomModes.Count; i++)
                ExternalCustomResolutions.Add(externalCustomModes[i]);

            for (int i = 0; i < systemModes.Count; i++)
                SystemResolutions.Add(systemModes[i]);

            OnPropertyChanged(nameof(HasCustomResolutions));
            OnPropertyChanged(nameof(HasSelectedCustomResolution));
            OnPropertyChanged(nameof(SelectedCustomResolutionText));
        }

        private static void AddResolutionIfMissing(List<CustomResolution> modes, CustomResolution candidate)
        {
            if (modes == null || candidate == null)
                return;

            for (int i = 0; i < modes.Count; i++)
            {
                if (ResolutionIdentityEquals(modes[i], candidate))
                    return;
            }

            modes.Add(candidate);
        }

        private static bool MatchesKnownCustomMode(ResolutionMode systemMode, IList<CustomResolution> knownCustomModes)
        {
            if (systemMode == null || knownCustomModes == null)
                return false;

            uint systemBpp = systemMode.BitsPerPixel == 0 ? 32U : systemMode.BitsPerPixel;
            for (int i = 0; i < knownCustomModes.Count; i++)
            {
                CustomResolution customMode = knownCustomModes[i];
                if (customMode == null)
                    continue;

                uint customBpp = customMode.BitsPerPixel == 0 ? 32U : customMode.BitsPerPixel;
                if (systemMode.Width == customMode.Width &&
                    systemMode.Height == customMode.Height &&
                    systemBpp == customBpp &&
                    Math.Abs((long)systemMode.RefreshRate - (long)customMode.RefreshRate) <= 1L)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddBestSystemMode(List<CustomResolution> modes, CustomResolution candidate)
        {
            if (candidate == null || candidate.Width == 0 || candidate.Height == 0 || candidate.RefreshRate == 0)
                return;

            for (int i = 0; i < modes.Count; i++)
            {
                CustomResolution existing = modes[i];
                if (existing == null || existing.Width != candidate.Width || existing.Height != candidate.Height)
                    continue;

                if (IsBetterSystemMode(candidate, existing))
                    modes[i] = CloneAsSystemMode(candidate);

                return;
            }

            modes.Add(CloneAsSystemMode(candidate));
        }

        private static bool IsBetterSystemMode(CustomResolution candidate, CustomResolution current)
        {
            if (candidate.RefreshRate != current.RefreshRate)
                return candidate.RefreshRate > current.RefreshRate;

            uint candidateMilliHz = RefreshRateMath.NormalizeDisplayMilliHz(candidate.RefreshRate, candidate.RefreshRateMilliHz);
            uint currentMilliHz = RefreshRateMath.NormalizeDisplayMilliHz(current.RefreshRate, current.RefreshRateMilliHz);
            if (candidateMilliHz != currentMilliHz)
                return candidateMilliHz > currentMilliHz;

            uint candidateBpp = candidate.BitsPerPixel == 0 ? 32U : candidate.BitsPerPixel;
            uint currentBpp = current.BitsPerPixel == 0 ? 32U : current.BitsPerPixel;
            return candidateBpp > currentBpp;
        }

        private static CustomResolution CloneAsSystemMode(CustomResolution candidate)
        {
            return new CustomResolution
            {
                Width = candidate.Width,
                Height = candidate.Height,
                RefreshRate = candidate.RefreshRate,
                RefreshRateMilliHz = candidate.RefreshRateMilliHz,
                BitsPerPixel = candidate.BitsPerPixel,
                Origin = ResolutionOrigin.System,
                AddedByDisplayScaler = false
            };
        }

        private static int CompareExternalCustomResolutionViews(CustomResolution a, CustomResolution b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int aspect = GetExternalCustomAspectSortKey(a.AspectRatio).CompareTo(GetExternalCustomAspectSortKey(b.AspectRatio));
            if (aspect != 0) return aspect;

            int area = ((long)b.Width * b.Height).CompareTo((long)a.Width * a.Height);
            if (area != 0) return area;

            int refresh = b.RefreshRate.CompareTo(a.RefreshRate);
            if (refresh != 0) return refresh;

            int exactRefresh = b.RefreshRateMilliHz.CompareTo(a.RefreshRateMilliHz);
            if (exactRefresh != 0) return exactRefresh;

            int width = a.Width.CompareTo(b.Width);
            if (width != 0) return width;

            return a.Height.CompareTo(b.Height);
        }

        private static int GetExternalCustomAspectSortKey(string aspectRatio)
        {
            switch (aspectRatio)
            {
                case "1:1": return 0;
                case "4:3": return 1;
                case "5:4": return 2;
                case "16:10": return 3;
                case "3:2": return 4;
                case "21:9": return 5;
                case "32:9": return 6;
                case "16:9": return 7;
                default: return 100;
            }
        }

        private static int CompareCustomResolutionViews(CustomResolution a, CustomResolution b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int aspect = GetAspectSortKey(a.AspectRatio).CompareTo(GetAspectSortKey(b.AspectRatio));
            if (aspect != 0) return aspect;

            int area = ((long)b.Width * b.Height).CompareTo((long)a.Width * a.Height);
            if (area != 0) return area;

            int refresh = b.RefreshRate.CompareTo(a.RefreshRate);
            if (refresh != 0) return refresh;

            int exactRefresh = b.RefreshRateMilliHz.CompareTo(a.RefreshRateMilliHz);
            if (exactRefresh != 0) return exactRefresh;

            int width = a.Width.CompareTo(b.Width);
            if (width != 0) return width;

            return a.Height.CompareTo(b.Height);
        }

        private static int GetAspectSortKey(string aspectRatio)
        {
            switch (aspectRatio)
            {
                case "16:9": return 0;
                case "16:10": return 1;
                case "21:9": return 2;
                case "32:9": return 3;
                case "4:3": return 4;
                case "5:4": return 5;
                case "3:2": return 6;
                case "1:1": return 7;
                default: return 100;
            }
        }

        private void UpdateNvidiaStatus(IList<GpuInfo> gpus = null)
        {
            if (!_nv.IsAvailable)
            {
                Gpus.Clear();
                SelectedGpu = null;
                NvidiaAvailable = false;
                NvidiaStatus = "Драйвер NVIDIA не обнаружен";
                return;
            }

            if (!_nv.IsInitialized)
            {
                Gpus.Clear();
                SelectedGpu = null;
                NvidiaAvailable = false;
                NvidiaStatus = "NVAPI не инициализирован: " + _nv.LastError;
                return;
            }

            if (gpus == null)
                gpus = _nv.EnumerateGpus();

            Gpus.Clear();
            for (int i = 0; i < gpus.Count; i++)
                Gpus.Add(gpus[i]);

            int gpuCount = Gpus.Count;
            SelectedGpu = gpuCount > 0 ? Gpus[0] : null;
            NvidiaAvailable = gpuCount > 0;
            NvidiaStatus = gpuCount > 0
                ? $"{gpuCount} GPU NVIDIA · NVAPI {(_nv.ApiVersion ?? "—")}"
                : "GPU NVIDIA не найден";
        }
    }
}

using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Infrastructure.Mvvm;
using DISPLAY_SCALER.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DISPLAY_SCALER.ViewModels
{
    public sealed class ImportProfileViewModel : ViewModelBase
    {
        private const double PreviewWidth = 430;
        private const double PreviewHeight = 242;

        private readonly MonitorInfo _targetMonitor;
        private readonly Func<CustomResolution, ProfileImportPlan> _revalidate;
        private readonly CustomResolution _originalResolution;
        private readonly Command _confirmCommand;
        private CancellationTokenSource _validationCts;

        private string _widthText;
        private string _heightText;
        private string _refreshRateText;
        private string _previewLabel;
        private string _previewChip;
        private string _previewFovLabel;
        private string _validationMessage;
        private string _summary;
        private string _riskLevel;
        private string _compatibilityStatus;
        private string _pixelClockText;
        private string _sourceTitle;
        private string _sourceNativeText;
        private string _sourceCurrentText;
        private string _targetTitle;
        private string _targetNativeText;
        private string _targetCurrentText;
        private string _targetRangeText;
        private string _editNotice;
        private bool _canImport;
        private bool _hasBlockingIssues;
        private bool _hasWarnings;
        private bool _hasChecks;
        private double _cropLeftWidth;
        private double _cropRightLeft;
        private double _cropTopHeight;
        private double _cropBottomTop;

        public ImportProfileViewModel(MonitorInfo targetMonitor, ProfileImportPlan initialPlan, Func<CustomResolution, ProfileImportPlan> revalidate)
        {
            _targetMonitor = targetMonitor;
            _revalidate = revalidate ?? throw new ArgumentNullException(nameof(revalidate));
            _originalResolution = CloneResolution(initialPlan == null ? null : initialPlan.Resolution);

            DialogTitle = "ИМПОРТ ПРОФИЛЯ РАЗРЕШЕНИЯ";
            MonitorTitle = targetMonitor == null ? "Монитор" : targetMonitor.DisplayTitle;
            ConfirmButtonText = "Импортировать";

            Checks = new ObservableCollection<string>();
            Warnings = new ObservableCollection<string>();
            BlockingIssues = new ObservableCollection<string>();

            _confirmCommand = new Command(Confirm, () => CanImport);
            ConfirmCommand = _confirmCommand;
            CancelCommand = new Command(Cancel);

            CustomResolution res = initialPlan == null ? null : initialPlan.Resolution;
            if (res != null)
            {
                _widthText = res.Width.ToString();
                _heightText = res.Height.ToString();
                _refreshRateText = res.RefreshRate.ToString();
            }
            else
            {
                _widthText = GetNativeWidth().ToString();
                _heightText = GetNativeHeight().ToString();
                _refreshRateText = targetMonitor != null && targetMonitor.CurrentRefreshRate > 0 ? targetMonitor.CurrentRefreshRate.ToString() : "144";
            }

            ApplyPlan(initialPlan);
            UpdatePreview();
        }

        public event Action<bool?> RequestClose;

        public string DialogTitle { get; private set; }
        public string MonitorTitle { get; private set; }
        public string ConfirmButtonText { get; private set; }
        public ProfileImportPlan ResultPlan { get; private set; }
        public ProfileImportPlan CurrentPlan { get; private set; }

        public ObservableCollection<string> Checks { get; private set; }
        public ObservableCollection<string> Warnings { get; private set; }
        public ObservableCollection<string> BlockingIssues { get; private set; }

        public ICommand ConfirmCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public string WidthText
        {
            get { return _widthText; }
            set
            {
                if (Set(ref _widthText, value))
                    UpdateFromInput();
            }
        }

        public string HeightText
        {
            get { return _heightText; }
            set
            {
                if (Set(ref _heightText, value))
                    UpdateFromInput();
            }
        }

        public string RefreshRateText
        {
            get { return _refreshRateText; }
            set
            {
                if (Set(ref _refreshRateText, value))
                    UpdateFromInput();
            }
        }

        public string PreviewLabel
        {
            get { return _previewLabel; }
            private set { Set(ref _previewLabel, value); }
        }

        public string PreviewChip
        {
            get { return _previewChip; }
            private set { Set(ref _previewChip, value); }
        }

        public string PreviewFovLabel
        {
            get { return _previewFovLabel; }
            private set { Set(ref _previewFovLabel, value); }
        }

        public string ValidationMessage
        {
            get { return _validationMessage; }
            private set { Set(ref _validationMessage, value); }
        }

        public string Summary
        {
            get { return _summary; }
            private set { Set(ref _summary, value); }
        }

        public string RiskLevel
        {
            get { return _riskLevel; }
            private set { Set(ref _riskLevel, value); }
        }

        public string CompatibilityStatus
        {
            get { return _compatibilityStatus; }
            private set { Set(ref _compatibilityStatus, value); }
        }

        public string PixelClockText
        {
            get { return _pixelClockText; }
            private set { Set(ref _pixelClockText, value); }
        }

        public string SourceTitle
        {
            get { return _sourceTitle; }
            private set { Set(ref _sourceTitle, value); }
        }

        public string SourceNativeText
        {
            get { return _sourceNativeText; }
            private set { Set(ref _sourceNativeText, value); }
        }

        public string SourceCurrentText
        {
            get { return _sourceCurrentText; }
            private set { Set(ref _sourceCurrentText, value); }
        }

        public string TargetTitle
        {
            get { return _targetTitle; }
            private set { Set(ref _targetTitle, value); }
        }

        public string TargetNativeText
        {
            get { return _targetNativeText; }
            private set { Set(ref _targetNativeText, value); }
        }

        public string TargetCurrentText
        {
            get { return _targetCurrentText; }
            private set { Set(ref _targetCurrentText, value); }
        }

        public string TargetRangeText
        {
            get { return _targetRangeText; }
            private set { Set(ref _targetRangeText, value); }
        }

        public string EditNotice
        {
            get { return _editNotice; }
            private set { Set(ref _editNotice, value); }
        }

        public bool CanImport
        {
            get { return _canImport; }
            private set
            {
                if (Set(ref _canImport, value))
                    _confirmCommand.RaiseCanExecuteChanged();
            }
        }

        public bool HasBlockingIssues
        {
            get { return _hasBlockingIssues; }
            private set { Set(ref _hasBlockingIssues, value); }
        }

        public bool HasWarnings
        {
            get { return _hasWarnings; }
            private set { Set(ref _hasWarnings, value); }
        }

        public bool HasChecks
        {
            get { return _hasChecks; }
            private set { Set(ref _hasChecks, value); }
        }

        public double CropLeftWidth
        {
            get { return _cropLeftWidth; }
            private set { Set(ref _cropLeftWidth, value); }
        }

        public double CropRightLeft
        {
            get { return _cropRightLeft; }
            private set { Set(ref _cropRightLeft, value); }
        }

        public double CropTopHeight
        {
            get { return _cropTopHeight; }
            private set { Set(ref _cropTopHeight, value); }
        }

        public double CropBottomTop
        {
            get { return _cropBottomTop; }
            private set { Set(ref _cropBottomTop, value); }
        }

        private void UpdateFromInput()
        {
            UpdatePreview();

            if (!TryBuildResolution(out var resolution, true))
            {
                CancelPendingValidation();
                ApplyInputBlock();
                return;
            }

            ScheduleValidation(resolution);
        }

        private async void ScheduleValidation(CustomResolution resolution)
        {
            CancelPendingValidation();
            _validationCts = new CancellationTokenSource();
            CancellationToken token = _validationCts.Token;

            try
            {
                CompatibilityStatus = "Проверка изменений..";
                await Task.Delay(350, token).ConfigureAwait(true);
                ProfileImportPlan plan = await Task.Run(() => _revalidate(resolution), token).ConfigureAwait(true);

                if (!token.IsCancellationRequested)
                    ApplyPlan(plan);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested)
                    return;

                CanImport = false;
                ValidationMessage = "Ошибка проверки профиля: " + ex.Message;
                CompatibilityStatus = "Проверка не выполнена.";
            }
        }

        public void CancelPendingWork()
        {
            CancelPendingValidation();
        }

        private void CancelPendingValidation()
        {
            CancellationTokenSource cts = _validationCts;
            _validationCts = null;
            if (cts == null)
                return;

            cts.Cancel();
            cts.Dispose();
        }

        private void ApplyInputBlock()
        {
            CanImport = false;
            Summary = "Импорт заблокирован: неверный ввод.";
            RiskLevel = "BLOCKED";
            CompatibilityStatus = "Введите корректные ширину, высоту и частоту.";
            PixelClockText = "—";
            Replace(BlockingIssues, new[] { string.IsNullOrWhiteSpace(ValidationMessage) ? "Некорректные параметры режима." : ValidationMessage });
            Replace(Warnings, null);
            Replace(Checks, null);
            HasBlockingIssues = true;
            HasWarnings = false;
            HasChecks = false;
        }

        private static string BuildCompatibilityStatus(ProfileImportPlan plan)
        {
            if (plan == null || !plan.CanImport)
                return "Импорт заблокирован до исправления критичных пунктов.";

            if (string.Equals(plan.RiskLevel, "HIGH", StringComparison.OrdinalIgnoreCase))
                return "Можно попытаться импортировать, но риск высокий: параметры заметно выше базовых данных текущего монитора.";

            if (string.Equals(plan.RiskLevel, "MEDIUM", StringComparison.OrdinalIgnoreCase))
                return "Можно попытаться импортировать: параметры немного выходят за базовые данные текущего монитора.";

            return "Можно импортировать. Введённый режим будет проверен автоматически на совместимость.";
        }

        private void ApplyPlan(ProfileImportPlan plan)
        {
            CurrentPlan = plan;

            if (plan == null)
            {
                Summary = "Профиль не загружен.";
                RiskLevel = "BLOCKED";
                CompatibilityStatus = "Импорт невозможен.";
                PixelClockText = "—";
                SourceTitle = "Источник: —";
                SourceNativeText = "Native: —";
                SourceCurrentText = "Current: —";
                TargetTitle = "Цель: " + MonitorTitle;
                TargetNativeText = "Native: " + FormatNative(_targetMonitor == null ? 0 : _targetMonitor.NativeWidth, _targetMonitor == null ? 0 : _targetMonitor.NativeHeight);
                TargetCurrentText = "Current: " + FormatCurrent(_targetMonitor == null ? 0 : _targetMonitor.CurrentWidth, _targetMonitor == null ? 0 : _targetMonitor.CurrentHeight, _targetMonitor == null ? 0 : _targetMonitor.CurrentRefreshRate);
                TargetRangeText = "Range: " + (_targetMonitor == null ? "—" : _targetMonitor.RangeText);
                Replace(Checks, null);
                Replace(Warnings, null);
                Replace(BlockingIssues, null);
                HasChecks = false;
                HasWarnings = false;
                HasBlockingIssues = false;
                CanImport = false;
                return;
            }

            Summary = plan.Summary ?? "Импорт профиля";
            RiskLevel = plan.RiskLevel ?? "UNKNOWN";
            PixelClockText = plan.EstimatedPixelClockMhz > 0 ? plan.EstimatedPixelClockMhz.ToString("F0") + " MHz" : "—";
            CanImport = plan.CanImport;
            CompatibilityStatus = BuildCompatibilityStatus(plan);

            DisplayProfileMonitor source = plan.Profile == null ? null : plan.Profile.SourceDisplay;
            SourceTitle = "Источник: " + (source == null || string.IsNullOrWhiteSpace(source.FriendlyName) ? "—" : source.FriendlyName);
            SourceNativeText = "Native: " + FormatNative(source == null ? 0 : source.NativeWidth, source == null ? 0 : source.NativeHeight);
            SourceCurrentText = "Current: " + FormatCurrent(source == null ? 0 : source.CurrentWidth, source == null ? 0 : source.CurrentHeight, source == null ? 0 : source.CurrentRefreshRate);
            TargetTitle = "Цель: " + (_targetMonitor == null ? "—" : _targetMonitor.DisplayTitle);
            TargetNativeText = "Native: " + FormatNative(_targetMonitor == null ? 0 : _targetMonitor.NativeWidth, _targetMonitor == null ? 0 : _targetMonitor.NativeHeight);
            TargetCurrentText = "Current: " + FormatCurrent(_targetMonitor == null ? 0 : _targetMonitor.CurrentWidth, _targetMonitor == null ? 0 : _targetMonitor.CurrentHeight, _targetMonitor == null ? 0 : _targetMonitor.CurrentRefreshRate);
            TargetRangeText = "Range: " + (_targetMonitor == null ? "—" : _targetMonitor.RangeText);

            Replace(Checks, plan.Checks);
            Replace(Warnings, plan.Warnings);
            Replace(BlockingIssues, plan.BlockingIssues);
            HasChecks = Checks.Count > 0;
            HasWarnings = Warnings.Count > 0;
            HasBlockingIssues = BlockingIssues.Count > 0;
            EditNotice = BuildEditNotice(plan.Resolution);
        }

        private void Confirm()
        {
            CancelPendingValidation();

            if (!TryBuildResolution(out var resolution, true))
                return;

            ProfileImportPlan finalPlan = CurrentPlan;
            if (finalPlan == null || finalPlan.Resolution == null || !ModesMatch(finalPlan.Resolution, resolution))
                finalPlan = _revalidate(resolution);

            ApplyPlan(finalPlan);
            if (finalPlan == null || !finalPlan.CanImport)
            {
                ValidationMessage = "Импорт невозможен: исправьте критичные пункты проверки.";
                return;
            }

            ResultPlan = finalPlan;
            RequestClose?.Invoke(true);
        }

        private void Cancel()
        {
            CancelPendingValidation();
            RequestClose?.Invoke(false);
        }

        private bool TryBuildResolution(out CustomResolution resolution, bool setValidation)
        {
            resolution = null;

            if (!uint.TryParse(WidthText, out var width) ||
                !uint.TryParse(HeightText, out var height) ||
                !uint.TryParse(RefreshRateText, out var refresh))
            {
                if (setValidation) ValidationMessage = "Введите корректные ширину, высоту и частоту.";
                return false;
            }

            if (!ResolutionRules.TryValidate(width, height, refresh, out var validationMessage))
            {
                if (setValidation) ValidationMessage = validationMessage;
                return false;
            }

            ValidationMessage = null;
            resolution = ResolutionRules.Create(width, height, refresh, true);
            if (_originalResolution != null &&
                refresh == _originalResolution.RefreshRate &&
                _originalResolution.RefreshRateMilliHz > 0)
            {
                resolution.RefreshRateMilliHz = RefreshRateMath.NormalizeDisplayMilliHz(
                    refresh,
                    _originalResolution.RefreshRateMilliHz);
            }
            return true;
        }

        private void UpdatePreview()
        {
            if (uint.TryParse(WidthText, out var width) &&
                uint.TryParse(HeightText, out var height) &&
                uint.TryParse(RefreshRateText, out var refresh))
            {
                string aspect = AspectRatioMath.Format(width, height);
                PreviewLabel = width + "×" + height + " @ " + refresh + " Гц - " + aspect;
                PreviewChip = width + "×" + height;
                UpdateVisualPreview(width, height);
            }
            else
            {
                PreviewLabel = "—";
                PreviewChip = string.Empty;
                PreviewFovLabel = "Введите ширину, высоту и частоту.";
                SetCrop(0, 0, 0, 0);
            }
        }

        private void UpdateVisualPreview(uint width, uint height)
        {
            if (height == 0)
            {
                SetCrop(0, 0, 0, 0);
                return;
            }

            double baselineAspect = FovPreviewCalculator.BaselineAspect;
            double customAspect = FovPreviewCalculator.GetAspect(width, height);
            if (baselineAspect <= 0 || customAspect <= 0)
            {
                SetCrop(0, 0, 0, 0);
                return;
            }

            if (customAspect < baselineAspect)
            {
                double visible = FovPreviewCalculator.GetHorizontalVisiblePercent(width, height);
                double crop = Math.Round((PreviewWidth * (1 - visible / 100)) / 2, 1);
                SetCrop(crop, crop, 0, 0);
                PreviewFovLabel = "База 16:9 - горизонтальный FOV: " + visible.ToString("F0") + "% - обрезка по ширине: " + (100 - visible).ToString("F0") + "%";
            }
            else if (customAspect > baselineAspect)
            {
                double visible = FovPreviewCalculator.GetVerticalVisiblePercent(width, height);
                double crop = Math.Round((PreviewHeight * (1 - visible / 100)) / 2, 1);
                SetCrop(0, 0, crop, crop);
                PreviewFovLabel = "База 16:9 - вертикальный FOV: " + visible.ToString("F0") + "% - обрезка по высоте: " + (100 - visible).ToString("F0") + "%";
            }
            else
            {
                SetCrop(0, 0, 0, 0);
                PreviewFovLabel = "База 16:9 - соотношение совпадает, обрезки нет";
            }
        }

        private void SetCrop(double left, double right, double top, double bottom)
        {
            CropLeftWidth = left;
            CropRightLeft = PreviewWidth - right;
            CropTopHeight = top;
            CropBottomTop = PreviewHeight - bottom;
        }

        private uint GetNativeWidth()
        {
            if (_targetMonitor == null) return 1920;
            if (_targetMonitor.NativeWidth > 0) return _targetMonitor.NativeWidth;
            if (_targetMonitor.CurrentWidth > 0) return _targetMonitor.CurrentWidth;
            return 1920;
        }

        private uint GetNativeHeight()
        {
            if (_targetMonitor == null) return 1080;
            if (_targetMonitor.NativeHeight > 0) return _targetMonitor.NativeHeight;
            if (_targetMonitor.CurrentHeight > 0) return _targetMonitor.CurrentHeight;
            return 1080;
        }

        private static void Replace(ObservableCollection<string> target, System.Collections.Generic.IEnumerable<string> source)
        {
            target.Clear();
            if (source == null) return;
            foreach (string item in source)
                target.Add(item);
        }

        private static CustomResolution CloneResolution(CustomResolution resolution)
        {
            if (resolution == null) return null;
            return new CustomResolution
            {
                Width = resolution.Width,
                Height = resolution.Height,
                RefreshRate = resolution.RefreshRate,
                RefreshRateMilliHz = resolution.RefreshRateMilliHz,
                BitsPerPixel = 32,
                AddedByDisplayScaler = true
            };
        }

        private static bool ModesMatch(CustomResolution a, CustomResolution b)
        {
            if (a == null || b == null)
                return false;
            return a.Width == b.Width &&
                   a.Height == b.Height &&
                   a.RefreshRate == b.RefreshRate &&
                   (a.BitsPerPixel == 0 || b.BitsPerPixel == 0 || a.BitsPerPixel == b.BitsPerPixel);
        }

        private string BuildEditNotice(CustomResolution current)
        {
            if (_originalResolution == null || current == null)
                return "Можно изменить ширину, высоту и частоту перед импортом.";

            bool edited = _originalResolution.Width != current.Width ||
                          _originalResolution.Height != current.Height ||
                          Math.Abs((long)_originalResolution.RefreshRate - (long)current.RefreshRate) > 0;
            return edited
                ? "Профиль изменён перед импортом. Будет создан режим " + current.Label + "."
                : "Будет импортирован режим " + current.Label + ". Далее будут проведены тесты на совместимость.";
        }

        private static string FormatNative(uint width, uint height)
        {
            return width > 0 && height > 0 ? width + "×" + height : "—";
        }

        private static string FormatCurrent(uint width, uint height, uint refresh)
        {
            return width > 0 && height > 0 ? width + "×" + height + " @ " + refresh + " Гц" : "—";
        }
    }
}

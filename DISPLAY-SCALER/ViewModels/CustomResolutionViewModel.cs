using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Infrastructure.Mvvm;
using DISPLAY_SCALER.Models;
using System;
using System.Windows.Input;

namespace DISPLAY_SCALER.ViewModels
{

    public sealed class CustomResolutionViewModel : ViewModelBase
    {
        private const double PreviewWidth = 430;
        private const double PreviewHeight = 242;

        private readonly MonitorInfo _monitor;
        private string _widthText;
        private string _heightText;
        private string _refreshRateText;
        private string _previewLabel;
        private string _previewChip;
        private string _previewFovLabel;
        private string _validationMessage;
        private double _cropLeftWidth;
        private double _cropRightLeft;
        private double _cropTopHeight;
        private double _cropBottomTop;

        public CustomResolutionViewModel(MonitorInfo monitor, CustomResolution initialResolution = null, bool editMode = false)
        {
            _monitor = monitor;
            IsEditMode = editMode;
            MonitorTitle = monitor?.DisplayTitle ?? "Монитор";
            DialogTitle = editMode ? "ИЗМЕНИТЬ РАЗРЕШЕНИЕ" : "ДОБАВИТЬ НОВОЕ РАЗРЕШЕНИЕ";
            ConfirmButtonText = editMode ? "Сохранить" : "Добавить";

            ConfirmCommand = new Command(Confirm);
            CancelCommand = new Command(Cancel);

            if (initialResolution != null)
            {
                WidthText = initialResolution.Width.ToString();
                HeightText = initialResolution.Height.ToString();
                RefreshRateText = initialResolution.RefreshRate.ToString();
            }
            else
            {
                uint baseWidth = GetNativeWidth();
                WidthText = baseWidth.ToString();
                HeightText = RoundTo(baseWidth * 3.0 / 4.0).ToString();
                RefreshRateText = monitor != null && monitor.CurrentRefreshRate > 0
                    ? monitor.CurrentRefreshRate.ToString()
                    : "240";
            }

            UpdatePreview();
        }

        public event Action<bool?> RequestClose;

        public bool IsEditMode { get; }
        public string MonitorTitle { get; }
        public string DialogTitle { get; }
        public string ConfirmButtonText { get; }
        public CustomResolution Result { get; private set; }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public string WidthText
        {
            get => _widthText;
            set
            {
                if (Set(ref _widthText, value))
                    UpdatePreview();
            }
        }

        public string HeightText
        {
            get => _heightText;
            set
            {
                if (Set(ref _heightText, value))
                    UpdatePreview();
            }
        }

        public string RefreshRateText
        {
            get => _refreshRateText;
            set
            {
                if (Set(ref _refreshRateText, value))
                    UpdatePreview();
            }
        }

        public string PreviewLabel
        {
            get => _previewLabel;
            private set => Set(ref _previewLabel, value);
        }

        public string PreviewChip
        {
            get => _previewChip;
            private set => Set(ref _previewChip, value);
        }

        public string PreviewFovLabel
        {
            get => _previewFovLabel;
            private set => Set(ref _previewFovLabel, value);
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set => Set(ref _validationMessage, value);
        }

        public double CropLeftWidth
        {
            get => _cropLeftWidth;
            private set => Set(ref _cropLeftWidth, value);
        }

        public double CropRightLeft
        {
            get => _cropRightLeft;
            private set => Set(ref _cropRightLeft, value);
        }

        public double CropTopHeight
        {
            get => _cropTopHeight;
            private set => Set(ref _cropTopHeight, value);
        }

        public double CropBottomTop
        {
            get => _cropBottomTop;
            private set => Set(ref _cropBottomTop, value);
        }

        private static uint RoundTo(double value) => (uint)Math.Round(value);

        private void Confirm()
        {
            if (!TryBuildResolution(out CustomResolution resolution))
                return;

            Result = resolution;
            RequestClose?.Invoke(true);
        }

        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }

        private bool TryBuildResolution(out CustomResolution resolution)
        {
            resolution = null;

            if (!uint.TryParse(WidthText, out uint width) ||
                !uint.TryParse(HeightText, out uint height) ||
                !uint.TryParse(RefreshRateText, out uint refresh))
            {
                ValidationMessage = "Введите корректные ширину, высоту и частоту.";
                return false;
            }

            if (!ResolutionRules.TryValidate(width, height, refresh, out var validationMessage))
            {
                ValidationMessage = validationMessage;
                return false;
            }

            ValidationMessage = null;
            resolution = ResolutionRules.Create(width, height, refresh, false);
            return true;
        }

        private void UpdatePreview()
        {
            if (uint.TryParse(WidthText, out uint width) &&
                uint.TryParse(HeightText, out uint height) &&
                uint.TryParse(RefreshRateText, out uint refresh))
            {
                string aspect = AspectRatioMath.Format(width, height);
                PreviewLabel = $"{width}×{height} @ {refresh} Гц · {aspect}";
                PreviewChip = $"{width}×{height}";
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
                PreviewFovLabel = $"База 16:9 · горизонтальный FOV: {visible:F0}% · обрезка по ширине: {100 - visible:F0}%";
            }
            else if (customAspect > baselineAspect)
            {
                double visible = FovPreviewCalculator.GetVerticalVisiblePercent(width, height);
                double crop = Math.Round((PreviewHeight * (1 - visible / 100)) / 2, 1);
                SetCrop(0, 0, crop, crop);
                PreviewFovLabel = $"База 16:9 · вертикальный FOV: {visible:F0}% · обрезка по высоте: {100 - visible:F0}%";
            }
            else
            {
                SetCrop(0, 0, 0, 0);
                PreviewFovLabel = "База 16:9 · соотношение совпадает, обрезки нет";
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
            if (_monitor == null) return 1920;
            if (_monitor.NativeWidth > 0) return _monitor.NativeWidth;
            if (_monitor.CurrentWidth > 0) return _monitor.CurrentWidth;
            return 1920;
        }

        private uint GetNativeHeight()
        {
            if (_monitor == null) return 1080;
            if (_monitor.NativeHeight > 0) return _monitor.NativeHeight;
            if (_monitor.CurrentHeight > 0) return _monitor.CurrentHeight;
            return 1080;
        }
    }
}

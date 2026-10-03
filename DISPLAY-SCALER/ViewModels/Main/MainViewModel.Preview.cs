using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Models;
using System;

namespace DISPLAY_SCALER.ViewModels
{
    public sealed partial class MainViewModel
    {
        public bool PreviewAvailable => SelectedMonitor != null && SelectedCustomResolution != null;
        public double PreviewFrameWidth => 640;
        public double PreviewFrameHeight => 360;
        public double PreviewCropLeftWidth => GetPreviewCropLeftWidth();
        public double PreviewCropRightLeft => PreviewFrameWidth - PreviewCropLeftWidth;
        public double PreviewCropTopHeight => GetPreviewCropTopHeight();
        public double PreviewCropBottomTop => PreviewFrameHeight - PreviewCropTopHeight;
        public string PreviewNativeText => SelectedMonitor == null ? "—" : GetNativePreviewText();
        public string PreviewCustomText => SelectedCustomResolution == null ? "—" : SelectedCustomResolution.Label;
        public string PreviewFovText => GetPreviewFovText();
        public string PreviewCropText => GetPreviewCropText();
        public string PreviewModeTitle => "ПРЕДПРОСМОТР FOV";
        public string PreviewModeText => GetPreviewFovText();
        public string PreviewModeDetailText => GetPreviewCropText();

        private void RaisePreviewProperties()
        {
            OnPropertyChanged(nameof(PreviewAvailable));
            OnPropertyChanged(nameof(PreviewFrameWidth));
            OnPropertyChanged(nameof(PreviewFrameHeight));
            OnPropertyChanged(nameof(PreviewCropLeftWidth));
            OnPropertyChanged(nameof(PreviewCropRightLeft));
            OnPropertyChanged(nameof(PreviewCropTopHeight));
            OnPropertyChanged(nameof(PreviewCropBottomTop));
            OnPropertyChanged(nameof(PreviewNativeText));
            OnPropertyChanged(nameof(PreviewCustomText));
            OnPropertyChanged(nameof(PreviewFovText));
            OnPropertyChanged(nameof(PreviewCropText));
            OnPropertyChanged(nameof(PreviewModeTitle));
            OnPropertyChanged(nameof(PreviewModeText));
            OnPropertyChanged(nameof(PreviewModeDetailText));
        }

        private double BaselineAspect
        {
            get { return FovPreviewCalculator.BaselineAspect; }
        }

        private double CustomAspect
        {
            get
            {
                if (SelectedCustomResolution == null || SelectedCustomResolution.Height == 0) return BaselineAspect;
                return FovPreviewCalculator.GetAspect(SelectedCustomResolution.Width, SelectedCustomResolution.Height);
            }
        }

        private double HorizontalFovPercent
        {
            get
            {
                if (SelectedCustomResolution == null) return 100;
                return FovPreviewCalculator.GetHorizontalVisiblePercent(SelectedCustomResolution.Width, SelectedCustomResolution.Height);
            }
        }

        private double VerticalFovPercent
        {
            get
            {
                if (SelectedCustomResolution == null) return 100;
                return FovPreviewCalculator.GetVerticalVisiblePercent(SelectedCustomResolution.Width, SelectedCustomResolution.Height);
            }
        }

        private double GetPreviewCropLeftWidth()
        {
            if (!PreviewAvailable || CustomAspect >= BaselineAspect) return 0;
            return Math.Round((PreviewFrameWidth * (1 - HorizontalFovPercent / 100)) / 2, 1);
        }

        private double GetPreviewCropTopHeight()
        {
            if (!PreviewAvailable || CustomAspect <= BaselineAspect) return 0;
            return Math.Round((PreviewFrameHeight * (1 - VerticalFovPercent / 100)) / 2, 1);
        }

        private string GetNativePreviewText()
        {
            return FovPreviewCalculator.BaselineLabel;
        }

        private string GetPreviewFovText()
        {
            if (!PreviewAvailable) return "Выберите кастомное разрешение";
            if (CustomAspect < BaselineAspect)
                return $"Горизонтальный FOV: {HorizontalFovPercent:F0}% от 16:9";
            if (CustomAspect > BaselineAspect)
                return $"Вертикальный FOV: {VerticalFovPercent:F0}% от 16:9";
            return "FOV совпадает с 16:9";
        }

        private string GetPreviewCropText()
        {
            if (!PreviewAvailable) return "—";
            if (CustomAspect < BaselineAspect)
                return $"Обрезка по ширине: {100 - HorizontalFovPercent:F0}%";
            if (CustomAspect > BaselineAspect)
                return $"Обрезка по высоте: {100 - VerticalFovPercent:F0}%";
            return "Без обрезки по соотношению сторон";
        }
    }
}

using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Models;
using System;
using System.Windows;
using System.Windows.Input;

namespace DISPLAY_SCALER.Views
{
    public partial class UnsupportedModeDialog : Window
    {
        public UnsupportedModeDialog(CustomResolution mode)
        {
            ModeLabel = mode == null ? "Выбранный режим" : mode.Width + " × " + mode.Height + " @ " + RefreshRateMath.Format(mode.RefreshRate, mode.RefreshRateMilliHz);
            DescriptionText = BuildDescription();
            DataContext = this;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public string ModeLabel { get; private set; }

        public string DescriptionText { get; private set; }

        private static string BuildDescription()
        {
            return "Монитор или драйвер отклонил данное разрешение с этой частотой во время NVAPI/Windows-проверки. Режим не был сохранён, предыдущие настройки оставлены без изменений.";
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Rect workArea = SystemParameters.WorkArea;
            MaxWidth = Math.Max(MinWidth, workArea.Width - 32);
            MaxHeight = Math.Max(MinHeight, workArea.Height - 32);

            if (Width > MaxWidth) Width = MaxWidth;
            if (Height > MaxHeight) Height = MaxHeight;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void TitleDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Native;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace DISPLAY_SCALER.Views
{
    public partial class TimedApplyDialog : Window, INotifyPropertyChanged
    {
        private readonly DispatcherTimer _timer;
        private int _secondsLeft;
        private bool _closedByButton;
        private readonly MonitorInfo _targetMonitor;

        public TimedApplyDialog(CustomResolution mode, int seconds)
            : this(mode, seconds, null)
        {
        }

        public TimedApplyDialog(CustomResolution mode, int seconds, MonitorInfo targetMonitor)
        {
            _targetMonitor = targetMonitor;
            ModeLabel = "20-ти секундный тест на совместимость";
            DescriptionText = mode == null
                ? "Сохранить это разрешение?"
                : "Применено пользовательское разрешение " + mode.Width + " × " + mode.Height + " с частотой " + RefreshRateMath.Format(mode.RefreshRate, mode.RefreshRateMilliHz) + ". Сохранить это разрешение?";
            SecondsLeft = seconds <= 0 ? 20 : seconds;
            DataContext = this;
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            Loaded += OnLoaded;
            Closing += OnClosing;
            Closed += OnClosed;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string ModeLabel { get; private set; }

        public string DescriptionText { get; private set; }

        public int SecondsLeft
        {
            get { return _secondsLeft; }
            private set
            {
                if (_secondsLeft == value) return;
                _secondsLeft = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CountdownText));
                OnPropertyChanged(nameof(RollbackButtonText));
            }
        }

        public string CountdownText
        {
            get { return "Восстановление через: " + SecondsLeft + " " + GetSecondsWord(SecondsLeft) + "."; }
        }

        public string RollbackButtonText
        {
            get { return "Отменить"; }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ConfigureAdaptiveBounds();
            Topmost = true;
            Activate();
            Focus();
            _timer.Start();
        }

        private void ConfigureAdaptiveBounds()
        {
            Rect workArea = GetTargetWorkAreaInWpfUnits();
            MaxWidth = Math.Max(MinWidth, workArea.Width - 32);
            MaxHeight = Math.Max(MinHeight, workArea.Height - 32);

            if (Width > MaxWidth) Width = MaxWidth;
            if (Height > MaxHeight) Height = MaxHeight;

            double dialogWidth = ActualWidth > 0 ? ActualWidth : Width;
            double dialogHeight = ActualHeight > 0 ? ActualHeight : Height;

            Left = workArea.Left + Math.Max(16, (workArea.Width - dialogWidth) / 2.0);
            Top = workArea.Top + Math.Max(16, (workArea.Height - dialogHeight) / 2.0);
        }

        private Rect GetTargetWorkAreaInWpfUnits()
        {
            Rect fallback = SystemParameters.WorkArea;
            if (_targetMonitor == null || string.IsNullOrWhiteSpace(_targetMonitor.DeviceName))
                return fallback;

            try
            {
                double scaleX;
                double scaleY;
                GetWpfScale(out scaleX, out scaleY);

                Rect monitorWorkArea;
                if (TryGetTargetMonitorWorkArea(out monitorWorkArea))
                    return ScalePhysicalRectToWpf(monitorWorkArea, scaleX, scaleY);

                var dm = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(_targetMonitor.DeviceName, Win32Display.ENUM_CURRENT_SETTINGS, ref dm, 0) == 0 ||
                    dm.dmPelsWidth == 0 || dm.dmPelsHeight == 0)
                {
                    return fallback;
                }

                return new Rect(
                    dm.dmPositionX / scaleX,
                    dm.dmPositionY / scaleY,
                    dm.dmPelsWidth / scaleX,
                    dm.dmPelsHeight / scaleY);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("TimedApplyDialog target placement failed: " + ex.Message);
                return fallback;
            }
        }

        private bool TryGetTargetMonitorWorkArea(out Rect workArea)
        {
            workArea = Rect.Empty;
            string targetDeviceName = _targetMonitor == null ? null : _targetMonitor.DeviceName;
            if (string.IsNullOrWhiteSpace(targetDeviceName))
                return false;

            bool found = false;
            Rect foundWorkArea = Rect.Empty;
            Win32Display.MonitorEnumProc callback = delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref Win32Display.RECT lprcMonitor, IntPtr dwData)
            {
                var info = new Win32Display.MONITORINFOEX();
                info.cbSize = Marshal.SizeOf(typeof(Win32Display.MONITORINFOEX));
                if (Win32Display.GetMonitorInfoW(hMonitor, ref info) &&
                    string.Equals(info.szDevice, targetDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    foundWorkArea = new Rect(
                        info.rcWork.Left,
                        info.rcWork.Top,
                        Math.Max(1, info.rcWork.Right - info.rcWork.Left),
                        Math.Max(1, info.rcWork.Bottom - info.rcWork.Top));
                    found = true;
                    return false;
                }

                return true;
            };

            Win32Display.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            if (found)
                workArea = foundWorkArea;
            return found;
        }

        private void GetWpfScale(out double scaleX, out double scaleY)
        {
            scaleX = 1.0;
            scaleY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            if (scaleX <= 0.0) scaleX = 1.0;
            if (scaleY <= 0.0) scaleY = 1.0;
        }

        private static Rect ScalePhysicalRectToWpf(Rect physical, double scaleX, double scaleY)
        {
            if (scaleX <= 0.0) scaleX = 1.0;
            if (scaleY <= 0.0) scaleY = 1.0;
            return new Rect(
                physical.Left / scaleX,
                physical.Top / scaleY,
                Math.Max(1.0, physical.Width / scaleX),
                Math.Max(1.0, physical.Height / scaleY));
        }

        private static Win32Display.DEVMODE CreateDevMode()
        {
            var dm = new Win32Display.DEVMODE();
            dm.dmSize = (ushort)Marshal.SizeOf(typeof(Win32Display.DEVMODE));
            return dm;
        }

        private static string GetSecondsWord(int seconds)
        {
            int value = Math.Abs(seconds) % 100;
            int lastDigit = value % 10;

            if (value >= 11 && value <= 14) return "секунд";
            if (lastDigit == 1) return "секунду";
            if (lastDigit >= 2 && lastDigit <= 4) return "секунды";
            return "секунд";
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            SecondsLeft--;
            if (SecondsLeft > 0) return;

            _timer.Stop();
            _closedByButton = true;
            DialogResult = false;
            Close();
        }

        private void Keep_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _closedByButton = true;
            DialogResult = true;
            Close();
        }

        private void Rollback_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _closedByButton = true;
            DialogResult = false;
            Close();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            _timer.Stop();
            if (!_closedByButton)
            {
                try { DialogResult = false; } catch (InvalidOperationException ex) { System.Diagnostics.Debug.WriteLine("TimedApplyDialog DialogResult reset failed: " + ex.Message); }
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            Loaded -= OnLoaded;
            Closing -= OnClosing;
            Closed -= OnClosed;
        }

        private void TitleDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, DISPLAY_SCALER.Infrastructure.Mvvm.PropertyChangedEventArgsCache.Get(propertyName));
        }
    }
}
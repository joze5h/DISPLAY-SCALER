using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace DISPLAY_SCALER.Views
{
    public partial class MainWindow : Window
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null)
                source.AddHook(WndProc);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnableDarkTitleBar();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                ApplyMaximizedBounds(hwnd, lParam);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
                return;
            }

            try
            {
                DragMove();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DragMove failed: " + ex.Message);
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ResolutionList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            if (listBox == null)
                return;

            ListBoxItem item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (item == null || !item.IsSelected)
                return;

            listBox.SelectedItem = null;
            e.Handled = true;
        }

        private static T FindVisualParent<T>(DependencyObject source) where T : DependencyObject
        {
            while (source != null)
            {
                T match = source as T;
                if (match != null)
                    return match;

                source = GetSafeParent(source);
            }

            return null;
        }

        private static DependencyObject GetSafeParent(DependencyObject source)
        {
            if (source == null)
                return null;

            FrameworkContentElement contentElement = source as FrameworkContentElement;
            if (contentElement != null)
            {
                DependencyObject parent = contentElement.Parent;
                if (parent != null)
                    return parent;

                parent = ContentOperations.GetParent(contentElement);
                if (parent != null)
                    return parent;

                return LogicalTreeHelper.GetParent(contentElement);
            }

            if (source is Visual || source is Visual3D)
            {
                DependencyObject parent = VisualTreeHelper.GetParent(source);
                if (parent != null)
                    return parent;
            }

            FrameworkElement frameworkElement = source as FrameworkElement;
            if (frameworkElement != null && frameworkElement.Parent != null)
                return frameworkElement.Parent;

            return LogicalTreeHelper.GetParent(source);
        }

        private void PreviewViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!(sender is FrameworkElement element))
                return;

            if (e.NewSize.Width <= 0)
                return;

            double targetHeight = Math.Round(e.NewSize.Width * 9.0 / 16.0);
            if (targetHeight < 260)
                targetHeight = 260;

            if (Math.Abs(element.Height - targetHeight) > 0.5)
                element.Height = targetHeight;
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void ApplyMaximizedBounds(IntPtr hwnd, IntPtr lParam)
        {
            if (lParam == IntPtr.Zero)
                return;

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
                return;

            MONITORINFO monitorInfo = new MONITORINFO();
            if (!GetMonitorInfo(monitor, monitorInfo))
                return;

            MINMAXINFO minMaxInfo = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            RECT workArea = monitorInfo.rcWork;
            RECT monitorArea = monitorInfo.rcMonitor;

            minMaxInfo.ptMaxPosition.x = workArea.Left - monitorArea.Left;
            minMaxInfo.ptMaxPosition.y = workArea.Top - monitorArea.Top;
            minMaxInfo.ptMaxSize.x = workArea.Right - workArea.Left;
            minMaxInfo.ptMaxSize.y = workArea.Bottom - workArea.Top;

            MatrixToDeviceScale(out double dpiX, out double dpiY);
            minMaxInfo.ptMinTrackSize.x = Math.Max(1, (int)Math.Ceiling(MinWidth * dpiX));
            minMaxInfo.ptMinTrackSize.y = Math.Max(1, (int)Math.Ceiling(MinHeight * dpiY));

            Marshal.StructureToPtr(minMaxInfo, lParam, true);
        }

        private void MatrixToDeviceScale(out double dpiX, out double dpiY)
        {
            dpiX = 1.0;
            dpiY = 1.0;

            try
            {
                PresentationSource source = PresentationSource.FromVisual(this);
                if (source != null && source.CompositionTarget != null)
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DPI read failed: " + ex.Message);
                dpiX = 1.0;
                dpiY = 1.0;
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

        private void EnableDarkTitleBar()
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                int dark = 1;

                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref dark, sizeof(int));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DWM dark mode setup failed: " + ex.Message);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private sealed class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }
    }
}
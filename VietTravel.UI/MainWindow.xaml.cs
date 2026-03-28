using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using VietTravel.UI.ViewModels;

namespace VietTravel.UI
{
    public partial class MainWindow : Window
    {
        private const int MonitorDefaultToNearest = 2;
        private const double WindowPadding = 24d;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
        }

        private void Window_OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyCurrentMonitorLimits();
            EnsureWindowFitsCurrentMonitor();
        }

        private void Window_OnLocationChanged(object sender, EventArgs e)
        {
            ApplyCurrentMonitorLimits();
        }

        private void MainContentClipHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            const double cornerRadius = 16d;
            MainContentClipHost.Clip = new RectangleGeometry(
                new Rect(0, 0, MainContentClipHost.ActualWidth, MainContentClipHost.ActualHeight),
                cornerRadius,
                cornerRadius);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    Maximize_Click(sender, e);
                }
                else
                {
                    this.DragMove();
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowMaximizeState();
        }

        private void ToggleWindowMaximizeState()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                return;
            }

            ApplyCurrentMonitorLimits();
            this.WindowState = WindowState.Maximized;
        }

        private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat)
            {
                return;
            }

            if (e.Key == Key.F11)
            {
                ToggleWindowMaximizeState();
                e.Handled = true;
                return;
            }

            if (e.Key != Key.LeftShift && e.Key != Key.RightShift)
            {
                return;
            }

            if (DataContext is MainViewModel viewModel)
            {
                viewModel.RegisterShiftPress();
            }
        }

        private void ApplyCurrentMonitorLimits()
        {
            Rect workArea = GetCurrentMonitorWorkArea();
            MaxWidth = workArea.Width;
            MaxHeight = workArea.Height;
        }

        private void EnsureWindowFitsCurrentMonitor()
        {
            Rect workArea = GetCurrentMonitorWorkArea();
            double maxAllowedWidth = Math.Max(MinWidth, workArea.Width - WindowPadding);
            double maxAllowedHeight = Math.Max(MinHeight, workArea.Height - WindowPadding);

            bool sizeAdjusted = false;

            if (Width > maxAllowedWidth)
            {
                Width = maxAllowedWidth;
                sizeAdjusted = true;
            }

            if (Height > maxAllowedHeight)
            {
                Height = maxAllowedHeight;
                sizeAdjusted = true;
            }

            if (!sizeAdjusted)
            {
                return;
            }

            Left = workArea.Left + ((workArea.Width - Width) / 2d);
            Top = workArea.Top + ((workArea.Height - Height) / 2d);
        }

        private Rect GetCurrentMonitorWorkArea()
        {
            Rect fallback = SystemParameters.WorkArea;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return fallback;
            }

            IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return fallback;
            }

            MONITORINFO monitorInfo = new MONITORINFO
            {
                cbSize = Marshal.SizeOf<MONITORINFO>()
            };

            return GetMonitorInfo(monitor, ref monitorInfo)
                ? new Rect(
                    monitorInfo.rcWork.Left,
                    monitorInfo.rcWork.Top,
                    monitorInfo.rcWork.Right - monitorInfo.rcWork.Left,
                    monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top)
                : fallback;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }
    }
}


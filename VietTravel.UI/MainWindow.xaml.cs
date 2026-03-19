using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VietTravel.UI.ViewModels;

namespace VietTravel.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
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
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat)
            {
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
    }
}


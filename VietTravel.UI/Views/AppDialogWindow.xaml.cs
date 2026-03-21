using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace VietTravel.UI.Views
{
    public partial class AppDialogWindow : Window
    {
        private MessageBoxResult _closeResult;
        private MessageBoxResult _primaryResult;
        private MessageBoxResult _secondaryResult;
        private MessageBoxResult _tertiaryResult;

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public AppDialogWindow(
            string? caption,
            string? message,
            MessageBoxButton button,
            MessageBoxImage image)
        {
            InitializeComponent();

            DialogTitleText.Text = string.IsNullOrWhiteSpace(caption) ? "Thông báo" : caption.Trim();
            DialogMessageText.Text = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();

            ConfigureIcon(image);
            ConfigureButtons(button);
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void ConfigureIcon(MessageBoxImage image)
        {
            switch (image)
            {
                case MessageBoxImage.Error:
                    DialogIcon.Kind = PackIconKind.AlertCircleOutline;
                    DialogIcon.Foreground = ResolveBrush("ErrorBrush", "#FF3B30");
                    break;
                case MessageBoxImage.Warning:
                    DialogIcon.Kind = PackIconKind.AlertOutline;
                    DialogIcon.Foreground = ResolveBrush("WarningBrush", "#FF9500");
                    break;
                case MessageBoxImage.Information:
                    DialogIcon.Kind = PackIconKind.InformationOutline;
                    DialogIcon.Foreground = ResolveBrush("PrimaryBrush", "#007AFF");
                    break;
                case MessageBoxImage.Question:
                    DialogIcon.Kind = PackIconKind.HelpCircleOutline;
                    DialogIcon.Foreground = ResolveBrush("PrimaryBrush", "#007AFF");
                    break;
                default:
                    DialogIcon.Kind = PackIconKind.BellOutline;
                    DialogIcon.Foreground = ResolveBrush("TextSecondaryBrush", "#8E8E93");
                    break;
            }
        }

        private void ConfigureButtons(MessageBoxButton button)
        {
            PrimaryButton.Click -= OnPrimaryButtonClick;
            SecondaryButton.Click -= OnSecondaryButtonClick;
            TertiaryButton.Click -= OnTertiaryButtonClick;

            PrimaryButton.Visibility = Visibility.Visible;
            SecondaryButton.Visibility = Visibility.Collapsed;
            TertiaryButton.Visibility = Visibility.Collapsed;

            switch (button)
            {
                case MessageBoxButton.OK:
                    PrimaryButton.Content = "Đồng ý";
                    _primaryResult = MessageBoxResult.OK;
                    _secondaryResult = MessageBoxResult.None;
                    _tertiaryResult = MessageBoxResult.None;
                    _closeResult = MessageBoxResult.OK;
                    break;
                case MessageBoxButton.OKCancel:
                    SecondaryButton.Visibility = Visibility.Visible;
                    SecondaryButton.Content = "Hủy";
                    PrimaryButton.Content = "Đồng ý";
                    _primaryResult = MessageBoxResult.OK;
                    _secondaryResult = MessageBoxResult.Cancel;
                    _tertiaryResult = MessageBoxResult.None;
                    _closeResult = MessageBoxResult.Cancel;
                    break;
                case MessageBoxButton.YesNo:
                    SecondaryButton.Visibility = Visibility.Visible;
                    SecondaryButton.Content = "Không";
                    PrimaryButton.Content = "Đồng ý";
                    _primaryResult = MessageBoxResult.Yes;
                    _secondaryResult = MessageBoxResult.No;
                    _tertiaryResult = MessageBoxResult.None;
                    _closeResult = MessageBoxResult.No;
                    break;
                case MessageBoxButton.YesNoCancel:
                    TertiaryButton.Visibility = Visibility.Visible;
                    TertiaryButton.Content = "Hủy";
                    SecondaryButton.Visibility = Visibility.Visible;
                    SecondaryButton.Content = "Không";
                    PrimaryButton.Content = "Đồng ý";
                    _primaryResult = MessageBoxResult.Yes;
                    _secondaryResult = MessageBoxResult.No;
                    _tertiaryResult = MessageBoxResult.Cancel;
                    _closeResult = MessageBoxResult.Cancel;
                    break;
                default:
                    PrimaryButton.Content = "Đồng ý";
                    _primaryResult = MessageBoxResult.OK;
                    _secondaryResult = MessageBoxResult.None;
                    _tertiaryResult = MessageBoxResult.None;
                    _closeResult = MessageBoxResult.OK;
                    break;
            }

            PrimaryButton.Click += OnPrimaryButtonClick;
            SecondaryButton.Click += OnSecondaryButtonClick;
            TertiaryButton.Click += OnTertiaryButtonClick;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Result = _closeResult;
                Close();
                e.Handled = true;
            }
        }

        private void OnPrimaryButtonClick(object sender, RoutedEventArgs e)
        {
            Result = _primaryResult;
            Close();
        }

        private void OnSecondaryButtonClick(object sender, RoutedEventArgs e)
        {
            Result = _secondaryResult == MessageBoxResult.None
                ? _closeResult
                : _secondaryResult;
            Close();
        }

        private void OnTertiaryButtonClick(object sender, RoutedEventArgs e)
        {
            Result = _tertiaryResult == MessageBoxResult.None
                ? _closeResult
                : _tertiaryResult;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            PreviewKeyDown -= OnPreviewKeyDown;

            if (Result == MessageBoxResult.None)
            {
                Result = _closeResult;
            }

            base.OnClosed(e);
        }

        private static Brush ResolveBrush(string key, string fallbackColorHex)
        {
            if (Application.Current?.Resources[key] is Brush brush)
            {
                return brush;
            }

            return (SolidColorBrush)new BrushConverter().ConvertFromString(fallbackColorHex)!;
        }
    }
}

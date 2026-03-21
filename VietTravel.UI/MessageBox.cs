using System.Windows;
using VietTravel.UI.Services;

namespace VietTravel.UI
{
    public static class MessageBox
    {
        public static MessageBoxResult Show(string? messageBoxText)
        {
            return AppDialogService.Show(messageBoxText);
        }

        public static MessageBoxResult Show(string? messageBoxText, string? caption)
        {
            return AppDialogService.Show(messageBoxText, caption);
        }

        public static MessageBoxResult Show(
            string? messageBoxText,
            string? caption,
            MessageBoxButton button,
            MessageBoxImage icon)
        {
            return AppDialogService.Show(messageBoxText, caption, button, icon);
        }
    }
}

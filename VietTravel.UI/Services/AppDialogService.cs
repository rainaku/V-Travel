using System.Linq;
using System.Windows;
using VietTravel.UI.Views;

namespace VietTravel.UI.Services
{
    public static class AppDialogService
    {
        public static MessageBoxResult Show(
            string? messageBoxText,
            string? caption = "Thông báo",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None)
        {
            if (Application.Current?.Dispatcher == null)
            {
                return System.Windows.MessageBox.Show(messageBoxText, caption, button, icon);
            }

            if (Application.Current.Dispatcher.CheckAccess())
            {
                return ShowInternal(messageBoxText, caption, button, icon);
            }

            return Application.Current.Dispatcher.Invoke(() =>
                ShowInternal(messageBoxText, caption, button, icon));
        }

        private static MessageBoxResult ShowInternal(
            string? messageBoxText,
            string? caption,
            MessageBoxButton button,
            MessageBoxImage icon)
        {
            var owner = ResolveOwnerWindow();
            var dialog = new AppDialogWindow(caption, messageBoxText, button, icon);

            if (owner != null)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.ShowDialog();
            return dialog.Result;
        }

        private static Window? ResolveOwnerWindow()
        {
            var windows = Application.Current?.Windows;
            if (windows == null || windows.Count == 0)
            {
                return null;
            }

            return windows
                       .OfType<Window>()
                       .FirstOrDefault(w => w.IsActive && w.IsVisible)
                   ?? windows
                       .OfType<Window>()
                       .FirstOrDefault(w => w.IsVisible)
                   ?? Application.Current?.MainWindow;
        }
    }
}

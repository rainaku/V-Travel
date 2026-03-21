using System.Windows;
using System.Windows.Controls;

namespace VietTravel.UI.Views
{
    public partial class AdminShellView : UserControl
    {
        public AdminShellView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyAdaptiveLayout(ActualWidth);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyAdaptiveLayout(e.NewSize.Width);
        }

        private void ApplyAdaptiveLayout(double width)
        {
            if (width <= 0)
            {
                return;
            }

            if (width < 1080)
            {
                SidebarColumn.Width = new GridLength(176);
                BrandSubtitleText.Visibility = Visibility.Collapsed;
                UserRoleText.Visibility = Visibility.Collapsed;
                return;
            }

            if (width < 1280)
            {
                SidebarColumn.Width = new GridLength(200);
                BrandSubtitleText.Visibility = Visibility.Collapsed;
                UserRoleText.Visibility = Visibility.Visible;
                return;
            }

            SidebarColumn.Width = new GridLength(240);
            BrandSubtitleText.Visibility = Visibility.Visible;
            UserRoleText.Visibility = Visibility.Visible;
        }
    }
}

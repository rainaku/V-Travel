using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using VietTravel.UI.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class AdminShellViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly NotificationCenterService _notificationCenter;

        [ObservableProperty]
        private ObservableObject _currentPageViewModel;

        [ObservableProperty]
        private string _selectedMenuItem = "Dashboard";

        public string FullName => _mainViewModel.CurrentUser?.FullName ?? "Quản Trị Viên";
        public string AvatarUrl => _mainViewModel.CurrentUser?.AvatarUrl ?? string.Empty;
        public string UserRole => _mainViewModel.CurrentUser?.Role ?? "Admin";
        public bool IsGuideRole => string.Equals(UserRole, "Guide", System.StringComparison.OrdinalIgnoreCase);
        public bool IsNonGuideRole => !IsGuideRole;
        public string UserInitials => GetInitials(FullName);
        public int NotificationUnreadCount => _notificationCenter.UnreadCount;
        public bool HasUnreadNotifications => NotificationUnreadCount > 0;
        public bool IsDebugMenuVisible => _mainViewModel.IsDebugMenuVisible;

        public AdminShellViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _notificationCenter = _mainViewModel.NotificationCenter;
            _notificationCenter.PropertyChanged += NotificationCenterOnPropertyChanged;
            _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
            if (IsGuideRole)
            {
                _selectedMenuItem = "Guides";
                _currentPageViewModel = new GuideManagementViewModel(_mainViewModel);
            }
            else
            {
                _currentPageViewModel = new DashboardViewModel(_mainViewModel, this);
            }
        }

        [RelayCommand]
        public void NavigateToPage(string pageName)
        {
            if (IsGuideRole && !string.Equals(pageName, "Guides", System.StringComparison.Ordinal))
            {
                pageName = "Guides";
            }

            if (SelectedMenuItem == pageName) return;
            SelectedMenuItem = pageName;

            CurrentPageViewModel = pageName switch
            {
                "Dashboard" => new DashboardViewModel(_mainViewModel, this),
                "Tours" => new TourListViewModel(_mainViewModel),
                "Departures" => new DepartureListViewModel(_mainViewModel),
                "Bookings" => new BookingListViewModel(_mainViewModel),
                "Guides" => new GuideManagementViewModel(_mainViewModel),
                "Customers" => new CustomerListViewModel(_mainViewModel),
                "Users" => new UserManagementViewModel(_mainViewModel),
                "Payments" => new PaymentListViewModel(_mainViewModel),
                "Promotions" => new PromoCodeManagementViewModel(_mainViewModel),
                "Notifications" => new NotificationListViewModel(_mainViewModel),
                "Debug" => new DebugToolsViewModel(_mainViewModel),
                "Reports" => new ReportViewModel(_mainViewModel),
                "Profile" => new AdminProfileViewModel(_mainViewModel),
                _ => new DashboardViewModel(_mainViewModel, this)
            };
        }

        [RelayCommand]
        public void Logout()
        {
            _notificationCenter.PropertyChanged -= NotificationCenterOnPropertyChanged;
            _mainViewModel.PropertyChanged -= MainViewModelOnPropertyChanged;
            _mainViewModel.StopNotifications();
            _mainViewModel.CurrentUser = null;
            _mainViewModel.NavigateTo(new LoginViewModel(_mainViewModel));
        }

        private void NotificationCenterOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NotificationCenterService.UnreadCount))
            {
                OnPropertyChanged(nameof(NotificationUnreadCount));
                OnPropertyChanged(nameof(HasUnreadNotifications));
            }
        }

        private void MainViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsDebugMenuVisible))
            {
                OnPropertyChanged(nameof(IsDebugMenuVisible));
            }

            if (e.PropertyName == nameof(MainViewModel.CurrentUser))
            {
                OnPropertyChanged(nameof(FullName));
                OnPropertyChanged(nameof(AvatarUrl));
                OnPropertyChanged(nameof(UserInitials));
                OnPropertyChanged(nameof(UserRole));
                OnPropertyChanged(nameof(IsGuideRole));
                OnPropertyChanged(nameof(IsNonGuideRole));
            }
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ');
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            return parts[0][0].ToString().ToUpper();
        }
    }
}

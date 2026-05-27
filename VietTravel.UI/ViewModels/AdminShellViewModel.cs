using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using VietTravel.UI.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class AdminShellViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly NotificationCenterService _notificationCenter;
        private readonly Dictionary<string, ObservableObject> _viewModelCache = new();

        [ObservableProperty]
        private ObservableObject _currentPageViewModel;

        [ObservableProperty]
        private string _selectedMenuItem = "Dashboard";

        public string FullName => _mainViewModel.CurrentUser?.FullName ?? "Quản Trị Viên";
        public string AvatarUrl => _mainViewModel.CurrentUser?.AvatarUrl ?? string.Empty;
        public string UserRole => _mainViewModel.CurrentUser?.Role ?? "Admin";
        public bool IsGuideRole => string.Equals(UserRole, "Guide", System.StringComparison.OrdinalIgnoreCase);
        public bool IsNonGuideRole => !IsGuideRole;
        public bool IsAdminOrHigher => GetRoleLevel(UserRole) >= GetRoleLevel("Admin");
        public bool IsEmployeeOrHigher => GetRoleLevel(UserRole) >= GetRoleLevel("Employee");
        public bool CanAccessAdminOnlyModules => IsAdminOrHigher;
        public string UserInitials => GetInitials(FullName);
        public int NotificationUnreadCount => _notificationCenter.UnreadCount;
        public bool HasUnreadNotifications => NotificationUnreadCount > 0;
        public bool IsDebugMenuVisible => _mainViewModel.IsDebugMenuVisible;

        // Granular permissions (RBAC)
        /// <summary>Tạo/sửa tour: chỉ Admin trở lên.</summary>
        public bool CanManageTours => IsAdminOrHigher;
        /// <summary>Sửa giá tour/departure: chỉ Admin trở lên.</summary>
        public bool CanEditPricing => IsAdminOrHigher;
        /// <summary>Tạo booking: Employee trở lên.</summary>
        public bool CanCreateBooking => IsEmployeeOrHigher;
        /// <summary>Xóa/hủy booking: chỉ Admin trở lên.</summary>
        public bool CanCancelBooking => IsAdminOrHigher;
        /// <summary>Quản lý khách hàng: chỉ Admin trở lên.</summary>
        public bool CanManageCustomers => IsAdminOrHigher;
        /// <summary>Quản lý user/phân quyền: chỉ Admin trở lên.</summary>
        public bool CanManageUsers => IsAdminOrHigher;
        /// <summary>Quản lý thanh toán: chỉ Admin trở lên.</summary>
        public bool CanManagePayments => IsAdminOrHigher;
        /// <summary>Quản lý mã giảm giá: chỉ Admin trở lên.</summary>
        public bool CanManagePromotions => IsAdminOrHigher;
        /// <summary>Xem báo cáo: Employee trở lên.</summary>
        public bool CanViewReports => IsEmployeeOrHigher;
        /// <summary>Quản lý departures: chỉ Admin trở lên.</summary>
        public bool CanManageDepartures => IsAdminOrHigher;

        public AdminShellViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _notificationCenter = _mainViewModel.NotificationCenter;
            _notificationCenter.PropertyChanged += NotificationCenterOnPropertyChanged;
            _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
            if (IsGuideRole)
            {
                _selectedMenuItem = "Guides";
                _currentPageViewModel = GetOrCreateViewModel("Guides");
            }
            else
            {
                _currentPageViewModel = GetOrCreateViewModel("Dashboard");
            }
        }

        [RelayCommand]
        public void NavigateToPage(string pageName)
        {
            var isBlockedByPermission = false;

            if (IsGuideRole && !string.Equals(pageName, "Guides", System.StringComparison.Ordinal))
            {
                pageName = "Guides";
                isBlockedByPermission = true;
            }
            else if (IsAdminOnlyPage(pageName) && !CanAccessAdminOnlyModules)
            {
                pageName = "Dashboard";
                isBlockedByPermission = true;
            }
            else if (IsEmployeeOrHigherPage(pageName) && !IsEmployeeOrHigher)
            {
                pageName = "Dashboard";
                isBlockedByPermission = true;
            }

            if (isBlockedByPermission)
            {
                MessageBox.Show(
                    "Chỉ Admin trở lên mới được truy cập mục này.",
                    "Không đủ quyền",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            if (SelectedMenuItem == pageName) return;
            SelectedMenuItem = pageName;

            CurrentPageViewModel = GetOrCreateViewModel(pageName);
        }

        private ObservableObject GetOrCreateViewModel(string pageName)
        {
            if (_viewModelCache.TryGetValue(pageName, out var cached))
                return cached;

            var vm = pageName switch
            {
                "Dashboard" => (ObservableObject)new DashboardViewModel(_mainViewModel, this),
                "Tours" => new TourListViewModel(_mainViewModel),
                "Departures" => new DepartureListViewModel(_mainViewModel),
                "Bookings" => new BookingListViewModel(_mainViewModel),
                "Guides" => new GuideManagementViewModel(_mainViewModel),
                "Customers" => new CustomerListViewModel(_mainViewModel),
                "TourRatings" => new RatingManagementViewModel(_mainViewModel),
                "GuideRatings" => new GuideRatingManagementViewModel(_mainViewModel),
                "Ratings" => new RatingManagementViewModel(_mainViewModel),
                "Users" => new UserManagementViewModel(_mainViewModel),
                "Payments" => new PaymentListViewModel(_mainViewModel),
                "Promotions" => new PromoCodeManagementViewModel(_mainViewModel),
                "Notifications" => new NotificationListViewModel(_mainViewModel),
                "Debug" => new DebugToolsViewModel(_mainViewModel),
                "Reports" => new ReportViewModel(_mainViewModel),
                "Profile" => new AdminProfileViewModel(_mainViewModel),
                _ => new DashboardViewModel(_mainViewModel, this)
            };

            _viewModelCache[pageName] = vm;
            return vm;
        }

        /// <summary>
        /// Xóa cache của một trang cụ thể, buộc reload lần tiếp theo.
        /// Gọi khi cần refresh data (ví dụ sau khi tạo/sửa/xóa dữ liệu từ trang khác).
        /// </summary>
        public void InvalidateCache(string pageName)
        {
            _viewModelCache.Remove(pageName);
        }

        /// <summary>
        /// Xóa toàn bộ cache, buộc tất cả trang reload.
        /// </summary>
        public void InvalidateAllCaches()
        {
            _viewModelCache.Clear();
        }

        [RelayCommand]
        public void Logout()
        {
            _notificationCenter.PropertyChanged -= NotificationCenterOnPropertyChanged;
            _mainViewModel.PropertyChanged -= MainViewModelOnPropertyChanged;
            _viewModelCache.Clear();
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
                OnPropertyChanged(nameof(IsAdminOrHigher));
                OnPropertyChanged(nameof(IsEmployeeOrHigher));
                OnPropertyChanged(nameof(CanAccessAdminOnlyModules));
                OnPropertyChanged(nameof(CanManageTours));
                OnPropertyChanged(nameof(CanEditPricing));
                OnPropertyChanged(nameof(CanCreateBooking));
                OnPropertyChanged(nameof(CanCancelBooking));
                OnPropertyChanged(nameof(CanManageCustomers));
                OnPropertyChanged(nameof(CanManageUsers));
                OnPropertyChanged(nameof(CanManagePayments));
                OnPropertyChanged(nameof(CanManagePromotions));
                OnPropertyChanged(nameof(CanViewReports));
                OnPropertyChanged(nameof(CanManageDepartures));

                if (IsAdminOnlyPage(SelectedMenuItem) && !CanAccessAdminOnlyModules)
                {
                    NavigateToPage("Dashboard");
                }
            }
        }

        private static bool IsAdminOnlyPage(string? pageName)
        {
            return string.Equals(pageName, "Customers", System.StringComparison.Ordinal)
                   || string.Equals(pageName, "Users", System.StringComparison.Ordinal)
                   || string.Equals(pageName, "Payments", System.StringComparison.Ordinal)
                   || string.Equals(pageName, "Promotions", System.StringComparison.Ordinal)
                   || string.Equals(pageName, "Tours", System.StringComparison.Ordinal)
                   || string.Equals(pageName, "Departures", System.StringComparison.Ordinal)
                   || string.Equals(pageName, "Debug", System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Pages that require at least Employee role (not accessible by Guide/Customer).
        /// </summary>
        private static bool IsEmployeeOrHigherPage(string? pageName)
        {
            return string.Equals(pageName, "Bookings", System.StringComparison.Ordinal)
                   || string.Equals(pageName, "Reports", System.StringComparison.Ordinal)
                   || string.Equals(pageName, "Notifications", System.StringComparison.Ordinal);
        }

        private static int GetRoleLevel(string? role)
        {
            return (role ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "customer" => 0,
                "guide" => 1,
                "employee" => 2,
                "admin" => 3,
                "superadmin" => 4,
                "owner" => 5,
                _ => 0
            };
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

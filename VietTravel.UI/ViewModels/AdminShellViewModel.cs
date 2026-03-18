using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VietTravel.UI.ViewModels
{
    public partial class AdminShellViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty]
        private ObservableObject _currentPageViewModel;

        [ObservableProperty]
        private string _selectedMenuItem = "Dashboard";

        public string FullName => _mainViewModel.CurrentUser?.FullName ?? "Quản Trị Viên";
        public string UserRole => _mainViewModel.CurrentUser?.Role ?? "Admin";
        public string UserInitials => GetInitials(FullName);

        public AdminShellViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _currentPageViewModel = new DashboardViewModel(_mainViewModel, this);
        }

        [RelayCommand]
        public void NavigateToPage(string pageName)
        {
            if (SelectedMenuItem == pageName) return;
            SelectedMenuItem = pageName;

            CurrentPageViewModel = pageName switch
            {
                "Dashboard" => new DashboardViewModel(_mainViewModel, this),
                "Tours" => new TourListViewModel(_mainViewModel),
                "Departures" => new DepartureListViewModel(_mainViewModel),
                "Bookings" => new BookingListViewModel(_mainViewModel),
                "Customers" => new CustomerListViewModel(_mainViewModel),
                "Payments" => new PaymentListViewModel(_mainViewModel),
                "Reports" => new ReportViewModel(_mainViewModel),
                _ => new DashboardViewModel(_mainViewModel, this)
            };
        }

        [RelayCommand]
        public void Logout()
        {
            _mainViewModel.CurrentUser = null;
            _mainViewModel.NavigateTo(new LoginViewModel(_mainViewModel));
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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VietTravel.UI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        // Stats
        [ObservableProperty] private int _totalTours = 0;
        [ObservableProperty] private int _activeBookings = 0;
        [ObservableProperty] private string _totalRevenue = "0 ₫";
        [ObservableProperty] private int _totalCustomers = 0;
        [ObservableProperty] private int _pendingBookings = 0;
        [ObservableProperty] private int _availableDepartures = 0;

        public string GreetingMessage
        {
            get
            {
                var hour = System.DateTime.Now.Hour;
                if (hour < 12) return "Chào buổi sáng";
                if (hour < 18) return "Chào buổi chiều";
                return "Chào buổi tối";
            }
        }

        public string UserName => _mainViewModel.CurrentUser?.FullName ?? "Admin";

        public DashboardViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }
    }
}

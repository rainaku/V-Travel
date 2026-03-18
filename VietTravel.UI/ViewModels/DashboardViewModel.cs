using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using VietTravel.Core.Models;
using VietTravel.Data;

namespace VietTravel.UI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        // Stats
        [ObservableProperty] private int _totalTours = 0;
        [ObservableProperty] private int _activeBookings = 0;
        [ObservableProperty] private string _totalRevenue = "0 đ";
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
            _ = LoadStatsAsync();
        }

        private async Task LoadStatsAsync()
        {
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                
                // Load Total Tours
                var toursResponse = await client.From<Tour>().Get();
                TotalTours = toursResponse.Models.Count;
                
                // Future update: Add more queries for bookings, customers, revenue, etc.
            }
            catch
            {
                // Optionally handle exception silently on Dashboard
            }
        }
    }
}

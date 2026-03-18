using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VietTravel.Core.Models;
using VietTravel.Data;

namespace VietTravel.UI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly AdminShellViewModel? _shellViewModel;

        // Stats
        [ObservableProperty] private int _totalTours = 0;
        [ObservableProperty] private int _activeBookings = 0;
        [ObservableProperty] private string _totalRevenue = "0 đ";
        [ObservableProperty] private int _totalCustomers = 0;
        [ObservableProperty] private int _pendingBookings = 0;
        [ObservableProperty] private int _availableDepartures = 0;

        // Recent bookings
        [ObservableProperty] private ObservableCollection<RecentBookingInfo> _recentBookings = new();
        [ObservableProperty] private bool _hasRecentData = false;

        public string GreetingMessage
        {
            get
            {
                var hour = DateTime.Now.Hour;
                if (hour < 12) return "Chào buổi sáng";
                if (hour < 18) return "Chào buổi chiều";
                return "Chào buổi tối";
            }
        }

        public string UserName => _mainViewModel.CurrentUser?.FullName ?? "Admin";

        public DashboardViewModel(MainViewModel mainViewModel, AdminShellViewModel? shellViewModel = null)
        {
            _mainViewModel = mainViewModel;
            _shellViewModel = shellViewModel;
            _ = LoadStatsAsync();
        }

        private async Task LoadStatsAsync()
        {
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                // Tours
                var toursResponse = await client.From<Tour>().Get();
                TotalTours = toursResponse.Models.Count;

                // Customers
                var custResponse = await client.From<Customer>().Get();
                TotalCustomers = custResponse.Models.Count;

                // Bookings
                var bookResponse = await client.From<Booking>().Get();
                ActiveBookings = bookResponse.Models.Count(b => b.Status == "Chờ xử lý");
                PendingBookings = ActiveBookings;

                // Departures
                var depResponse = await client.From<Departure>().Get();
                AvailableDepartures = depResponse.Models.Count(d => d.Status == "Mở bán");

                // Revenue
                var payResponse = await client.From<Payment>().Get();
                var revenue = payResponse.Models.Where(p => p.Status == "Đã thanh toán").Sum(p => p.PaidAmount);
                TotalRevenue = $"{revenue:N0} đ";

                // Recent bookings
                var recentList = new ObservableCollection<RecentBookingInfo>();
                var recent = bookResponse.Models.OrderByDescending(b => b.BookingDate).Take(5);

                var customers = custResponse.Models;
                var departures = depResponse.Models;
                var tours = toursResponse.Models;

                foreach (var b in recent)
                {
                    var customer = customers.FirstOrDefault(c => c.Id == b.CustomerId);
                    var departure = departures.FirstOrDefault(d => d.Id == b.DepartureId);
                    var tour = departure != null ? tours.FirstOrDefault(t => t.Id == departure.TourId) : null;

                    recentList.Add(new RecentBookingInfo
                    {
                        Id = b.Id,
                        CustomerName = customer?.FullName ?? "N/A",
                        TourName = tour?.Name ?? "N/A",
                        Status = b.Status,
                        BookingDate = b.BookingDate.ToString("dd/MM/yyyy")
                    });
                }
                RecentBookings = recentList;
                HasRecentData = recentList.Count > 0;
            }
            catch
            {
                // Dashboard stats silently fail
            }
        }

        [RelayCommand]
        private void NavigateToTours() => _shellViewModel?.NavigateToPage("Tours");

        [RelayCommand]
        private void NavigateToDepartures() => _shellViewModel?.NavigateToPage("Departures");

        [RelayCommand]
        private void NavigateToBookings() => _shellViewModel?.NavigateToPage("Bookings");

        [RelayCommand]
        private void NavigateToCustomers() => _shellViewModel?.NavigateToPage("Customers");
    }

    public class RecentBookingInfo
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string TourName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string BookingDate { get; set; } = string.Empty;
    }
}

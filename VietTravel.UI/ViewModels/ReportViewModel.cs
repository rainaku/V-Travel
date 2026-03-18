using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VietTravel.Core.Models;
using VietTravel.Data;

namespace VietTravel.UI.ViewModels
{
    public partial class ReportViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private int _totalTours = 0;
        [ObservableProperty] private int _totalBookings = 0;
        [ObservableProperty] private string _totalRevenue = "0 ₫";
        [ObservableProperty] private string _topTourName = "—";
        [ObservableProperty] private ObservableCollection<TopTourInfo> _topTours = new();
        [ObservableProperty] private bool _isLoading = false;

        public ReportViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = LoadReportAsync();
        }

        [RelayCommand]
        private async Task LoadReportAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                // Tours
                var tours = (await client.From<Tour>().Get()).Models;
                TotalTours = tours.Count;

                // Bookings
                var bookings = (await client.From<Booking>().Get()).Models;
                TotalBookings = bookings.Count;

                // Payments → Revenue
                var payments = (await client.From<Payment>().Get()).Models;
                var totalAmount = payments.Where(p => p.Status == "Đã thanh toán").Sum(p => p.PaidAmount);
                TotalRevenue = $"{totalAmount:N0} ₫";

                // Departures
                var departures = (await client.From<Departure>().Get()).Models;

                // Top tours by booking count
                var tourBookingCounts = bookings
                    .GroupBy(b => b.DepartureId)
                    .Select(g => new
                    {
                        DepartureId = g.Key,
                        Count = g.Count(),
                        Revenue = payments.Where(p => g.Select(b => b.Id).Contains(p.BookingId) && p.Status == "Đã thanh toán").Sum(p => p.PaidAmount)
                    })
                    .ToList();

                var topTourList = new ObservableCollection<TopTourInfo>();
                var tourDepartures = tourBookingCounts
                    .Select(tc =>
                    {
                        var dep = departures.FirstOrDefault(d => d.Id == tc.DepartureId);
                        var tour = dep != null ? tours.FirstOrDefault(t => t.Id == dep.TourId) : null;
                        return new { Tour = tour, tc.Count, tc.Revenue };
                    })
                    .Where(x => x.Tour != null)
                    .GroupBy(x => x.Tour!.Id)
                    .Select(g => new TopTourInfo
                    {
                        TourName = g.First().Tour!.Name,
                        Destination = g.First().Tour!.Destination,
                        BookingCount = g.Sum(x => x.Count),
                        Revenue = $"{g.Sum(x => x.Revenue):N0} ₫"
                    })
                    .OrderByDescending(x => x.BookingCount)
                    .Take(10);

                int rank = 1;
                foreach (var t in tourDepartures)
                {
                    t.Rank = rank++;
                    topTourList.Add(t);
                }
                TopTours = topTourList;
                TopTourName = topTourList.FirstOrDefault()?.TourName ?? "—";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải báo cáo: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class TopTourInfo
    {
        public int Rank { get; set; }
        public string TourName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public int BookingCount { get; set; }
        public string Revenue { get; set; } = "0 ₫";
    }
}

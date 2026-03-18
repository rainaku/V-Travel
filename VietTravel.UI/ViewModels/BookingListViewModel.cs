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
    public partial class BookingListViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private ObservableCollection<Booking> _bookings = new();
        [ObservableProperty] private ObservableCollection<Booking> _filteredBookings = new();
        [ObservableProperty] private bool _isLoading = false;

        // Stats
        [ObservableProperty] private int _totalBookings = 0;
        [ObservableProperty] private int _pendingCount = 0;
        [ObservableProperty] private int _confirmedCount = 0;
        [ObservableProperty] private int _cancelledCount = 0;

        // Form
        [ObservableProperty] private bool _isFormVisible = false;
        [ObservableProperty] private ObservableCollection<Customer> _customerList = new();
        [ObservableProperty] private ObservableCollection<Departure> _departureList = new();
        [ObservableProperty] private Customer? _formCustomer;
        [ObservableProperty] private Departure? _formDeparture;
        [ObservableProperty] private string _formGuestCount = string.Empty;

        public bool HasNoData => !IsLoading && FilteredBookings.Count == 0;

        public BookingListViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = LoadDataAsync();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredBookings = new ObservableCollection<Booking>(Bookings);
            }
            else
            {
                var lower = SearchText.ToLower();
                FilteredBookings = new ObservableCollection<Booking>(
                    Bookings.Where(b =>
                        (b.Customer?.FullName?.ToLower().Contains(lower) ?? false) ||
                        (b.Departure?.Tour?.Name?.ToLower().Contains(lower) ?? false) ||
                        b.Status.ToLower().Contains(lower))
                );
            }
            OnPropertyChanged(nameof(HasNoData));
        }

        private void UpdateStats()
        {
            TotalBookings = Bookings.Count;
            PendingCount = Bookings.Count(b => b.Status == "Chờ xử lý");
            ConfirmedCount = Bookings.Count(b => b.Status == "Đã xác nhận");
            CancelledCount = Bookings.Count(b => b.Status == "Hủy");
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            OnPropertyChanged(nameof(HasNoData));
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                // Load customers
                var custResp = await client.From<Customer>().Get();
                CustomerList = new ObservableCollection<Customer>(custResp.Models);

                // Load departures with tours
                var tourResp = await client.From<Tour>().Get();
                var tours = tourResp.Models;
                var depResp = await client.From<Departure>().Get();
                var deps = depResp.Models;
                foreach (var d in deps) d.Tour = tours.FirstOrDefault(t => t.Id == d.TourId);
                DepartureList = new ObservableCollection<Departure>(deps);

                // Load bookings
                var bookResp = await client.From<Booking>().Get();
                Bookings.Clear();
                foreach (var b in bookResp.Models)
                {
                    b.Customer = CustomerList.FirstOrDefault(c => c.Id == b.CustomerId);
                    b.Departure = DepartureList.FirstOrDefault(d => d.Id == b.DepartureId);
                    Bookings.Add(b);
                }
                ApplyFilter();
                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải bookings: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoData));
            }
        }

        [RelayCommand]
        private void ShowAddForm()
        {
            FormCustomer = null;
            FormDeparture = null;
            FormGuestCount = string.Empty;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void CancelForm() => IsFormVisible = false;

        [RelayCommand]
        private async Task SaveBookingAsync()
        {
            if (FormCustomer == null)
            {
                MessageBox.Show("Vui lòng chọn khách hàng.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (FormDeparture == null)
            {
                MessageBox.Show("Vui lòng chọn lịch khởi hành.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(FormGuestCount, out int guests) || guests <= 0)
            {
                MessageBox.Show("Số khách phải là số nguyên dương.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var booking = new Booking
                {
                    CustomerId = FormCustomer.Id,
                    DepartureId = FormDeparture.Id,
                    UserId = _mainViewModel.CurrentUser?.Id ?? 1,
                    BookingDate = DateTime.Now,
                    GuestCount = guests,
                    Status = "Chờ xử lý"
                };
                await client.From<Booking>().Insert(booking);
                IsFormVisible = false;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tạo booking: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ConfirmBookingAsync(Booking booking)
        {
            if (booking == null) return;
            try
            {
                booking.Status = "Đã xác nhận";
                booking.Customer = null;
                booking.Departure = null;
                booking.User = null;
                var client = await SupabaseClientFactory.GetClientAsync();
                await client.From<Booking>().Update(booking);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CancelBookingAsync(Booking booking)
        {
            if (booking == null) return;
            var result = MessageBox.Show("Bạn có chắc chắn muốn hủy booking này?",
                "Xác nhận hủy", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                booking.Status = "Hủy";
                booking.Customer = null;
                booking.Departure = null;
                booking.User = null;
                var client = await SupabaseClientFactory.GetClientAsync();
                await client.From<Booking>().Update(booking);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

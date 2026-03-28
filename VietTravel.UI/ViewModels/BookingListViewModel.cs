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
    public partial class BookingListViewModel : PaginatedListViewModelBase<Booking>
    {
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private ObservableCollection<Booking> _bookings = new();
        [ObservableProperty] private ObservableCollection<Booking> _filteredBookings = new();
        [ObservableProperty] private bool _isLoading = false;

        // Filters
        [ObservableProperty] private ObservableCollection<string> _statuses = new() { "Tất cả" };
        [ObservableProperty] private string _selectedStatus = "Tất cả";

        // Stats
        [ObservableProperty] private int _totalBookings = 0;
        [ObservableProperty] private int _pendingCount = 0;
        [ObservableProperty] private int _confirmedCount = 0;
        [ObservableProperty] private int _cancelledCount = 0;

        // Form
        [ObservableProperty] private bool _isFormVisible = false;
        [ObservableProperty] private ObservableCollection<Customer> _customerList = new();
        [ObservableProperty] private ObservableCollection<Departure> _departureList = new();
        [ObservableProperty] private ObservableCollection<Customer> _formCustomers = new();
        [ObservableProperty] private ObservableCollection<Departure> _formDepartures = new();
        [ObservableProperty] private string _customerSearchText = string.Empty;
        [ObservableProperty] private string _departureSearchText = string.Empty;
        [ObservableProperty] private bool _isCustomerDropdownOpen = false;
        [ObservableProperty] private bool _isDepartureDropdownOpen = false;
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
        partial void OnSelectedStatusChanged(string value) => ApplyFilter();
        partial void OnCustomerSearchTextChanged(string value)
        {
            if (FormCustomer != null &&
                !string.Equals(FormCustomer.FullName, value?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                FormCustomer = null;
            }

            ApplyFormFilters();
        }

        partial void OnDepartureSearchTextChanged(string value)
        {
            if (FormDeparture != null &&
                !string.Equals(BuildDepartureSearchText(FormDeparture), value?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                FormDeparture = null;
            }

            ApplyFormFilters();
        }

        partial void OnFormCustomerChanged(Customer? value)
        {
            if (value == null)
            {
                return;
            }

            var selectedText = value.FullName;
            if (!string.Equals(CustomerSearchText, selectedText, StringComparison.OrdinalIgnoreCase))
            {
                CustomerSearchText = selectedText;
            }

            IsCustomerDropdownOpen = false;
        }

        partial void OnFormDepartureChanged(Departure? value)
        {
            if (value == null)
            {
                return;
            }

            var selectedText = BuildDepartureSearchText(value);
            if (!string.Equals(DepartureSearchText, selectedText, StringComparison.OrdinalIgnoreCase))
            {
                DepartureSearchText = selectedText;
            }

            IsDepartureDropdownOpen = false;
        }

        private void ApplyFilter()
        {
            var isSearchEmpty = string.IsNullOrWhiteSpace(SearchText);
            var lower = isSearchEmpty ? string.Empty : SearchText.ToLower();
            var filterStatus = SelectedStatus == "Tất cả" ? null : SelectedStatus;

            var filtered = Bookings.Where(b =>
                    (isSearchEmpty ||
                     (b.Customer?.FullName?.ToLower().Contains(lower) ?? false) ||
                     (b.Departure?.Tour?.Name?.ToLower().Contains(lower) ?? false) ||
                     b.Status.ToLower().Contains(lower)) &&
                    (filterStatus == null || b.Status == filterStatus))
                .ToList();
            SetPagedItems(filtered, FilteredBookings);
            OnPropertyChanged(nameof(HasNoData));
        }

        private void UpdateStats()
        {
            TotalBookings = Bookings.Count;
            PendingCount = Bookings.Count(b =>
                b.Status == "Chờ xử lý" ||
                b.Status == "Chờ thanh toán" ||
                b.Status == "Đợi xác nhận");
            ConfirmedCount = Bookings.Count(b => b.Status == "Đã xác nhận");
            CancelledCount = Bookings.Count(b => b.Status == "Đã hủy" || b.Status == "Hủy");
        }

        private void ApplyFormFilters()
        {
            var customerKeyword = string.IsNullOrWhiteSpace(CustomerSearchText)
                ? null
                : CustomerSearchText.Trim().ToLowerInvariant();
            var selectedCustomerId = FormCustomer?.Id;

            var filteredCustomers = CustomerList
                .Where(c =>
                    customerKeyword == null ||
                    (c.FullName?.ToLowerInvariant().Contains(customerKeyword) ?? false) ||
                    (c.PhoneNumber?.ToLowerInvariant().Contains(customerKeyword) ?? false) ||
                    (c.Email?.ToLowerInvariant().Contains(customerKeyword) ?? false))
                .OrderBy(c => c.FullName)
                .ToList();

            FormCustomers = new ObservableCollection<Customer>(filteredCustomers);
            if (selectedCustomerId.HasValue)
            {
                FormCustomer = FormCustomers.FirstOrDefault(c => c.Id == selectedCustomerId.Value);
            }
            var normalizedCustomerText = CustomerSearchText?.Trim() ?? string.Empty;
            IsCustomerDropdownOpen =
                !string.IsNullOrWhiteSpace(normalizedCustomerText) &&
                FormCustomers.Count > 0 &&
                (FormCustomer == null ||
                 !string.Equals(FormCustomer.FullName, normalizedCustomerText, StringComparison.OrdinalIgnoreCase));

            var departureKeyword = string.IsNullOrWhiteSpace(DepartureSearchText)
                ? null
                : DepartureSearchText.Trim().ToLowerInvariant();
            var selectedDepartureId = FormDeparture?.Id;

            var filteredDepartures = DepartureList
                .Where(d =>
                {
                    if (!IsDepartureStillBookable(d))
                    {
                        return false;
                    }

                    if (departureKeyword == null) return true;

                    var tourName = d.Tour?.Name?.ToLowerInvariant() ?? string.Empty;
                    var destination = d.Tour?.Destination?.ToLowerInvariant() ?? string.Empty;
                    var startDateText = d.StartDate.ToString("dd/MM/yyyy").ToLowerInvariant();
                    var departureId = d.Id.ToString();
                    var displayText = BuildDepartureSearchText(d).ToLowerInvariant();

                    return tourName.Contains(departureKeyword)
                           || destination.Contains(departureKeyword)
                           || startDateText.Contains(departureKeyword)
                           || departureId.Contains(departureKeyword)
                           || displayText.Contains(departureKeyword);
                })
                .OrderBy(d => d.StartDate)
                .ToList();

            FormDepartures = new ObservableCollection<Departure>(filteredDepartures);
            if (selectedDepartureId.HasValue)
            {
                FormDeparture = FormDepartures.FirstOrDefault(d => d.Id == selectedDepartureId.Value);
            }
            var normalizedDepartureText = DepartureSearchText?.Trim() ?? string.Empty;
            IsDepartureDropdownOpen =
                !string.IsNullOrWhiteSpace(normalizedDepartureText) &&
                FormDepartures.Count > 0 &&
                (FormDeparture == null ||
                 !string.Equals(BuildDepartureSearchText(FormDeparture), normalizedDepartureText, StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildDepartureSearchText(Departure departure)
        {
            var tourName = departure.Tour?.Name;
            return string.IsNullOrWhiteSpace(tourName)
                ? departure.StartDate.ToString("dd/MM/yyyy")
                : $"{tourName} - {departure.StartDate:dd/MM/yyyy}";
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
                ApplyFormFilters();

                // Load bookings
                var bookResp = await client.From<Booking>().Get();
                var sortedBookings = bookResp.Models
                    .OrderByDescending(b => b.BookingDate)
                    .ThenByDescending(b => b.Id)
                    .ToList();
                Bookings.Clear();
                foreach (var b in sortedBookings)
                {
                    b.Customer = CustomerList.FirstOrDefault(c => c.Id == b.CustomerId);
                    b.Departure = DepartureList.FirstOrDefault(d => d.Id == b.DepartureId);
                    Bookings.Add(b);
                }

                var distinctStatuses = sortedBookings.Where(b => !string.IsNullOrWhiteSpace(b.Status)).Select(b => b.Status!).Distinct().OrderBy(s => s).ToList();
                var currentSelected = SelectedStatus;
                Statuses.Clear();
                Statuses.Add("Tất cả");
                foreach (var status in distinctStatuses)
                {
                    Statuses.Add(status);
                }
                if (Statuses.Contains(currentSelected))
                    SelectedStatus = currentSelected;
                else
                    SelectedStatus = "Tất cả";

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
            CustomerSearchText = string.Empty;
            DepartureSearchText = string.Empty;
            IsCustomerDropdownOpen = false;
            IsDepartureDropdownOpen = false;
            ApplyFormFilters();
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

            Departure? latestDeparture = null;
            int originalAvailableSlots = 0;
            string originalDepartureStatus = string.Empty;
            bool hasReservedSlots = false;
            int createdBookingId = 0;

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                // Always re-check from database to avoid overbooking due to stale UI data.
                var depResp = await client.From<Departure>().Where(d => d.Id == FormDeparture.Id).Get();
                latestDeparture = depResp.Models.FirstOrDefault();
                if (latestDeparture == null)
                {
                    MessageBox.Show("Không tìm thấy lịch khởi hành.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!IsDepartureStillBookable(latestDeparture))
                {
                    MessageBox.Show("Lịch khởi hành đã bị khóa đặt vé (từ 1 ngày trước ngày khởi hành).", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (latestDeparture.Status != "Mở bán")
                {
                    MessageBox.Show("Lịch khởi hành hiện không mở bán.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (guests > latestDeparture.AvailableSlots)
                {
                    MessageBox.Show($"Chỉ còn {latestDeparture.AvailableSlots} chỗ trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var tourResp = await client.From<Tour>().Where(t => t.Id == latestDeparture.TourId).Get();
                var tour = tourResp.Models.FirstOrDefault();
                if (tour == null)
                {
                    MessageBox.Show("Không tìm thấy thông tin tour của lịch khởi hành.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                originalAvailableSlots = latestDeparture.AvailableSlots;
                originalDepartureStatus = latestDeparture.Status;

                latestDeparture.AvailableSlots -= guests;
                if (latestDeparture.AvailableSlots <= 0)
                {
                    latestDeparture.AvailableSlots = 0;
                    if (latestDeparture.Status != "Đóng")
                        latestDeparture.Status = "Hết chỗ";
                }
                latestDeparture.Tour = null;
                await client.From<Departure>().Update(latestDeparture);
                hasReservedSlots = true;

                var booking = new Booking
                {
                    CustomerId = FormCustomer.Id,
                    DepartureId = latestDeparture.Id,
                    UserId = _mainViewModel.CurrentUser?.Id ?? 1,
                    BookingDate = DateTime.Now,
                    GuestCount = guests,
                    Status = "Đã xác nhận"
                };

                var bookingResp = await client.From<Booking>().Insert(booking);
                var createdBooking = bookingResp.Models.FirstOrDefault();
                if (createdBooking == null)
                    throw new InvalidOperationException("Không lấy được dữ liệu booking sau khi tạo.");

                createdBookingId = createdBooking.Id;
                var payment = new Payment
                {
                    BookingId = createdBooking.Id,
                    OriginalAmount = tour.BasePrice * guests,
                    DiscountAmount = 0,
                    TotalAmount = tour.BasePrice * guests,
                    PaidAmount = 0,
                    PromoCode = string.Empty,
                    Status = "Chưa thanh toán",
                    PaymentMethod = "Tiền mặt"
                };
                await client.From<Payment>().Insert(payment);

                IsFormVisible = false;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                try
                {
                    var client = await SupabaseClientFactory.GetClientAsync();
                    if (createdBookingId > 0)
                    {
                        await client.From<Booking>().Where(b => b.Id == createdBookingId).Delete();
                    }

                    if (hasReservedSlots && latestDeparture != null)
                    {
                        latestDeparture.AvailableSlots = originalAvailableSlots;
                        latestDeparture.Status = originalDepartureStatus;
                        latestDeparture.Tour = null;
                        await client.From<Departure>().Update(latestDeparture);
                    }
                }
                catch
                {
                    // Ignore rollback errors and show original failure to user.
                }

                MessageBox.Show($"Lỗi tạo booking: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CancelBookingAsync(Booking booking)
        {
            if (booking == null) return;
            if (booking.Status == "Đã hủy" || booking.Status == "Hủy") return;
            var result = MessageBox.Show("Bạn có chắc chắn muốn hủy booking này?",
                "Xác nhận hủy", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                var depResp = await client.From<Departure>().Where(d => d.Id == booking.DepartureId).Get();
                var departure = depResp.Models.FirstOrDefault();
                if (departure != null)
                {
                    departure.AvailableSlots = Math.Min(departure.MaxSlots, departure.AvailableSlots + booking.GuestCount);
                    if (departure.Status != "Đóng")
                    {
                        departure.Status = departure.AvailableSlots > 0 ? "Mở bán" : "Hết chỗ";
                    }
                    departure.Tour = null;
                    await client.From<Departure>().Update(departure);
                }

                booking.Status = "Đã hủy";
                booking.Customer = null;
                booking.Departure = null;
                booking.User = null;
                await client.From<Booking>().Update(booking);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool IsDepartureStillBookable(Departure departure)
        {
            return departure.StartDate.Date > DateTime.Today.AddDays(1);
        }
    }
}

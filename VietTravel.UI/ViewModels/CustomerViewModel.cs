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
    public partial class CustomerViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        public string FullName => _mainViewModel.CurrentUser?.FullName ?? "Khách Hàng";

        // Browse Tours
        [ObservableProperty] private ObservableCollection<Tour> _availableTours = new();
        [ObservableProperty] private ObservableCollection<Departure> _availableDepartures = new();
        [ObservableProperty] private Tour? _selectedTour;
        [ObservableProperty] private Departure? _selectedDeparture;
        [ObservableProperty] private string _bookingGuestCount = "1";

        // My Bookings
        [ObservableProperty] private ObservableCollection<Booking> _myBookings = new();

        // Personal Info
        [ObservableProperty] private string _infoFullName = string.Empty;
        [ObservableProperty] private string _infoPhone = string.Empty;
        [ObservableProperty] private string _infoEmail = string.Empty;
        [ObservableProperty] private string _infoAddress = string.Empty;

        [ObservableProperty] private bool _isLoading = false;

        private Customer? _customerProfile;

        public CustomerViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                // Load tours
                var tours = (await client.From<Tour>().Get()).Models;
                AvailableTours = new ObservableCollection<Tour>(tours);

                // Load departures
                var deps = (await client.From<Departure>().Get()).Models;
                foreach (var d in deps) d.Tour = tours.FirstOrDefault(t => t.Id == d.TourId);
                AvailableDepartures = new ObservableCollection<Departure>(deps.Where(d => d.Status == "Mở bán" && d.AvailableSlots > 0));

                // Load customer profile (match by user full name)
                var custs = (await client.From<Customer>().Get()).Models;
                _customerProfile = custs.FirstOrDefault(c =>
                    c.FullName.Equals(_mainViewModel.CurrentUser?.FullName ?? "", StringComparison.OrdinalIgnoreCase));

                if (_customerProfile != null)
                {
                    InfoFullName = _customerProfile.FullName;
                    InfoPhone = _customerProfile.PhoneNumber;
                    InfoEmail = _customerProfile.Email;
                    InfoAddress = _customerProfile.Address;

                    // Load my bookings
                    var bookings = (await client.From<Booking>().Get()).Models
                        .Where(b => b.CustomerId == _customerProfile.Id)
                        .OrderByDescending(b => b.BookingDate);

                    MyBookings.Clear();
                    foreach (var b in bookings)
                    {
                        b.Departure = deps.FirstOrDefault(d => d.Id == b.DepartureId);
                        MyBookings.Add(b);
                    }
                }
                else
                {
                    InfoFullName = _mainViewModel.CurrentUser?.FullName ?? "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task BookTourAsync()
        {
            if (SelectedDeparture == null)
            {
                MessageBox.Show("Vui lòng chọn lịch khởi hành.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(BookingGuestCount, out int guests) || guests <= 0)
            {
                MessageBox.Show("Số khách phải là số nguyên dương.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                // Ensure customer exists
                if (_customerProfile == null)
                {
                    var newCust = new Customer
                    {
                        FullName = _mainViewModel.CurrentUser?.FullName ?? "Khách hàng",
                        Email = "",
                        PhoneNumber = "",
                        Address = ""
                    };
                    var resp = await client.From<Customer>().Insert(newCust);
                    _customerProfile = resp.Models.FirstOrDefault();
                }

                if (_customerProfile == null) return;

                var booking = new Booking
                {
                    CustomerId = _customerProfile.Id,
                    DepartureId = SelectedDeparture.Id,
                    UserId = _mainViewModel.CurrentUser?.Id ?? 1,
                    BookingDate = DateTime.Now,
                    GuestCount = guests,
                    Status = "Chờ xử lý"
                };
                await client.From<Booking>().Insert(booking);

                MessageBox.Show("Đặt tour thành công! Booking đang chờ xác nhận.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đặt tour: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SaveProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(InfoFullName))
            {
                MessageBox.Show("Vui lòng nhập họ tên.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                if (_customerProfile != null)
                {
                    _customerProfile.FullName = InfoFullName;
                    _customerProfile.PhoneNumber = InfoPhone;
                    _customerProfile.Email = InfoEmail;
                    _customerProfile.Address = InfoAddress;
                    await client.From<Customer>().Update(_customerProfile);
                }
                else
                {
                    var c = new Customer
                    {
                        FullName = InfoFullName,
                        PhoneNumber = InfoPhone,
                        Email = InfoEmail,
                        Address = InfoAddress
                    };
                    var resp = await client.From<Customer>().Insert(c);
                    _customerProfile = resp.Models.FirstOrDefault();
                }
                MessageBox.Show("Cập nhật thông tin thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void Logout()
        {
            _mainViewModel.CurrentUser = null;
            _mainViewModel.NavigateTo(new LoginViewModel(_mainViewModel));
        }
    }
}

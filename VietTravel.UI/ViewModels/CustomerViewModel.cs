using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
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
        public string UserInitials => GetInitials(FullName);

        // Navigation
        [ObservableProperty] private string _selectedPage = "Explore";

        // Browse Tours
        [ObservableProperty] private ObservableCollection<TourDisplayInfo> _allTours = new();
        [ObservableProperty] private ObservableCollection<TourDisplayInfo> _filteredTours = new();
        [ObservableProperty] private ObservableCollection<Departure> _allDepartures = new();
        [ObservableProperty] private ObservableCollection<DepartureDisplayInfo> _availableDepartures = new();
        [ObservableProperty] private string _tourSearchText = string.Empty;

        // Tour Details
        [ObservableProperty] private TourDisplayInfo? _selectedTour;
        [ObservableProperty] private bool _isTourDetailsVisible = false;

        // Booking
        [ObservableProperty] private DepartureDisplayInfo? _selectedDepartureInfo;
        [ObservableProperty] private string _bookingGuestCount = "1";
        [ObservableProperty] private bool _isBookingFormVisible = false;

        // My Bookings
        [ObservableProperty] private ObservableCollection<BookingDisplayInfo> _myBookings = new();

        // Personal Info
        [ObservableProperty] private string _infoFullName = string.Empty;
        [ObservableProperty] private string _infoPhone = string.Empty;
        [ObservableProperty] private string _infoEmail = string.Empty;
        [ObservableProperty] private string _infoAddress = string.Empty;
        [ObservableProperty] private bool _isSavingProfile = false;

        // Stats
        [ObservableProperty] private int _totalBookingsCount = 0;
        [ObservableProperty] private int _pendingBookingsCount = 0;
        [ObservableProperty] private int _confirmedBookingsCount = 0;
        [ObservableProperty] private int _totalToursAvailable = 0;

        [ObservableProperty] private bool _isLoading = false;

        private Customer? _customerProfile;

        public CustomerViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = LoadDataAsync();
        }

        partial void OnTourSearchTextChanged(string value) => ApplyTourFilter();

        private void ApplyTourFilter()
        {
            if (string.IsNullOrWhiteSpace(TourSearchText))
            {
                FilteredTours = new ObservableCollection<TourDisplayInfo>(AllTours);
            }
            else
            {
                var lower = TourSearchText.ToLower();
                FilteredTours = new ObservableCollection<TourDisplayInfo>(
                    AllTours.Where(t =>
                        t.Name.ToLower().Contains(lower) ||
                        t.Destination.ToLower().Contains(lower) ||
                        t.Description.ToLower().Contains(lower))
                );
            }
        }

        [RelayCommand]
        private void NavigateTo(string page)
        {
            SelectedPage = page;
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                // Load tours
                var tours = (await client.From<Tour>().Get()).Models;
                
                // Stable and destination-aware tour thumbnails (direct JPEG URLs).
                var displayTours = tours.Select((t, i) => new TourDisplayInfo
                {
                    Tour = t,
                    ImageUrl = ResolveTourImageUrl(t, i)
                }).ToList();

                AllTours = new ObservableCollection<TourDisplayInfo>(displayTours);
                FilteredTours = new ObservableCollection<TourDisplayInfo>(displayTours);
                TotalToursAvailable = tours.Count;

                // Load departures with tour info
                var deps = (await client.From<Departure>().Get()).Models;
                foreach (var d in deps) d.Tour = tours.FirstOrDefault(t => t.Id == d.TourId);
                AllDepartures = new ObservableCollection<Departure>(deps);

                // Create display info for available departures
                var displayDeps = deps
                    .Where(d => d.Status == "Mở bán" && d.AvailableSlots > 0)
                    .OrderBy(d => d.StartDate)
                    .Select(d => new DepartureDisplayInfo
                    {
                        Departure = d,
                        TourName = d.Tour?.Name ?? "N/A",
                        Destination = d.Tour?.Destination ?? "",
                        StartDateFormatted = d.StartDate.ToString("dd/MM/yyyy"),
                        AvailableSlots = d.AvailableSlots,
                        Price = d.Tour?.BasePrice ?? 0,
                        PriceFormatted = $"{(d.Tour?.BasePrice ?? 0):N0} đ",
                        DurationDays = d.Tour?.DurationDays ?? 0
                    })
                    .ToList();
                AvailableDepartures = new ObservableCollection<DepartureDisplayInfo>(displayDeps);

                // Load customer profile
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
                        .OrderByDescending(b => b.BookingDate)
                        .ToList();

                    MyBookings.Clear();
                    foreach (var b in bookings)
                    {
                        var dep = deps.FirstOrDefault(d => d.Id == b.DepartureId);
                        var tour = dep?.Tour;
                        MyBookings.Add(new BookingDisplayInfo
                        {
                            Booking = b,
                            TourName = tour?.Name ?? "N/A",
                            Destination = tour?.Destination ?? "",
                            DepartureDate = dep?.StartDate.ToString("dd/MM/yyyy") ?? "N/A",
                            BookingDateFormatted = b.BookingDate.ToString("dd/MM/yyyy"),
                            GuestCount = b.GuestCount,
                            Status = b.Status,
                            StatusColor = b.Status switch
                            {
                                "Đã xác nhận" => "#34C759",
                                "Đã hủy" => "#FF3B30",
                                "Hủy" => "#FF3B30",
                                _ => "#FF9500"
                            }
                        });
                    }

                    TotalBookingsCount = bookings.Count;
                    PendingBookingsCount = bookings.Count(b => b.Status == "Chờ xử lý" || b.Status == "Chờ thanh toán");
                    ConfirmedBookingsCount = bookings.Count(b => b.Status == "Đã xác nhận");
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
        private void ShowBookingForm(DepartureDisplayInfo? info = null)
        {
            SelectedDepartureInfo = info;
            BookingGuestCount = "1";
            IsBookingFormVisible = true;
            SelectedPage = "Book";
        }

        [RelayCommand]
        private void ShowTourDetails(TourDisplayInfo tour)
        {
            if (tour == null) return;
            SelectedTour = tour;
            IsTourDetailsVisible = true;
        }

        [RelayCommand]
        private void CloseTourDetails()
        {
            IsTourDetailsVisible = false;
        }

        [RelayCommand]
        private void CancelBookingForm()
        {
            IsBookingFormVisible = false;
        }

        [RelayCommand]
        private async Task BookTourAsync()
        {
            if (SelectedDepartureInfo == null)
            {
                MessageBox.Show("Vui lòng chọn lịch khởi hành.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(InfoFullName)
                || string.IsNullOrWhiteSpace(InfoPhone)
                || string.IsNullOrWhiteSpace(InfoEmail)
                || string.IsNullOrWhiteSpace(InfoAddress))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ họ tên, số điện thoại, email và địa chỉ trước khi đặt tour.",
                    "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(BookingGuestCount, out int guests) || guests <= 0)
            {
                MessageBox.Show("Số khách phải là số nguyên dương.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (guests > SelectedDepartureInfo.AvailableSlots)
            {
                MessageBox.Show($"Chỉ còn {SelectedDepartureInfo.AvailableSlots} chỗ trống.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                // Ensure customer profile exists
                if (_customerProfile == null)
                {
                    var newCust = new Customer
                    {
                        FullName = _mainViewModel.CurrentUser?.FullName ?? "Khách hàng",
                        Email = InfoEmail,
                        PhoneNumber = InfoPhone,
                        Address = InfoAddress
                    };
                    var resp = await client.From<Customer>().Insert(newCust);
                    _customerProfile = resp.Models.FirstOrDefault();
                }
                else
                {
                    _customerProfile.FullName = InfoFullName;
                    _customerProfile.PhoneNumber = InfoPhone;
                    _customerProfile.Email = InfoEmail;
                    _customerProfile.Address = InfoAddress;
                    await client.From<Customer>().Update(_customerProfile);
                }

                if (_customerProfile == null) return;

                // Always re-check from database to avoid overbooking due to stale UI data.
                var depResp = await client.From<Departure>().Get();
                latestDeparture = depResp.Models.FirstOrDefault(d => d.Id == SelectedDepartureInfo.Departure.Id);
                if (latestDeparture == null)
                {
                    MessageBox.Show("Không tìm thấy lịch khởi hành.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (latestDeparture.Status != "Mở bán")
                {
                    MessageBox.Show("Lịch khởi hành hiện không mở bán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (guests > latestDeparture.AvailableSlots)
                {
                    MessageBox.Show($"Chỉ còn {latestDeparture.AvailableSlots} chỗ trống.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var tourResp = await client.From<Tour>().Get();
                var tour = tourResp.Models.FirstOrDefault(t => t.Id == latestDeparture.TourId);
                if (tour == null)
                {
                    MessageBox.Show("Không tìm thấy thông tin tour.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    CustomerId = _customerProfile.Id,
                    DepartureId = latestDeparture.Id,
                    UserId = _mainViewModel.CurrentUser?.Id ?? 1,
                    BookingDate = DateTime.Now,
                    GuestCount = guests,
                    Status = "Chờ thanh toán"
                };

                var bookingResp = await client.From<Booking>().Insert(booking);
                var createdBooking = bookingResp.Models.FirstOrDefault();
                if (createdBooking == null)
                    throw new InvalidOperationException("Không lấy được dữ liệu booking sau khi tạo.");

                createdBookingId = createdBooking.Id;
                var payment = new Payment
                {
                    BookingId = createdBooking.Id,
                    TotalAmount = tour.BasePrice * guests,
                    PaidAmount = 0,
                    Status = "Chưa thanh toán",
                    PaymentMethod = "Tiền mặt"
                };
                await client.From<Payment>().Insert(payment);

                IsBookingFormVisible = false;
                MessageBox.Show(
                    $"🎉 Đặt tour \"{SelectedDepartureInfo.TourName}\" thành công!\n\n" +
                    $"📅 Ngày khởi hành: {SelectedDepartureInfo.StartDateFormatted}\n" +
                    $"👥 Số khách: {guests}\n" +
                    $"💰 Tổng: {(tour.BasePrice * guests):N0} đ\n\n" +
                    "Booking đã tạo và đang chờ thanh toán/xử lý.",
                    "Đặt tour thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadDataAsync();
                SelectedPage = "MyBookings";
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

            IsSavingProfile = true;
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
                MessageBox.Show("✅ Cập nhật thông tin thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSavingProfile = false;
            }
        }

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            await LoadDataAsync();
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

        private static string ResolveTourImageUrl(Tour tour, int index)
        {
            var normalized = NormalizeText($"{tour.Name} {tour.Destination}");

            foreach (var rule in TourImageRules)
            {
                if (rule.Keywords.Any(normalized.Contains))
                {
                    return rule.Url;
                }
            }

            return DefaultTourImages[index % DefaultTourImages.Length];
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static readonly (string[] Keywords, string Url)[] TourImageRules =
        {
            (new[] { "ha long", "halong", "quang ninh" }, "https://images.pexels.com/photos/325185/pexels-photo-325185.jpeg?auto=compress&cs=tinysrgb&w=1200"),
            (new[] { "sapa", "sa pa", "lao cai", "fansipan" }, "https://images.pexels.com/photos/417173/pexels-photo-417173.jpeg?auto=compress&cs=tinysrgb&w=1200"),
            (new[] { "da nang", "danang" }, "https://images.pexels.com/photos/248771/pexels-photo-248771.jpeg?auto=compress&cs=tinysrgb&w=1200"),
            (new[] { "hoi an", "quang nam" }, "https://images.pexels.com/photos/1714361/pexels-photo-1714361.jpeg?auto=compress&cs=tinysrgb&w=1200"),
            (new[] { "phu quoc", "kien giang", "dao ngoc" }, "https://images.pexels.com/photos/457882/pexels-photo-457882.jpeg?auto=compress&cs=tinysrgb&w=1200"),
            (new[] { "ha giang", "dong van", "meo vac" }, "https://images.pexels.com/photos/672532/pexels-photo-672532.jpeg?auto=compress&cs=tinysrgb&w=1200"),
            (new[] { "cao bang", "ban gioc" }, "https://images.pexels.com/photos/291732/pexels-photo-291732.jpeg?auto=compress&cs=tinysrgb&w=1200"),
            (new[] { "da lat", "lam dong" }, "https://images.pexels.com/photos/2387873/pexels-photo-2387873.jpeg?auto=compress&cs=tinysrgb&w=1200"),
            (new[] { "ninh binh", "trang an", "tam coc" }, "https://images.pexels.com/photos/1005417/pexels-photo-1005417.jpeg?auto=compress&cs=tinysrgb&w=1200"),
            (new[] { "hue", "co do", "thua thien" }, "https://images.pexels.com/photos/2166559/pexels-photo-2166559.jpeg?auto=compress&cs=tinysrgb&w=1200")
        };

        private static readonly string[] DefaultTourImages =
        {
            "https://images.pexels.com/photos/338515/pexels-photo-338515.jpeg?auto=compress&cs=tinysrgb&w=1200",
            "https://images.pexels.com/photos/2662116/pexels-photo-2662116.jpeg?auto=compress&cs=tinysrgb&w=1200",
            "https://images.pexels.com/photos/1365425/pexels-photo-1365425.jpeg?auto=compress&cs=tinysrgb&w=1200",
            "https://images.pexels.com/photos/2161467/pexels-photo-2161467.jpeg?auto=compress&cs=tinysrgb&w=1200"
        };
    }

    // Display models for rich UI binding
    public class DepartureDisplayInfo
    {
        public Departure Departure { get; set; } = null!;
        public string TourName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string StartDateFormatted { get; set; } = string.Empty;
        public int AvailableSlots { get; set; }
        public decimal Price { get; set; }
        public string PriceFormatted { get; set; } = "0 đ";
        public int DurationDays { get; set; }
    }

    public class BookingDisplayInfo
    {
        public Booking Booking { get; set; } = null!;
        public string TourName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string DepartureDate { get; set; } = string.Empty;
        public string BookingDateFormatted { get; set; } = string.Empty;
        public int GuestCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#FF9500";
    }

    public class TourDisplayInfo
    {
        public Tour Tour { get; set; } = null!;
        public int Id => Tour?.Id ?? 0;
        public string Name => Tour?.Name ?? string.Empty;
        public string Destination => Tour?.Destination ?? string.Empty;
        public decimal BasePrice => Tour?.BasePrice ?? 0;
        public int DurationDays => Tour?.DurationDays ?? 0;
        public string Description => Tour?.Description ?? string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
    }
}

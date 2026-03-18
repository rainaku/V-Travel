using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        [ObservableProperty] private ObservableCollection<DepartureDisplayInfo> _filteredAvailableDepartures = new();
        [ObservableProperty] private string _bookDestinationFilter = string.Empty;
        [ObservableProperty] private string _tourSearchText = string.Empty;

        // Tour Details
        [ObservableProperty] private TourDisplayInfo? _selectedTour;
        [ObservableProperty] private bool _isTourDetailsVisible = false;

        // Booking
        [ObservableProperty] private DepartureDisplayInfo? _selectedDepartureInfo;
        [ObservableProperty] private string _bookingGuestCount = "1";
        [ObservableProperty] private bool _isBookingFormVisible = false;
        [ObservableProperty] private bool _isPaymentModalVisible = false;
        [ObservableProperty] private string _paymentQrImageUrl = string.Empty;
        [ObservableProperty] private string _paymentTotalFormatted = "0 đ";
        [ObservableProperty] private string _paymentTourName = string.Empty;
        [ObservableProperty] private string _paymentScheduleText = string.Empty;
        [ObservableProperty] private string _paymentGuestText = string.Empty;
        [ObservableProperty] private bool _isProcessingPayment = false;
        [ObservableProperty] private bool _isAppDialogVisible = false;
        [ObservableProperty] private string _appDialogTitle = string.Empty;
        [ObservableProperty] private string _appDialogMessage = string.Empty;
        [ObservableProperty] private string _appDialogPrimaryButtonText = "Đồng ý";
        [ObservableProperty] private string _appDialogSecondaryButtonText = "Hủy";
        [ObservableProperty] private bool _isAppDialogSecondaryVisible = false;

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
        private const int InitialTourBatchSize = 6;
        private const int TourBatchSize = 6;
        private readonly List<TourDisplayInfo> _filteredTourCache = new();
        private int _loadedTourCount;
        private bool _isLoadingMoreTours;
        private TaskCompletionSource<bool>? _appDialogDecisionTcs;
        private bool _isAppDialogDecisionPending;
        private static readonly Regex FullNamePattern = new(@"^[\p{L}\p{M}]+(?:[ '\-][\p{L}\p{M}]+)*$", RegexOptions.Compiled);
        private static readonly Regex EmailPattern = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex VietnamMobilePattern = new(@"^0(?:3|5|7|8|9)\d{8}$", RegexOptions.Compiled);

        public bool HasMoreToursToLoad => _loadedTourCount < _filteredTourCache.Count;
        public bool IsBookingModalOverlayVisible => IsBookingFormVisible && !IsPaymentModalVisible && !IsAppDialogVisible;
        public bool IsPaymentModalOverlayVisible => IsPaymentModalVisible && !IsAppDialogVisible;
        public bool IsBookDestinationFilterActive => !string.IsNullOrWhiteSpace(BookDestinationFilter);

        public CustomerViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = LoadDataAsync();
        }

        partial void OnTourSearchTextChanged(string value) => ApplyTourFilter();
        partial void OnBookDestinationFilterChanged(string value)
        {
            ApplyDepartureFilter();
            OnPropertyChanged(nameof(IsBookDestinationFilterActive));
        }
        partial void OnIsBookingFormVisibleChanged(bool value) => NotifyOverlayVisibilityStateChanged();
        partial void OnIsPaymentModalVisibleChanged(bool value) => NotifyOverlayVisibilityStateChanged();
        partial void OnIsAppDialogVisibleChanged(bool value) => NotifyOverlayVisibilityStateChanged();

        private void ApplyTourFilter()
        {
            IEnumerable<TourDisplayInfo> filteredQuery;

            if (string.IsNullOrWhiteSpace(TourSearchText))
            {
                filteredQuery = AllTours;
            }
            else
            {
                var lower = TourSearchText.ToLower();
                filteredQuery = AllTours.Where(t =>
                        t.Name.ToLower().Contains(lower) ||
                        t.Destination.ToLower().Contains(lower) ||
                        t.Description.ToLower().Contains(lower));
            }

            _filteredTourCache.Clear();
            _filteredTourCache.AddRange(filteredQuery);
            ResetVisibleTours();
        }

        private void ApplyDepartureFilter()
        {
            IEnumerable<DepartureDisplayInfo> query = AvailableDepartures;

            if (!string.IsNullOrWhiteSpace(BookDestinationFilter))
            {
                query = query.Where(d => IsSameDestination(d.Destination, BookDestinationFilter));
            }

            FilteredAvailableDepartures = new ObservableCollection<DepartureDisplayInfo>(query);
        }

        [RelayCommand]
        private void NavigateTo(string page)
        {
            SelectedPage = page;
        }

        [RelayCommand]
        private void ClearBookDestinationFilter()
        {
            BookDestinationFilter = string.Empty;
        }

        [RelayCommand]
        private void ConfirmAppDialog()
        {
            if (_isAppDialogDecisionPending)
            {
                _appDialogDecisionTcs?.TrySetResult(true);
            }

            CloseAppDialog();
        }

        [RelayCommand]
        private void CancelAppDialog()
        {
            if (_isAppDialogDecisionPending)
            {
                _appDialogDecisionTcs?.TrySetResult(false);
            }

            CloseAppDialog();
        }

        private void NotifyOverlayVisibilityStateChanged()
        {
            OnPropertyChanged(nameof(IsBookingModalOverlayVisible));
            OnPropertyChanged(nameof(IsPaymentModalOverlayVisible));
        }

        private void ShowAppDialogInfo(string title, string message)
        {
            PrepareAppDialog(
                title,
                message,
                primaryButtonText: "Đóng",
                secondaryButtonText: "Hủy",
                showSecondaryButton: false,
                isDecisionDialog: false);
        }

        private Task<bool> ShowAppDialogConfirmationAsync(
            string title,
            string message,
            string confirmText = "Xác nhận",
            string cancelText = "Hủy")
        {
            PrepareAppDialog(
                title,
                message,
                primaryButtonText: confirmText,
                secondaryButtonText: cancelText,
                showSecondaryButton: true,
                isDecisionDialog: true);

            return _appDialogDecisionTcs?.Task ?? Task.FromResult(false);
        }

        private void PrepareAppDialog(
            string title,
            string message,
            string primaryButtonText,
            string secondaryButtonText,
            bool showSecondaryButton,
            bool isDecisionDialog)
        {
            if (_isAppDialogDecisionPending)
            {
                _appDialogDecisionTcs?.TrySetResult(false);
            }

            _isAppDialogDecisionPending = isDecisionDialog;
            _appDialogDecisionTcs = isDecisionDialog
                ? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
                : null;

            AppDialogTitle = title;
            AppDialogMessage = message;
            AppDialogPrimaryButtonText = primaryButtonText;
            AppDialogSecondaryButtonText = secondaryButtonText;
            IsAppDialogSecondaryVisible = showSecondaryButton;
            IsAppDialogVisible = true;
        }

        private void CloseAppDialog()
        {
            IsAppDialogVisible = false;
            IsAppDialogSecondaryVisible = false;
            _isAppDialogDecisionPending = false;
            _appDialogDecisionTcs = null;
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
                ApplyTourFilter();
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
                        EndDateFormatted = d.StartDate
                            .AddDays(Math.Max((d.Tour?.DurationDays ?? 1) - 1, 0))
                            .ToString("dd/MM/yyyy"),
                        AvailableSlots = d.AvailableSlots,
                        Price = d.Tour?.BasePrice ?? 0,
                        PriceFormatted = $"{(d.Tour?.BasePrice ?? 0):N0} đ",
                        DurationDays = d.Tour?.DurationDays ?? 0
                    })
                    .ToList();
                AvailableDepartures = new ObservableCollection<DepartureDisplayInfo>(displayDeps);
                ApplyDepartureFilter();

                // Load customer profile linked to current user.
                var custs = (await client.From<Customer>().Get()).Models;
                _customerProfile = await ResolveCustomerProfileAsync(client, custs);

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
                ShowAppDialogInfo("Lỗi", $"Lỗi tải dữ liệu: {ex.Message}");
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
            IsPaymentModalVisible = false;
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
        private void GoToBookingFromSelectedTour()
        {
            if (SelectedTour == null)
            {
                return;
            }

            var destinationMatches = AvailableDepartures
                .Where(d => IsSameDestination(d.Destination, SelectedTour.Destination))
                .ToList();

            if (destinationMatches.Count == 0)
            {
                ShowAppDialogInfo("Thông báo", $"Hiện chưa có lịch mở bán tại {SelectedTour.Destination}.");
                return;
            }

            BookDestinationFilter = SelectedTour.Destination;
            SelectedPage = "Book";
            IsTourDetailsVisible = false;
            IsBookingFormVisible = false;
            IsPaymentModalVisible = false;
        }

        [RelayCommand]
        private void CancelBookingForm()
        {
            IsBookingFormVisible = false;
            IsPaymentModalVisible = false;
        }

        [RelayCommand]
        private async Task BookTourAsync()
        {
            await ProceedToPaymentAsync();
        }

        [RelayCommand(CanExecute = nameof(CanLoadMoreTours))]
        private void LoadMoreTours()
        {
            LoadMoreToursInternal(TourBatchSize);
        }

        [RelayCommand]
        private async Task ProceedToPaymentAsync()
        {
            if (!TryValidateBookingInput(out var guests) || SelectedDepartureInfo == null)
            {
                return;
            }

            var total = SelectedDepartureInfo.Price * guests;
            PaymentTourName = SelectedDepartureInfo.TourName;
            PaymentScheduleText = $"{SelectedDepartureInfo.StartDateFormatted} - {SelectedDepartureInfo.EndDateFormatted}";
            PaymentGuestText = $"{guests} khách";
            PaymentTotalFormatted = $"{total:N0} đ";

            var qrPayload = $"VIETTRAVEL|DEP:{SelectedDepartureInfo.Departure.Id}|GUEST:{guests}|AMT:{total:0}|{DateTime.Now:yyyyMMddHHmmss}";
            PaymentQrImageUrl = $"https://quickchart.io/qr?size=280&text={Uri.EscapeDataString(qrPayload)}";
            IsPaymentModalVisible = true;
            await Task.CompletedTask;
        }

        [RelayCommand]
        private void CancelPayment()
        {
            IsPaymentModalVisible = false;
        }

        [RelayCommand]
        private async Task ConfirmPaymentAsync()
        {
            if (IsProcessingPayment) return;
            if (!TryValidateBookingInput(out var guests)) return;

            IsProcessingPayment = true;
            try
            {
                var success = await CreateBookingAndPaymentAsync(guests, markAsPaid: true);
                if (!success) return;

                IsPaymentModalVisible = false;
                IsBookingFormVisible = false;

                ShowAppDialogInfo(
                    "Thanh toán thành công",
                    $"🎉 Thanh toán thành công!\n\n" +
                    $"Tour: {PaymentTourName}\n" +
                    $"Lịch đi: {PaymentScheduleText}\n" +
                    $"Số khách: {PaymentGuestText}\n" +
                    $"Tổng thanh toán: {PaymentTotalFormatted}\n\n" +
                    "Booking đã hoàn tất.");

                await LoadDataAsync();
                SelectedPage = "MyBookings";
            }
            finally
            {
                IsProcessingPayment = false;
            }
        }

        private bool TryValidateBookingInput(out int guests)
        {
            guests = 0;
            NormalizeBookingInputValues();

            if (SelectedDepartureInfo == null)
            {
                ShowAppDialogInfo("Thông báo", "Vui lòng chọn lịch khởi hành.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(InfoFullName)
                || string.IsNullOrWhiteSpace(InfoPhone)
                || string.IsNullOrWhiteSpace(InfoEmail)
                || string.IsNullOrWhiteSpace(InfoAddress))
            {
                ShowAppDialogInfo("Thiếu thông tin", "Vui lòng nhập đầy đủ họ tên, số điện thoại, email và địa chỉ.");
                return false;
            }

            if (InfoFullName.Length < 4 || InfoFullName.Length > 80)
            {
                ShowAppDialogInfo("Họ tên chưa hợp lệ", "Họ tên phải từ 4 đến 80 ký tự.");
                return false;
            }

            var fullNameParts = InfoFullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fullNameParts.Length < 2)
            {
                ShowAppDialogInfo("Họ tên chưa hợp lệ", "Vui lòng nhập đầy đủ họ và tên.");
                return false;
            }

            if (!FullNamePattern.IsMatch(InfoFullName))
            {
                ShowAppDialogInfo("Họ tên chưa hợp lệ", "Họ tên chỉ được chứa chữ cái và khoảng trắng.");
                return false;
            }

            if (!VietnamMobilePattern.IsMatch(InfoPhone))
            {
                ShowAppDialogInfo("Số điện thoại chưa hợp lệ", "Số điện thoại phải là số di động Việt Nam hợp lệ (vd: 09xxxxxxxx).");
                return false;
            }

            if (InfoEmail.Length > 120 || !EmailPattern.IsMatch(InfoEmail))
            {
                ShowAppDialogInfo("Email chưa hợp lệ", "Vui lòng nhập đúng định dạng email (vd: ten@email.com).");
                return false;
            }

            if (InfoAddress.Length < 10 || InfoAddress.Length > 200)
            {
                ShowAppDialogInfo("Địa chỉ chưa hợp lệ", "Địa chỉ phải từ 10 đến 200 ký tự.");
                return false;
            }

            if (!InfoAddress.Any(ch => char.IsLetter(ch)))
            {
                ShowAppDialogInfo("Địa chỉ chưa hợp lệ", "Địa chỉ cần có thông tin cụ thể hơn.");
                return false;
            }

            if (!int.TryParse(BookingGuestCount, out guests) || guests <= 0)
            {
                ShowAppDialogInfo("Thông báo", "Số khách phải là số nguyên dương.");
                return false;
            }

            if (guests > 20)
            {
                ShowAppDialogInfo("Thông báo", "Một booking chỉ được đặt tối đa 20 khách.");
                return false;
            }

            if (guests > SelectedDepartureInfo.AvailableSlots)
            {
                ShowAppDialogInfo("Thông báo", $"Chỉ còn {SelectedDepartureInfo.AvailableSlots} chỗ trống.");
                return false;
            }

            return true;
        }

        private void NormalizeBookingInputValues()
        {
            InfoFullName = NormalizeWhitespace(InfoFullName);
            InfoEmail = (InfoEmail ?? string.Empty).Trim();
            InfoAddress = NormalizeWhitespace(InfoAddress);
            InfoPhone = NormalizeVietnamPhone(InfoPhone);
        }

        private static string NormalizeWhitespace(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value.Trim(), @"\s+", " ");
        }

        private static string NormalizeVietnamPhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var digitsOnly = Regex.Replace(value, @"[^\d+]", string.Empty);

            if (digitsOnly.StartsWith("+84"))
            {
                digitsOnly = "0" + digitsOnly[3..];
            }
            else if (digitsOnly.StartsWith("84") && digitsOnly.Length == 11)
            {
                digitsOnly = "0" + digitsOnly[2..];
            }

            return digitsOnly;
        }

        private bool CanLoadMoreTours()
        {
            return HasMoreToursToLoad && !_isLoadingMoreTours;
        }

        private void ResetVisibleTours()
        {
            _loadedTourCount = 0;
            FilteredTours = new ObservableCollection<TourDisplayInfo>();
            OnPropertyChanged(nameof(HasMoreToursToLoad));
            LoadMoreToursCommand.NotifyCanExecuteChanged();
            LoadMoreToursInternal(InitialTourBatchSize);
        }

        private bool LoadMoreToursInternal(int batchSize)
        {
            if (_isLoadingMoreTours || !HasMoreToursToLoad)
            {
                return false;
            }

            _isLoadingMoreTours = true;
            LoadMoreToursCommand.NotifyCanExecuteChanged();

            try
            {
                var remaining = _filteredTourCache.Count - _loadedTourCount;
                var take = Math.Min(batchSize, remaining);

                for (var i = 0; i < take; i++)
                {
                    FilteredTours.Add(_filteredTourCache[_loadedTourCount + i]);
                }

                _loadedTourCount += take;
                OnPropertyChanged(nameof(HasMoreToursToLoad));
                return take > 0;
            }
            finally
            {
                _isLoadingMoreTours = false;
                LoadMoreToursCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task<bool> CreateBookingAndPaymentAsync(int guests, bool markAsPaid)
        {
            if (SelectedDepartureInfo == null) return false;

            Departure? latestDeparture = null;
            int originalAvailableSlots = 0;
            string originalDepartureStatus = string.Empty;
            bool hasReservedSlots = false;
            int createdBookingId = 0;

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                if (_customerProfile == null)
                {
                    var newCust = new Customer
                    {
                        FullName = InfoFullName,
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

                await SyncCurrentUserProfileAsync(client);

                if (_customerProfile == null) return false;

                var depResp = await client.From<Departure>().Get();
                latestDeparture = depResp.Models.FirstOrDefault(d => d.Id == SelectedDepartureInfo.Departure.Id);
                if (latestDeparture == null)
                {
                    ShowAppDialogInfo("Lỗi", "Không tìm thấy lịch khởi hành.");
                    return false;
                }
                if (latestDeparture.Status != "Mở bán")
                {
                    ShowAppDialogInfo("Thông báo", "Lịch khởi hành hiện không mở bán.");
                    return false;
                }
                if (guests > latestDeparture.AvailableSlots)
                {
                    ShowAppDialogInfo("Thông báo", $"Chỉ còn {latestDeparture.AvailableSlots} chỗ trống.");
                    return false;
                }

                var tourResp = await client.From<Tour>().Get();
                var tour = tourResp.Models.FirstOrDefault(t => t.Id == latestDeparture.TourId);
                if (tour == null)
                {
                    ShowAppDialogInfo("Lỗi", "Không tìm thấy thông tin tour.");
                    return false;
                }

                var existingBookingsResp = await client.From<Booking>().Get();
                var scheduleConflictType = GetDepartureDateConflictType(
                    existingBookingsResp.Models,
                    depResp.Models,
                    tourResp.Models,
                    _customerProfile.Id,
                    latestDeparture,
                    tour);

                if (scheduleConflictType == DepartureDateConflictType.DifferentDestination)
                {
                    ShowAppDialogInfo(
                        "Không thể đặt tour",
                        "Bạn đã có tour khác địa điểm trong cùng ngày khởi hành.\nVui lòng chọn lịch khác để tránh trùng lịch di chuyển.");
                    return false;
                }

                if (scheduleConflictType == DepartureDateConflictType.SameDestination)
                {
                    var confirmConflict = await ShowAppDialogConfirmationAsync(
                        "Cảnh báo trùng lịch",
                        "Bạn đang có 1 tour cùng ngày khởi hành tại địa điểm này.\nBạn có muốn đặt tiếp không?",
                        confirmText: "Vẫn đặt",
                        cancelText: "Không");

                    if (!confirmConflict)
                    {
                        return false;
                    }
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
                    Status = markAsPaid ? "Đã xác nhận" : "Chờ thanh toán"
                };

                var bookingResp = await client.From<Booking>().Insert(booking);
                var createdBooking = bookingResp.Models.FirstOrDefault();
                if (createdBooking == null)
                    throw new InvalidOperationException("Không lấy được dữ liệu booking sau khi tạo.");

                createdBookingId = createdBooking.Id;
                var totalAmount = tour.BasePrice * guests;
                var payment = new Payment
                {
                    BookingId = createdBooking.Id,
                    TotalAmount = totalAmount,
                    PaidAmount = markAsPaid ? totalAmount : 0,
                    Status = markAsPaid ? "Đã thanh toán đủ" : "Chưa thanh toán",
                    PaymentDate = markAsPaid ? DateTime.Now : null,
                    PaymentMethod = "Chuyển khoản QR (Mock)"
                };
                await client.From<Payment>().Insert(payment);

                return true;
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

                ShowAppDialogInfo("Lỗi", $"Lỗi đặt tour: {ex.Message}");
                return false;
            }
        }

        private static DepartureDateConflictType GetDepartureDateConflictType(
            System.Collections.Generic.IEnumerable<Booking> bookings,
            System.Collections.Generic.IEnumerable<Departure> departures,
            System.Collections.Generic.IEnumerable<Tour> tours,
            int customerId,
            Departure candidateDeparture,
            Tour candidateTour)
        {
            var activeBookings = bookings.Where(b =>
                b.CustomerId == customerId &&
                b.Status != "Đã hủy" &&
                b.Status != "Hủy");

            var departureById = departures.ToDictionary(d => d.Id);
            var tourById = tours.ToDictionary(t => t.Id);
            var candidateStart = candidateDeparture.StartDate.Date;
            var candidateDestination = candidateTour.Destination ?? string.Empty;
            var hasSameDestinationConflict = false;

            foreach (var booking in activeBookings)
            {
                if (!departureById.TryGetValue(booking.DepartureId, out var existingDeparture))
                {
                    continue;
                }

                if (existingDeparture.StartDate.Date != candidateStart)
                {
                    continue;
                }

                if (!tourById.TryGetValue(existingDeparture.TourId, out var existingTour))
                {
                    continue;
                }

                if (IsSameDestination(existingTour.Destination, candidateDestination))
                {
                    hasSameDestinationConflict = true;
                    continue;
                }

                return DepartureDateConflictType.DifferentDestination;
            }

            if (hasSameDestinationConflict)
            {
                return DepartureDateConflictType.SameDestination;
            }

            return DepartureDateConflictType.None;
        }

        private enum DepartureDateConflictType
        {
            None = 0,
            SameDestination = 1,
            DifferentDestination = 2
        }

        private async Task<Customer?> ResolveCustomerProfileAsync(
            Supabase.Client client,
            System.Collections.Generic.IEnumerable<Customer> customers)
        {
            var currentUser = _mainViewModel.CurrentUser;
            if (currentUser == null)
            {
                return null;
            }

            var customerList = customers.ToList();
            var currentFullName = (currentUser.FullName ?? string.Empty).Trim();
            var currentUsername = (currentUser.Username ?? string.Empty).Trim();

            var profile = customerList.FirstOrDefault(c =>
                string.Equals((c.FullName ?? string.Empty).Trim(), currentFullName, StringComparison.OrdinalIgnoreCase));

            // Fallback for older accounts: match username as email when possible.
            if (profile == null &&
                !string.IsNullOrWhiteSpace(currentUsername) &&
                currentUsername.Contains("@", StringComparison.Ordinal))
            {
                profile = customerList.FirstOrDefault(c =>
                    string.Equals((c.Email ?? string.Empty).Trim(), currentUsername, StringComparison.OrdinalIgnoreCase));
            }

            // Fallback from booking history linked by user_id.
            if (profile == null)
            {
                var bookings = (await client.From<Booking>().Get()).Models
                    .Where(b => b.UserId == currentUser.Id)
                    .OrderByDescending(b => b.BookingDate)
                    .ToList();

                var customerId = bookings.Select(b => b.CustomerId).FirstOrDefault();
                if (customerId > 0)
                {
                    profile = customerList.FirstOrDefault(c => c.Id == customerId);
                    if (profile == null)
                    {
                        profile = (await client.From<Customer>().Where(c => c.Id == customerId).Get())
                            .Models
                            .FirstOrDefault();
                    }
                }
            }

            return profile;
        }

        private async Task SyncCurrentUserProfileAsync(Supabase.Client client)
        {
            var currentUser = _mainViewModel.CurrentUser;
            if (currentUser == null)
            {
                return;
            }

            currentUser.FullName = InfoFullName;
            await client.From<User>().Update(currentUser);
            OnPropertyChanged(nameof(FullName));
            OnPropertyChanged(nameof(UserInitials));
        }

        [RelayCommand]
        private async Task SaveProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(InfoFullName))
            {
                ShowAppDialogInfo("Thông báo", "Vui lòng nhập họ tên.");
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

                await SyncCurrentUserProfileAsync(client);
                ShowAppDialogInfo("Thành công", "✅ Cập nhật thông tin thành công!");
            }
            catch (Exception ex)
            {
                ShowAppDialogInfo("Lỗi", $"Lỗi: {ex.Message}");
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

        private static bool IsSameDestination(string left, string right)
        {
            return NormalizeText(left) == NormalizeText(right);
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
        public string EndDateFormatted { get; set; } = string.Empty;
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

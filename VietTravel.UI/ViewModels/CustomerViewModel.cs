using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using VietTravel.Core.Models;
using VietTravel.Data;
using VietTravel.Data.Services;
using VietTravel.UI.Models;
using Postgrest;

namespace VietTravel.UI.ViewModels
{
    public partial class CustomerViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly CloudinaryImageService _cloudinaryImageService = new();
        private readonly PromoCodeService _promoCodeService = new();
        private readonly TourRatingService _tourRatingService = new();
        private readonly GuideRatingService _guideRatingService = new();

        public string FullName => _mainViewModel.CurrentUser?.FullName ?? "Khách Hàng";
        public string UserInitials => GetInitials(FullName);
        [ObservableProperty] private string _avatarUrl = string.Empty;

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
        [ObservableProperty] private string _paymentOriginalAmountFormatted = "0 đ";
        [ObservableProperty] private string _paymentDiscountAmountFormatted = "0 đ";
        [ObservableProperty] private string _promoCodeInput = string.Empty;
        [ObservableProperty] private string _promoCodeStatusMessage = string.Empty;
        [ObservableProperty] private bool _isPromoCodeStatusSuccess = false;
        [ObservableProperty] private string _appliedPromoCode = string.Empty;
        [ObservableProperty] private bool _isProcessingPayment = false;
        [ObservableProperty] private bool _isAppDialogVisible = false;
        [ObservableProperty] private string _appDialogTitle = string.Empty;
        [ObservableProperty] private string _appDialogMessage = string.Empty;
        [ObservableProperty] private string _appDialogPrimaryButtonText = "Đồng ý";
        [ObservableProperty] private string _appDialogSecondaryButtonText = "Hủy";
        [ObservableProperty] private bool _isAppDialogSecondaryVisible = false;

        // My Bookings
        [ObservableProperty] private ObservableCollection<BookingDisplayInfo> _myBookings = new();
        [ObservableProperty] private bool _isRatingFormVisible = false;
        [ObservableProperty] private BookingDisplayInfo? _selectedRatingBooking;
        [ObservableProperty] private int _formTourRatingValue = 5;
        [ObservableProperty] private string _formTourRatingComment = string.Empty;
        [ObservableProperty] private int _formGuideRatingValue = 5;
        [ObservableProperty] private string _formGuideRatingComment = string.Empty;
        [ObservableProperty] private string _ratingSchemaWarningMessage = string.Empty;
        [ObservableProperty] private string _guideRatingSchemaWarningMessage = string.Empty;

        // Personal Info
        [ObservableProperty] private string _infoFullName = string.Empty;
        [ObservableProperty] private string _infoPhone = string.Empty;
        [ObservableProperty] private string _infoEmail = string.Empty;
        [ObservableProperty] private string _infoAddress = string.Empty;
        [ObservableProperty] private bool _isSavingProfile = false;
        [ObservableProperty] private bool _isUploadingAvatar = false;

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
        private static readonly Regex AddressPattern = new(@"^[\p{L}\p{M}\d\s,./\-#]+$", RegexOptions.Compiled);
        private static readonly string[] CancelledBookingStatuses = { "Đã hủy", "Hủy" };

        public bool HasMoreToursToLoad => _loadedTourCount < _filteredTourCache.Count;
        public bool IsBookingModalOverlayVisible => IsBookingFormVisible && !IsPaymentModalVisible && !IsAppDialogVisible;
        public bool IsPaymentModalOverlayVisible => IsPaymentModalVisible && !IsAppDialogVisible;
        public bool IsBookDestinationFilterActive => !string.IsNullOrWhiteSpace(BookDestinationFilter);
        public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
        public string ChangeAvatarButtonText => IsUploadingAvatar ? "Đang tải ảnh..." : "Đổi ảnh đại diện";
        public bool IsDebugMenuVisible => _mainViewModel.IsDebugMenuVisible;
        public ReadOnlyObservableCollection<AppNotification> AccountNotifications => _mainViewModel.NotificationCenter.Notifications;
        public int UnreadNotificationCount => _mainViewModel.NotificationCenter.UnreadCount;
        public bool HasNoNotifications => AccountNotifications.Count == 0;
        public bool HasTourRatingSchemaWarning => !string.IsNullOrWhiteSpace(RatingSchemaWarningMessage);
        public bool HasGuideRatingSchemaWarning => !string.IsNullOrWhiteSpace(GuideRatingSchemaWarningMessage);
        public bool HasRatingSchemaWarning => HasTourRatingSchemaWarning || HasGuideRatingSchemaWarning;
        public string RatingSchemaWarningSummary =>
            string.Join(Environment.NewLine, new[] { RatingSchemaWarningMessage, GuideRatingSchemaWarningMessage }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        public string RatingFormTitle => SelectedRatingBooking?.HasBothRatings == true
            ? "Chỉnh sửa đánh giá tour và HDV"
            : "Đánh giá tour và hướng dẫn viên";
        public string RatingFormActionText => SelectedRatingBooking?.HasBothRatings == true
            ? "Cập nhật đánh giá"
            : "Gửi đánh giá";
        public string RatingFormScopeNotice =>
            "Vui lòng hoàn thành cả đánh giá tour và hướng dẫn viên trong cùng một lần gửi.";
        public string TourRatingFormCommentHint => "Chia sẻ trải nghiệm của bạn về tour này";
        public string GuideRatingFormCommentHint => "Chia sẻ trải nghiệm của bạn với hướng dẫn viên trong chuyến đi này";
        public string RatingFormContextText
        {
            get
            {
                if (SelectedRatingBooking == null)
                {
                    return string.Empty;
                }

                return $"{SelectedRatingBooking.TourName} • HDV: {SelectedRatingBooking.GuideName} • {SelectedRatingBooking.StartDateFormatted}";
            }
        }
        public string TourRatingFormStarsText => TourRatingDisplayHelper.ToStarsText(FormTourRatingValue);
        public string GuideRatingFormStarsText => TourRatingDisplayHelper.ToStarsText(FormGuideRatingValue);

        public CustomerViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            PropertyChangedEventManager.AddHandler(_mainViewModel, MainViewModelOnPropertyChanged, nameof(MainViewModel.IsDebugMenuVisible));
            PropertyChangedEventManager.AddHandler(_mainViewModel.NotificationCenter, NotificationCenterOnPropertyChanged, nameof(_mainViewModel.NotificationCenter.UnreadCount));
            CollectionChangedEventManager.AddHandler(AccountNotifications, OnNotificationCollectionChanged);
            AvatarUrl = _mainViewModel.CurrentUser?.AvatarUrl ?? string.Empty;
            _ = LoadDataAsync();
        }

        partial void OnTourSearchTextChanged(string value) => ApplyTourFilter();
        partial void OnAvatarUrlChanged(string value) => OnPropertyChanged(nameof(HasAvatar));
        partial void OnBookDestinationFilterChanged(string value)
        {
            ApplyDepartureFilter();
            OnPropertyChanged(nameof(IsBookDestinationFilterActive));
        }
        partial void OnBookingGuestCountChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(AppliedPromoCode))
            {
                IsPromoCodeStatusSuccess = false;
                PromoCodeStatusMessage = "Số khách thay đổi, vui lòng kiểm tra lại mã giảm giá.";
                AppliedPromoCode = string.Empty;
            }
        }
        partial void OnIsBookingFormVisibleChanged(bool value) => NotifyOverlayVisibilityStateChanged();
        partial void OnIsPaymentModalVisibleChanged(bool value) => NotifyOverlayVisibilityStateChanged();
        partial void OnIsAppDialogVisibleChanged(bool value) => NotifyOverlayVisibilityStateChanged();
        partial void OnPromoCodeInputChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ClearPromoCodeStatus(clearCodeInput: false);
                return;
            }

            if (!string.Equals(AppliedPromoCode, PromoCodeService.NormalizeCode(value), StringComparison.Ordinal))
            {
                IsPromoCodeStatusSuccess = false;
                PromoCodeStatusMessage = "Mã đã thay đổi, vui lòng kiểm tra lại.";
                AppliedPromoCode = string.Empty;
            }
        }
        partial void OnIsUploadingAvatarChanged(bool value)
        {
            OnPropertyChanged(nameof(ChangeAvatarButtonText));
            ChangeAvatarCommand.NotifyCanExecuteChanged();
        }
        partial void OnFormTourRatingValueChanged(int value) => OnPropertyChanged(nameof(TourRatingFormStarsText));
        partial void OnFormGuideRatingValueChanged(int value) => OnPropertyChanged(nameof(GuideRatingFormStarsText));
        partial void OnRatingSchemaWarningMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasTourRatingSchemaWarning));
            OnPropertyChanged(nameof(HasRatingSchemaWarning));
            OnPropertyChanged(nameof(RatingSchemaWarningSummary));
        }
        partial void OnGuideRatingSchemaWarningMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasGuideRatingSchemaWarning));
            OnPropertyChanged(nameof(HasRatingSchemaWarning));
            OnPropertyChanged(nameof(RatingSchemaWarningSummary));
        }
        partial void OnSelectedRatingBookingChanged(BookingDisplayInfo? value)
        {
            OnPropertyChanged(nameof(RatingFormTitle));
            OnPropertyChanged(nameof(RatingFormActionText));
            OnPropertyChanged(nameof(RatingFormScopeNotice));
            OnPropertyChanged(nameof(RatingFormContextText));
        }

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
        private void SimulatePaymentDebug()
        {
            var bookingId = Random.Shared.Next(1000, 9999);
            _mainViewModel.NotificationCenter.AddDebugNotification(
                "Xác nhận thanh toán",
                $"(Debug) Booking BK-{bookingId} đã được xác nhận thanh toán.",
                "Thanh toán");
        }

        [RelayCommand]
        private void SimulateDepartureReminderDebug()
        {
            var departureTime = DateTime.Now.AddHours(6);
            _mainViewModel.NotificationCenter.AddDebugNotification(
                "Nhắc lịch khởi hành",
                $"(Debug) Tour giả lập sẽ khởi hành lúc {departureTime:dd/MM/yyyy HH:mm}.",
                "Khởi hành");
        }

        [RelayCommand]
        private void SeedDebugNotifications()
        {
            for (var i = 1; i <= 5; i++)
            {
                _mainViewModel.NotificationCenter.AddDebugNotification(
                    $"Debug Notification #{i}",
                    $"Mẫu thông báo số {i} để test UI.",
                    "Debug");
            }
        }

        [RelayCommand]
        private async Task ForceDebugSyncAsync()
        {
            await _mainViewModel.NotificationCenter.RefreshNowAsync();
            _mainViewModel.NotificationCenter.AddDebugNotification(
                "Debug Sync",
                "Đã force đồng bộ notification từ dữ liệu thật.",
                "Debug");
        }

        [RelayCommand]
        private void MarkAllDebugNotificationsRead()
        {
            _mainViewModel.NotificationCenter.MarkAllAsRead();
        }

        [RelayCommand]
        private void ClearDebugNotifications()
        {
            _mainViewModel.NotificationCenter.ClearAllNotifications();
        }

        [RelayCommand]
        private void MarkNotificationAsRead(AppNotification? notification)
        {
            _mainViewModel.NotificationCenter.MarkAsRead(notification);
        }

        [RelayCommand]
        private void MarkAllNotificationsAsRead()
        {
            _mainViewModel.NotificationCenter.MarkAllAsRead();
        }

        [RelayCommand]
        private async Task RefreshNotificationsAsync()
        {
            await _mainViewModel.NotificationCenter.RefreshNowAsync();
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

        private void MainViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsDebugMenuVisible))
            {
                OnPropertyChanged(nameof(IsDebugMenuVisible));
            }
        }

        private void NotificationCenterOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_mainViewModel.NotificationCenter.UnreadCount))
            {
                OnPropertyChanged(nameof(UnreadNotificationCount));
            }
        }

        private void OnNotificationCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasNoNotifications));
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
                var tours = (await client.From<Tour>().Get()).Models ?? new List<Tour>();
                var approvedTourRatings = await LoadApprovedTourRatingsAsync();
                try
                {
                    var transports = (await client.From<Transport>().Get()).Models ?? new List<Transport>();
                    var hotels = (await client.From<Hotel>().Get()).Models ?? new List<Hotel>();
                    var attractions = (await client.From<Attraction>().Get()).Models ?? new List<Attraction>();
                    var tourTransports = (await client.From<TourTransport>().Get()).Models ?? new List<TourTransport>();
                    var tourHotels = (await client.From<TourHotel>().Get()).Models ?? new List<TourHotel>();
                    var tourAttractions = (await client.From<TourAttraction>().Get()).Models ?? new List<TourAttraction>();

                    var transportById = transports.ToDictionary(x => x.Id);
                    var hotelById = hotels.ToDictionary(x => x.Id);
                    var attractionById = attractions.ToDictionary(x => x.Id);
                    var tourTransportLookup = tourTransports.GroupBy(x => x.TourId).ToDictionary(g => g.Key, g => g.ToList());
                    var tourHotelLookup = tourHotels.GroupBy(x => x.TourId).ToDictionary(g => g.Key, g => g.ToList());
                    var tourAttractionLookup = tourAttractions
                        .GroupBy(x => x.TourId)
                        .ToDictionary(g => g.Key, g => g.OrderBy(x => x.OrderIndex).ToList());

                    foreach (var tour in tours)
                    {
                        if (tourTransportLookup.TryGetValue(tour.Id, out var mappedTransports))
                        {
                            foreach (var mapped in mappedTransports)
                            {
                                mapped.Transport = transportById.TryGetValue(mapped.TransportId, out var transport) ? transport : null;
                            }

                            tour.TourTransports = mappedTransports
                                .Where(x => x.Transport != null)
                                .ToList();
                        }
                        else
                        {
                            tour.TourTransports = new List<TourTransport>();
                        }

                        if (tourHotelLookup.TryGetValue(tour.Id, out var mappedHotels))
                        {
                            foreach (var mapped in mappedHotels)
                            {
                                mapped.Hotel = hotelById.TryGetValue(mapped.HotelId, out var hotel) ? hotel : null;
                            }

                            tour.TourHotels = mappedHotels
                                .Where(x => x.Hotel != null)
                                .ToList();
                        }
                        else
                        {
                            tour.TourHotels = new List<TourHotel>();
                        }

                        if (tourAttractionLookup.TryGetValue(tour.Id, out var mappedAttractions))
                        {
                            foreach (var mapped in mappedAttractions)
                            {
                                mapped.Attraction = attractionById.TryGetValue(mapped.AttractionId, out var attraction) ? attraction : null;
                            }

                            tour.TourAttractions = mappedAttractions
                                .Where(x => x.Attraction != null)
                                .ToList();
                        }
                        else
                        {
                            tour.TourAttractions = new List<TourAttraction>();
                        }
                    }
                }
                catch
                {
                    foreach (var tour in tours)
                    {
                        tour.TourTransports = new List<TourTransport>();
                        tour.TourHotels = new List<TourHotel>();
                        tour.TourAttractions = new List<TourAttraction>();
                    }
                }

                // Stable and destination-aware tour thumbnails (direct JPEG URLs).
                var displayTours = tours.Select((t, i) =>
                {
                    approvedTourRatings.TryGetValue(t.Id, out var ratingSummary);
                    return new TourDisplayInfo
                    {
                        Tour = t,
                        ImageUrl = ResolveTourImageUrl(t, i),
                        AverageRating = ratingSummary.AverageRating,
                        RatingCount = ratingSummary.RatingCount
                    };
                }).ToList();

                AllTours = new ObservableCollection<TourDisplayInfo>(displayTours);
                ApplyTourFilter();
                TotalToursAvailable = tours.Count;

                // Load departures with tour info
                var deps = (await client.From<Departure>().Get()).Models;
                var assignments = (await client.From<TourGuideAssignment>().Get()).Models ?? new List<TourGuideAssignment>();
                var users = (await client.From<User>().Get()).Models ?? new List<User>();

                foreach (var d in deps) d.Tour = tours.FirstOrDefault(t => t.Id == d.TourId);
                AllDepartures = new ObservableCollection<Departure>(deps);

                // Create display info for available departures
                var displayDeps = deps
                    .Where(d => d.Status == "Mở bán" && d.AvailableSlots > 0 && IsDepartureStillBookable(d))
                    .OrderBy(d => d.StartDate)
                    .Select(d => {
                        var guideId = assignments.FirstOrDefault(a => a.DepartureId == d.Id)?.GuideUserId;
                        var guideName = guideId != null ? users.FirstOrDefault(u => u.Id == guideId)?.FullName : null;
                        return new DepartureDisplayInfo
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
                            DurationDays = d.Tour?.DurationDays ?? 0,
                            GuideName = string.IsNullOrWhiteSpace(guideName) ? "Chưa phân công" : guideName
                        };
                    })
                    .ToList();
                AvailableDepartures = new ObservableCollection<DepartureDisplayInfo>(displayDeps);
                ApplyDepartureFilter();

                // Load customer profile linked to current user.
                _customerProfile = await ResolveCustomerProfileAsync(client);
                AvatarUrl = _mainViewModel.CurrentUser?.AvatarUrl ?? string.Empty;

                if (_customerProfile != null)
                {
                    InfoFullName = _customerProfile.FullName;
                    InfoPhone = _customerProfile.PhoneNumber;
                    InfoEmail = _customerProfile.Email;
                    InfoAddress = _customerProfile.Address;
                }
                else
                {
                    InfoFullName = _mainViewModel.CurrentUser?.FullName ?? "";
                }

                // Load my bookings (server-side filter for performance)
                var currentUserId = _mainViewModel.CurrentUser?.Id ?? -1;
                
                var bookingsRes = await client.From<Booking>()
                    .Where(b => b.UserId == currentUserId)
                    .Order(b => b.BookingDate, Postgrest.Constants.Ordering.Descending)
                    .Get();
                var bookingsList = bookingsRes.Models ?? new List<Booking>();
                
                if (_customerProfile != null)
                {
                    var custBookingsRes = await client.From<Booking>()
                        .Where(b => b.CustomerId == _customerProfile.Id)
                        .Order(b => b.BookingDate, Postgrest.Constants.Ordering.Descending)
                        .Get();
                    
                    if (custBookingsRes.Models.Any())
                    {
                        var custBookings = custBookingsRes.Models;
                        bookingsList = bookingsList.UnionBy(custBookings, b => b.Id)
                            .OrderByDescending(b => b.BookingDate)
                            .ToList();
                    }
                }

                var bookings = bookingsList;

                // Load payments only for these bookings
                var paymentByBookingId = new Dictionary<int, Payment>();
                var ratingByBookingId = new Dictionary<int, TourRating>();
                var guideRatingByBookingId = new Dictionary<int, GuideRating>();
                RatingSchemaWarningMessage = string.Empty;
                GuideRatingSchemaWarningMessage = string.Empty;
                if (bookings.Any())
                {
                    var bookingIds = bookings.Select(b => (object)b.Id).Distinct().ToList();
                    var paymentsResponse = await client.From<Payment>()
                        .Filter("booking_id", Postgrest.Constants.Operator.In, bookingIds)
                        .Get();
                    
                    var payments = paymentsResponse.Models ?? new List<Payment>();

                    paymentByBookingId = payments
                        .GroupBy(p => p.BookingId)
                        .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Id).First());

                    try
                    {
                        var ratings = await _tourRatingService.GetByBookingIdsAsync(bookings.Select(b => b.Id));
                        ratingByBookingId = ratings
                            .GroupBy(r => r.BookingId)
                            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.UpdatedAt).First());
                    }
                    catch (Exception ex) when (TourRatingService.HasMissingRatingSchema(ex))
                    {
                        RatingSchemaWarningMessage =
                            "Tính năng đánh giá chưa sẵn sàng. Vui lòng chạy update_ratings.sql trên Supabase để bật đánh giá tour.";
                    }

                    try
                    {
                        var guideRatings = await _guideRatingService.GetByBookingIdsAsync(bookings.Select(b => b.Id));
                        guideRatingByBookingId = guideRatings
                            .GroupBy(r => r.BookingId)
                            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.UpdatedAt).First());
                    }
                    catch (Exception ex) when (GuideRatingService.HasMissingRatingSchema(ex))
                    {
                        GuideRatingSchemaWarningMessage =
                            "Tính năng đánh giá hướng dẫn viên chưa sẵn sàng. Vui lòng chạy update_ratings.sql trên Supabase để bật đánh giá HDV.";
                    }
                }

                MyBookings.Clear();
                foreach (var b in bookings)
                {
                    var dep = deps.FirstOrDefault(d => d.Id == b.DepartureId);
                    var tour = dep?.Tour;
                    var durationDays = tour?.DurationDays ?? 0;
                    var startDateFormatted = dep?.StartDate.ToString("dd/MM/yyyy") ?? "N/A";
                    var endDateFormatted = dep?.StartDate
                        .AddDays(Math.Max(durationDays - 1, 0))
                        .ToString("dd/MM/yyyy") ?? "N/A";
                    var guideId = dep != null ? assignments.FirstOrDefault(a => a.DepartureId == dep.Id)?.GuideUserId : null;
                    var guideName = guideId != null ? users.FirstOrDefault(u => u.Id == guideId)?.FullName : null;
                    
                    paymentByBookingId.TryGetValue(b.Id, out var payment);
                    var basePricePerGuest = tour?.BasePrice ?? 0;
                    var fallbackTotalAmount = basePricePerGuest * b.GuestCount;
                    var paidTotalAmount = payment?.TotalAmount ?? fallbackTotalAmount;
                    var price = b.GuestCount > 0 ? paidTotalAmount / b.GuestCount : paidTotalAmount;
                    var isCompletedBooking = IsCompletedBooking(dep?.StartDate, durationDays);
                    var cancelDisabledReason = GetCancelDisabledReason(b, dep);
                    ratingByBookingId.TryGetValue(b.Id, out var rating);
                    guideRatingByBookingId.TryGetValue(b.Id, out var guideRating);
                    var ratingDisabledReason = GetRatingDisabledReason(b, dep, durationDays);
                    var guideRatingDisabledReason = GetGuideRatingDisabledReason(b, dep, guideId, durationDays);
                    var combinedRatingDisabledReason = GetCombinedRatingDisabledReason(
                        rating != null,
                        guideRating != null,
                        ratingDisabledReason,
                        guideRatingDisabledReason);

                    MyBookings.Add(new BookingDisplayInfo
                    {
                        Booking = b,
                        TourName = tour?.Name ?? "Khách lẻ (N/A)",
                        Destination = tour?.Destination ?? "N/A",
                        DepartureDate = dep?.StartDate.ToString("dd/MM/yyyy") ?? "N/A",
                        StartDateFormatted = startDateFormatted,
                        EndDateFormatted = endDateFormatted,
                        DurationDays = durationDays,
                        GuideName = string.IsNullOrWhiteSpace(guideName) ? "Chưa phân công" : guideName,
                        Price = price,
                        PriceFormatted = $"{price:N0} đ",
                        AvailableSlots = dep?.AvailableSlots ?? 0,
                        DepartureStartDate = dep?.StartDate,
                        BookingDateFormatted = b.BookingDate.ToString("dd/MM/yyyy HH:mm"),
                        GuestCount = b.GuestCount,
                        Status = b.Status,
                        StatusColor = GetStatusColor(b.Status, isCompletedBooking),
                        ShowCancelButton = !CancelledBookingStatuses.Contains(b.Status),
                        CanCancel = string.IsNullOrEmpty(cancelDisabledReason),
                        CancelDisabledReason = cancelDisabledReason,
                        GuideUserId = guideId,
                        Rating = rating,
                        CanRate = string.IsNullOrEmpty(combinedRatingDisabledReason),
                        RatingDisabledReason = combinedRatingDisabledReason,
                        GuideRating = guideRating,
                        TourRatingDisabledReason = ratingDisabledReason,
                        GuideRatingDisabledReason = guideRatingDisabledReason
                    });
                }

                TotalBookingsCount = MyBookings.Count;
                PendingBookingsCount = MyBookings.Count(b => b.Status == "Chờ thanh toán" || b.Status == "Chờ xác nhận" || b.Status == "Đợi xác nhận");
                ConfirmedBookingsCount = MyBookings.Count(b => b.Status == "Đã xác nhận");
                TotalToursAvailable = tours.Count;
            }
            catch (Exception ex)
            {
                ShowAppDialogInfo("Lỗi", $"Không thể tải dữ liệu: {ex.Message}");
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
            PaymentOriginalAmountFormatted = "0 đ";
            PaymentDiscountAmountFormatted = "0 đ";
            PaymentTotalFormatted = "0 đ";
            ClearPromoCodeStatus(clearCodeInput: true);
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
            ClearPromoCodeStatus(clearCodeInput: true);
        }

        [RelayCommand]
        private async Task CancelMyBookingAsync(BookingDisplayInfo? bookingInfo)
        {
            if (bookingInfo?.Booking == null)
            {
                return;
            }

            if (!bookingInfo.CanCancel)
            {
                ShowAppDialogInfo(
                    "Không thể hủy tour",
                    string.IsNullOrWhiteSpace(bookingInfo.CancelDisabledReason)
                        ? "Booking hiện không thể hủy."
                        : bookingInfo.CancelDisabledReason);
                return;
            }

            var confirmCancel = await ShowAppDialogConfirmationAsync(
                "Xác nhận hủy tour",
                "Bạn có chắc chắn muốn hủy tour này?\nNếu đã thanh toán, hệ thống sẽ hoàn tiền và gửi thông báo cho bạn.",
                confirmText: "Xác nhận hủy",
                cancelText: "Giữ booking");

            if (!confirmCancel)
            {
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                var bookingResp = await client.From<Booking>().Where(b => b.Id == bookingInfo.Booking.Id).Get();
                var booking = bookingResp.Models.FirstOrDefault();
                if (booking == null)
                {
                    ShowAppDialogInfo("Lỗi", "Không tìm thấy booking.");
                    return;
                }

                if (IsCancelledBookingStatus(booking.Status))
                {
                    ShowAppDialogInfo("Thông báo", "Booking đã được hủy trước đó.");
                    await LoadDataAsync();
                    return;
                }

                var depResp = await client.From<Departure>().Where(d => d.Id == booking.DepartureId).Get();
                var departure = depResp.Models.FirstOrDefault();
                var cancelDisabledReason = GetCancelDisabledReason(booking, departure);
                if (!string.IsNullOrWhiteSpace(cancelDisabledReason))
                {
                    ShowAppDialogInfo("Không thể hủy tour", cancelDisabledReason);
                    await LoadDataAsync();
                    return;
                }

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

                var paymentResp = await client.From<Payment>().Where(p => p.BookingId == booking.Id).Get();
                var payment = paymentResp.Models.FirstOrDefault();
                if (payment != null)
                {
                    var previousStatus = payment.Status ?? string.Empty;
                    var shouldIssueRefund =
                        payment.PaidAmount > 0 ||
                        string.Equals(previousStatus, "Đợi xác nhận", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(previousStatus, "Đã cọc", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(previousStatus, "Đã thanh toán", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(previousStatus, "Đã thanh toán đủ", StringComparison.OrdinalIgnoreCase);

                    if (shouldIssueRefund)
                    {
                        payment.Status = "Đã hoàn tiền";
                        payment.PaidAmount = 0;
                        payment.PaymentDate = DateTime.Now;
                        payment.Booking = null;
                        await client.From<Payment>().Update(payment);
                        await _mainViewModel.NotificationCenter.NotifyPaymentStatusChangedAsync(
                            payment,
                            previousStatus,
                            _mainViewModel.CurrentUser?.Id);
                    }
                }

                ShowAppDialogInfo(
                    "Đã hủy tour",
                    "Booking đã được hủy thành công. Nếu booking đã thanh toán, thông báo hoàn tiền đã được gửi.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowAppDialogInfo("Lỗi", $"Không thể hủy booking: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ShowRatingForm(BookingDisplayInfo? bookingInfo)
        {
            if (bookingInfo?.Booking == null)
            {
                return;
            }

            if (HasRatingSchemaWarning)
            {
                ShowAppDialogInfo("Thiếu cấu hình", RatingSchemaWarningSummary);
                return;
            }

            if (!bookingInfo.CanRate && !bookingInfo.HasBothRatings)
            {
                ShowAppDialogInfo(
                    "Chưa thể đánh giá",
                    string.IsNullOrWhiteSpace(bookingInfo.RatingDisabledReason)
                        ? "Booking này hiện chưa đủ điều kiện để đánh giá tour và hướng dẫn viên."
                        : bookingInfo.RatingDisabledReason);
                return;
            }

            SelectedRatingBooking = bookingInfo;
            FormTourRatingValue = bookingInfo.Rating?.RatingValue ?? 5;
            FormTourRatingComment = bookingInfo.Rating?.Comment ?? string.Empty;
            FormGuideRatingValue = bookingInfo.GuideRating?.RatingValue ?? 5;
            FormGuideRatingComment = bookingInfo.GuideRating?.Comment ?? string.Empty;
            IsRatingFormVisible = true;
            SelectedPage = "MyBookings";
        }

        [RelayCommand]
        private void CancelRatingForm()
        {
            IsRatingFormVisible = false;
            SelectedRatingBooking = null;
            FormTourRatingValue = 5;
            FormTourRatingComment = string.Empty;
            FormGuideRatingValue = 5;
            FormGuideRatingComment = string.Empty;
        }

        [RelayCommand]
        private async Task SaveRatingAsync()
        {
            if (_customerProfile == null)
            {
                ShowAppDialogInfo("Thiếu thông tin", "Vui lòng cập nhật hồ sơ khách hàng trước khi gửi đánh giá.");
                return;
            }

            if (SelectedRatingBooking?.Booking == null)
            {
                ShowAppDialogInfo("Thiếu dữ liệu", "Không tìm thấy booking cần đánh giá.");
                return;
            }

            if (HasRatingSchemaWarning)
            {
                ShowAppDialogInfo("Thiếu cấu hình", RatingSchemaWarningSummary);
                return;
            }

            if (!SelectedRatingBooking.CanRate && !SelectedRatingBooking.HasBothRatings)
            {
                ShowAppDialogInfo(
                    "Chưa thể đánh giá",
                    string.IsNullOrWhiteSpace(SelectedRatingBooking.RatingDisabledReason)
                        ? "Booking này hiện chưa đủ điều kiện để đánh giá tour và hướng dẫn viên."
                        : SelectedRatingBooking.RatingDisabledReason);
                return;
            }

            if (string.IsNullOrWhiteSpace(FormTourRatingComment) || string.IsNullOrWhiteSpace(FormGuideRatingComment))
            {
                ShowAppDialogInfo("Thiếu nhận xét", "Vui lòng nhập đầy đủ nhận xét cho cả tour và hướng dẫn viên.");
                return;
            }

            if (!SelectedRatingBooking.GuideUserId.HasValue || SelectedRatingBooking.GuideUserId.Value <= 0)
            {
                ShowAppDialogInfo("Thiếu dữ liệu", "Booking này chưa có hướng dẫn viên để đánh giá.");
                return;
            }

            try
            {
                await _tourRatingService.SaveCustomerRatingAsync(new TourRatingInput(
                    SelectedRatingBooking.Booking.Id,
                    _customerProfile.Id,
                    Math.Clamp(FormTourRatingValue, 1, 5),
                    FormTourRatingComment));

                await _guideRatingService.SaveCustomerRatingAsync(new GuideRatingInput(
                    SelectedRatingBooking.Booking.Id,
                    _customerProfile.Id,
                    SelectedRatingBooking.GuideUserId.Value,
                    Math.Clamp(FormGuideRatingValue, 1, 5),
                    FormGuideRatingComment));

                CancelRatingForm();
                await LoadDataAsync();
                SelectedPage = "MyBookings";

                ShowAppDialogInfo(
                    "Đã ghi nhận đánh giá",
                    "Đánh giá tour và hướng dẫn viên của bạn đã được lưu thành công và đang chờ Admin kiểm duyệt.");
            }
            catch (Exception ex)
            {
                ShowAppDialogInfo("Lỗi", $"Không thể gửi đánh giá: {ex.Message}");
            }
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
        private async Task CheckPromoCodeAsync()
        {
            if (!TryValidateBookingInput(out var guests) || SelectedDepartureInfo == null)
            {
                return;
            }

            var tour = await ResolveSelectedTourAsync(SelectedDepartureInfo.Departure.TourId);
            if (tour == null)
            {
                ShowAppDialogInfo("Lỗi", "Không tìm thấy thông tin tour để kiểm tra mã.");
                return;
            }

            var originalAmount = tour.BasePrice * guests;
            var promoResult = await ValidatePromoCodeForOrderAsync(tour, originalAmount, showDialogOnFailure: true);
            UpdatePaymentSummary(originalAmount, promoResult, SelectedDepartureInfo.Departure.Id, guests);
        }

        [RelayCommand]
        private async Task ProceedToPaymentAsync()
        {
            if (!TryValidateBookingInput(out var guests) || SelectedDepartureInfo == null)
            {
                return;
            }

            var tour = await ResolveSelectedTourAsync(SelectedDepartureInfo.Departure.TourId);
            if (tour == null)
            {
                ShowAppDialogInfo("Lỗi", "Không tìm thấy thông tin tour.");
                return;
            }

            var total = tour.BasePrice * guests;
            var promoResult = await ValidatePromoCodeForOrderAsync(tour, total, showDialogOnFailure: true);
            if (!string.IsNullOrWhiteSpace(PromoCodeInput) && (promoResult == null || !promoResult.IsValid))
            {
                return;
            }

            PaymentTourName = SelectedDepartureInfo.TourName;
            PaymentScheduleText = $"{SelectedDepartureInfo.StartDateFormatted} - {SelectedDepartureInfo.EndDateFormatted}";
            UpdatePaymentSummary(total, promoResult, SelectedDepartureInfo.Departure.Id, guests);
            IsPaymentModalVisible = true;
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
                var success = await CreateBookingAndPaymentAsync(guests);
                if (!success) return;

                IsPaymentModalVisible = false;
                IsBookingFormVisible = false;

                ShowAppDialogInfo(
                    "Đã gửi thanh toán",
                    $"🎉 Đã ghi nhận yêu cầu thanh toán.\n\n" +
                    $"Tour: {PaymentTourName}\n" +
                    $"Lịch đi: {PaymentScheduleText}\n" +
                    $"Số khách: {PaymentGuestText}\n" +
                    $"Tạm tính: {PaymentOriginalAmountFormatted}\n" +
                    $"Giảm giá: {PaymentDiscountAmountFormatted}\n" +
                    $"Tổng thanh toán: {PaymentTotalFormatted}\n\n" +
                    "Trạng thái hiện tại: Đợi xác nhận.\n" +
                    "Admin sẽ xác nhận và gửi thông báo cho bạn.");

                await LoadDataAsync();
                SelectedPage = "MyBookings";
                ClearPromoCodeStatus(clearCodeInput: true);
            }
            finally
            {
                IsProcessingPayment = false;
            }
        }

        private async Task<Tour?> ResolveSelectedTourAsync(int tourId)
        {
            var inMemoryTour = SelectedDepartureInfo?.Departure?.Tour;
            if (inMemoryTour != null && inMemoryTour.Id == tourId)
            {
                return inMemoryTour;
            }

            var client = await SupabaseClientFactory.GetClientAsync();
            return (await client.From<Tour>().Where(t => t.Id == tourId).Get()).Models.FirstOrDefault();
        }

        private async Task<PromoValidationResult?> ValidatePromoCodeForOrderAsync(
            Tour tour,
            decimal orderAmount,
            bool showDialogOnFailure)
        {
            if (string.IsNullOrWhiteSpace(PromoCodeInput))
            {
                ClearPromoCodeStatus(clearCodeInput: false);
                return null;
            }

            var client = await SupabaseClientFactory.GetClientAsync();
            var result = await _promoCodeService.ValidateAsync(client, PromoCodeInput, new PromoValidationContext
            {
                CustomerId = _customerProfile?.Id ?? 0,
                UserId = _mainViewModel.CurrentUser?.Id,
                TourId = tour.Id,
                TourType = tour.TourType,
                OrderAmount = orderAmount,
                Now = DateTime.Now
            });

            if (result.IsValid)
            {
                AppliedPromoCode = result.NormalizedCode;
                IsPromoCodeStatusSuccess = true;
                PromoCodeStatusMessage = $"Áp dụng thành công mã {result.NormalizedCode}. Giảm {result.DiscountAmount:N0} đ.";
                PromoCodeInput = result.NormalizedCode;
                return result;
            }

            AppliedPromoCode = string.Empty;
            IsPromoCodeStatusSuccess = false;
            PromoCodeStatusMessage = result.Reason;

            if (showDialogOnFailure)
            {
                ShowAppDialogInfo("Mã giảm giá không hợp lệ", result.Reason);
            }

            return result;
        }

        private void UpdatePaymentSummary(decimal originalAmount, PromoValidationResult? promoResult, int departureId, int guests)
        {
            var discountAmount = promoResult != null && promoResult.IsValid ? promoResult.DiscountAmount : 0;
            var finalAmount = Math.Max(originalAmount - discountAmount, 0);

            PaymentGuestText = $"{guests} khách";
            PaymentOriginalAmountFormatted = $"{originalAmount:N0} đ";
            PaymentDiscountAmountFormatted = $"-{discountAmount:N0} đ";
            PaymentTotalFormatted = $"{finalAmount:N0} đ";

            var qrPayload = $"VIETTRAVEL|DEP:{departureId}|GUEST:{guests}|AMT:{finalAmount:0}|{DateTime.Now:yyyyMMddHHmmss}";
            PaymentQrImageUrl = $"https://quickchart.io/qr?size=280&text={Uri.EscapeDataString(qrPayload)}";
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

            if (!TryValidateProfileInput())
            {
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

        private bool TryValidateProfileInput()
        {
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
            if (fullNameParts.Length < 2 || fullNameParts.Any(p => p.Length < 2))
            {
                ShowAppDialogInfo("Họ tên chưa hợp lệ", "Vui lòng nhập họ tên đầy đủ (ít nhất 2 từ, mỗi từ tối thiểu 2 ký tự).");
                return false;
            }

            if (!FullNamePattern.IsMatch(InfoFullName))
            {
                ShowAppDialogInfo("Họ tên chưa hợp lệ", "Họ tên chỉ được chứa chữ cái, khoảng trắng, dấu nháy hoặc gạch nối.");
                return false;
            }

            if (!VietnamMobilePattern.IsMatch(InfoPhone))
            {
                ShowAppDialogInfo("Số điện thoại chưa hợp lệ", "Số điện thoại phải là số di động Việt Nam hợp lệ (vd: 09xxxxxxxx).");
                return false;
            }

            if (InfoEmail.Length > 120 ||
                !EmailPattern.IsMatch(InfoEmail) ||
                InfoEmail.Contains("..", StringComparison.Ordinal))
            {
                ShowAppDialogInfo("Email chưa hợp lệ", "Vui lòng nhập đúng định dạng email (vd: ten@email.com).");
                return false;
            }

            if (InfoAddress.Length < 12 || InfoAddress.Length > 200)
            {
                ShowAppDialogInfo("Địa chỉ chưa hợp lệ", "Địa chỉ phải từ 12 đến 200 ký tự.");
                return false;
            }

            if (!InfoAddress.Any(char.IsLetter) || !InfoAddress.Any(char.IsDigit))
            {
                ShowAppDialogInfo("Địa chỉ chưa hợp lệ", "Địa chỉ cần có cả chữ và số (ví dụ số nhà/tên đường).");
                return false;
            }

            if (!AddressPattern.IsMatch(InfoAddress))
            {
                ShowAppDialogInfo("Địa chỉ chưa hợp lệ", "Địa chỉ chứa ký tự không hợp lệ.");
                return false;
            }

            return true;
        }

        private void ClearPromoCodeStatus(bool clearCodeInput)
        {
            if (clearCodeInput)
            {
                PromoCodeInput = string.Empty;
            }

            PromoCodeStatusMessage = string.Empty;
            IsPromoCodeStatusSuccess = false;
            AppliedPromoCode = string.Empty;
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

        private static bool IsCancelledBookingStatus(string status)
        {
            return CancelledBookingStatuses.Any(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsDepartureStillBookable(Departure departure)
        {
            // Booking closes from 1 day before departure date.
            return departure.StartDate.Date > DateTime.Today.AddDays(1);
        }

        private static string GetCancelDisabledReason(Booking booking, Departure? departure)
        {
            if (IsCancelledBookingStatus(booking.Status))
            {
                return "Booking đã hủy nên không thể thao tác thêm.";
            }

            if (departure == null)
            {
                return "Không thể kiểm tra lịch khởi hành của booking này.";
            }

            if (departure.StartDate.Date <= DateTime.Today.AddDays(1))
            {
                return "Không thể hủy tour trong vòng 1 ngày trước ngày khởi hành.";
            }

            return string.Empty;
        }

        private static string GetRatingDisabledReason(Booking booking, Departure? departure, int durationDays)
        {
            if (IsCancelledBookingStatus(booking.Status))
            {
                return "Booking đã hủy nên không thể gửi đánh giá.";
            }

            if (!string.Equals(booking.Status, "Đã xác nhận", StringComparison.OrdinalIgnoreCase))
            {
                return "Chỉ booking đã xác nhận mới có thể đánh giá tour.";
            }

            if (departure == null)
            {
                return "Không thể kiểm tra lịch khởi hành cho booking này.";
            }

            if (!IsTourEndedForRating(departure, durationDays))
            {
                return "Chỉ có thể đánh giá sau khi tour kết thúc.";
            }

            return string.Empty;
        }

        private static string GetGuideRatingDisabledReason(Booking booking, Departure? departure, int? guideUserId, int durationDays)
        {
            if (IsCancelledBookingStatus(booking.Status))
            {
                return "Booking đã hủy nên không thể gửi đánh giá hướng dẫn viên.";
            }

            if (!string.Equals(booking.Status, "Đã xác nhận", StringComparison.OrdinalIgnoreCase))
            {
                return "Chỉ booking đã xác nhận mới có thể đánh giá hướng dẫn viên.";
            }

            if (departure == null)
            {
                return "Không thể kiểm tra lịch khởi hành cho booking này.";
            }

            if (!IsTourEndedForRating(departure, durationDays))
            {
                return "Chỉ có thể đánh giá sau khi tour kết thúc.";
            }

            if (!guideUserId.HasValue || guideUserId.Value <= 0)
            {
                return "Booking này chưa có hướng dẫn viên để đánh giá.";
            }

            return string.Empty;
        }

        private static bool IsTourEndedForRating(Departure departure, int durationDays)
        {
            var normalizedDuration = Math.Max(durationDays, 1);
            var endDate = departure.StartDate.Date.AddDays(normalizedDuration - 1);
            return endDate < DateTime.Today;
        }

        private static string GetCombinedRatingDisabledReason(
            bool hasTourRating,
            bool hasGuideRating,
            string tourRatingDisabledReason,
            string guideRatingDisabledReason)
        {
            if (hasTourRating && hasGuideRating)
            {
                return string.Empty;
            }

            var missingTourReason = !hasTourRating ? tourRatingDisabledReason : string.Empty;
            var missingGuideReason = !hasGuideRating ? guideRatingDisabledReason : string.Empty;

            if (string.IsNullOrWhiteSpace(missingTourReason))
            {
                return missingGuideReason;
            }

            if (string.IsNullOrWhiteSpace(missingGuideReason))
            {
                return missingTourReason;
            }

            if (string.Equals(missingTourReason, missingGuideReason, StringComparison.Ordinal))
            {
                return missingTourReason;
            }

            return $"{missingTourReason} {missingGuideReason}";
        }

        private static string GetStatusColor(string status, bool isCompletedBooking)
        {
            if (isCompletedBooking && string.Equals(status, "Đã xác nhận", StringComparison.OrdinalIgnoreCase))
            {
                return "#8E8E93";
            }

            return (status ?? "").Trim() switch
            {
                "Đã xác nhận" => "#34C759",
                "Đợi xác nhận" => "#5AC8FA",
                "Chờ xử lý" => "#5AC8FA",
                "Đã hủy" => "#FF3B30",
                "Hủy" => "#FF3B30",
                "Chờ thanh toán" => "#FF9500",
                _ => "#8E8E93" // Default gray
            };
        }

        private static bool IsCompletedBooking(DateTime? departureStartDate, int durationDays)
        {
            if (!departureStartDate.HasValue)
            {
                return false;
            }

            var normalizedDuration = Math.Max(durationDays, 1);
            var endDate = departureStartDate.Value.Date.AddDays(normalizedDuration - 1);
            return endDate <= DateTime.Today;
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

        private async Task<bool> CreateBookingAndPaymentAsync(int guests)
        {
            if (SelectedDepartureInfo == null) return false;

            Departure? latestDeparture = null;
            int originalAvailableSlots = 0;
            string originalDepartureStatus = string.Empty;
            bool hasReservedSlots = false;
            int createdBookingId = 0;
            PromoValidationResult? promoValidation = null;
            decimal originalAmount = 0;
            decimal discountAmount = 0;
            decimal finalAmount = 0;

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

                var depResp = await client.From<Departure>().Where(d => d.Id == SelectedDepartureInfo.Departure.Id).Get();
                latestDeparture = depResp.Models.FirstOrDefault();
                if (latestDeparture == null)
                {
                    ShowAppDialogInfo("Lỗi", "Không tìm thấy lịch khởi hành.");
                    return false;
                }
                if (!IsDepartureStillBookable(latestDeparture))
                {
                    ShowAppDialogInfo("Thông báo", "Lịch khởi hành đã bị khóa đặt vé (từ 1 ngày trước ngày khởi hành).");
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

                var tourResp = await client.From<Tour>().Where(t => t.Id == latestDeparture.TourId).Get();
                var tour = tourResp.Models.FirstOrDefault();
                if (tour == null)
                {
                    ShowAppDialogInfo("Lỗi", "Không tìm thấy thông tin tour.");
                    return false;
                }

                var scheduleConflictType = DepartureDateConflictType.None;
                foreach (var b in MyBookings)
                {
                    if (b.Status != "Đã hủy" && b.Status != "Hủy" && b.DepartureStartDate?.Date == latestDeparture.StartDate.Date)
                    {
                        if (IsSameDestination(b.Destination, tour.Destination))
                        {
                            scheduleConflictType = DepartureDateConflictType.SameDestination;
                        }
                        else
                        {
                            scheduleConflictType = DepartureDateConflictType.DifferentDestination;
                            break;
                        }
                    }
                }

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

                originalAmount = tour.BasePrice * guests;
                if (!string.IsNullOrWhiteSpace(PromoCodeInput))
                {
                    promoValidation = await _promoCodeService.ValidateAsync(client, PromoCodeInput, new PromoValidationContext
                    {
                        CustomerId = _customerProfile.Id,
                        UserId = _mainViewModel.CurrentUser?.Id,
                        TourId = tour.Id,
                        TourType = tour.TourType,
                        OrderAmount = originalAmount,
                        Now = DateTime.Now
                    });

                    if (!promoValidation.IsValid)
                    {
                        IsPromoCodeStatusSuccess = false;
                        PromoCodeStatusMessage = promoValidation.Reason;
                        ShowAppDialogInfo("Mã giảm giá không hợp lệ", promoValidation.Reason);
                        return false;
                    }

                    IsPromoCodeStatusSuccess = true;
                    PromoCodeStatusMessage = $"Áp dụng thành công mã {promoValidation.NormalizedCode}. Giảm {promoValidation.DiscountAmount:N0} đ.";
                    AppliedPromoCode = promoValidation.NormalizedCode;
                }

                discountAmount = promoValidation?.DiscountAmount ?? 0;
                finalAmount = Math.Max(originalAmount - discountAmount, 0);
                UpdatePaymentSummary(originalAmount, promoValidation, latestDeparture.Id, guests);

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
                    OriginalAmount = originalAmount,
                    DiscountAmount = discountAmount,
                    TotalAmount = finalAmount,
                    PaidAmount = finalAmount,
                    PromoCodeId = promoValidation?.PromoCode?.Id,
                    PromoCode = promoValidation?.NormalizedCode ?? string.Empty,
                    Status = "Đã thanh toán đủ",
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "Chuyển khoản QR (Mock)"
                };
                await client.From<Payment>().Insert(payment);

                if (promoValidation != null && promoValidation.IsValid)
                {
                    await _promoCodeService.RecordUsageAsync(
                        client,
                        promoValidation,
                        createdBooking.Id,
                        _customerProfile.Id,
                        _mainViewModel.CurrentUser?.Id,
                        originalAmount,
                        finalAmount);
                }

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



        private async Task<Customer?> ResolveCustomerProfileAsync(Supabase.Client client)
        {
            var currentUser = _mainViewModel.CurrentUser;
            if (currentUser == null)
            {
                return null;
            }

            var currentFullName = (currentUser.FullName ?? string.Empty).Trim();
            var currentUsername = (currentUser.Username ?? string.Empty).Trim();

            // 1. Try to resolve via booking history (most reliable link for active customers)
            try
            {
                var bookingsResponse = await client.From<Booking>()
                    .Where(b => b.UserId == currentUser.Id)
                    .Order(b => b.BookingDate, Postgrest.Constants.Ordering.Descending)
                    .Limit(1)
                    .Get();

                if (bookingsResponse.Models.Any())
                {
                    var lastBooking = bookingsResponse.Models.First();
                    var customerResponse = await client.From<Customer>()
                        .Where(c => c.Id == lastBooking.CustomerId)
                        .Get();

                    if (customerResponse.Models.Any())
                    {
                        return customerResponse.Models.First();
                    }
                }
            }
            catch { /* Ignore and fallback */ }

            // 2. Try to match by FullName (Standard for new/converted accounts)
            if (!string.IsNullOrWhiteSpace(currentFullName))
            {
                var nameResponse = await client.From<Customer>()
                    .Where(c => c.FullName == currentFullName)
                    .Get();

                if (nameResponse.Models.Any())
                {
                    return nameResponse.Models.First();
                }
            }

            // 3. Fallback for accounts where username is email
            if (!string.IsNullOrWhiteSpace(currentUsername) && currentUsername.Contains("@", StringComparison.Ordinal))
            {
                var emailResponse = await client.From<Customer>()
                    .Where(c => c.Email == currentUsername)
                    .Get();

                if (emailResponse.Models.Any())
                {
                    return emailResponse.Models.First();
                }
            }

            return null;
        }

        private async Task SyncCurrentUserProfileAsync(Supabase.Client client)
        {
            var currentUser = _mainViewModel.CurrentUser;
            if (currentUser == null)
            {
                return;
            }

            currentUser.FullName = InfoFullName;
            currentUser.AvatarUrl = AvatarUrl;
            await client.From<User>().Update(currentUser);
            OnPropertyChanged(nameof(FullName));
            OnPropertyChanged(nameof(UserInitials));
            OnPropertyChanged(nameof(AvatarUrl));
            OnPropertyChanged(nameof(HasAvatar));
        }

        [RelayCommand]
        private async Task SaveProfileAsync()
        {
            NormalizeBookingInputValues();
            if (!TryValidateProfileInput())
            {
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

        [RelayCommand(CanExecute = nameof(CanChangeAvatar))]
        private async Task ChangeAvatarAsync()
        {
            var currentUser = _mainViewModel.CurrentUser;
            if (currentUser == null)
            {
                ShowAppDialogInfo("Thông báo", "Không tìm thấy thông tin người dùng hiện tại.");
                return;
            }

            var fileDialog = new OpenFileDialog
            {
                Title = "Chọn ảnh đại diện",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp",
                CheckFileExists = true,
                Multiselect = false
            };

            if (fileDialog.ShowDialog() != true)
            {
                return;
            }

            IsUploadingAvatar = true;
            try
            {
                var avatarUrl = await _cloudinaryImageService.UploadAvatarAsync(fileDialog.FileName, currentUser.Id);
                currentUser.AvatarUrl = avatarUrl;
                AvatarUrl = avatarUrl;

                var client = await SupabaseClientFactory.GetClientAsync();
                await client.From<User>().Update(currentUser);
                ShowAppDialogInfo("Thành công", "Đã cập nhật ảnh đại diện.");
            }
            catch (Exception ex)
            {
                ShowAppDialogInfo("Lỗi", $"Không thể đổi ảnh đại diện: {ex.Message}");
            }
            finally
            {
                IsUploadingAvatar = false;
            }
        }

        private bool CanChangeAvatar()
        {
            return !IsUploadingAvatar;
        }

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand]
        public void Logout()
        {
            PropertyChangedEventManager.RemoveHandler(_mainViewModel, MainViewModelOnPropertyChanged, nameof(MainViewModel.IsDebugMenuVisible));
            PropertyChangedEventManager.RemoveHandler(_mainViewModel.NotificationCenter, NotificationCenterOnPropertyChanged, nameof(_mainViewModel.NotificationCenter.UnreadCount));
            CollectionChangedEventManager.RemoveHandler(AccountNotifications, OnNotificationCollectionChanged);
            _mainViewModel.StopNotifications();
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

        private async Task<Dictionary<int, TourRatingSummary>> LoadApprovedTourRatingsAsync()
        {
            try
            {
                var ratings = await _tourRatingService.GetAllAsync();
                return ratings
                    .Where(x => string.Equals(x.Status, TourRatingStatuses.Approved, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(x => x.TourId)
                    .ToDictionary(
                        x => x.Key,
                        x => new TourRatingSummary(
                            Math.Round((decimal)x.Average(y => y.RatingValue), 1),
                            x.Count()));
            }
            catch (Exception ex) when (TourRatingService.HasMissingRatingSchema(ex))
            {
                return new Dictionary<int, TourRatingSummary>();
            }
        }

        private static string ResolveTourImageUrl(Tour tour, int index)
        {
            if (!string.IsNullOrWhiteSpace(tour.ImageUrl))
            {
                return tour.ImageUrl;
            }

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

        private enum DepartureDateConflictType
        {
            None = 0,
            SameDestination = 1,
            DifferentDestination = 2
        }
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
        public string GuideName { get; set; } = string.Empty;
    }

    public class BookingDisplayInfo
    {
        public Booking Booking { get; set; } = null!;
        public string TourName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string DepartureDate { get; set; } = string.Empty;
        public string StartDateFormatted { get; set; } = string.Empty;
        public string EndDateFormatted { get; set; } = string.Empty;
        public int DurationDays { get; set; }
        public string GuideName { get; set; } = string.Empty;
        public int? GuideUserId { get; set; }
        public decimal Price { get; set; }
        public string PriceFormatted { get; set; } = "0 đ";
        public int AvailableSlots { get; set; }
        public DateTime? DepartureStartDate { get; set; }
        public string BookingDateFormatted { get; set; } = string.Empty;
        public int GuestCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#FF9500";
        public bool ShowCancelButton { get; set; }
        public bool CanCancel { get; set; }
        public string CancelDisabledReason { get; set; } = string.Empty;
        public TourRating? Rating { get; set; }
        public bool HasRating => Rating != null;
        public GuideRating? GuideRating { get; set; }
        public bool HasGuideRating => GuideRating != null;
        public bool HasBothRatings => HasRating && HasGuideRating;
        public bool HasAnyRating => HasRating || HasGuideRating;
        public bool CanRate { get; set; }
        public string RatingDisabledReason { get; set; } = string.Empty;
        public string TourRatingDisabledReason { get; set; } = string.Empty;
        public string GuideRatingDisabledReason { get; set; } = string.Empty;
        public bool ShowRatingAction => HasAnyRating || CanRate;
        public string RatingActionText => "Đánh giá";
        public string RatingActionToolTip =>
            string.IsNullOrWhiteSpace(RatingDisabledReason)
                ? "Đánh giá tour và hướng dẫn viên"
                : RatingDisabledReason;
        public string RatingStarsText => Rating == null ? string.Empty : TourRatingDisplayHelper.ToStarsText(Rating.RatingValue);
        public string RatingStatusLabel => TourRatingDisplayHelper.ToStatusLabel(Rating?.Status);
        public string RatingStatusColor => TourRatingDisplayHelper.ToStatusColor(Rating?.Status);
        public string RatingCommentPreview => Rating == null
            ? string.Empty
            : TourRatingDisplayHelper.Truncate(Rating.Comment, 120);
        public bool HasAdminReply => !string.IsNullOrWhiteSpace(Rating?.AdminReply);
        public string RatingAdminReplyPreview => !HasAdminReply
            ? string.Empty
            : TourRatingDisplayHelper.Truncate(Rating?.AdminReply, 120);
        public string GuideRatingStarsText => GuideRating == null ? string.Empty : TourRatingDisplayHelper.ToStarsText(GuideRating.RatingValue);
        public string GuideRatingStatusLabel => TourRatingDisplayHelper.ToStatusLabel(GuideRating?.Status);
        public string GuideRatingStatusColor => TourRatingDisplayHelper.ToStatusColor(GuideRating?.Status);
        public string GuideRatingCommentPreview => GuideRating == null
            ? string.Empty
            : TourRatingDisplayHelper.Truncate(GuideRating.Comment, 120);
        public bool HasGuideAdminReply => !string.IsNullOrWhiteSpace(GuideRating?.AdminReply);
        public string GuideRatingAdminReplyPreview => !HasGuideAdminReply
            ? string.Empty
            : TourRatingDisplayHelper.Truncate(GuideRating?.AdminReply, 120);
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
        public decimal AverageRating { get; set; }
        public int RatingCount { get; set; }
        public bool HasRatings => RatingCount > 0;
        public int RoundedAverageRating => Math.Clamp((int)Math.Round(AverageRating, MidpointRounding.AwayFromZero), 0, 5);
        public string RatingStarsText => TourRatingDisplayHelper.ToStarsText(RoundedAverageRating);
        public string RatingSummaryText => HasRatings
            ? $"{AverageRating:0.0}/5 ({RatingCount} đánh giá)"
            : "Chưa có đánh giá";

        public List<Transport> Transports => Tour?.TourTransports?.Select(t => t.Transport!).Where(x => x != null).ToList() ?? new List<Transport>();
        public List<Hotel> Hotels => Tour?.TourHotels?.Select(t => t.Hotel!).Where(x => x != null).ToList() ?? new List<Hotel>();
        public List<Attraction> Attractions => Tour?.TourAttractions?.Select(t => t.Attraction!).Where(x => x != null).ToList() ?? new List<Attraction>();

        public bool HasTransports => Transports.Any();
        public bool HasHotels => Hotels.Any();
        public bool HasAttractions => Attractions.Any();
    }

    public readonly record struct TourRatingSummary(decimal AverageRating, int RatingCount);
}

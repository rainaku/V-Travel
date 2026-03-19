using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using VietTravel.Core.Models;
using VietTravel.Data;
using VietTravel.UI.Models;

namespace VietTravel.UI.Services
{
    public sealed partial class NotificationCenterService : ObservableObject
    {
        private static readonly Lazy<NotificationCenterService> _lazy = new(() => new NotificationCenterService());
        private static readonly string[] PaidStatuses = { "Đã cọc", "Đã thanh toán", "Đã thanh toán đủ" };
        private static readonly string[] RefundedStatuses = { "Đã hoàn tiền" };
        private static readonly string[] AdminPendingReviewStatuses = { "Đợi xác nhận" };
        private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(3);
        private const int MaxNotifications = 100;

        private readonly ObservableCollection<AppNotification> _notifications = new();
        private readonly ReadOnlyObservableCollection<AppNotification> _readonlyNotifications;
        private readonly Dictionary<int, string> _paymentStatusById = new();
        private readonly HashSet<string> _departureReminderKeys = new();
        private readonly Dictionary<string, DateTime> _recentPushByKey = new();
        private readonly DispatcherTimer _pollingTimer;

        private bool _isStarted;
        private bool _isPolling;
        private int _currentUserId;
        private string _currentRole = string.Empty;
        private bool IsCustomerRole => string.Equals(_currentRole, "Customer", StringComparison.OrdinalIgnoreCase);
        private bool IsAdminOrEmployeeRole =>
            string.Equals(_currentRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_currentRole, "Employee", StringComparison.OrdinalIgnoreCase);

        [ObservableProperty]
        private int _unreadCount;

        public static NotificationCenterService Instance => _lazy.Value;

        public ReadOnlyObservableCollection<AppNotification> Notifications => _readonlyNotifications;

        public event Action<AppNotification>? NotificationPushed;

        private NotificationCenterService()
        {
            _readonlyNotifications = new ReadOnlyObservableCollection<AppNotification>(_notifications);
            _notifications.CollectionChanged += OnNotificationsCollectionChanged;

            _pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(45)
            };
            _pollingTimer.Tick += PollingTimerOnTick;
        }

        public async Task StartAsync(User? currentUser)
        {
            if (currentUser == null)
            {
                return;
            }

            if (_isStarted && _currentUserId == currentUser.Id &&
                string.Equals(_currentRole, currentUser.Role, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Stop(clearNotifications: true);
            _currentUserId = currentUser.Id;
            _currentRole = currentUser.Role ?? string.Empty;
            _isStarted = true;

            await LoadPersistedNotificationsAsync();
            await PollAsync(isInitialSnapshot: true);
            _pollingTimer.Start();
        }

        public void Stop(bool clearNotifications)
        {
            _pollingTimer.Stop();
            _isStarted = false;
            _isPolling = false;
            _currentUserId = 0;
            _currentRole = string.Empty;
            _paymentStatusById.Clear();
            _departureReminderKeys.Clear();
            _recentPushByKey.Clear();

            if (!clearNotifications)
            {
                return;
            }

            foreach (var item in _notifications)
            {
                item.PropertyChanged -= OnNotificationPropertyChanged;
            }

            _notifications.Clear();
            UnreadCount = 0;
        }

        public async Task RefreshNowAsync()
        {
            await PollAsync(isInitialSnapshot: false);
        }

        public void MarkAsRead(AppNotification? notification)
        {
            if (notification == null || notification.IsRead)
            {
                return;
            }

            notification.IsRead = true;
            RecalculateUnreadCount();
            _ = PersistReadStateAsync(notification);
        }

        public void MarkAllAsRead()
        {
            foreach (var notification in _notifications.Where(n => !n.IsRead))
            {
                notification.IsRead = true;
                _ = PersistReadStateAsync(notification);
            }

            RecalculateUnreadCount();
        }

        public void AddDebugNotification(string title, string message, string category = "Debug")
        {
            PushNotification(
                title,
                message,
                category,
                deduplicationKey: $"debug:{Guid.NewGuid():N}");
        }

        public void ClearAllNotifications()
        {
            if (_isStarted && _currentUserId > 0)
            {
                _ = ClearPersistedNotificationsAsync();
            }

            _notifications.Clear();
            UnreadCount = 0;
        }

        public async Task NotifyPaymentStatusChangedAsync(Payment payment, string previousStatus, int? targetUserId = null)
        {
            if (payment == null)
            {
                return;
            }

            _paymentStatusById[payment.Id] = payment.Status ?? string.Empty;
            if (string.Equals(previousStatus, payment.Status, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var status = payment.Status ?? string.Empty;
            var isPaidLike = PaidStatuses.Any(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
            var isRefunded = RefundedStatuses.Any(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
            var isPendingReview = AdminPendingReviewStatuses.Any(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));

            if (IsCustomerRole)
            {
                if (isPaidLike)
                {
                    PushCustomerPaymentNotification(payment, status, isInitialSnapshot: false, isRefund: false);
                }
                else if (isRefunded)
                {
                    PushCustomerPaymentNotification(payment, status, isInitialSnapshot: false, isRefund: true);
                }

                return;
            }

            if (IsAdminOrEmployeeRole && isPendingReview)
            {
                PushAdminPaymentReviewNotification(payment, status, isInitialSnapshot: false);
            }

            if (!IsAdminOrEmployeeRole || (!isPaidLike && !isRefunded))
            {
                return;
            }

            var resolvedTargetUserId = targetUserId.GetValueOrDefault();
            if (resolvedTargetUserId <= 0)
            {
                try
                {
                    var client = await SupabaseClientFactory.GetClientAsync();
                    var bookingResponse = await client.From<Booking>().Get();
                    var booking = bookingResponse.Models.FirstOrDefault(b => b.Id == payment.BookingId);
                    resolvedTargetUserId = booking?.UserId ?? 0;
                }
                catch
                {
                    resolvedTargetUserId = 0;
                }
            }

            if (resolvedTargetUserId <= 0)
            {
                return;
            }

            var title = isRefunded ? "Hoàn tiền" : "Xác nhận thanh toán";
            var message = isRefunded
                ? $"Booking BK-{payment.BookingId} đã xác nhận hủy và hoàn tiền cho bạn."
                : $"Booking BK-{payment.BookingId} đã cập nhật trạng thái: {status}.";
            var category = isRefunded ? "Hoàn tiền" : "Thanh toán";
            var deduplicationKey = $"payment:{payment.Id}:{status}";

            await PushNotificationToUserAsync(
                resolvedTargetUserId,
                title,
                message,
                category,
                deduplicationKey);
        }

        private async void PollingTimerOnTick(object? sender, EventArgs e)
        {
            await PollAsync(isInitialSnapshot: false);
        }

        private async Task PollAsync(bool isInitialSnapshot)
        {
            if (!_isStarted || _isPolling)
            {
                return;
            }

            _isPolling = true;
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                var bookingResponse = await client.From<Booking>().Get();
                var bookings = bookingResponse.Models.ToList();

                var paymentResponse = await client.From<Payment>().Get();
                var payments = paymentResponse.Models.ToList();

                var departureResponse = await client.From<Departure>().Get();
                var departures = departureResponse.Models.ToList();

                var tourResponse = await client.From<Tour>().Get();
                var tours = tourResponse.Models.ToList();
                var tourById = tours.ToDictionary(t => t.Id);

                var scopedBookings = GetScopedBookings(bookings);
                var bookingById = scopedBookings.ToDictionary(b => b.Id);
                var scopedPayments = payments.Where(p => bookingById.ContainsKey(p.BookingId)).ToList();

                DetectPaymentStatusChanges(scopedPayments, isInitialSnapshot);
                DetectDepartureReminders(scopedBookings, departures, tourById);
            }
            catch
            {
                // Notifications should never block the app flow.
            }
            finally
            {
                _isPolling = false;
            }
        }

        private List<Booking> GetScopedBookings(List<Booking> allBookings)
        {
            if (string.Equals(_currentRole, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                return allBookings.Where(b => b.UserId == _currentUserId).ToList();
            }

            return allBookings;
        }

        private void DetectPaymentStatusChanges(List<Payment> scopedPayments, bool isInitialSnapshot)
        {
            var currentIds = scopedPayments.Select(p => p.Id).ToHashSet();
            foreach (var staleId in _paymentStatusById.Keys.Where(id => !currentIds.Contains(id)).ToList())
            {
                _paymentStatusById.Remove(staleId);
            }

            foreach (var payment in scopedPayments)
            {
                var currentStatus = payment.Status ?? string.Empty;
                var isPaidLike = PaidStatuses.Any(s => string.Equals(s, currentStatus, StringComparison.OrdinalIgnoreCase));
                var isRefunded = RefundedStatuses.Any(s => string.Equals(s, currentStatus, StringComparison.OrdinalIgnoreCase));
                var isPendingReview = AdminPendingReviewStatuses.Any(s => string.Equals(s, currentStatus, StringComparison.OrdinalIgnoreCase));

                if (_paymentStatusById.TryGetValue(payment.Id, out var oldStatus))
                {
                    if (string.Equals(oldStatus, currentStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        _paymentStatusById[payment.Id] = currentStatus;
                        continue;
                    }

                    if (IsCustomerRole)
                    {
                        if (isPaidLike)
                        {
                            PushCustomerPaymentNotification(payment, currentStatus, isInitialSnapshot, isRefund: false);
                        }
                        else if (isRefunded)
                        {
                            PushCustomerPaymentNotification(payment, currentStatus, isInitialSnapshot, isRefund: true);
                        }
                    }
                    else if (IsAdminOrEmployeeRole && isPendingReview)
                    {
                        PushAdminPaymentReviewNotification(payment, currentStatus, isInitialSnapshot);
                    }
                }
                else
                {
                    if (IsCustomerRole)
                    {
                        if (isPaidLike)
                        {
                            PushCustomerPaymentNotification(payment, currentStatus, isInitialSnapshot, isRefund: false);
                        }
                        else if (isRefunded)
                        {
                            PushCustomerPaymentNotification(payment, currentStatus, isInitialSnapshot, isRefund: true);
                        }
                    }
                    else if (IsAdminOrEmployeeRole && isPendingReview)
                    {
                        PushAdminPaymentReviewNotification(payment, currentStatus, isInitialSnapshot);
                    }
                }

                _paymentStatusById[payment.Id] = currentStatus;
            }
        }

        private void PushCustomerPaymentNotification(Payment payment, string status, bool isInitialSnapshot, bool isRefund)
        {
            if (isInitialSnapshot)
            {
                return;
            }

            if (isRefund)
            {
                PushNotification(
                    "Hoàn tiền",
                    $"Booking BK-{payment.BookingId} đã xác nhận hủy và hoàn tiền cho bạn.",
                    category: "Hoàn tiền",
                    deduplicationKey: $"payment:{payment.Id}:{status}");
                return;
            }

            PushNotification(
                "Xác nhận thanh toán",
                $"Booking BK-{payment.BookingId} đã cập nhật trạng thái: {status}.",
                category: "Thanh toán",
                deduplicationKey: $"payment:{payment.Id}:{status}");
        }

        private void PushAdminPaymentReviewNotification(Payment payment, string status, bool isInitialSnapshot)
        {
            if (isInitialSnapshot)
            {
                return;
            }

            PushNotification(
                "Yêu cầu xác nhận thanh toán",
                $"Booking BK-{payment.BookingId} đang ở trạng thái {status}.",
                category: "Duyệt thanh toán",
                deduplicationKey: $"payment:review:{payment.Id}:{status}");
        }

        private async Task PushNotificationToUserAsync(
            int userId,
            string title,
            string message,
            string category,
            string deduplicationKey)
        {
            if (userId <= 0)
            {
                return;
            }

            if (_isStarted && _currentUserId == userId)
            {
                PushNotification(title, message, category, deduplicationKey);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var existingResponse = await client
                    .From<UserNotification>()
                    .Where(n => n.UserId == userId)
                    .Where(n => n.DeduplicationKey == deduplicationKey)
                    .Get();

                if (existingResponse.Models.Any())
                {
                    return;
                }

                var record = new UserNotification
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    Category = category,
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    DeduplicationKey = deduplicationKey
                };

                await client.From<UserNotification>().Insert(record);
            }
            catch
            {
                // Cross-user notification persistence is best-effort only.
            }
        }

        private void DetectDepartureReminders(
            List<Booking> scopedBookings,
            List<Departure> allDepartures,
            Dictionary<int, Tour> tourById)
        {
            var now = DateTime.Now;
            var bookingDepartureIds = scopedBookings.Select(b => b.DepartureId).ToHashSet();

            var candidateDepartures = allDepartures
                .Where(d =>
                    d.StartDate > now &&
                    d.StartDate <= now.AddHours(24) &&
                    (string.Equals(_currentRole, "Customer", StringComparison.OrdinalIgnoreCase)
                        ? bookingDepartureIds.Contains(d.Id)
                        : true))
                .ToList();

            foreach (var departure in candidateDepartures)
            {
                var reminderKey = $"departure:{departure.Id}:{departure.StartDate:yyyyMMddHHmm}";
                if (_departureReminderKeys.Contains(reminderKey))
                {
                    continue;
                }

                var hoursLeft = Math.Max(1, (int)Math.Ceiling((departure.StartDate - now).TotalHours));
                var tourName = tourById.TryGetValue(departure.TourId, out var tour)
                    ? tour.Name
                    : $"Tour #{departure.TourId}";

                PushNotification(
                    "Nhắc lịch khởi hành",
                    $"{tourName} sẽ khởi hành sau khoảng {hoursLeft} giờ ({departure.StartDate:dd/MM/yyyy HH:mm}).",
                    category: "Khởi hành",
                    deduplicationKey: reminderKey);
                _departureReminderKeys.Add(reminderKey);
            }
        }

        private async Task LoadPersistedNotificationsAsync()
        {
            if (!_isStarted || _currentUserId <= 0)
            {
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var response = await client
                    .From<UserNotification>()
                    .Where(n => n.UserId == _currentUserId)
                    .Get();

                var persistedNotifications = response.Models
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(MaxNotifications)
                    .ToList();

                foreach (var item in _notifications)
                {
                    item.PropertyChanged -= OnNotificationPropertyChanged;
                }

                _notifications.Clear();
                foreach (var persisted in persistedNotifications)
                {
                    var appNotification = new AppNotification
                    {
                        DatabaseId = persisted.Id,
                        Title = persisted.Title,
                        Message = persisted.Message,
                        Category = persisted.Category,
                        CreatedAt = persisted.CreatedAt,
                        IsRead = persisted.IsRead,
                        DeduplicationKey = persisted.DeduplicationKey
                    };

                    _notifications.Add(appNotification);
                    if (!string.IsNullOrWhiteSpace(appNotification.DeduplicationKey))
                    {
                        _recentPushByKey[appNotification.DeduplicationKey] = appNotification.CreatedAt;
                    }
                }

                RecalculateUnreadCount();
            }
            catch
            {
                // Persisted notifications are best-effort only.
            }
        }

        private async Task PersistNotificationAsync(AppNotification notification)
        {
            if (!_isStarted || _currentUserId <= 0 || notification.DatabaseId > 0)
            {
                return;
            }

            var userId = _currentUserId;

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                if (!string.IsNullOrWhiteSpace(notification.DeduplicationKey))
                {
                    var existingResponse = await client
                        .From<UserNotification>()
                        .Where(n => n.UserId == userId)
                        .Where(n => n.DeduplicationKey == notification.DeduplicationKey)
                        .Get();

                    var existing = existingResponse.Models
                        .OrderByDescending(n => n.CreatedAt)
                        .FirstOrDefault();
                    if (existing != null)
                    {
                        notification.DatabaseId = existing.Id;
                        return;
                    }
                }

                var record = new UserNotification
                {
                    UserId = userId,
                    Title = notification.Title,
                    Message = notification.Message,
                    Category = notification.Category,
                    CreatedAt = notification.CreatedAt,
                    IsRead = notification.IsRead,
                    DeduplicationKey = notification.DeduplicationKey
                };

                var insertResponse = await client.From<UserNotification>().Insert(record);
                var created = insertResponse.Models.FirstOrDefault();
                if (created != null)
                {
                    notification.DatabaseId = created.Id;
                }
            }
            catch
            {
                // Persisted notifications are best-effort only.
            }
        }

        private async Task PersistReadStateAsync(AppNotification notification)
        {
            if (!_isStarted || _currentUserId <= 0)
            {
                return;
            }

            var userId = _currentUserId;

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                if (notification.DatabaseId > 0)
                {
                    var updateById = new UserNotification
                    {
                        Id = notification.DatabaseId,
                        UserId = userId,
                        Title = notification.Title,
                        Message = notification.Message,
                        Category = notification.Category,
                        CreatedAt = notification.CreatedAt,
                        IsRead = notification.IsRead,
                        DeduplicationKey = notification.DeduplicationKey
                    };

                    await client.From<UserNotification>().Update(updateById);
                    return;
                }

                if (string.IsNullOrWhiteSpace(notification.DeduplicationKey))
                {
                    return;
                }

                var existingResponse = await client
                    .From<UserNotification>()
                    .Where(n => n.UserId == userId)
                    .Where(n => n.DeduplicationKey == notification.DeduplicationKey)
                    .Get();

                var existing = existingResponse.Models
                    .OrderByDescending(n => n.CreatedAt)
                    .FirstOrDefault();
                if (existing == null)
                {
                    return;
                }

                existing.IsRead = notification.IsRead;
                await client.From<UserNotification>().Update(existing);
                notification.DatabaseId = existing.Id;
            }
            catch
            {
                // Persisted notifications are best-effort only.
            }
        }

        private async Task ClearPersistedNotificationsAsync()
        {
            if (!_isStarted || _currentUserId <= 0)
            {
                return;
            }

            var userId = _currentUserId;

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                await client
                    .From<UserNotification>()
                    .Where(n => n.UserId == userId)
                    .Delete();
            }
            catch
            {
                // Persisted notifications are best-effort only.
            }
        }

        private void PushNotification(string title, string message, string category, string deduplicationKey)
        {
            var now = DateTime.Now;
            PruneDeduplicationCache(now);
            if (_recentPushByKey.TryGetValue(deduplicationKey, out var previousPushAt))
            {
                if ((now - previousPushAt) < DuplicateWindow)
                {
                    return;
                }
            }

            _recentPushByKey[deduplicationKey] = now;

            var notification = new AppNotification
            {
                DatabaseId = 0,
                Title = title,
                Message = message,
                Category = category,
                CreatedAt = now,
                IsRead = false,
                DeduplicationKey = deduplicationKey
            };

            _notifications.Insert(0, notification);
            if (_notifications.Count > MaxNotifications)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }

            RecalculateUnreadCount();
            NotificationPushed?.Invoke(notification);
            _ = PersistNotificationAsync(notification);
        }

        private void PruneDeduplicationCache(DateTime now)
        {
            var staleKeys = _recentPushByKey
                .Where(kvp => now - kvp.Value > DuplicateWindow)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleKeys)
            {
                _recentPushByKey.Remove(key);
            }
        }

        private void OnNotificationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (AppNotification oldItem in e.OldItems)
                {
                    oldItem.PropertyChanged -= OnNotificationPropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (AppNotification newItem in e.NewItems)
                {
                    newItem.PropertyChanged += OnNotificationPropertyChanged;
                }
            }

            RecalculateUnreadCount();
        }

        private void OnNotificationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppNotification.IsRead))
            {
                RecalculateUnreadCount();
            }
        }

        private void RecalculateUnreadCount()
        {
            UnreadCount = _notifications.Count(n => !n.IsRead);
        }
    }
}

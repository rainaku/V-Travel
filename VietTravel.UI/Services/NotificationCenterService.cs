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
        }

        public void MarkAllAsRead()
        {
            foreach (var notification in _notifications.Where(n => !n.IsRead))
            {
                notification.IsRead = true;
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
            _notifications.Clear();
            UnreadCount = 0;
        }

        public void NotifyPaymentStatusChanged(Payment payment, string previousStatus)
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

            if (!PaidStatuses.Any(s => string.Equals(s, payment.Status, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var title = "Xác nhận thanh toán";
            var message = $"Booking BK-{payment.BookingId} đã cập nhật trạng thái: {payment.Status}.";
            PushNotification(
                title,
                message,
                category: "Thanh toán",
                deduplicationKey: $"payment:{payment.Id}:{payment.Status}");
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

                if (_paymentStatusById.TryGetValue(payment.Id, out var oldStatus))
                {
                    if (!string.Equals(oldStatus, currentStatus, StringComparison.OrdinalIgnoreCase) && isPaidLike)
                    {
                        PushNotification(
                            "Xác nhận thanh toán",
                            $"Booking BK-{payment.BookingId} đã cập nhật trạng thái: {currentStatus}.",
                            category: "Thanh toán",
                            deduplicationKey: $"payment:{payment.Id}:{currentStatus}");
                    }
                }
                else if (!isInitialSnapshot && isPaidLike)
                {
                    PushNotification(
                        "Xác nhận thanh toán",
                        $"Booking BK-{payment.BookingId} vừa được ghi nhận: {currentStatus}.",
                        category: "Thanh toán",
                        deduplicationKey: $"payment:new:{payment.Id}:{currentStatus}");
                }

                _paymentStatusById[payment.Id] = currentStatus;
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

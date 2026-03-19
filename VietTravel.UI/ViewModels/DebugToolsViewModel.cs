using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VietTravel.UI.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class DebugToolsViewModel : ObservableObject
    {
        private readonly NotificationCenterService _notificationCenter;

        [ObservableProperty]
        private bool _isRunning;

        public int NotificationCount => _notificationCenter.Notifications.Count;
        public int UnreadCount => _notificationCenter.UnreadCount;

        public DebugToolsViewModel(MainViewModel mainViewModel)
        {
            _notificationCenter = mainViewModel.NotificationCenter;
            PropertyChangedEventManager.AddHandler(_notificationCenter, NotificationCenterOnPropertyChanged, string.Empty);
            CollectionChangedEventManager.AddHandler(_notificationCenter.Notifications, NotificationsOnCollectionChanged);
        }

        [RelayCommand]
        private void SimulatePaymentConfirmation()
        {
            var bookingId = Random.Shared.Next(1000, 9999);
            _notificationCenter.AddDebugNotification(
                "Xác nhận thanh toán",
                $"(Debug) Booking BK-{bookingId} đã được xác nhận thanh toán.",
                "Thanh toán");
        }

        [RelayCommand]
        private void SimulateDepartureReminder()
        {
            var departureTime = DateTime.Now.AddHours(6);
            _notificationCenter.AddDebugNotification(
                "Nhắc lịch khởi hành",
                $"(Debug) Tour giả lập sẽ khởi hành lúc {departureTime:dd/MM/yyyy HH:mm}.",
                "Khởi hành");
        }

        [RelayCommand]
        private void SeedNotificationBatch()
        {
            for (var i = 1; i <= 5; i++)
            {
                _notificationCenter.AddDebugNotification(
                    $"Debug Notification #{i}",
                    $"Mẫu thông báo số {i} để test UI/scroll/badge.",
                    "Debug");
            }
        }

        [RelayCommand]
        private async Task ForceSyncAsync()
        {
            if (IsRunning)
            {
                return;
            }

            IsRunning = true;
            try
            {
                await _notificationCenter.RefreshNowAsync();
                _notificationCenter.AddDebugNotification(
                    "Debug Sync",
                    "Đã force đồng bộ notification từ dữ liệu thật.",
                    "Debug");
            }
            finally
            {
                IsRunning = false;
            }
        }

        [RelayCommand]
        private void MarkAllAsRead()
        {
            _notificationCenter.MarkAllAsRead();
        }

        [RelayCommand]
        private void ClearNotifications()
        {
            _notificationCenter.ClearAllNotifications();
        }

        private void NotificationCenterOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NotificationCenterService.UnreadCount))
            {
                OnPropertyChanged(nameof(UnreadCount));
            }
        }

        private void NotificationsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(NotificationCount));
        }
    }
}

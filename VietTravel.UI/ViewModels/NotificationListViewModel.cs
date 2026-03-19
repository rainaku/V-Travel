using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using VietTravel.UI.Models;
using VietTravel.UI.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class NotificationListViewModel : ObservableObject
    {
        private readonly NotificationCenterService _notificationCenter;

        [ObservableProperty]
        private bool _isRefreshing;

        public ReadOnlyObservableCollection<AppNotification> Notifications => _notificationCenter.Notifications;
        public int UnreadCount => _notificationCenter.UnreadCount;
        public bool HasNoData => Notifications.Count == 0;

        public NotificationListViewModel(MainViewModel mainViewModel)
        {
            _notificationCenter = mainViewModel.NotificationCenter;
            PropertyChangedEventManager.AddHandler(_notificationCenter, NotificationCenterOnPropertyChanged, string.Empty);
            CollectionChangedEventManager.AddHandler(Notifications, NotificationsOnCollectionChanged);
        }

        [RelayCommand]
        private void MarkAsRead(AppNotification notification)
        {
            _notificationCenter.MarkAsRead(notification);
        }

        [RelayCommand]
        private void MarkAllAsRead()
        {
            _notificationCenter.MarkAllAsRead();
        }

        [RelayCommand]
        private async Task RefreshNowAsync()
        {
            if (IsRefreshing)
            {
                return;
            }

            IsRefreshing = true;
            try
            {
                await _notificationCenter.RefreshNowAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
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
            OnPropertyChanged(nameof(HasNoData));
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VietTravel.UI.Models;
using VietTravel.UI.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class NotificationListViewModel : PaginatedListViewModelBase<AppNotification>
    {
        private readonly NotificationCenterService _notificationCenter;

        [ObservableProperty]
        private bool _isRefreshing;

        [ObservableProperty]
        private int _unreadCount;

        [ObservableProperty] private ObservableCollection<string> _readStatuses = new() { "Tất cả", "Chưa đọc", "Đã đọc" };
        [ObservableProperty] private string _selectedReadStatus = "Tất cả";
        [ObservableProperty] private ObservableCollection<AppNotification> _filteredNotifications = new();

        public ReadOnlyObservableCollection<AppNotification> Notifications => _notificationCenter.Notifications;
        public bool HasNoData => FilteredNotifications.Count == 0;

        public NotificationListViewModel(MainViewModel mainViewModel)
        {
            _notificationCenter = mainViewModel.NotificationCenter;
            UnreadCount = _notificationCenter.UnreadCount;
            PropertyChangedEventManager.AddHandler(_notificationCenter, NotificationCenterOnPropertyChanged, string.Empty);
            CollectionChangedEventManager.AddHandler(Notifications, NotificationsOnCollectionChanged);
            ApplyFilter();
        }

        partial void OnSelectedReadStatusChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            var filterStatus = SelectedReadStatus;
            
            var query = _notificationCenter.Notifications.AsEnumerable();
            if (filterStatus == "Chưa đọc")
            {
                query = query.Where(n => !n.IsRead);
            }
            else if (filterStatus == "Đã đọc")
            {
                query = query.Where(n => n.IsRead);
            }

            SetPagedItems(query.ToList(), FilteredNotifications);
            OnPropertyChanged(nameof(HasNoData));
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
                UnreadCount = _notificationCenter.UnreadCount;
                ApplyFilter();
            }
        }

        private void NotificationsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ApplyFilter();
        }
    }
}

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using VietTravel.Core.Models;
using VietTravel.UI.Models;
using VietTravel.UI.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private static readonly TimeSpan ShiftActivationWindow = TimeSpan.FromSeconds(4);
        private DateTime _lastShiftPressedAt = DateTime.MinValue;
        private int _shiftPressCount;

        [ObservableProperty]
        private ObservableObject _currentViewModel;

        [ObservableProperty]
        private bool _isDebugMenuVisible;

        [ObservableProperty]
        private User? _currentUser;
        public bool IsLoginViewActive => CurrentViewModel is LoginViewModel;
        public NotificationCenterService NotificationCenter => NotificationCenterService.Instance;
        public ISnackbarMessageQueue SnackbarMessageQueue { get; }

        public MainViewModel()
        {
            SnackbarMessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(4));
            NotificationCenter.NotificationPushed += OnNotificationPushed;

            // Bắt đầu với màn hình Login
            CurrentViewModel = new LoginViewModel(this);
        }

        public void NavigateTo(ObservableObject viewModel)
        {
            CurrentViewModel = viewModel;
        }

        public void RefreshCurrentUser()
        {
            OnPropertyChanged(nameof(CurrentUser));
        }

        partial void OnCurrentViewModelChanged(ObservableObject value)
        {
            OnPropertyChanged(nameof(IsLoginViewActive));
        }

        public async Task StartNotificationsAsync()
        {
            await NotificationCenter.StartAsync(CurrentUser);
        }

        public void StopNotifications(bool clearNotifications = true)
        {
            NotificationCenter.Stop(clearNotifications);
        }

        public void RegisterShiftPress()
        {
            var now = DateTime.Now;
            if ((now - _lastShiftPressedAt) > ShiftActivationWindow)
            {
                _shiftPressCount = 0;
            }

            _lastShiftPressedAt = now;
            _shiftPressCount++;

            if (_shiftPressCount < 5)
            {
                return;
            }

            _shiftPressCount = 0;
            if (!IsDebugMenuVisible)
            {
                IsDebugMenuVisible = true;
                SnackbarMessageQueue.Enqueue("Debug mode đã bật. Menu Debug đã xuất hiện.");
            }
        }

        private void OnNotificationPushed(AppNotification notification)
        {
            SnackbarMessageQueue.Enqueue($"{notification.Title}: {notification.Message}");
        }
    }
}

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VietTravel.UI.Models
{
    public partial class AppNotification : ObservableObject
    {
        [ObservableProperty]
        private int _databaseId;

        [ObservableProperty]
        private Guid _id = Guid.NewGuid();

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _message = string.Empty;

        [ObservableProperty]
        private string _category = "Hệ thống";

        [ObservableProperty]
        private DateTime _createdAt = DateTime.Now;

        [ObservableProperty]
        private bool _isRead = false;

        [ObservableProperty]
        private string _deduplicationKey = string.Empty;
    }
}

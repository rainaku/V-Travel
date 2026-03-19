using CommunityToolkit.Mvvm.ComponentModel;

namespace VietTravel.UI.Models
{
    public partial class UserRoleItem : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _role = string.Empty;

        [ObservableProperty]
        private bool _isActive;
    }
}

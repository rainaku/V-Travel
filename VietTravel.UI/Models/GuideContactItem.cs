using CommunityToolkit.Mvvm.ComponentModel;

namespace VietTravel.UI.Models
{
    public partial class GuideContactItem : ObservableObject
    {
        [ObservableProperty]
        private int _userId;

        [ObservableProperty]
        private int _profileId;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _phoneNumber = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _emergencyContact = string.Empty;

        [ObservableProperty]
        private string _notes = string.Empty;
    }
}

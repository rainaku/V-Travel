using CommunityToolkit.Mvvm.ComponentModel;

namespace VietTravel.UI.Models
{
    public partial class GuideScheduleItem : ObservableObject
    {
        [ObservableProperty]
        private int _assignmentId;

        [ObservableProperty]
        private int _guideUserId;

        [ObservableProperty]
        private int _departureId;

        [ObservableProperty]
        private string _guideName = string.Empty;

        [ObservableProperty]
        private string _tourName = string.Empty;

        [ObservableProperty]
        private DateTime _departureStartDate;

        [ObservableProperty]
        private DateTime _workStart;

        [ObservableProperty]
        private DateTime _workEnd;

        [ObservableProperty]
        private string _status = string.Empty;

        [ObservableProperty]
        private string _notes = string.Empty;

        public string WorkRange => $"{WorkStart:dd/MM/yyyy} - {WorkEnd:dd/MM/yyyy}";

        partial void OnWorkStartChanged(DateTime value) => OnPropertyChanged(nameof(WorkRange));
        partial void OnWorkEndChanged(DateTime value) => OnPropertyChanged(nameof(WorkRange));
    }
}

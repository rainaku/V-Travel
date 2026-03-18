using CommunityToolkit.Mvvm.ComponentModel;

namespace VietTravel.UI.ViewModels
{
    public partial class ReportViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        public ReportViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }
    }
}

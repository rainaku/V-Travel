using CommunityToolkit.Mvvm.ComponentModel;

namespace VietTravel.UI.ViewModels
{
    public partial class BookingListViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private string _searchText = string.Empty;

        public BookingListViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }
    }
}

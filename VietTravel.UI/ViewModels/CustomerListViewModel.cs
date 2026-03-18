using CommunityToolkit.Mvvm.ComponentModel;

namespace VietTravel.UI.ViewModels
{
    public partial class CustomerListViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private string _searchText = string.Empty;

        public CustomerListViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }
    }
}

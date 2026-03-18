using CommunityToolkit.Mvvm.ComponentModel;

namespace VietTravel.UI.ViewModels
{
    public partial class PaymentListViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private string _searchText = string.Empty;

        public PaymentListViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace VietTravel.UI.ViewModels
{
    public partial class CustomerViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        public string FullName => _mainViewModel.CurrentUser?.FullName ?? "Khách Hàng";

        public CustomerViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        public void Logout()
        {
            _mainViewModel.CurrentUser = null;
            _mainViewModel.NavigateTo(new LoginViewModel(_mainViewModel));
        }
    }
}

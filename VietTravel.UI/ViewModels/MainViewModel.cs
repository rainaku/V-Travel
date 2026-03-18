using CommunityToolkit.Mvvm.ComponentModel;
using VietTravel.Core.Models;

namespace VietTravel.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        public User? CurrentUser { get; set; }

        public MainViewModel()
        {
            // Bắt đầu với màn hình Login
            CurrentViewModel = new LoginViewModel(this);
        }

        public void NavigateTo(ObservableObject viewModel)
        {
            CurrentViewModel = viewModel;
        }
    }
}

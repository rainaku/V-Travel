using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VietTravel.Data.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly AuthService _authService;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool IsNotLoading => !IsLoading;

        public LoginViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _authService = new AuthService();
        }

        partial void OnErrorMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasError));
        }

        partial void OnIsLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsNotLoading));
        }

        [RelayCommand]
        public void ToRegisterPage()
        {
            _mainViewModel.NavigateTo(new RegistrationViewModel(_mainViewModel));
        }

        [RelayCommand]
        public async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Vui lòng nhập tài khoản và mật khẩu.";
                return;
            }

            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                var user = await _authService.LoginAsync(Username, Password);
                if (user != null)
                {
                    _mainViewModel.CurrentUser = user;

                    // Both Admin and Employee go to Admin Shell
                    if (user.Role == "Admin" || user.Role == "Employee")
                    {
                        _mainViewModel.NavigateTo(new AdminShellViewModel(_mainViewModel));
                    }
                    else
                    {
                        _mainViewModel.NavigateTo(new CustomerViewModel(_mainViewModel));
                    }
                }
                else
                {
                    ErrorMessage = "Sai tài khoản hoặc mật khẩu.";
                }
            }
            catch (System.Exception ex)
            {
                ErrorMessage = "Lỗi kết nối: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

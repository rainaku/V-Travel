using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VietTravel.Data.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class RegistrationViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly AuthService _authService;

        [ObservableProperty] private string _fullName = "";
        [ObservableProperty] private string _username = "";
        [ObservableProperty] private string _password = "";
        [ObservableProperty] private string _confirmPassword = "";
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private bool _isLoading;

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool IsNotLoading => !IsLoading;

        public RegistrationViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _authService = new AuthService();
        }

        [RelayCommand]
        private async Task RegisterAsync()
        {
            if (string.IsNullOrWhiteSpace(FullName) || 
                string.IsNullOrWhiteSpace(Username) || 
                string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ thông tin";
                return;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Mật khẩu xác nhận không khớp";
                return;
            }

            IsLoading = true;
            ErrorMessage = "";
            try
            {
                var newUser = await _authService.RegisterCustomerAsync(Username, Password, FullName);
                if (newUser != null)
                {
                    MessageBox.Show("Đăng ký thành công! Vui lòng đăng nhập.", "Viet Travel", MessageBoxButton.OK, MessageBoxImage.Information);
                    _mainViewModel.NavigateTo(new LoginViewModel(_mainViewModel));
                }
                else
                {
                    ErrorMessage = "Đăng ký thất bại. Tên đăng nhập có thể đã tồn tại.";
                }
            }
            catch (System.Exception ex)
            {
                ErrorMessage = "Lỗi hệ thống: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void BackToLogin()
        {
            _mainViewModel.NavigateTo(new LoginViewModel(_mainViewModel));
        }
    }
}

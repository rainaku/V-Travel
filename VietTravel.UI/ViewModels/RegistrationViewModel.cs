using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Text.RegularExpressions;
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
        private static readonly Regex FullNamePattern = new(@"^[\p{L}\p{M}]+(?:[ '\-][\p{L}\p{M}]+)*$", RegexOptions.Compiled);
        private static readonly Regex EmailPattern = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex UsernamePattern = new(@"^[a-zA-Z0-9._-]{4,50}$", RegexOptions.Compiled);

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool IsNotLoading => !IsLoading;

        partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
        partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

        public RegistrationViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _authService = new AuthService();
        }

        [RelayCommand]
        private async Task RegisterAsync()
        {
            FullName = NormalizeWhitespace(FullName);
            Username = (Username ?? string.Empty).Trim();

            if (!TryValidateRegistrationInput(out var validationMessage))
            {
                ErrorMessage = validationMessage;
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
            catch (System.InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
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

        private bool TryValidateRegistrationInput(out string message)
        {
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                message = "Vui lòng nhập đầy đủ họ tên, tên đăng nhập và mật khẩu.";
                return false;
            }

            if (FullName.Length < 4 || FullName.Length > 80)
            {
                message = "Họ tên phải từ 4 đến 80 ký tự.";
                return false;
            }

            var fullNameParts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fullNameParts.Length < 2)
            {
                message = "Vui lòng nhập họ tên đầy đủ (ít nhất 2 từ).";
                return false;
            }

            if (!FullNamePattern.IsMatch(FullName))
            {
                message = "Họ tên chỉ được chứa chữ cái, khoảng trắng, dấu nháy hoặc gạch nối.";
                return false;
            }

            var isEmailUsername = Username.Contains("@", System.StringComparison.Ordinal);
            if (isEmailUsername)
            {
                if (Username.Length > 120 || !EmailPattern.IsMatch(Username))
                {
                    message = "Tên đăng nhập dạng email chưa đúng định dạng.";
                    return false;
                }
            }
            else if (!UsernamePattern.IsMatch(Username))
            {
                message = "Tên đăng nhập phải 4-50 ký tự, chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.";
                return false;
            }

            if (Password.Length < 8 || Password.Length > 64)
            {
                message = "Mật khẩu phải từ 8 đến 64 ký tự.";
                return false;
            }

            if (Password.Any(char.IsWhiteSpace))
            {
                message = "Mật khẩu không được chứa khoảng trắng.";
                return false;
            }

            if (!Password.Any(char.IsUpper) ||
                !Password.Any(char.IsLower) ||
                !Password.Any(char.IsDigit))
            {
                message = "Mật khẩu cần có ít nhất chữ hoa, chữ thường và số.";
                return false;
            }

            if (Password != ConfirmPassword)
            {
                message = "Mật khẩu xác nhận không khớp.";
                return false;
            }

            return true;
        }

        private static string NormalizeWhitespace(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value.Trim(), @"\s+", " ");
        }
    }
}

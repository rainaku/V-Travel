using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VietTravel.Data.Services;
using VietTravel.UI.Views;

namespace VietTravel.UI.ViewModels
{
    public partial class RegistrationViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly AuthService _authService;
        private readonly EmailService _emailService;

        [ObservableProperty] private string _fullName = "";
        [ObservableProperty] private string _username = "";
        [ObservableProperty] private string _phoneNumber = "";
        [ObservableProperty] private string _email = "";
        [ObservableProperty] private string _address = "";
        [ObservableProperty] private string _password = "";
        [ObservableProperty] private string _confirmPassword = "";
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private bool _isLoading;

        private static readonly Regex FullNamePattern = new(@"^[\p{L}\p{M}]+(?:[ '\-][\p{L}\p{M}]+)*$", RegexOptions.Compiled);
        private static readonly Regex EmailPattern = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex UsernamePattern = new(@"^[a-zA-Z0-9._-]{4,50}$", RegexOptions.Compiled);
        private static readonly Regex VietnamMobilePattern = new(@"^0(?:3|5|7|8|9)\d{8}$", RegexOptions.Compiled);
        private static readonly Regex AddressPattern = new(@"^[\p{L}\p{M}\d\s,./\-#]+$", RegexOptions.Compiled);

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool IsNotLoading => !IsLoading;

        partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
        partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

        public RegistrationViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _authService = new AuthService();
            _emailService = new EmailService();
        }

        [RelayCommand]
        private async Task RegisterAsync()
        {
            FullName = NormalizeWhitespace(FullName);
            Username = (Username ?? string.Empty).Trim();
            Email = (Email ?? string.Empty).Trim();
            Address = NormalizeWhitespace(Address);
            PhoneNumber = NormalizeVietnamPhone(PhoneNumber);

            if (!TryValidateRegistrationInput(out var validationMessage))
            {
                ErrorMessage = validationMessage;
                return;
            }

            IsLoading = true;
            ErrorMessage = "";

            try
            {
                // 1. Kiểm tra thông tin đã tồn tại trong DB chưa
                if (await _authService.IsUsernameExistsAsync(Username))
                {
                    ErrorMessage = "Tên đăng nhập này đã được sử dụng.";
                    return;
                }

                if (await _authService.IsEmailExistsAsync(Email))
                {
                    ErrorMessage = "Email này đã được đăng ký bằng một tài khoản khác.";
                    return;
                }

                if (await _authService.IsPhoneNumberExistsAsync(PhoneNumber))
                {
                    ErrorMessage = "Số điện thoại này đã được đăng ký bằng một tài khoản khác.";
                    return;
                }

                // 2. Gửi mã OTP trước khi hiện popup
                await _emailService.SendVerificationEmailAsync(Email);

                // 2. Hiển thị popup xác thực
                var verificationVM = new VerificationViewModel(Email);
                var verificationWindow = new VerificationWindow(verificationVM);
                
                // ShowDialog sẽ chặn đến khi window đóng
                bool? result = verificationWindow.ShowDialog();

                if (result != true)
                {
                    ErrorMessage = "Xác thực email không thành công. Vui lòng thử lại.";
                    return;
                }

                // 3. Nếu xác thực thành công, tiến hành tạo tài khoản
                var newUser = await _authService.RegisterCustomerAsync(Username, Password, FullName, PhoneNumber, Email, Address);
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
            catch (InvalidOperationException ex)
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
                string.IsNullOrWhiteSpace(PhoneNumber) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Address) ||
                string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                message = "Vui lòng nhập đầy đủ họ tên, tên đăng nhập, số điện thoại, email, địa chỉ và mật khẩu.";
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

            if (!VietnamMobilePattern.IsMatch(PhoneNumber))
            {
                message = "Số điện thoại chưa hợp lệ (định dạng VN: 03/05/07/08/09 + 8 số).";
                return false;
            }

            if (Email.Length > 120 || !EmailPattern.IsMatch(Email) || Email.Contains("..", System.StringComparison.Ordinal))
            {
                message = "Email chưa hợp lệ (ví dụ: ten@email.com).";
                return false;
            }

            if (Address.Length < 6 || Address.Length > 200)
            {
                message = "Địa chỉ phải từ 6 đến 200 ký tự.";
                return false;
            }

            if (!AddressPattern.IsMatch(Address))
            {
                message = "Địa chỉ chứa ký tự không hợp lệ.";
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

        private static string NormalizeVietnamPhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("84", System.StringComparison.Ordinal))
            {
                digits = "0" + digits.Substring(2);
            }

            return digits;
        }
    }
}

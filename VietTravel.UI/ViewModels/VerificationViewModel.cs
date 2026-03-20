using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VietTravel.Data.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class VerificationViewModel : ObservableObject
    {
        private readonly EmailService _emailService;
        private DispatcherTimer? _countdownTimer;
        private int _countdownSeconds;

        [ObservableProperty] private string _email = "";
        [ObservableProperty] private string _verificationCode = "";
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private string _countdownText = "Gửi lại (60s)";
        [ObservableProperty] private bool _canSendCode;
        [ObservableProperty] private bool _isSendingCode;
        [ObservableProperty] private bool? _dialogResult;

        public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);
        partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasErrorMessage));

        public VerificationViewModel(string email)
        {
            Email = email;
            _emailService = new EmailService();
            StartCountdown(60);
        }

        [RelayCommand]
        private async Task ResendCodeAsync()
        {
            IsSendingCode = true;
            ErrorMessage = "";
            try
            {
                await _emailService.SendVerificationEmailAsync(Email);
                StartCountdown(60);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Lỗi gửi lại mã: " + ex.Message;
                CanSendCode = true;
                CountdownText = "Gửi lại mã";
            }
            finally
            {
                IsSendingCode = false;
            }
        }

        [RelayCommand]
        private void Verify()
        {
            if (string.IsNullOrWhiteSpace(VerificationCode))
            {
                ErrorMessage = "Vui lòng nhập mã xác nhận.";
                return;
            }

            if (_emailService.ValidateOtp(Email, VerificationCode))
            {
                DialogResult = true;
            }
            else
            {
                ErrorMessage = "Mã xác nhận không đúng hoặc đã hết hạn.";
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
        }

        private void StartCountdown(int seconds)
        {
            StopCountdown();
            _countdownSeconds = seconds;
            CanSendCode = false;
            CountdownText = $"Gửi lại ({_countdownSeconds}s)";

            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += (s, e) =>
            {
                _countdownSeconds--;
                if (_countdownSeconds <= 0)
                {
                    StopCountdown();
                }
                else
                {
                    CountdownText = $"Gửi lại ({_countdownSeconds}s)";
                }
            };
            _countdownTimer.Start();
        }

        public void StopCountdown()
        {
            _countdownTimer?.Stop();
            _countdownTimer = null;
            CanSendCode = true;
            CountdownText = "Gửi lại mã";
        }
    }
}

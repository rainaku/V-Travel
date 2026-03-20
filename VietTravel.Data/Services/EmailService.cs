using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;

namespace VietTravel.Data.Services
{
    public class EmailService
    {
        private static readonly ConcurrentDictionary<string, (string Code, DateTime ExpiresAt)> _otpStore = new();
        private const int OtpExpirationMinutes = 5;
        private const int OtpLength = 6;

        private readonly string _smtpEmail;
        private readonly string _smtpPassword;
        private readonly string _smtpDisplayName;

        public EmailService()
        {
            DotNetEnv.Env.Load();
            _smtpEmail = Environment.GetEnvironmentVariable("SMTP_EMAIL") ?? "";
            _smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? "";
            _smtpDisplayName = Environment.GetEnvironmentVariable("SMTP_DISPLAY_NAME") ?? "Viet Travel";
        }

        /// <summary>
        /// Sinh mã OTP 6 số ngẫu nhiên.
        /// </summary>
        public static string GenerateOtp()
        {
            var bytes = RandomNumberGenerator.GetBytes(4);
            var number = BitConverter.ToUInt32(bytes, 0) % (uint)Math.Pow(10, OtpLength);
            return number.ToString().PadLeft(OtpLength, '0');
        }

        /// <summary>
        /// Gửi email chứa mã OTP và lưu OTP vào bộ nhớ.
        /// </summary>
        public async Task SendVerificationEmailAsync(string recipientEmail)
        {
            if (string.IsNullOrWhiteSpace(_smtpEmail) || string.IsNullOrWhiteSpace(_smtpPassword))
            {
                throw new InvalidOperationException("Chưa cấu hình SMTP. Vui lòng thêm SMTP_EMAIL và SMTP_PASSWORD vào file .env");
            }

            var otp = GenerateOtp();
            StoreOtp(recipientEmail, otp);

            using var smtpClient = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(_smtpEmail, _smtpPassword),
                EnableSsl = true,
                Timeout = 15000
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtpEmail, _smtpDisplayName),
                Subject = "Mã xác nhận đăng ký - Viet Travel",
                IsBodyHtml = true,
                Body = BuildEmailBody(otp)
            };
            mailMessage.To.Add(recipientEmail);

            await smtpClient.SendMailAsync(mailMessage);
        }

        /// <summary>
        /// Xác thực mã OTP. Trả về true nếu hợp lệ và chưa hết hạn.
        /// </summary>
        public bool ValidateOtp(string email, string inputCode)
        {
            var key = email.Trim().ToLowerInvariant();

            if (!_otpStore.TryGetValue(key, out var stored))
            {
                return false;
            }

            if (DateTime.UtcNow > stored.ExpiresAt)
            {
                _otpStore.TryRemove(key, out _);
                return false;
            }

            if (!string.Equals(stored.Code, inputCode?.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            // OTP hợp lệ — xóa để không dùng lại
            _otpStore.TryRemove(key, out _);
            return true;
        }

        /// <summary>
        /// Kiểm tra OTP đã hết hạn chưa (dùng cho UI hiển thị trạng thái).
        /// </summary>
        public bool IsOtpExpired(string email)
        {
            var key = email.Trim().ToLowerInvariant();
            if (!_otpStore.TryGetValue(key, out var stored))
            {
                return true;
            }
            return DateTime.UtcNow > stored.ExpiresAt;
        }

        private void StoreOtp(string email, string otp)
        {
            var key = email.Trim().ToLowerInvariant();
            _otpStore[key] = (otp, DateTime.UtcNow.AddMinutes(OtpExpirationMinutes));
        }

        private static string BuildEmailBody(string otp)
        {
            return $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""margin:0;padding:0;font-family:'Segoe UI',Arial,sans-serif;background:#f0f4f8"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:480px;margin:40px auto"">
    <tr><td style=""background:linear-gradient(135deg,#007AFF,#5AC8FA);padding:32px 24px;border-radius:16px 16px 0 0;text-align:center"">
      <h1 style=""color:#fff;margin:0;font-size:22px"">🌏 Viet Travel</h1>
      <p style=""color:rgba(255,255,255,0.85);margin:8px 0 0;font-size:14px"">Xác nhận đăng ký tài khoản</p>
    </td></tr>
    <tr><td style=""background:#fff;padding:32px 24px;text-align:center"">
      <p style=""color:#333;font-size:15px;margin:0 0 24px"">Mã xác nhận của bạn là:</p>
      <div style=""background:#f0f4f8;border-radius:12px;padding:20px;display:inline-block"">
        <span style=""font-size:36px;font-weight:700;letter-spacing:8px;color:#007AFF"">{otp}</span>
      </div>
      <p style=""color:#888;font-size:13px;margin:24px 0 0"">Mã có hiệu lực trong <strong>5 phút</strong>.<br>Vui lòng không chia sẻ mã này với ai.</p>
    </td></tr>
    <tr><td style=""background:#f8f9fa;padding:16px 24px;border-radius:0 0 16px 16px;text-align:center"">
      <p style=""color:#aaa;font-size:11px;margin:0"">© Viet Travel — Email tự động, vui lòng không trả lời.</p>
    </td></tr>
  </table>
</body>
</html>";
        }
    }
}

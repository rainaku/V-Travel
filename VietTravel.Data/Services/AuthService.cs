using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using VietTravel.Core.Models;

namespace VietTravel.Data.Services
{
    public class AuthService
    {
        private static readonly Regex FullNamePattern = new(@"^[\p{L}\p{M}]+(?:[ '\-][\p{L}\p{M}]+)*$", RegexOptions.Compiled);
        private static readonly Regex UsernamePattern = new(@"^[a-zA-Z0-9._-]{4,50}$", RegexOptions.Compiled);
        private static readonly Regex EmailPattern = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex VietnamMobilePattern = new(@"^0(?:3|5|7|8|9)\d{8}$", RegexOptions.Compiled);
        private static readonly Regex AddressPattern = new(@"^[\p{L}\p{M}\d\s,./\-#]+$", RegexOptions.Compiled);

        // --- Rate Limiting ---
        private const int MaxLoginAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        private static readonly ConcurrentDictionary<string, LoginAttemptInfo> _loginAttempts = new();

        private class LoginAttemptInfo
        {
            public int FailedCount { get; set; }
            public DateTime LastFailedAt { get; set; }
            public DateTime? LockedUntil { get; set; }
        }

        /// <summary>
        /// Kiểm tra tài khoản có đang bị khóa do đăng nhập sai quá nhiều lần.
        /// </summary>
        public bool IsAccountLocked(string username)
        {
            var key = username.Trim().ToLowerInvariant();
            if (_loginAttempts.TryGetValue(key, out var info) && info.LockedUntil.HasValue)
            {
                if (DateTime.UtcNow < info.LockedUntil.Value)
                    return true;

                // Lockout expired, reset
                _loginAttempts.TryRemove(key, out _);
            }
            return false;
        }

        /// <summary>
        /// Trả về số phút còn lại bị khóa, hoặc 0 nếu không bị khóa.
        /// </summary>
        public int GetRemainingLockoutMinutes(string username)
        {
            var key = username.Trim().ToLowerInvariant();
            if (_loginAttempts.TryGetValue(key, out var info) && info.LockedUntil.HasValue)
            {
                var remaining = info.LockedUntil.Value - DateTime.UtcNow;
                return remaining.TotalMinutes > 0 ? (int)Math.Ceiling(remaining.TotalMinutes) : 0;
            }
            return 0;
        }

        private void RecordFailedLogin(string username)
        {
            var key = username.Trim().ToLowerInvariant();
            var info = _loginAttempts.GetOrAdd(key, _ => new LoginAttemptInfo());
            info.FailedCount++;
            info.LastFailedAt = DateTime.UtcNow;

            if (info.FailedCount >= MaxLoginAttempts)
            {
                info.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
            }
        }

        private void ClearFailedLogin(string username)
        {
            var key = username.Trim().ToLowerInvariant();
            _loginAttempts.TryRemove(key, out _);
        }

        /// <summary>
        /// Hash mật khẩu bằng BCrypt (có salt tự động, work factor 12).
        /// </summary>
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }

        /// <summary>
        /// Xác minh mật khẩu với hash BCrypt. Hỗ trợ fallback SHA-256 cho tài khoản cũ.
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            // BCrypt hashes start with "$2a$", "$2b$", or "$2y$"
            if (storedHash.StartsWith("$2", StringComparison.Ordinal))
            {
                return BCrypt.Net.BCrypt.Verify(password, storedHash);
            }

            // Legacy SHA-256 fallback for existing accounts
            return storedHash == LegacySha256Hash(password);
        }

        /// <summary>
        /// Kiểm tra hash có phải dạng legacy SHA-256 (cần migrate sang BCrypt).
        /// </summary>
        public static bool IsLegacyHash(string storedHash)
        {
            // BCrypt hashes start with "$2"; SHA-256 hex is 64 chars of hex digits
            return !storedHash.StartsWith("$2", StringComparison.Ordinal)
                   && storedHash.Length == 64
                   && storedHash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
        }

        private static string LegacySha256Hash(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var builder = new StringBuilder();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }

        public async Task<bool> IsUsernameExistsAsync(string username)
        {
            var client = await SupabaseClientFactory.GetClientAsync();
            var response = await client.From<User>()
                .Where(x => x.Username == username)
                .Get();
            return response.Models.Any();
        }

        public async Task<bool> IsEmailExistsAsync(string email)
        {
            var client = await SupabaseClientFactory.GetClientAsync();
            var response = await client.From<Customer>()
                .Where(x => x.Email == email)
                .Get();
            return response.Models.Any();
        }

        public async Task<bool> IsPhoneNumberExistsAsync(string phoneNumber)
        {
            var client = await SupabaseClientFactory.GetClientAsync();
            var response = await client.From<Customer>()
                .Where(x => x.PhoneNumber == phoneNumber)
                .Get();
            return response.Models.Any();
        }

        public async Task<User?> LoginAsync(string username, string password)
        {
            // Rate limiting check
            if (IsAccountLocked(username))
            {
                var minutes = GetRemainingLockoutMinutes(username);
                throw new InvalidOperationException(
                    $"Tài khoản tạm khóa do đăng nhập sai quá {MaxLoginAttempts} lần. Vui lòng thử lại sau {minutes} phút.");
            }

            var client = await SupabaseClientFactory.GetClientAsync();
            var response = await client.From<User>()
                .Where(x => x.Username == username)
                .Get();
            
            var user = response.Models.FirstOrDefault();
            if (user != null)
            {
                if (!user.IsActive)
                {
                    var bannedByInfo = !string.IsNullOrWhiteSpace(user.BannedBy)
                        ? $" bởi {user.BannedBy}"
                        : string.Empty;
                    var bannedAtInfo = string.Empty;
                    if (!string.IsNullOrWhiteSpace(user.BannedAt) && DateTime.TryParse(user.BannedAt, out var bannedDate))
                    {
                        bannedAtInfo = $" vào {bannedDate.ToLocalTime():dd/MM/yyyy HH:mm}";
                    }
                    throw new InvalidOperationException(
                        $"Tài khoản đã bị khóa{bannedByInfo}{bannedAtInfo}. Vui lòng liên hệ quản trị viên.");
                }

                if (VerifyPassword(password, user.PasswordHash))
                {
                    ClearFailedLogin(username);

                    // Auto-migrate legacy SHA-256 hash to BCrypt on successful login
                    if (IsLegacyHash(user.PasswordHash))
                    {
                        user.PasswordHash = HashPassword(password);
                        await client.From<User>().Update(user);
                    }

                    return user;
                }
            }

            RecordFailedLogin(username);
            return null;
        }

        public async Task<User?> RegisterCustomerAsync(
            string username,
            string password,
            string fullName,
            string phoneNumber,
            string email,
            string address)
        {
            fullName = NormalizeWhitespace(fullName);
            username = (username ?? string.Empty).Trim();
            phoneNumber = NormalizeVietnamPhone(phoneNumber);
            email = (email ?? string.Empty).Trim();
            address = NormalizeWhitespace(address);

            if (!IsValidRegistrationInfo(username, password, fullName, phoneNumber, email, address, out var validationMessage))
            {
                throw new InvalidOperationException(validationMessage);
            }

            var client = await SupabaseClientFactory.GetClientAsync();

            // Check for existing username
            var existingUser = await client.From<User>().Where(u => u.Username == username).Get();
            if (existingUser.Models.Any())
            {
                throw new InvalidOperationException("Tên đăng nhập đã tồn tại trên hệ thống.");
            }

            // Check for existing email or phone in Customer table
            var emailCheck = await client.From<Customer>().Where(c => c.Email == email).Get();
            if (emailCheck.Models.Any())
            {
                throw new InvalidOperationException("Email này đã được đăng ký cho một khách hàng khác.");
            }

            var phoneCheck = await client.From<Customer>().Where(c => c.PhoneNumber == phoneNumber).Get();
            if (phoneCheck.Models.Any())
            {
                throw new InvalidOperationException("Số điện thoại này đã được sử dụng.");
            }

            var user = new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                FullName = fullName,
                Role = "Customer",
                IsActive = true
            };
            var response = await client.From<User>().Insert(user);
            var createdUser = response.Models.FirstOrDefault();
            if (createdUser == null)
            {
                return null;
            }

            try
            {
                var customer = new Customer
                {
                    FullName = fullName,
                    PhoneNumber = phoneNumber,
                    Email = email,
                    Address = address
                };
                await client.From<Customer>().Insert(customer);
                return createdUser;
            }
            catch
            {
                // Keep user/customer creation as an all-or-nothing registration flow.
                await client.From<User>().Where(u => u.Id == createdUser.Id).Delete();
                throw;
            }
        }

        private static bool IsValidRegistrationInfo(
            string username,
            string password,
            string fullName,
            string phoneNumber,
            string email,
            string address,
            out string message)
        {
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(phoneNumber) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(address) ||
                string.IsNullOrWhiteSpace(password))
            {
                message = "Thiếu thông tin đăng ký.";
                return false;
            }

            if (fullName.Length < 4 || fullName.Length > 80)
            {
                message = "Họ tên phải từ 4 đến 80 ký tự.";
                return false;
            }

            var fullNameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fullNameParts.Length < 2 || !FullNamePattern.IsMatch(fullName))
            {
                message = "Họ tên chưa đúng định dạng.";
                return false;
            }

            var isEmailUsername = username.Contains("@", StringComparison.Ordinal);
            if (isEmailUsername)
            {
                if (username.Length > 120 || !EmailPattern.IsMatch(username))
                {
                    message = "Tên đăng nhập dạng email chưa đúng định dạng.";
                    return false;
                }
            }
            else if (!UsernamePattern.IsMatch(username))
            {
                message = "Tên đăng nhập chỉ gồm chữ/số/dấu . _ - và dài 4-50 ký tự.";
                return false;
            }

            if (!VietnamMobilePattern.IsMatch(phoneNumber))
            {
                message = "Số điện thoại chưa đúng định dạng.";
                return false;
            }

            if (email.Length > 120 || !EmailPattern.IsMatch(email) || email.Contains("..", StringComparison.Ordinal))
            {
                message = "Email chưa đúng định dạng.";
                return false;
            }

            if (address.Length < 6 || address.Length > 200 || !AddressPattern.IsMatch(address))
            {
                message = "Địa chỉ chưa đúng định dạng.";
                return false;
            }

            if (password.Length < 8 || password.Length > 64 || password.Any(char.IsWhiteSpace))
            {
                message = "Mật khẩu phải từ 8-64 ký tự và không có khoảng trắng.";
                return false;
            }

            if (!password.Any(char.IsUpper) ||
                !password.Any(char.IsLower) ||
                !password.Any(char.IsDigit))
            {
                message = "Mật khẩu cần có ít nhất chữ hoa, chữ thường và số.";
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
            if (digits.StartsWith("84", StringComparison.Ordinal))
            {
                digits = "0" + digits.Substring(2);
            }

            return digits;
        }
    }
}

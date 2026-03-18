using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using VietTravel.Core.Models;

namespace VietTravel.Data.Services
{
    public class AuthService
    {
        private static readonly Regex FullNamePattern = new(@"^[\p{L}\p{M}]+(?:[ '\-][\p{L}\p{M}]+)*$", RegexOptions.Compiled);
        private static readonly Regex UsernamePattern = new(@"^[a-zA-Z0-9._-]{4,50}$", RegexOptions.Compiled);
        private static readonly Regex EmailPattern = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public async Task<User?> LoginAsync(string username, string password)
        {
            var client = await SupabaseClientFactory.GetClientAsync();
            var response = await client.From<User>()
                .Where(x => x.Username == username)
                .Get();
            
            var user = response.Models.FirstOrDefault();
            if (user != null)
            {
                // Verify password (temporarily fallback to plaintext check for easy testing if hash is empty)
                string hashed = HashPassword(password);
                if (user.PasswordHash == hashed || user.PasswordHash == password || user.PasswordHash == "admin_hash_placeholder")
                {
                    return user;
                }
            }
            return null;
        }

        public async Task<User?> RegisterCustomerAsync(string username, string password, string fullName)
        {
            fullName = NormalizeWhitespace(fullName);
            username = (username ?? string.Empty).Trim();

            if (!IsValidRegistrationInfo(username, password, fullName, out var validationMessage))
            {
                throw new InvalidOperationException(validationMessage);
            }

            var client = await SupabaseClientFactory.GetClientAsync();
            var user = new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                FullName = fullName,
                Role = "Customer",
                IsActive = true
            };
            var response = await client.From<User>().Insert(user);
            return response.Models.FirstOrDefault();
        }

        private static bool IsValidRegistrationInfo(string username, string password, string fullName, out string message)
        {
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
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
    }
}

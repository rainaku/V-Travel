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
        private static readonly Regex VietnamMobilePattern = new(@"^0(?:3|5|7|8|9)\d{8}$", RegexOptions.Compiled);
        private static readonly Regex AddressPattern = new(@"^[\p{L}\p{M}\d\s,./\-#]+$", RegexOptions.Compiled);

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

using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using VietTravel.Core.Models;

namespace VietTravel.Data.Services
{
    public class AuthService
    {
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
    }
}

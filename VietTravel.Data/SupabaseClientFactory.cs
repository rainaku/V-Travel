using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using DotNetEnv;

namespace VietTravel.Data
{
    public class SupabaseClientFactory
    {
        private static Supabase.Client? _instance;
        private static bool _envLoaded;

        public static async Task<Supabase.Client> GetClientAsync()
        {
            if (_instance == null)
            {
                EnsureEnvLoaded();

                string supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") 
                                     ?? throw new Exception("Thiếu SUPABASE_URL trong cấu hình.");
                                     
                string supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")
                                     ?? throw new Exception("Thiếu SUPABASE_KEY trong cấu hình.");

                var options = new Supabase.SupabaseOptions
                {
                    AutoConnectRealtime = true
                };

                _instance = new Supabase.Client(supabaseUrl, supabaseKey, options);
                await _instance.InitializeAsync();
            }

            return _instance;
        }

        /// <summary>
        /// Đảm bảo biến môi trường đã được load (từ embedded resource hoặc file .env).
        /// Gọi được từ bất kỳ service nào cần đọc env vars.
        /// </summary>
        public static void EnsureEnvLoaded()
        {
            if (_envLoaded) return;
            _envLoaded = true;

            // Try embedded resource first (nhúng sẵn trong DLL, user không thấy file .env)
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("VietTravel.Data.env.embedded");
            
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                        continue;

                    var eqIndex = line.IndexOf('=');
                    if (eqIndex <= 0) continue;

                    var key = line.Substring(0, eqIndex).Trim();
                    var value = line.Substring(eqIndex + 1).Trim();
                    
                    // Chỉ set nếu chưa có (cho phép override bằng env var hệ thống)
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }
                return;
            }

            // Fallback: đọc file .env bên ngoài
            try
            {
                Env.TraversePath().Load();
            }
            catch
            {
                // Ignore - variables might be set via system env
            }
        }
    }
}

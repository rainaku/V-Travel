using System;
using System.Threading.Tasks;
using DotNetEnv;

namespace VietTravel.Data
{
    public class SupabaseClientFactory
    {
        private static Supabase.Client? _instance;

        public static async Task<Supabase.Client> GetClientAsync()
        {
            if (_instance == null)
            {
                // Tự động tìm file .env tại thư mục gốc và tải biến môi trường
                Env.TraversePath().Load();

                string supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") 
                                     ?? throw new Exception("Thiếu SUPABASE_URL trong file .env");
                                     
                string supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")
                                     ?? throw new Exception("Thiếu SUPABASE_KEY trong file .env");

                var options = new Supabase.SupabaseOptions
                {
                    AutoConnectRealtime = true
                };

                _instance = new Supabase.Client(supabaseUrl, supabaseKey, options);
                await _instance.InitializeAsync();
            }

            return _instance;
        }
    }
}

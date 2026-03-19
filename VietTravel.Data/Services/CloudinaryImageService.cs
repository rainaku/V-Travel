using System;
using System.IO;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DotNetEnv;

namespace VietTravel.Data.Services
{
    public class CloudinaryImageService
    {
        public async Task<string> UploadAvatarAsync(string filePath, int userId)
        {
            return await UploadImageAsync(filePath, $"viet-travel/users/{userId}");
        }

        public async Task<string> UploadTourImageAsync(string filePath, string tourKey)
        {
            var safeKey = string.IsNullOrWhiteSpace(tourKey) ? "draft" : tourKey.Trim();
            return await UploadImageAsync(filePath, $"viet-travel/tours/{safeKey}");
        }

        private static async Task<string> UploadImageAsync(string filePath, string folder)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("Không tìm thấy file ảnh để upload.", filePath);
            }

            Env.TraversePath().Load();

            var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");

            if (string.IsNullOrWhiteSpace(cloudName))
            {
                throw new InvalidOperationException("Thiếu CLOUDINARY_CLOUD_NAME trong file .env.");
            }

            var apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
            var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            {
                throw new InvalidOperationException("Thiếu CLOUDINARY_API_KEY hoặc CLOUDINARY_API_SECRET trong file .env.");
            }

            var account = new Account(cloudName, apiKey, apiSecret);
            var cloudinary = new Cloudinary(account);

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(filePath),
                Folder = folder,
                UseFilename = false,
                UniqueFilename = true
            };

            var result = await cloudinary.UploadAsync(uploadParams);
            if (result.Error != null)
            {
                throw new InvalidOperationException($"Upload ảnh thất bại: {result.Error.Message}");
            }

            var secureUrl = result.SecureUrl?.ToString();
            if (string.IsNullOrWhiteSpace(secureUrl))
            {
                throw new InvalidOperationException("Cloudinary trả về URL ảnh rỗng.");
            }

            return secureUrl;
        }
    }
}

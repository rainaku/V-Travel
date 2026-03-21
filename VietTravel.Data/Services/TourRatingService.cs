using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VietTravel.Core.Models;

namespace VietTravel.Data.Services
{
    public class TourRatingService
    {
        public async Task<List<TourRating>> GetAllAsync()
        {
            var client = await SupabaseClientFactory.GetClientAsync();
            return (await client
                    .From<TourRating>()
                    .Order("created_at", Postgrest.Constants.Ordering.Descending)
                    .Range(0, 4999)
                    .Get())
                .Models;
        }

        public async Task<List<TourRating>> GetByBookingIdsAsync(IEnumerable<int> bookingIds)
        {
            var ids = bookingIds
                .Distinct()
                .Cast<object>()
                .ToList();

            if (ids.Count == 0)
            {
                return new List<TourRating>();
            }

            var client = await SupabaseClientFactory.GetClientAsync();
            return (await client
                    .From<TourRating>()
                    .Filter("booking_id", Postgrest.Constants.Operator.In, ids)
                    .Range(0, 4999)
                    .Get())
                .Models;
        }

        public async Task<TourRating?> GetByBookingIdAsync(int bookingId)
        {
            var client = await SupabaseClientFactory.GetClientAsync();
            return (await client
                    .From<TourRating>()
                    .Filter("booking_id", Postgrest.Constants.Operator.Equals, bookingId)
                    .Get())
                .Models
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();
        }

        public async Task<TourRating> SaveCustomerRatingAsync(TourRatingInput input)
        {
            if (input.BookingId <= 0)
            {
                throw new InvalidOperationException("Thiếu booking để đánh giá.");
            }

            if (input.CustomerId <= 0)
            {
                throw new InvalidOperationException("Thiếu thông tin khách hàng để đánh giá.");
            }

            if (input.RatingValue < 1 || input.RatingValue > 5)
            {
                throw new InvalidOperationException("Số sao phải từ 1 đến 5.");
            }

            var comment = NormalizeComment(input.Comment);
            if (string.IsNullOrWhiteSpace(comment))
            {
                throw new InvalidOperationException("Vui lòng nhập nhận xét trước khi gửi đánh giá.");
            }

            var client = await SupabaseClientFactory.GetClientAsync();
            var booking = (await client
                    .From<Booking>()
                    .Where(x => x.Id == input.BookingId)
                    .Get())
                .Models
                .FirstOrDefault();

            if (booking == null)
            {
                throw new InvalidOperationException("Không tìm thấy booking cần đánh giá.");
            }

            if (booking.CustomerId != input.CustomerId)
            {
                throw new InvalidOperationException("Booking không thuộc khách hàng hiện tại.");
            }

            if (string.Equals(booking.Status, "Đã hủy", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(booking.Status, "Hủy", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Không thể đánh giá booking đã hủy.");
            }

            if (!string.Equals(booking.Status, "Đã xác nhận", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Chỉ booking đã xác nhận mới được đánh giá.");
            }

            var departure = (await client
                    .From<Departure>()
                    .Where(x => x.Id == booking.DepartureId)
                    .Get())
                .Models
                .FirstOrDefault();

            if (departure == null)
            {
                throw new InvalidOperationException("Không tìm thấy lịch khởi hành của booking.");
            }

            var now = DateTime.Now;
            var existing = await GetByBookingIdAsync(input.BookingId);
            if (existing != null)
            {
                existing.Tour = null;
                existing.Customer = null;
                existing.Booking = null;
                existing.ModeratedByUser = null;
                existing.TourId = departure.TourId;
                existing.CustomerId = input.CustomerId;
                existing.RatingValue = input.RatingValue;
                existing.Comment = comment;
                existing.Status = TourRatingStatuses.Pending;
                existing.AdminReply = string.Empty;
                existing.ModeratedAt = null;
                existing.ModeratedByUserId = null;
                existing.UpdatedAt = now;

                await client.From<TourRating>().Update(existing);
                return existing;
            }

            var newRating = new TourRating
            {
                BookingId = input.BookingId,
                TourId = departure.TourId,
                CustomerId = input.CustomerId,
                RatingValue = input.RatingValue,
                Comment = comment,
                Status = TourRatingStatuses.Pending,
                AdminReply = string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            };

            var insertResponse = await client.From<TourRating>().Insert(newRating);
            return insertResponse.Models.FirstOrDefault()
                ?? throw new InvalidOperationException("Không thể lưu đánh giá.");
        }

        public async Task<TourRating> ModerateAsync(
            TourRating rating,
            string status,
            int? moderatedByUserId,
            string? adminReply)
        {
            if (rating.Id <= 0)
            {
                throw new InvalidOperationException("Đánh giá không hợp lệ.");
            }

            if (status != TourRatingStatuses.Pending &&
                status != TourRatingStatuses.Approved &&
                status != TourRatingStatuses.Hidden)
            {
                throw new InvalidOperationException("Trạng thái kiểm duyệt không hợp lệ.");
            }

            var client = await SupabaseClientFactory.GetClientAsync();
            rating.Tour = null;
            rating.Customer = null;
            rating.Booking = null;
            rating.ModeratedByUser = null;
            rating.Status = status;
            rating.AdminReply = NormalizeReply(adminReply);
            rating.ModeratedAt = DateTime.Now;
            rating.ModeratedByUserId = moderatedByUserId;
            rating.UpdatedAt = DateTime.Now;

            await client.From<TourRating>().Update(rating);
            return rating;
        }

        public static bool HasMissingRatingSchema(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            return message.Contains("tour_ratings", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("schema cache", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("booking_id", StringComparison.OrdinalIgnoreCase)
                      && message.Contains("tour_ratings", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeComment(string? comment)
        {
            return string.IsNullOrWhiteSpace(comment)
                ? string.Empty
                : comment.Trim();
        }

        private static string NormalizeReply(string? reply)
        {
            return string.IsNullOrWhiteSpace(reply)
                ? string.Empty
                : reply.Trim();
        }
    }

    public readonly record struct TourRatingInput(
        int BookingId,
        int CustomerId,
        int RatingValue,
        string Comment);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VietTravel.Core.Models;

namespace VietTravel.Data.Services
{
    public class GuideRatingService
    {
        public async Task<List<GuideRating>> GetAllAsync()
        {
            var client = await SupabaseClientFactory.GetClientAsync();
            return (await client
                    .From<GuideRating>()
                    .Order("created_at", Postgrest.Constants.Ordering.Descending)
                    .Range(0, 4999)
                    .Get())
                .Models;
        }

        public async Task<List<GuideRating>> GetByBookingIdsAsync(IEnumerable<int> bookingIds)
        {
            var ids = bookingIds
                .Distinct()
                .Cast<object>()
                .ToList();

            if (ids.Count == 0)
            {
                return new List<GuideRating>();
            }

            var client = await SupabaseClientFactory.GetClientAsync();
            return (await client
                    .From<GuideRating>()
                    .Filter("booking_id", Postgrest.Constants.Operator.In, ids)
                    .Range(0, 4999)
                    .Get())
                .Models;
        }

        public async Task<GuideRating?> GetByBookingIdAsync(int bookingId)
        {
            var client = await SupabaseClientFactory.GetClientAsync();
            return (await client
                    .From<GuideRating>()
                    .Filter("booking_id", Postgrest.Constants.Operator.Equals, bookingId)
                    .Get())
                .Models
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();
        }

        public async Task<GuideRating> SaveCustomerRatingAsync(GuideRatingInput input)
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

            var assignment = (await client
                    .From<TourGuideAssignment>()
                    .Where(x => x.DepartureId == booking.DepartureId)
                    .Get())
                .Models
                .OrderByDescending(x => x.AssignedAt)
                .FirstOrDefault();

            if (assignment == null)
            {
                throw new InvalidOperationException("Booking này chưa có hướng dẫn viên để đánh giá.");
            }

            if (input.GuideUserId.HasValue &&
                input.GuideUserId.Value > 0 &&
                assignment.GuideUserId != input.GuideUserId.Value)
            {
                throw new InvalidOperationException("Hướng dẫn viên của booking đã thay đổi. Vui lòng tải lại dữ liệu.");
            }

            var now = DateTime.Now;
            var existing = await GetByBookingIdAsync(input.BookingId);
            if (existing != null)
            {
                existing.Booking = null;
                existing.Departure = null;
                existing.Tour = null;
                existing.GuideUser = null;
                existing.Customer = null;
                existing.ModeratedByUser = null;
                existing.DepartureId = booking.DepartureId;
                existing.TourId = departure.TourId;
                existing.GuideUserId = assignment.GuideUserId;
                existing.CustomerId = input.CustomerId;
                existing.RatingValue = input.RatingValue;
                existing.Comment = comment;
                existing.Status = GuideRatingStatuses.Pending;
                existing.AdminReply = string.Empty;
                existing.ModeratedAt = null;
                existing.ModeratedByUserId = null;
                existing.UpdatedAt = now;

                await client.From<GuideRating>().Update(existing);
                return existing;
            }

            var newRating = new GuideRating
            {
                BookingId = input.BookingId,
                DepartureId = booking.DepartureId,
                TourId = departure.TourId,
                GuideUserId = assignment.GuideUserId,
                CustomerId = input.CustomerId,
                RatingValue = input.RatingValue,
                Comment = comment,
                Status = GuideRatingStatuses.Pending,
                AdminReply = string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            };

            var insertResponse = await client.From<GuideRating>().Insert(newRating);
            return insertResponse.Models.FirstOrDefault()
                ?? throw new InvalidOperationException("Không thể lưu đánh giá hướng dẫn viên.");
        }

        public async Task<GuideRating> ModerateAsync(
            GuideRating rating,
            string status,
            int? moderatedByUserId,
            string? adminReply)
        {
            if (rating.Id <= 0)
            {
                throw new InvalidOperationException("Đánh giá không hợp lệ.");
            }

            if (status != GuideRatingStatuses.Pending &&
                status != GuideRatingStatuses.Approved &&
                status != GuideRatingStatuses.Hidden)
            {
                throw new InvalidOperationException("Trạng thái kiểm duyệt không hợp lệ.");
            }

            var client = await SupabaseClientFactory.GetClientAsync();
            rating.Booking = null;
            rating.Departure = null;
            rating.Tour = null;
            rating.GuideUser = null;
            rating.Customer = null;
            rating.ModeratedByUser = null;
            rating.Status = status;
            rating.AdminReply = NormalizeReply(adminReply);
            rating.ModeratedAt = DateTime.Now;
            rating.ModeratedByUserId = moderatedByUserId;
            rating.UpdatedAt = DateTime.Now;

            await client.From<GuideRating>().Update(rating);
            return rating;
        }

        public static bool HasMissingRatingSchema(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            return message.Contains("guide_ratings", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("schema cache", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("guide_user_id", StringComparison.OrdinalIgnoreCase)
                      && message.Contains("guide_ratings", StringComparison.OrdinalIgnoreCase);
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

    public readonly record struct GuideRatingInput(
        int BookingId,
        int CustomerId,
        int? GuideUserId,
        int RatingValue,
        string Comment);
}

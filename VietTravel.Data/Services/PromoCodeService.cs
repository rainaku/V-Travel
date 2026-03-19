using System;
using System.Linq;
using System.Threading.Tasks;
using VietTravel.Core.Models;

namespace VietTravel.Data.Services
{
    public class PromoCodeService
    {
        private static readonly string[] CancelledBookingStatuses = { "Đã hủy", "Hủy" };

        public async Task<PromoValidationResult> ValidateAsync(
            Supabase.Client client,
            string rawCode,
            PromoValidationContext context)
        {
            var normalizedCode = NormalizeCode(rawCode);
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                return PromoValidationResult.Fail(
                    reason: "Vui lòng nhập mã giảm giá.",
                    code: normalizedCode,
                    failureReason: PromoCodeFailureReason.EmptyCode);
            }

            if (context.OrderAmount <= 0)
            {
                return PromoValidationResult.Fail(
                    reason: "Giá trị đơn hàng chưa hợp lệ để áp mã giảm giá.",
                    code: normalizedCode,
                    failureReason: PromoCodeFailureReason.InvalidOrderAmount);
            }

            var promo = (await client
                    .From<PromoCode>()
                    .Where(x => x.Code == normalizedCode)
                    .Get())
                .Models
                .FirstOrDefault();

            if (promo == null)
            {
                return PromoValidationResult.Fail(
                    reason: "Mã giảm giá không tồn tại.",
                    code: normalizedCode,
                    failureReason: PromoCodeFailureReason.NotFound);
            }

            var now = context.Now == default ? DateTime.Now : context.Now;
            var status = ResolveStatus(promo, now);
            if (status == PromoCodeStatus.Disabled)
            {
                return PromoValidationResult.Fail(
                    reason: "Mã giảm giá đã bị vô hiệu hóa.",
                    code: normalizedCode,
                    failureReason: PromoCodeFailureReason.Disabled,
                    promoCode: promo);
            }

            if (status == PromoCodeStatus.Upcoming)
            {
                return PromoValidationResult.Fail(
                    reason: $"Mã sẽ có hiệu lực từ {promo.StartDate:dd/MM/yyyy HH:mm}.",
                    code: normalizedCode,
                    failureReason: PromoCodeFailureReason.Upcoming,
                    promoCode: promo);
            }

            if (status == PromoCodeStatus.Expired)
            {
                return PromoValidationResult.Fail(
                    reason: "Mã giảm giá đã hết hạn.",
                    code: normalizedCode,
                    failureReason: PromoCodeFailureReason.Expired,
                    promoCode: promo);
            }

            if (promo.MinOrderAmount > context.OrderAmount)
            {
                return PromoValidationResult.Fail(
                    reason: $"Đơn hàng tối thiểu để áp dụng mã là {promo.MinOrderAmount:N0} đ.",
                    code: normalizedCode,
                    failureReason: PromoCodeFailureReason.MinOrderNotMet,
                    promoCode: promo);
            }

            if (string.Equals(promo.DiscountType, PromoDiscountTypes.Fixed, StringComparison.Ordinal) &&
                promo.DiscountValue > context.OrderAmount)
            {
                return PromoValidationResult.Fail(
                    reason: "Giá trị giảm cố định vượt quá tổng đơn hàng hiện tại.",
                    code: normalizedCode,
                    failureReason: PromoCodeFailureReason.FixedAmountExceedsOrder,
                    promoCode: promo);
            }

            if (!string.IsNullOrWhiteSpace(promo.ApplicableTourType))
            {
                var targetTourType = (promo.ApplicableTourType ?? string.Empty).Trim();
                var currentTourType = (context.TourType ?? string.Empty).Trim();
                if (!string.Equals(targetTourType, currentTourType, StringComparison.OrdinalIgnoreCase))
                {
                    return PromoValidationResult.Fail(
                        reason: $"Mã chỉ áp dụng cho loại tour \"{targetTourType}\".",
                        code: normalizedCode,
                        failureReason: PromoCodeFailureReason.TourTypeNotApplicable,
                        promoCode: promo);
                }
            }

            var scopedTourIds = (await client
                    .From<PromoCodeTour>()
                    .Where(x => x.PromoCodeId == promo.Id)
                    .Get())
                .Models
                .Select(x => x.TourId)
                .ToHashSet();

            if (scopedTourIds.Count > 0 && !scopedTourIds.Contains(context.TourId))
            {
                return PromoValidationResult.Fail(
                    reason: "Mã giảm giá này không áp dụng cho tour đã chọn.",
                    code: normalizedCode,
                    failureReason: PromoCodeFailureReason.TourNotApplicable,
                    promoCode: promo);
            }

            if (promo.OnlyNewCustomers && context.CustomerId > 0)
            {
                var bookings = (await client
                        .From<Booking>()
                        .Where(x => x.CustomerId == context.CustomerId)
                        .Get())
                    .Models;

                var hasPriorBooking = bookings.Any(b =>
                    !CancelledBookingStatuses.Any(s => string.Equals(s, b.Status, StringComparison.OrdinalIgnoreCase)));

                if (hasPriorBooking)
                {
                    return PromoValidationResult.Fail(
                        reason: "Mã chỉ áp dụng cho khách hàng mới.",
                        code: normalizedCode,
                        failureReason: PromoCodeFailureReason.NewCustomerOnly,
                        promoCode: promo);
                }
            }

            var usages = (await client
                    .From<PromoCodeUsage>()
                    .Where(x => x.PromoCodeId == promo.Id)
                    .Get())
                .Models
                .ToList();

            if (promo.MaxTotalUses.HasValue && usages.Count >= promo.MaxTotalUses.Value)
            {
                return PromoValidationResult.Fail(
                    reason: "Mã đã hết lượt sử dụng toàn hệ thống.",
                    code: normalizedCode,
                    failureReason: PromoCodeFailureReason.TotalUsageLimitReached,
                    promoCode: promo);
            }

            var hasCustomerScope = context.CustomerId > 0;
            var currentUserId = context.UserId.GetValueOrDefault();
            var hasUserScope = currentUserId > 0;
            if (promo.MaxUsesPerUser.HasValue && (hasCustomerScope || hasUserScope))
            {
                var usedByRequester = usages.Count(x =>
                    (hasCustomerScope && x.CustomerId == context.CustomerId) ||
                    (hasUserScope && x.UserId == currentUserId));

                if (usedByRequester >= promo.MaxUsesPerUser.Value)
                {
                    return PromoValidationResult.Fail(
                        reason: "Bạn đã dùng hết số lượt cho mã giảm giá này.",
                        code: normalizedCode,
                        failureReason: PromoCodeFailureReason.PerUserUsageLimitReached,
                        promoCode: promo);
                }
            }

            var discountAmount = CalculateDiscountAmount(promo, context.OrderAmount);
            if (discountAmount <= 0)
            {
                return PromoValidationResult.Fail(
                    reason: "Mã giảm giá không tạo ra mức giảm hợp lệ.",
                    code: normalizedCode,
                    failureReason: PromoCodeFailureReason.InvalidDiscountValue,
                    promoCode: promo);
            }

            var finalAmount = Math.Max(context.OrderAmount - discountAmount, 0);
            return PromoValidationResult.Success(
                promoCode: promo,
                normalizedCode: normalizedCode,
                discountAmount: discountAmount,
                finalAmount: finalAmount);
        }

        public static string NormalizeCode(string? rawCode)
        {
            return (rawCode ?? string.Empty).Trim().ToUpperInvariant();
        }

        public static decimal CalculateDiscountAmount(PromoCode promoCode, decimal orderAmount)
        {
            if (orderAmount <= 0)
            {
                return 0;
            }

            decimal discount = promoCode.DiscountType switch
            {
                PromoDiscountTypes.Percent => Math.Round(orderAmount * promoCode.DiscountValue / 100m, 0, MidpointRounding.AwayFromZero),
                PromoDiscountTypes.Fixed => promoCode.DiscountValue,
                _ => 0
            };

            if (discount < 0)
            {
                discount = 0;
            }

            if (discount > orderAmount)
            {
                discount = orderAmount;
            }

            return discount;
        }

        public static PromoCodeStatus ResolveStatus(PromoCode promoCode, DateTime now)
        {
            if (!promoCode.IsActive)
            {
                return PromoCodeStatus.Disabled;
            }

            if (now < promoCode.StartDate)
            {
                return PromoCodeStatus.Upcoming;
            }

            if (now > promoCode.EndDate)
            {
                return PromoCodeStatus.Expired;
            }

            return PromoCodeStatus.Active;
        }

        public async Task RecordUsageAsync(
            Supabase.Client client,
            PromoValidationResult validationResult,
            int bookingId,
            int customerId,
            int? userId,
            decimal orderAmount,
            decimal finalAmount)
        {
            if (!validationResult.IsValid || validationResult.PromoCode == null)
            {
                return;
            }

            var usage = new PromoCodeUsage
            {
                PromoCodeId = validationResult.PromoCode.Id,
                BookingId = bookingId,
                CustomerId = customerId,
                UserId = userId,
                PromoCode = validationResult.NormalizedCode,
                OrderAmount = orderAmount,
                DiscountAmount = validationResult.DiscountAmount,
                FinalAmount = finalAmount,
                UsedAt = DateTime.Now
            };

            await client.From<PromoCodeUsage>().Insert(usage);
        }
    }

    public class PromoValidationContext
    {
        public int CustomerId { get; set; }
        public int? UserId { get; set; }
        public int TourId { get; set; }
        public string TourType { get; set; } = string.Empty;
        public decimal OrderAmount { get; set; }
        public DateTime Now { get; set; } = DateTime.Now;
    }

    public class PromoValidationResult
    {
        private PromoValidationResult() { }

        public bool IsValid { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public string NormalizedCode { get; private set; } = string.Empty;
        public decimal DiscountAmount { get; private set; }
        public decimal FinalAmount { get; private set; }
        public PromoCode? PromoCode { get; private set; }
        public PromoCodeFailureReason FailureReason { get; private set; }

        public static PromoValidationResult Fail(
            string reason,
            string code,
            PromoCodeFailureReason failureReason,
            PromoCode? promoCode = null)
        {
            return new PromoValidationResult
            {
                IsValid = false,
                Reason = reason,
                NormalizedCode = code,
                FailureReason = failureReason,
                PromoCode = promoCode
            };
        }

        public static PromoValidationResult Success(
            PromoCode promoCode,
            string normalizedCode,
            decimal discountAmount,
            decimal finalAmount)
        {
            return new PromoValidationResult
            {
                IsValid = true,
                Reason = string.Empty,
                NormalizedCode = normalizedCode,
                PromoCode = promoCode,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount,
                FailureReason = PromoCodeFailureReason.None
            };
        }
    }

    public enum PromoCodeFailureReason
    {
        None = 0,
        EmptyCode = 1,
        NotFound = 2,
        Disabled = 3,
        Upcoming = 4,
        Expired = 5,
        TotalUsageLimitReached = 6,
        PerUserUsageLimitReached = 7,
        MinOrderNotMet = 8,
        TourNotApplicable = 9,
        TourTypeNotApplicable = 10,
        NewCustomerOnly = 11,
        InvalidOrderAmount = 12,
        InvalidDiscountValue = 13,
        FixedAmountExceedsOrder = 14
    }

    public enum PromoCodeStatus
    {
        Upcoming = 0,
        Active = 1,
        Expired = 2,
        Disabled = 3
    }
}

namespace VietTravel.Core.Models
{
    /// <summary>
    /// Booking status constants. Values must match the database column values exactly.
    /// </summary>
    public static class BookingStatuses
    {
        public const string PendingPayment = "Chờ thanh toán";
        public const string PendingProcessing = "Chờ xử lý";
        public const string PendingConfirmation = "Đợi xác nhận";
        public const string Confirmed = "Đã xác nhận";
        public const string Cancelled = "Đã hủy";

        /// <summary>Legacy alias — some old records use "Hủy" instead of "Đã hủy".</summary>
        public const string CancelledLegacy = "Hủy";

        public static bool IsCancelled(string? status) =>
            string.Equals(status, Cancelled, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, CancelledLegacy, System.StringComparison.OrdinalIgnoreCase);

        public static bool IsPending(string? status) =>
            string.Equals(status, PendingPayment, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, PendingProcessing, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, PendingConfirmation, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Departure status constants. Values must match the database column values exactly.
    /// </summary>
    public static class DepartureStatuses
    {
        public const string Open = "Mở bán";
        public const string SoldOut = "Hết chỗ";
        public const string Closed = "Đóng";
    }

    /// <summary>
    /// Payment status constants. Values must match the database column values exactly.
    /// </summary>
    public static class PaymentStatuses
    {
        public const string Unpaid = "Chưa thanh toán";
        public const string PendingConfirmation = "Đợi xác nhận";
        public const string Deposit = "Đã cọc";
        public const string FullyPaid = "Đã thanh toán đủ";
        public const string Refunded = "Đã hoàn tiền";

        /// <summary>Legacy alias — some old records use "Đã thanh toán".</summary>
        public const string PaidLegacy = "Đã thanh toán";

        public static bool IsPaid(string? status) =>
            string.Equals(status, FullyPaid, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, PaidLegacy, System.StringComparison.OrdinalIgnoreCase);
    }
}

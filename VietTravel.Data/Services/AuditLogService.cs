using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VietTravel.Data.Services
{
    /// <summary>
    /// Service ghi log audit cho các thao tác quan trọng (thanh toán, booking, promo code, user).
    /// Dữ liệu được lưu vào bảng audit_logs trên Supabase (append-only).
    /// </summary>
    public static class AuditLogService
    {
        /// <summary>
        /// Ghi một dòng audit log vào database.
        /// Nếu ghi thất bại (bảng chưa tồn tại, lỗi mạng...), sẽ swallow exception
        /// để không ảnh hưởng luồng nghiệp vụ chính.
        /// </summary>
        /// <param name="client">Supabase client</param>
        /// <param name="userId">ID user thực hiện thao tác (0 nếu không xác định)</param>
        /// <param name="action">Tên hành động (VD: PAYMENT_MARK_PAID, BOOKING_CANCEL)</param>
        /// <param name="entityType">Loại entity (VD: Payment, Booking, PromoCode)</param>
        /// <param name="entityId">ID của entity bị tác động</param>
        /// <param name="oldValue">Giá trị cũ (nullable)</param>
        /// <param name="newValue">Giá trị mới (nullable)</param>
        /// <param name="details">Chi tiết bổ sung (nullable)</param>
        public static async Task LogAsync(
            Supabase.Client client,
            int userId,
            string action,
            string entityType,
            int entityId,
            string? oldValue = null,
            string? newValue = null,
            string? details = null)
        {
            try
            {
                await client.Rpc("insert_audit_log", new Dictionary<string, object>
                {
                    ["p_user_id"] = userId,
                    ["p_action"] = action,
                    ["p_entity_type"] = entityType,
                    ["p_entity_id"] = entityId,
                    ["p_old_value"] = oldValue ?? string.Empty,
                    ["p_new_value"] = newValue ?? string.Empty,
                    ["p_details"] = details ?? string.Empty
                });
            }
            catch
            {
                // Audit logging should never break the main business flow.
                // In production, consider writing to a local fallback log file.
            }
        }

        /// <summary>
        /// Ghi audit log không cần await (fire-and-forget cho các thao tác không critical).
        /// </summary>
        public static void LogFireAndForget(
            Supabase.Client client,
            int userId,
            string action,
            string entityType,
            int entityId,
            string? oldValue = null,
            string? newValue = null,
            string? details = null)
        {
            _ = Task.Run(async () =>
            {
                await LogAsync(client, userId, action, entityType, entityId, oldValue, newValue, details);
            });
        }
    }
}

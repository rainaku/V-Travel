using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("promo_codes")]
    public class PromoCode : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("discount_type")]
        public string DiscountType { get; set; } = PromoDiscountTypes.Percent;

        [Column("discount_value")]
        public decimal DiscountValue { get; set; }

        [Column("start_date")]
        public DateTime StartDate { get; set; } = DateTime.Now;

        [Column("end_date")]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(7);

        [Column("max_total_uses")]
        public int? MaxTotalUses { get; set; }

        [Column("max_uses_per_user")]
        public int? MaxUsesPerUser { get; set; }

        [Column("min_order_amount")]
        public decimal MinOrderAmount { get; set; }

        [Column("applicable_tour_type")]
        public string? ApplicableTourType { get; set; }

        [Column("only_new_customers")]
        public bool OnlyNewCustomers { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public static class PromoDiscountTypes
    {
        public const string Percent = "Percent";
        public const string Fixed = "Fixed";
    }
}

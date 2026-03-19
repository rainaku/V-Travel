using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("promo_code_usages")]
    public class PromoCodeUsage : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("promo_code_id")]
        public int PromoCodeId { get; set; }

        [Reference(typeof(PromoCode))]
        public PromoCode? PromoCodeEntity { get; set; }

        [Column("booking_id")]
        public int BookingId { get; set; }

        [Reference(typeof(Booking))]
        public Booking? Booking { get; set; }

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Reference(typeof(Customer))]
        public Customer? Customer { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Reference(typeof(User))]
        public User? User { get; set; }

        [Column("promo_code")]
        public string PromoCode { get; set; } = string.Empty;

        [Column("order_amount")]
        public decimal OrderAmount { get; set; }

        [Column("discount_amount")]
        public decimal DiscountAmount { get; set; }

        [Column("final_amount")]
        public decimal FinalAmount { get; set; }

        [Column("used_at")]
        public DateTime UsedAt { get; set; } = DateTime.Now;
    }
}

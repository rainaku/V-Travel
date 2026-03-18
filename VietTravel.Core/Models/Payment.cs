using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("payments")]
    public class Payment : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }
        
        [Column("booking_id")]
        public int BookingId { get; set; }
        
        [Reference(typeof(Booking))]
        public Booking? Booking { get; set; }
        
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }
        
        [Column("paid_amount")]
        public decimal PaidAmount { get; set; }
        
        [Column("status")]
        public string Status { get; set; } = "Chưa thanh toán"; // "Chưa thanh toán", "Đã cọc", "Đã thanh toán đủ"
        
        [Column("payment_date")]
        public DateTime? PaymentDate { get; set; }
        
        [Column("payment_method")]
        public string PaymentMethod { get; set; } = "Tiền mặt";
    }
}

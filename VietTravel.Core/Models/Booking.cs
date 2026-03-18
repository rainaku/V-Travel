using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("bookings")]
    public class Booking : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }
        
        [Column("customer_id")]
        public int CustomerId { get; set; }
        
        [Reference(typeof(Customer))]
        public Customer? Customer { get; set; }
        
        [Column("departure_id")]
        public int DepartureId { get; set; }
        
        [Reference(typeof(Departure))]
        public Departure? Departure { get; set; }
        
        [Column("user_id")]
        public int UserId { get; set; } // Nhân viên tạo booking
        
        [Reference(typeof(User))]
        public User? User { get; set; }
        
        [Column("booking_date")]
        public DateTime BookingDate { get; set; } = DateTime.Now;
        
        [Column("guest_count")]
        public int GuestCount { get; set; }
        
        [Column("status")]
        public string Status { get; set; } = "Chờ thanh toán"; // "Chờ thanh toán", "Chờ xử lý", "Đã xác nhận", "Đã hủy"
    }
}

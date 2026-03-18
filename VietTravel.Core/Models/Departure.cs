using System;
using System.Collections.Generic;
using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("departures")]
    public class Departure : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("tour_id")]
        public int TourId { get; set; }
        
        [Reference(typeof(Tour))]
        public Tour? Tour { get; set; }

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("max_slots")]
        public int MaxSlots { get; set; }

        [Column("available_slots")]
        public int AvailableSlots { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Mở bán"; // "Mở bán", "Hết chỗ", "Đóng"
    }
}

using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("tour_hotels")]
    public class TourHotel : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("tour_id")]
        public int TourId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("nights")]
        public int Nights { get; set; }

        [Column("notes")]
        public string Notes { get; set; } = string.Empty;

        [Reference(typeof(Hotel))]
        public Hotel? Hotel { get; set; }
    }
}

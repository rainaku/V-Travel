using System.Collections.Generic;
using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("tours")]
    public class Tour : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("base_price")]
        public decimal BasePrice { get; set; }

        [Column("duration_days")]
        public int DurationDays { get; set; }

        [Column("destination")]
        public string Destination { get; set; } = string.Empty;

        [Column("image_url")]
        public string ImageUrl { get; set; } = string.Empty;

        public List<TourTransport> TourTransports { get; set; } = new();

        public List<TourHotel> TourHotels { get; set; } = new();

        public List<TourAttraction> TourAttractions { get; set; } = new();
    }
}

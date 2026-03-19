using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("tour_attractions")]
    public class TourAttraction : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("tour_id")]
        public int TourId { get; set; }

        [Column("attraction_id")]
        public int AttractionId { get; set; }

        [Column("order_index")]
        public int OrderIndex { get; set; }

        [Column("notes")]
        public string Notes { get; set; } = string.Empty;

        [Reference(typeof(Attraction))]
        public Attraction? Attraction { get; set; }
    }
}

using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("hotels")]
    public class Hotel : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("address")]
        public string Address { get; set; } = string.Empty;

        [Column("star_rating")]
        public int StarRating { get; set; }

        [Column("cost_per_night")]
        public decimal CostPerNight { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Hoạt động";
    }
}

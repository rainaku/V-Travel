using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("tour_transports")]
    public class TourTransport : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("tour_id")]
        public int TourId { get; set; }

        [Column("transport_id")]
        public int TransportId { get; set; }

        [Column("notes")]
        public string Notes { get; set; } = string.Empty;

        [Reference(typeof(Transport))]
        public Transport? Transport { get; set; }
    }
}

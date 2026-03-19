using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("attractions")]
    public class Attraction : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("address")]
        public string Address { get; set; } = string.Empty;

        [Column("ticket_price")]
        public decimal TicketPrice { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Hoạt động";
    }
}

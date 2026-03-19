using Postgrest.Attributes;
using Postgrest.Models;
using System.Collections.Generic;

namespace VietTravel.Core.Models
{
    [Table("transports")]
    public class Transport : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("type")]
        public string Type { get; set; } = string.Empty;

        [Column("capacity")]
        public int Capacity { get; set; }

        [Column("cost")]
        public decimal Cost { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Hoạt động";
    }
}

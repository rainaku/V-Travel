using System;
using System.Collections.Generic;
using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("customers")]
    public class Customer : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("address")]
        public string Address { get; set; } = string.Empty;
    }
}

using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("users")]
    public class User : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("avatar_url")]
        public string AvatarUrl { get; set; } = string.Empty;

        [Column("role")]
        public string Role { get; set; } = string.Empty; // "Admin", "Employee"

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}

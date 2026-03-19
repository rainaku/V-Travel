using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("guide_profiles")]
    public class GuideProfile : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Reference(typeof(User))]
        public User? User { get; set; }

        [Column("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("emergency_contact")]
        public string EmergencyContact { get; set; } = string.Empty;

        [Column("notes")]
        public string Notes { get; set; } = string.Empty;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

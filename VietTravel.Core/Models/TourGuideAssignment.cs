using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("tour_guide_assignments")]
    public class TourGuideAssignment : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("guide_user_id")]
        public int GuideUserId { get; set; }

        [Reference(typeof(User))]
        public User? Guide { get; set; }

        [Column("departure_id")]
        public int DepartureId { get; set; }

        [Reference(typeof(Departure))]
        public Departure? Departure { get; set; }

        [Column("work_start")]
        public DateTime WorkStart { get; set; } = DateTime.Now;

        [Column("work_end")]
        public DateTime WorkEnd { get; set; } = DateTime.Now.AddHours(8);

        [Column("status")]
        public string Status { get; set; } = "Đang phân công";

        [Column("notes")]
        public string Notes { get; set; } = string.Empty;

        [Column("assigned_at")]
        public DateTime AssignedAt { get; set; } = DateTime.Now;
    }
}

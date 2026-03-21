using System;
using Newtonsoft.Json;
using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("guide_ratings")]
    public class GuideRating : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("booking_id")]
        public int BookingId { get; set; }

        [JsonIgnore]
        public Booking? Booking { get; set; }

        [Column("departure_id")]
        public int DepartureId { get; set; }

        [JsonIgnore]
        public Departure? Departure { get; set; }

        [Column("tour_id")]
        public int TourId { get; set; }

        [JsonIgnore]
        public Tour? Tour { get; set; }

        [Column("guide_user_id")]
        public int GuideUserId { get; set; }

        [JsonIgnore]
        public User? GuideUser { get; set; }

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [JsonIgnore]
        public Customer? Customer { get; set; }

        [Column("rating_value")]
        public int RatingValue { get; set; }

        [Column("comment")]
        public string Comment { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = GuideRatingStatuses.Pending;

        [Column("admin_reply")]
        public string AdminReply { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Column("moderated_at")]
        public DateTime? ModeratedAt { get; set; }

        [Column("moderated_by_user_id")]
        public int? ModeratedByUserId { get; set; }

        [JsonIgnore]
        public User? ModeratedByUser { get; set; }
    }

    public static class GuideRatingStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Hidden = "Hidden";
    }
}

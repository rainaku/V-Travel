using Postgrest.Attributes;
using Postgrest.Models;

namespace VietTravel.Core.Models
{
    [Table("promo_code_tours")]
    public class PromoCodeTour : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("promo_code_id")]
        public int PromoCodeId { get; set; }

        [Reference(typeof(PromoCode))]
        public PromoCode? PromoCode { get; set; }

        [Column("tour_id")]
        public int TourId { get; set; }

        [Reference(typeof(Tour))]
        public Tour? Tour { get; set; }
    }
}

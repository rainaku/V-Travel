using System;
using VietTravel.Core.Models;

namespace VietTravel.UI.ViewModels
{
    internal static class TourRatingDisplayHelper
    {
        public static string ToStatusLabel(string? status)
        {
            return status switch
            {
                TourRatingStatuses.Approved => "Đã duyệt",
                TourRatingStatuses.Hidden => "Đang ẩn",
                _ => "Chờ duyệt"
            };
        }

        public static string ToStatusColor(string? status)
        {
            return status switch
            {
                TourRatingStatuses.Approved => "#FF15803D",
                TourRatingStatuses.Hidden => "#FFB91C1C",
                _ => "#FFB45309"
            };
        }

        public static string ToStarsText(int ratingValue)
        {
            var clamped = Math.Clamp(ratingValue, 0, 5);
            return $"{new string('★', clamped)}{new string('☆', 5 - clamped)}";
        }

        public static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            if (trimmed.Length <= maxLength)
            {
                return trimmed;
            }

            return $"{trimmed[..Math.Max(maxLength - 3, 0)]}...";
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VietTravel.Core.Models;
using VietTravel.Data;
using VietTravel.Data.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class GuideRatingManagementViewModel : PaginatedListViewModelBase<GuideRatingManagementItem>
    {
        private readonly MainViewModel _mainViewModel;
        private readonly GuideRatingService _guideRatingService = new();

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private ObservableCollection<GuideRatingManagementItem> _ratings = new();
        [ObservableProperty] private ObservableCollection<GuideRatingManagementItem> _filteredRatings = new();
        [ObservableProperty] private ObservableCollection<string> _statusOptions = new() { "Tất cả", "Chờ duyệt", "Đã duyệt", "Đang ẩn" };
        [ObservableProperty] private ObservableCollection<string> _starOptions = new() { "Tất cả", "5 sao", "4 sao", "3 sao", "2 sao", "1 sao" };
        [ObservableProperty] private string _selectedStatus = "Tất cả";
        [ObservableProperty] private string _selectedStar = "Tất cả";
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _schemaWarningMessage = string.Empty;
        [ObservableProperty] private GuideRatingManagementItem? _selectedRating;
        [ObservableProperty] private string _moderationReply = string.Empty;
        [ObservableProperty] private ObservableCollection<string> _moderationStatuses = new() { "Chờ duyệt", "Đã duyệt", "Đang ẩn" };
        [ObservableProperty] private string _selectedModerationStatus = "Chờ duyệt";
        [ObservableProperty] private int _totalRatings;
        [ObservableProperty] private int _pendingRatings;
        [ObservableProperty] private int _approvedRatings;
        [ObservableProperty] private decimal _averageRating;

        public bool HasNoData => !IsLoading && FilteredRatings.Count == 0;
        public bool HasSchemaWarning => !string.IsNullOrWhiteSpace(SchemaWarningMessage);
        public bool IsModerationPanelVisible => SelectedRating != null;
        public string AverageRatingText => $"{AverageRating:0.0}/5";

        public GuideRatingManagementViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = LoadDataAsync();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedStatusChanged(string value) => ApplyFilter();
        partial void OnSelectedStarChanged(string value) => ApplyFilter();

        partial void OnSchemaWarningMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasSchemaWarning));
        }

        partial void OnAverageRatingChanged(decimal value)
        {
            OnPropertyChanged(nameof(AverageRatingText));
        }

        partial void OnSelectedRatingChanged(GuideRatingManagementItem? value)
        {
            ModerationReply = value?.AdminReply ?? string.Empty;
            SelectedModerationStatus = value?.StatusLabel ?? "Chờ duyệt";
            OnPropertyChanged(nameof(IsModerationPanelVisible));
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand]
        private void ShowModerationForm(GuideRatingManagementItem? item)
        {
            SelectedRating = item;
        }

        [RelayCommand]
        private void CancelModeration()
        {
            SelectedRating = null;
            ModerationReply = string.Empty;
        }

        [RelayCommand]
        private async Task SaveModerationAsync()
        {
            if (SelectedRating == null || HasSchemaWarning)
            {
                return;
            }

            try
            {
                await _guideRatingService.ModerateAsync(
                    SelectedRating.GuideRating,
                    ToStatusValue(SelectedModerationStatus),
                    _mainViewModel.CurrentUser?.Id,
                    ModerationReply);

                await LoadDataAsync();
                CancelModeration();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu phản hồi đánh giá HDV: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ApproveRatingAsync(GuideRatingManagementItem? item)
        {
            await QuickModerateAsync(item, GuideRatingStatuses.Approved);
        }

        [RelayCommand]
        private async Task HideRatingAsync(GuideRatingManagementItem? item)
        {
            await QuickModerateAsync(item, GuideRatingStatuses.Hidden);
        }

        private async Task LoadDataAsync(int? reselectRatingId = null)
        {
            IsLoading = true;
            SchemaWarningMessage = string.Empty;
            OnPropertyChanged(nameof(HasNoData));

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var ratings = await _guideRatingService.GetAllAsync();
                var tours = (await client.From<Tour>().Range(0, 4999).Get()).Models;
                var departures = (await client.From<Departure>().Range(0, 4999).Get()).Models;
                var customers = (await client.From<Customer>().Range(0, 4999).Get()).Models;
                var users = (await client.From<User>().Range(0, 4999).Get()).Models;

                Ratings = new ObservableCollection<GuideRatingManagementItem>(
                    ratings.Select(rating =>
                    {
                        var tour = tours.FirstOrDefault(x => x.Id == rating.TourId);
                        var departure = departures.FirstOrDefault(x => x.Id == rating.DepartureId);
                        var customer = customers.FirstOrDefault(x => x.Id == rating.CustomerId);
                        var guide = users.FirstOrDefault(x => x.Id == rating.GuideUserId);
                        var moderator = rating.ModeratedByUserId.HasValue
                            ? users.FirstOrDefault(x => x.Id == rating.ModeratedByUserId.Value)
                            : null;

                        return GuideRatingManagementItem.From(rating, guide, tour, departure, customer, moderator);
                    }));

                UpdateStats();
                ApplyFilter();

                if (reselectRatingId.HasValue)
                {
                    SelectedRating = Ratings.FirstOrDefault(x => x.Id == reselectRatingId.Value);
                }
            }
            catch (Exception ex)
            {
                if (GuideRatingService.HasMissingRatingSchema(ex))
                {
                    Ratings = new ObservableCollection<GuideRatingManagementItem>();
                    FilteredRatings = new ObservableCollection<GuideRatingManagementItem>();
                    SelectedRating = null;
                    SchemaWarningMessage = "Cơ sở dữ liệu chưa có bảng đánh giá hướng dẫn viên. Vui lòng chạy update_ratings.sql trên Supabase rồi mở lại ứng dụng.";
                    UpdateStats();
                }
                else
                {
                    MessageBox.Show($"Lỗi tải đánh giá hướng dẫn viên: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoData));
            }
        }

        private async Task QuickModerateAsync(GuideRatingManagementItem? item, string status)
        {
            if (item == null || HasSchemaWarning)
            {
                return;
            }

            try
            {
                await _guideRatingService.ModerateAsync(
                    item.GuideRating,
                    status,
                    _mainViewModel.CurrentUser?.Id,
                    item.AdminReply);

                await LoadDataAsync();
                CancelModeration();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi cập nhật trạng thái đánh giá HDV: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            var isSearchEmpty = string.IsNullOrWhiteSpace(SearchText);
            var lower = isSearchEmpty ? string.Empty : SearchText.Trim().ToLowerInvariant();
            var statusFilter = SelectedStatus == "Tất cả" ? null : SelectedStatus;
            int? starFilter = SelectedStar == "Tất cả" ? null : int.Parse(SelectedStar[..1]);

            var filtered = Ratings.Where(x =>
                    (isSearchEmpty ||
                     x.CustomerName.ToLowerInvariant().Contains(lower) ||
                     x.CustomerEmail.ToLowerInvariant().Contains(lower) ||
                     x.GuideName.ToLowerInvariant().Contains(lower) ||
                     x.TourName.ToLowerInvariant().Contains(lower) ||
                     x.Comment.ToLowerInvariant().Contains(lower) ||
                     x.StatusLabel.ToLowerInvariant().Contains(lower) ||
                     x.AdminReply.ToLowerInvariant().Contains(lower)) &&
                    (statusFilter == null || x.StatusLabel == statusFilter) &&
                    (!starFilter.HasValue || x.RatingValue == starFilter.Value))
                .ToList();
            SetPagedItems(filtered, FilteredRatings);

            OnPropertyChanged(nameof(HasNoData));
        }

        private void UpdateStats()
        {
            TotalRatings = Ratings.Count;
            PendingRatings = Ratings.Count(x => x.GuideRating.Status == GuideRatingStatuses.Pending);
            ApprovedRatings = Ratings.Count(x => x.GuideRating.Status == GuideRatingStatuses.Approved);
            AverageRating = Ratings.Count == 0
                ? 0
                : Math.Round((decimal)Ratings.Average(x => x.RatingValue), 1);
        }

        private static string ToStatusValue(string statusLabel)
        {
            return statusLabel switch
            {
                "Đã duyệt" => GuideRatingStatuses.Approved,
                "Đang ẩn" => GuideRatingStatuses.Hidden,
                _ => GuideRatingStatuses.Pending
            };
        }
    }

    public class GuideRatingManagementItem
    {
        public GuideRating GuideRating { get; init; } = new();
        public string GuideName { get; init; } = "HDV không xác định";
        public string TourName { get; init; } = "Tour không xác định";
        public string DepartureDateText { get; init; } = "N/A";
        public string CustomerName { get; init; } = "Khách hàng không xác định";
        public string CustomerEmail { get; init; } = string.Empty;
        public string ModeratorName { get; init; } = string.Empty;

        public int Id => GuideRating.Id;
        public int BookingId => GuideRating.BookingId;
        public int RatingValue => GuideRating.RatingValue;
        public string StarsText => TourRatingDisplayHelper.ToStarsText(GuideRating.RatingValue);
        public string Comment => string.IsNullOrWhiteSpace(GuideRating.Comment) ? "Không có bình luận." : GuideRating.Comment.Trim();
        public string CommentPreview => TourRatingDisplayHelper.Truncate(Comment, 96);
        public string AdminReply => string.IsNullOrWhiteSpace(GuideRating.AdminReply) ? string.Empty : GuideRating.AdminReply.Trim();
        public string AdminReplyPreview => string.IsNullOrWhiteSpace(AdminReply)
            ? "Chưa phản hồi"
            : TourRatingDisplayHelper.Truncate(AdminReply, 96);
        public string StatusLabel => TourRatingDisplayHelper.ToStatusLabel(GuideRating.Status);
        public string StatusColor => TourRatingDisplayHelper.ToStatusColor(GuideRating.Status);
        public string CreatedAtText => GuideRating.CreatedAt.ToString("dd/MM/yyyy HH:mm");
        public string ModeratedAtText => GuideRating.ModeratedAt?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa kiểm duyệt";
        public string ModerationSummary => string.IsNullOrWhiteSpace(ModeratorName)
            ? ModeratedAtText
            : $"{ModeratedAtText} • {ModeratorName}";

        public static GuideRatingManagementItem From(
            GuideRating rating,
            User? guide,
            Tour? tour,
            Departure? departure,
            Customer? customer,
            User? moderator)
        {
            return new GuideRatingManagementItem
            {
                GuideRating = rating,
                GuideName = string.IsNullOrWhiteSpace(guide?.FullName) ? $"Guide #{rating.GuideUserId}" : guide.FullName,
                TourName = string.IsNullOrWhiteSpace(tour?.Name) ? $"Tour #{rating.TourId}" : tour.Name,
                DepartureDateText = departure == null ? "N/A" : departure.StartDate.ToString("dd/MM/yyyy"),
                CustomerName = string.IsNullOrWhiteSpace(customer?.FullName) ? $"KH #{rating.CustomerId}" : customer.FullName,
                CustomerEmail = customer?.Email ?? string.Empty,
                ModeratorName = moderator?.FullName ?? string.Empty
            };
        }
    }
}

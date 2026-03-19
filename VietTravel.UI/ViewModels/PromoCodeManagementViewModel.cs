using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VietTravel.Core.Models;
using VietTravel.Data;
using VietTravel.Data.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class PromoCodeManagementViewModel : ObservableObject
    {
        private static readonly Regex PromoCodePattern = new("^[A-Z0-9_-]{3,30}$", RegexOptions.Compiled);
        private readonly PromoCodeService _promoCodeService = new();
        private PromoCode? _editingPromoCode;
        private bool _isLoadingUsages;

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private ObservableCollection<PromoCodeDisplayItem> _promoCodes = new();
        [ObservableProperty] private ObservableCollection<PromoCodeDisplayItem> _filteredPromoCodes = new();
        [ObservableProperty] private ObservableCollection<TourSelectionItem> _tourSelections = new();
        [ObservableProperty] private ObservableCollection<PromoCodeUsageDisplayItem> _selectedPromoUsages = new();
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private bool _isFormVisible;
        [ObservableProperty] private bool _isEditing;
        [ObservableProperty] private string _formTitle = "Tạo mã giảm giá";
        [ObservableProperty] private string _formCode = string.Empty;
        [ObservableProperty] private string _formDiscountType = PromoDiscountTypes.Percent;
        [ObservableProperty] private string _formDiscountValue = string.Empty;
        [ObservableProperty] private DateTime _formStartDate = DateTime.Now;
        [ObservableProperty] private DateTime _formEndDate = DateTime.Now.AddDays(7);
        [ObservableProperty] private string _formMaxTotalUses = string.Empty;
        [ObservableProperty] private string _formMaxUsesPerUser = string.Empty;
        [ObservableProperty] private string _formMinOrderAmount = "0";
        [ObservableProperty] private string _formApplicableTourType = string.Empty;
        [ObservableProperty] private bool _formOnlyNewCustomers;
        [ObservableProperty] private bool _formIsActive = true;
        [ObservableProperty] private string _formValidationMessage = string.Empty;
        [ObservableProperty] private string _schemaWarningMessage = string.Empty;
        [ObservableProperty] private PromoCodeDisplayItem? _selectedPromoCode;

        public ObservableCollection<string> DiscountTypeOptions { get; } =
            new(new[]
            {
                PromoDiscountTypes.Percent,
                PromoDiscountTypes.Fixed
            });

        public bool HasNoData => !IsLoading && FilteredPromoCodes.Count == 0;
        public bool HasSelectedPromo => SelectedPromoCode != null;
        public bool HasSchemaWarning => !string.IsNullOrWhiteSpace(SchemaWarningMessage);

        public PromoCodeManagementViewModel(MainViewModel mainViewModel)
        {
            _ = mainViewModel;
            _ = LoadInitialDataAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnSelectedPromoCodeChanged(PromoCodeDisplayItem? value)
        {
            OnPropertyChanged(nameof(HasSelectedPromo));
            _ = LoadUsageHistoryAsync(value);
        }

        private async Task LoadInitialDataAsync()
        {
            SchemaWarningMessage = string.Empty;
            await LoadToursAsync();
            await LoadPromoCodesAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadInitialDataAsync();
        }

        [RelayCommand]
        private void ShowAddForm()
        {
            if (HasSchemaWarning)
            {
                return;
            }

            _editingPromoCode = null;
            IsEditing = false;
            FormTitle = "Tạo mã giảm giá";
            FormCode = string.Empty;
            FormDiscountType = PromoDiscountTypes.Percent;
            FormDiscountValue = "10";
            FormStartDate = DateTime.Now;
            FormEndDate = DateTime.Now.AddDays(7);
            FormMaxTotalUses = string.Empty;
            FormMaxUsesPerUser = string.Empty;
            FormMinOrderAmount = "0";
            FormApplicableTourType = string.Empty;
            FormOnlyNewCustomers = false;
            FormIsActive = true;
            FormValidationMessage = string.Empty;
            SetAllToursSelected(false);
            IsFormVisible = true;
        }

        [RelayCommand]
        private void ShowEditForm(PromoCodeDisplayItem? item)
        {
            if (item == null || HasSchemaWarning)
            {
                return;
            }

            _editingPromoCode = item.PromoCode;
            IsEditing = true;
            FormTitle = "Chỉnh sửa mã giảm giá";
            FormCode = item.PromoCode.Code;
            FormDiscountType = item.PromoCode.DiscountType;
            FormDiscountValue = item.PromoCode.DiscountValue.ToString("0.##", CultureInfo.InvariantCulture);
            FormStartDate = item.PromoCode.StartDate;
            FormEndDate = item.PromoCode.EndDate;
            FormMaxTotalUses = item.PromoCode.MaxTotalUses?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            FormMaxUsesPerUser = item.PromoCode.MaxUsesPerUser?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            FormMinOrderAmount = item.PromoCode.MinOrderAmount.ToString("0.##", CultureInfo.InvariantCulture);
            FormApplicableTourType = item.PromoCode.ApplicableTourType ?? string.Empty;
            FormOnlyNewCustomers = item.PromoCode.OnlyNewCustomers;
            FormIsActive = item.PromoCode.IsActive;
            FormValidationMessage = string.Empty;

            var scopedTourIds = item.ScopedTourIds.ToHashSet();
            foreach (var selection in TourSelections)
            {
                selection.IsSelected = scopedTourIds.Contains(selection.TourId);
            }

            IsFormVisible = true;
        }

        [RelayCommand]
        private void CancelForm()
        {
            IsFormVisible = false;
            FormValidationMessage = string.Empty;
        }

        [RelayCommand]
        private void ClearSelectedTours()
        {
            SetAllToursSelected(false);
        }

        [RelayCommand]
        private async Task SavePromoCodeAsync()
        {
            if (HasSchemaWarning)
            {
                FormValidationMessage = SchemaWarningMessage;
                return;
            }

            FormValidationMessage = string.Empty;
            if (!TryBuildPromoCodeInput(out var input, out var message))
            {
                FormValidationMessage = message;
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var existingByCode = (await client
                        .From<PromoCode>()
                        .Where(x => x.Code == input.Code)
                        .Get())
                    .Models
                    .FirstOrDefault();

                if (existingByCode != null && (!IsEditing || _editingPromoCode == null || existingByCode.Id != _editingPromoCode.Id))
                {
                    FormValidationMessage = "Mã giảm giá đã tồn tại. Vui lòng chọn mã khác.";
                    return;
                }

                PromoCode target;
                if (IsEditing && _editingPromoCode != null)
                {
                    _editingPromoCode.Code = input.Code;
                    _editingPromoCode.DiscountType = input.DiscountType;
                    _editingPromoCode.DiscountValue = input.DiscountValue;
                    _editingPromoCode.StartDate = input.StartDate;
                    _editingPromoCode.EndDate = input.EndDate;
                    _editingPromoCode.MaxTotalUses = input.MaxTotalUses;
                    _editingPromoCode.MaxUsesPerUser = input.MaxUsesPerUser;
                    _editingPromoCode.MinOrderAmount = input.MinOrderAmount;
                    _editingPromoCode.ApplicableTourType = input.ApplicableTourType;
                    _editingPromoCode.OnlyNewCustomers = input.OnlyNewCustomers;
                    _editingPromoCode.IsActive = input.IsActive;
                    _editingPromoCode.UpdatedAt = DateTime.Now;
                    await client.From<PromoCode>().Update(_editingPromoCode);
                    target = _editingPromoCode;
                }
                else
                {
                    var newPromo = new PromoCode
                    {
                        Code = input.Code,
                        DiscountType = input.DiscountType,
                        DiscountValue = input.DiscountValue,
                        StartDate = input.StartDate,
                        EndDate = input.EndDate,
                        MaxTotalUses = input.MaxTotalUses,
                        MaxUsesPerUser = input.MaxUsesPerUser,
                        MinOrderAmount = input.MinOrderAmount,
                        ApplicableTourType = input.ApplicableTourType,
                        OnlyNewCustomers = input.OnlyNewCustomers,
                        IsActive = input.IsActive,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    var insertResponse = await client.From<PromoCode>().Insert(newPromo);
                    target = insertResponse.Models.FirstOrDefault()
                        ?? throw new InvalidOperationException("Không thể lưu mã giảm giá.");
                }

                await client.From<PromoCodeTour>().Where(x => x.PromoCodeId == target.Id).Delete();
                foreach (var selectedTourId in input.ScopedTourIds)
                {
                    await client.From<PromoCodeTour>().Insert(new PromoCodeTour
                    {
                        PromoCodeId = target.Id,
                        TourId = selectedTourId
                    });
                }

                IsFormVisible = false;
                await LoadPromoCodesAsync();
            }
            catch (Exception ex)
            {
                FormValidationMessage = $"Lỗi lưu mã giảm giá: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ToggleActiveAsync(PromoCodeDisplayItem? item)
        {
            if (item == null || HasSchemaWarning)
            {
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                item.PromoCode.IsActive = !item.PromoCode.IsActive;
                item.PromoCode.UpdatedAt = DateTime.Now;
                await client.From<PromoCode>().Update(item.PromoCode);
                await LoadPromoCodesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đổi trạng thái mã: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeletePromoCodeAsync(PromoCodeDisplayItem? item)
        {
            if (item == null || HasSchemaWarning)
            {
                return;
            }

            var confirmation = MessageBox.Show(
                $"Bạn có chắc muốn xóa mã \"{item.Code}\"?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var usages = (await client
                        .From<PromoCodeUsage>()
                        .Where(x => x.PromoCodeId == item.Id)
                        .Get())
                    .Models;

                if (usages.Count > 0)
                {
                    MessageBox.Show(
                        "Mã đã có lịch sử sử dụng nên không thể xóa. Bạn có thể tắt mã để ngừng áp dụng.",
                        "Không thể xóa",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                await client.From<PromoCodeTour>().Where(x => x.PromoCodeId == item.Id).Delete();
                await client.From<PromoCode>().Where(x => x.Id == item.Id).Delete();
                await LoadPromoCodesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi xóa mã giảm giá: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadToursAsync()
        {
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var tours = (await client
                        .From<Tour>()
                        .Order(x => x.Id, Postgrest.Constants.Ordering.Ascending)
                        .Range(0, 4999)
                        .Get())
                    .Models;

                TourSelections = new ObservableCollection<TourSelectionItem>(
                    tours.Select(t => new TourSelectionItem(
                        t.Id,
                        $"#{t.Id} - {t.Name} ({t.Destination})")));
            }
            catch (Exception ex)
            {
                if (!HasMissingPromotionSchema(ex))
                {
                    MessageBox.Show($"Lỗi tải danh sách tour: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task LoadPromoCodesAsync()
        {
            IsLoading = true;
            OnPropertyChanged(nameof(HasNoData));
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var promoCodes = (await client
                        .From<PromoCode>()
                        .Order(x => x.Id, Postgrest.Constants.Ordering.Descending)
                        .Range(0, 4999)
                        .Get())
                    .Models
                    .ToList();

                var allUsages = (await client
                        .From<PromoCodeUsage>()
                        .Range(0, 4999)
                        .Get())
                    .Models;

                var allScopes = (await client
                        .From<PromoCodeTour>()
                        .Range(0, 4999)
                        .Get())
                    .Models
                    .GroupBy(x => x.PromoCodeId)
                    .ToDictionary(x => x.Key, x => x.Select(y => y.TourId).ToList());

                PromoCodes = new ObservableCollection<PromoCodeDisplayItem>(
                    promoCodes.Select(p =>
                    {
                        allScopes.TryGetValue(p.Id, out var scopedTours);
                        var usageCount = allUsages.Count(u => u.PromoCodeId == p.Id);
                        return PromoCodeDisplayItem.From(p, scopedTours ?? new List<int>(), usageCount);
                    }));

                ApplyFilter();

                if (SelectedPromoCode != null)
                {
                    SelectedPromoCode = FilteredPromoCodes.FirstOrDefault(x => x.Id == SelectedPromoCode.Id);
                }
            }
            catch (Exception ex)
            {
                if (HasMissingPromotionSchema(ex))
                {
                    PromoCodes = new ObservableCollection<PromoCodeDisplayItem>();
                    FilteredPromoCodes = new ObservableCollection<PromoCodeDisplayItem>();
                    SelectedPromoUsages = new ObservableCollection<PromoCodeUsageDisplayItem>();
                    SchemaWarningMessage =
                        "Cơ sở dữ liệu chưa có bảng/cột cho mã giảm giá. Vui lòng chạy update_database.sql trên Supabase rồi mở lại ứng dụng.";
                    OnPropertyChanged(nameof(HasSchemaWarning));
                }
                else
                {
                    MessageBox.Show($"Lỗi tải mã giảm giá: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoData));
            }
        }

        private async Task LoadUsageHistoryAsync(PromoCodeDisplayItem? item)
        {
            if (_isLoadingUsages)
            {
                return;
            }

            if (item == null)
            {
                SelectedPromoUsages = new ObservableCollection<PromoCodeUsageDisplayItem>();
                return;
            }

            _isLoadingUsages = true;
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var usageRows = (await client
                        .From<PromoCodeUsage>()
                        .Where(x => x.PromoCodeId == item.Id)
                        .Get())
                    .Models
                    .OrderByDescending(x => x.UsedAt)
                    .Take(100)
                    .Select(PromoCodeUsageDisplayItem.From)
                    .ToList();

                SelectedPromoUsages = new ObservableCollection<PromoCodeUsageDisplayItem>(usageRows);
            }
            catch (Exception ex)
            {
                if (!HasMissingPromotionSchema(ex))
                {
                    MessageBox.Show($"Lỗi tải lịch sử sử dụng mã: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                _isLoadingUsages = false;
            }
        }

        partial void OnSchemaWarningMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasSchemaWarning));
        }

        private static bool HasMissingPromotionSchema(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            return message.Contains("promo_codes", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("promo_code_usages", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("schema cache", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredPromoCodes = new ObservableCollection<PromoCodeDisplayItem>(PromoCodes);
            }
            else
            {
                var lower = SearchText.Trim().ToLowerInvariant();
                FilteredPromoCodes = new ObservableCollection<PromoCodeDisplayItem>(
                    PromoCodes.Where(x =>
                        x.Code.ToLowerInvariant().Contains(lower) ||
                        x.StatusLabel.ToLowerInvariant().Contains(lower) ||
                        x.ScopeSummary.ToLowerInvariant().Contains(lower)));
            }

            OnPropertyChanged(nameof(HasNoData));
        }

        private void SetAllToursSelected(bool isSelected)
        {
            foreach (var selection in TourSelections)
            {
                selection.IsSelected = isSelected;
            }
        }

        private bool TryBuildPromoCodeInput(out PromoCodeFormInput input, out string message)
        {
            input = default;
            message = string.Empty;

            var code = PromoCodeService.NormalizeCode(FormCode);
            if (!PromoCodePattern.IsMatch(code))
            {
                message = "Mã chỉ gồm chữ in hoa, số, dấu gạch dưới hoặc gạch ngang (3-30 ký tự).";
                return false;
            }

            if (FormDiscountType != PromoDiscountTypes.Percent && FormDiscountType != PromoDiscountTypes.Fixed)
            {
                message = "Loại giảm giá chưa hợp lệ.";
                return false;
            }

            if (!TryParseMoney(FormDiscountValue, out var discountValue) || discountValue <= 0)
            {
                message = "Giá trị giảm phải là số dương.";
                return false;
            }

            if (FormDiscountType == PromoDiscountTypes.Percent && (discountValue < 1 || discountValue > 100))
            {
                message = "Giảm theo phần trăm phải từ 1 đến 100.";
                return false;
            }

            if (FormEndDate < FormStartDate)
            {
                message = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.";
                return false;
            }

            int? maxTotalUses = null;
            if (!string.IsNullOrWhiteSpace(FormMaxTotalUses))
            {
                if (!int.TryParse(FormMaxTotalUses.Trim(), out var parsed) || parsed <= 0)
                {
                    message = "Giới hạn tổng lượt dùng phải là số nguyên dương.";
                    return false;
                }

                maxTotalUses = parsed;
            }

            int? maxUsesPerUser = null;
            if (!string.IsNullOrWhiteSpace(FormMaxUsesPerUser))
            {
                if (!int.TryParse(FormMaxUsesPerUser.Trim(), out var parsed) || parsed <= 0)
                {
                    message = "Giới hạn lượt dùng mỗi người phải là số nguyên dương.";
                    return false;
                }

                maxUsesPerUser = parsed;
            }

            if (!TryParseMoney(FormMinOrderAmount, out var minOrderAmount) || minOrderAmount < 0)
            {
                message = "Đơn tối thiểu phải là số không âm.";
                return false;
            }

            var scopedTourIds = TourSelections
                .Where(x => x.IsSelected)
                .Select(x => x.TourId)
                .Distinct()
                .ToList();

            input = new PromoCodeFormInput(
                code,
                FormDiscountType,
                discountValue,
                FormStartDate,
                FormEndDate,
                maxTotalUses,
                maxUsesPerUser,
                minOrderAmount,
                string.IsNullOrWhiteSpace(FormApplicableTourType) ? null : FormApplicableTourType.Trim(),
                FormOnlyNewCustomers,
                FormIsActive,
                scopedTourIds);
            return true;
        }

        private static bool TryParseMoney(string rawValue, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            var cleaned = rawValue
                .Trim()
                .Replace("đ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("vnd", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty)
                .Replace(".", string.Empty)
                .Replace(",", string.Empty);

            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }

    public readonly record struct PromoCodeFormInput(
        string Code,
        string DiscountType,
        decimal DiscountValue,
        DateTime StartDate,
        DateTime EndDate,
        int? MaxTotalUses,
        int? MaxUsesPerUser,
        decimal MinOrderAmount,
        string? ApplicableTourType,
        bool OnlyNewCustomers,
        bool IsActive,
        List<int> ScopedTourIds);

    public partial class TourSelectionItem : ObservableObject
    {
        [ObservableProperty] private bool _isSelected;

        public int TourId { get; }
        public string DisplayName { get; }

        public TourSelectionItem(int tourId, string displayName)
        {
            TourId = tourId;
            DisplayName = displayName;
        }
    }

    public class PromoCodeDisplayItem
    {
        public PromoCode PromoCode { get; init; } = new();
        public List<int> ScopedTourIds { get; init; } = new();
        public int UsedCount { get; init; }

        public int Id => PromoCode.Id;
        public string Code => PromoCode.Code;
        public string DiscountSummary => PromoCode.DiscountType == PromoDiscountTypes.Percent
            ? $"{PromoCode.DiscountValue:0.##}%"
            : $"{PromoCode.DiscountValue:N0} đ";
        public string DateRange => $"{PromoCode.StartDate:dd/MM/yyyy HH:mm} - {PromoCode.EndDate:dd/MM/yyyy HH:mm}";
        public string StatusLabel => PromoCodeService.ResolveStatus(PromoCode, DateTime.Now) switch
        {
            PromoCodeStatus.Upcoming => "Sắp diễn ra",
            PromoCodeStatus.Active => "Đang hoạt động",
            PromoCodeStatus.Expired => "Hết hạn",
            PromoCodeStatus.Disabled => "Đã vô hiệu hoá",
            _ => "Không xác định"
        };
        public string StatusColor => PromoCodeService.ResolveStatus(PromoCode, DateTime.Now) switch
        {
            PromoCodeStatus.Upcoming => "#FFB45309",
            PromoCodeStatus.Active => "#FF15803D",
            PromoCodeStatus.Expired => "#FFB91C1C",
            PromoCodeStatus.Disabled => "#FF6B7280",
            _ => "#FF6B7280"
        };
        public string UsageSummary
        {
            get
            {
                if (PromoCode.MaxTotalUses.HasValue)
                {
                    return $"{UsedCount}/{PromoCode.MaxTotalUses.Value}";
                }

                return $"{UsedCount}/∞";
            }
        }
        public string ScopeSummary
        {
            get
            {
                var conditions = new List<string>();

                if (PromoCode.MinOrderAmount > 0)
                {
                    conditions.Add($"Đơn tối thiểu {PromoCode.MinOrderAmount:N0} đ");
                }

                if (!string.IsNullOrWhiteSpace(PromoCode.ApplicableTourType))
                {
                    conditions.Add($"Loại tour: {PromoCode.ApplicableTourType}");
                }

                if (ScopedTourIds.Count > 0)
                {
                    conditions.Add($"Tour chỉ định: {ScopedTourIds.Count}");
                }

                if (PromoCode.OnlyNewCustomers)
                {
                    conditions.Add("Khách mới");
                }

                return conditions.Count == 0 ? "Áp dụng toàn hệ thống" : string.Join(" | ", conditions);
            }
        }

        public static PromoCodeDisplayItem From(PromoCode promoCode, List<int> scopedTourIds, int usedCount)
        {
            return new PromoCodeDisplayItem
            {
                PromoCode = promoCode,
                ScopedTourIds = scopedTourIds,
                UsedCount = usedCount
            };
        }
    }

    public class PromoCodeUsageDisplayItem
    {
        public int BookingId { get; init; }
        public int CustomerId { get; init; }
        public DateTime UsedAt { get; init; }
        public decimal OrderAmount { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal FinalAmount { get; init; }
        public string UsedAtText => UsedAt.ToString("dd/MM/yyyy HH:mm");
        public string OrderAmountText => $"{OrderAmount:N0} đ";
        public string DiscountAmountText => $"-{DiscountAmount:N0} đ";
        public string FinalAmountText => $"{FinalAmount:N0} đ";

        public static PromoCodeUsageDisplayItem From(PromoCodeUsage usage)
        {
            return new PromoCodeUsageDisplayItem
            {
                BookingId = usage.BookingId,
                CustomerId = usage.CustomerId,
                UsedAt = usage.UsedAt,
                OrderAmount = usage.OrderAmount,
                DiscountAmount = usage.DiscountAmount,
                FinalAmount = usage.FinalAmount
            };
        }
    }
}

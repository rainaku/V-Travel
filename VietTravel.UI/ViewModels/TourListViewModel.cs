using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VietTravel.Core.Models;
using VietTravel.Data;
using VietTravel.Data.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class TourListViewModel : PaginatedListViewModelBase<Tour>
    {
        private readonly MainViewModel _mainViewModel;
        private readonly CloudinaryImageService _cloudinaryImageService = new();

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private ObservableCollection<Tour> _tours = new();
        [ObservableProperty] private ObservableCollection<Tour> _filteredTours = new();
        [ObservableProperty] private bool _isLoading = false;

        // Filters
        [ObservableProperty] private ObservableCollection<string> _tourTypes = new() { "Tất cả" };
        [ObservableProperty] private string _selectedTourType = "Tất cả";

        // Form fields
        [ObservableProperty] private string _formName = string.Empty;
        [ObservableProperty] private string _formDescription = string.Empty;
        [ObservableProperty] private string _formDestination = string.Empty;
        [ObservableProperty] private string _formTourType = "Tiêu chuẩn";
        [ObservableProperty] private string _formBasePrice = string.Empty;
        [ObservableProperty] private string _formDurationDays = string.Empty;
        [ObservableProperty] private string _formImageUrl = string.Empty;
        [ObservableProperty] private string _formTransportPlanText = string.Empty;
        [ObservableProperty] private string _formAttractionPlanText = string.Empty;
        [ObservableProperty] private string _formHotelPlanText = string.Empty;
        [ObservableProperty] private bool _isUploadingImage = false;
        [ObservableProperty] private bool _isFormVisible = false;
        [ObservableProperty] private bool _isEditing = false;
        [ObservableProperty] private string _formTitle = "Thêm Tour Mới";

        private Tour? _editingTour;
        private List<Transport> _allTransports = new();
        private List<Hotel> _allHotels = new();
        private List<Attraction> _allAttractions = new();

        public bool HasNoData => !IsLoading && FilteredTours.Count == 0;
        public string UploadImageButtonText => IsUploadingImage ? "Đang tải ảnh..." : "Tải ảnh lên";

        public TourListViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            LoadToursCommand.Execute(null);
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnSelectedTourTypeChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnIsUploadingImageChanged(bool value)
        {
            OnPropertyChanged(nameof(UploadImageButtonText));
            UploadTourImageCommand.NotifyCanExecuteChanged();
        }

        private void ApplyFilter()
        {
            var isSearchEmpty = string.IsNullOrWhiteSpace(SearchText);
            var lower = isSearchEmpty ? string.Empty : SearchText.Trim().ToLowerInvariant();
            var filterType = SelectedTourType == "Tất cả" ? null : SelectedTourType;

            var filtered = Tours.Where(t =>
                    (isSearchEmpty ||
                     (t.Name ?? string.Empty).ToLowerInvariant().Contains(lower) ||
                     (t.Destination ?? string.Empty).ToLowerInvariant().Contains(lower) ||
                     (t.Description ?? string.Empty).ToLowerInvariant().Contains(lower)) &&
                    (filterType == null || t.TourType == filterType))
                .ToList();
            SetPagedItems(filtered, FilteredTours);
            OnPropertyChanged(nameof(HasNoData));
        }

        [RelayCommand]
        private async Task LoadToursAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            OnPropertyChanged(nameof(HasNoData));

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var tourResponse = await client
                    .From<Tour>()
                    .Order(x => x.Id, Postgrest.Constants.Ordering.Descending)
                    .Range(0, 4999)
                    .Get();
                var models = (tourResponse.Models ?? new List<Tour>()).ToList();

                try
                {
                    _allTransports = ((await client.From<Transport>().Range(0, 4999).Get()).Models ?? new List<Transport>()).ToList();
                    _allHotels = ((await client.From<Hotel>().Range(0, 4999).Get()).Models ?? new List<Hotel>()).ToList();
                    _allAttractions = ((await client.From<Attraction>().Range(0, 4999).Get()).Models ?? new List<Attraction>()).ToList();

                    var tourTransports = ((await client.From<TourTransport>().Range(0, 4999).Get()).Models ?? new List<TourTransport>()).ToList();
                    var tourHotels = ((await client.From<TourHotel>().Range(0, 4999).Get()).Models ?? new List<TourHotel>()).ToList();
                    var tourAttractions = ((await client.From<TourAttraction>().Range(0, 4999).Get()).Models ?? new List<TourAttraction>()).ToList();

                    var transportById = _allTransports.ToDictionary(x => x.Id);
                    var hotelById = _allHotels.ToDictionary(x => x.Id);
                    var attractionById = _allAttractions.ToDictionary(x => x.Id);
                    var tourTransportLookup = tourTransports.GroupBy(x => x.TourId).ToDictionary(g => g.Key, g => g.ToList());
                    var tourHotelLookup = tourHotels.GroupBy(x => x.TourId).ToDictionary(g => g.Key, g => g.ToList());
                    var tourAttractionLookup = tourAttractions
                        .GroupBy(x => x.TourId)
                        .ToDictionary(g => g.Key, g => g.OrderBy(x => x.OrderIndex).ToList());

                    foreach (var tour in models)
                    {
                        if (tourTransportLookup.TryGetValue(tour.Id, out var mappedTransports))
                        {
                            foreach (var mapped in mappedTransports)
                            {
                                mapped.Transport = transportById.TryGetValue(mapped.TransportId, out var transport) ? transport : null;
                            }

                            tour.TourTransports = mappedTransports
                                .Where(x => x.Transport != null)
                                .ToList();
                        }
                        else
                        {
                            tour.TourTransports = new List<TourTransport>();
                        }

                        if (tourHotelLookup.TryGetValue(tour.Id, out var mappedHotels))
                        {
                            foreach (var mapped in mappedHotels)
                            {
                                mapped.Hotel = hotelById.TryGetValue(mapped.HotelId, out var hotel) ? hotel : null;
                            }

                            tour.TourHotels = mappedHotels
                                .Where(x => x.Hotel != null)
                                .ToList();
                        }
                        else
                        {
                            tour.TourHotels = new List<TourHotel>();
                        }

                        if (tourAttractionLookup.TryGetValue(tour.Id, out var mappedAttractions))
                        {
                            foreach (var mapped in mappedAttractions)
                            {
                                mapped.Attraction = attractionById.TryGetValue(mapped.AttractionId, out var attraction) ? attraction : null;
                            }

                            tour.TourAttractions = mappedAttractions
                                .Where(x => x.Attraction != null)
                                .ToList();
                        }
                        else
                        {
                            tour.TourAttractions = new List<TourAttraction>();
                        }
                    }
                }
                catch
                {
                    _allTransports = new List<Transport>();
                    _allHotels = new List<Hotel>();
                    _allAttractions = new List<Attraction>();

                    foreach (var tour in models)
                    {
                        tour.TourTransports = new List<TourTransport>();
                        tour.TourHotels = new List<TourHotel>();
                        tour.TourAttractions = new List<TourAttraction>();
                    }
                }

                Tours.Clear();
                foreach (var tour in models)
                {
                    Tours.Add(tour);
                }

                var distinctTypes = models.Where(t => !string.IsNullOrWhiteSpace(t.TourType)).Select(t => t.TourType!).Distinct().OrderBy(t => t).ToList();
                var currentSelected = SelectedTourType;
                TourTypes.Clear();
                TourTypes.Add("Tất cả");
                foreach (var type in distinctTypes)
                {
                    TourTypes.Add(type);
                }
                if (TourTypes.Contains(currentSelected))
                    SelectedTourType = currentSelected;
                else
                    SelectedTourType = "Tất cả";

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách tour: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoData));
            }
        }

        [RelayCommand]
        private void ShowAddForm()
        {
            FormTitle = "Thêm Tour Mới";
            FormName = string.Empty;
            FormDescription = string.Empty;
            FormDestination = string.Empty;
            FormTourType = "Tiêu chuẩn";
            FormBasePrice = string.Empty;
            FormDurationDays = string.Empty;
            FormImageUrl = string.Empty;
            FormTransportPlanText = string.Empty;
            FormAttractionPlanText = string.Empty;
            FormHotelPlanText = string.Empty;
            IsEditing = false;
            _editingTour = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void ShowEditForm(Tour tour)
        {
            if (tour == null) return;
            FormTitle = "Chỉnh sửa Tour";
            FormName = tour.Name;
            FormDescription = tour.Description;
            FormDestination = tour.Destination;
            FormTourType = string.IsNullOrWhiteSpace(tour.TourType) ? "Tiêu chuẩn" : tour.TourType;
            FormBasePrice = tour.BasePrice.ToString("0");
            FormDurationDays = tour.DurationDays.ToString();
            FormImageUrl = tour.ImageUrl;
            FormTransportPlanText = BuildTransportPlanText(tour.TourTransports);
            FormAttractionPlanText = BuildAttractionPlanText(tour.TourAttractions);
            FormHotelPlanText = BuildHotelPlanText(tour.TourHotels);
            IsEditing = true;
            _editingTour = tour;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void CancelForm()
        {
            IsFormVisible = false;
        }

        [RelayCommand(CanExecute = nameof(CanUploadTourImage))]
        private async Task UploadTourImageAsync()
        {
            var fileDialog = new OpenFileDialog
            {
                Title = "Chọn ảnh tour",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp",
                CheckFileExists = true,
                Multiselect = false
            };

            if (fileDialog.ShowDialog() != true)
            {
                return;
            }

            IsUploadingImage = true;
            try
            {
                var tourKey = IsEditing && _editingTour != null
                    ? _editingTour.Id.ToString(CultureInfo.InvariantCulture)
                    : $"draft-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                FormImageUrl = await _cloudinaryImageService.UploadTourImageAsync(fileDialog.FileName, tourKey);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải ảnh tour: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsUploadingImage = false;
            }
        }

        private bool CanUploadTourImage()
        {
            return !IsUploadingImage;
        }

        [RelayCommand]
        private async Task SaveTourAsync()
        {
            // Validate
            var name = FormName.Trim();
            var destination = FormDestination.Trim();
            var tourType = string.IsNullOrWhiteSpace(FormTourType) ? "Tiêu chuẩn" : FormTourType.Trim();
            var description = FormDescription.Trim();
            var imageUrl = FormImageUrl.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Vui lòng nhập tên tour.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(destination))
            {
                MessageBox.Show("Vui lòng nhập điểm đến.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!TryParsePositiveMoney(FormBasePrice, out var price))
            {
                MessageBox.Show("Giá cơ bản phải là số dương.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(FormDurationDays.Trim(), out var days) || days <= 0)
            {
                MessageBox.Show("Số ngày phải là số nguyên dương.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!string.IsNullOrWhiteSpace(imageUrl) && !Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            {
                MessageBox.Show("URL ảnh tour không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!TryParseTransportPlan(FormTransportPlanText, out var transportPlanItems, out var transportError))
            {
                MessageBox.Show(transportError, "Lỗi dữ liệu phương tiện", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!TryParseAttractionPlan(FormAttractionPlanText, out var attractionPlanItems, out var attractionError))
            {
                MessageBox.Show(attractionError, "Lỗi dữ liệu địa danh", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!TryParseHotelPlan(FormHotelPlanText, out var hotelPlanItems, out var hotelError))
            {
                MessageBox.Show(hotelError, "Lỗi dữ liệu khách sạn", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var targetTourId = 0;

                if (IsEditing && _editingTour != null)
                {
                    _editingTour.Name = name;
                    _editingTour.Description = description;
                    _editingTour.Destination = destination;
                    _editingTour.TourType = tourType;
                    _editingTour.BasePrice = price;
                    _editingTour.DurationDays = days;
                    _editingTour.ImageUrl = imageUrl;
                    await client.From<Tour>().Update(_editingTour);
                    targetTourId = _editingTour.Id;
                }
                else
                {
                    var newTour = new Tour
                    {
                        Name = name,
                        Description = description,
                        Destination = destination,
                        TourType = tourType,
                        BasePrice = price,
                        DurationDays = days,
                        ImageUrl = imageUrl
                    };

                    var insertResponse = await client.From<Tour>().Insert(newTour);
                    targetTourId = insertResponse.Models.FirstOrDefault()?.Id ?? 0;
                    if (targetTourId <= 0)
                    {
                        var lookupResponse = await client
                            .From<Tour>()
                            .Order(x => x.Id, Postgrest.Constants.Ordering.Descending)
                            .Range(0, 99)
                            .Get();
                        targetTourId = (lookupResponse.Models ?? new List<Tour>())
                            .FirstOrDefault(x =>
                                string.Equals(x.Name, newTour.Name, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(x.Destination, newTour.Destination, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
                    }
                }

                if (targetTourId <= 0)
                {
                    throw new InvalidOperationException("Không xác định được tour vừa lưu để đồng bộ phương tiện/địa danh/khách sạn.");
                }

                await SyncTourResourcesAsync(
                    client,
                    targetTourId,
                    transportPlanItems,
                    hotelPlanItems,
                    attractionPlanItems);

                IsFormVisible = false;
                SearchText = string.Empty;
                await LoadToursAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu tour: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SyncTourResourcesAsync(
            Supabase.Client client,
            int tourId,
            IReadOnlyList<TransportPlanItem> transportPlanItems,
            IReadOnlyList<HotelPlanItem> hotelPlanItems,
            IReadOnlyList<AttractionPlanItem> attractionPlanItems)
        {
            var mappedTransports = await UpsertTransportsAsync(client, transportPlanItems);
            var mappedHotels = await UpsertHotelsAsync(client, hotelPlanItems);
            var mappedAttractions = await UpsertAttractionsAsync(client, attractionPlanItems);

            await client.From<TourTransport>().Where(x => x.TourId == tourId).Delete();
            await client.From<TourHotel>().Where(x => x.TourId == tourId).Delete();
            await client.From<TourAttraction>().Where(x => x.TourId == tourId).Delete();

            foreach (var transport in mappedTransports)
            {
                await client.From<TourTransport>().Insert(new TourTransport
                {
                    TourId = tourId,
                    TransportId = transport.Id,
                    Notes = string.Empty
                });
            }

            foreach (var hotel in mappedHotels)
            {
                await client.From<TourHotel>().Insert(new TourHotel
                {
                    TourId = tourId,
                    HotelId = hotel.Id,
                    Nights = 1,
                    Notes = string.Empty
                });
            }

            var orderIndex = 0;
            foreach (var attraction in mappedAttractions)
            {
                await client.From<TourAttraction>().Insert(new TourAttraction
                {
                    TourId = tourId,
                    AttractionId = attraction.Id,
                    OrderIndex = orderIndex++,
                    Notes = string.Empty
                });
            }
        }

        private async Task<List<Transport>> UpsertTransportsAsync(
            Supabase.Client client,
            IReadOnlyList<TransportPlanItem> planItems)
        {
            var existing = _allTransports.Any()
                ? _allTransports.ToList()
                : ((await client.From<Transport>().Range(0, 4999).Get()).Models ?? new List<Transport>()).ToList();
            var existingByName = existing
                .GroupBy(x => NormalizeKey(x.Name))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());
            var resolved = new List<Transport>();

            foreach (var item in planItems)
            {
                var key = NormalizeKey(item.Name);
                if (existingByName.TryGetValue(key, out var transport))
                {
                    var shouldUpdate =
                        !string.Equals(transport.Type, item.Type, StringComparison.Ordinal) ||
                        transport.Capacity != item.Capacity ||
                        transport.Cost != item.Cost ||
                        !string.Equals(transport.Status, "Hoạt động", StringComparison.Ordinal);

                    if (shouldUpdate)
                    {
                        transport.Type = item.Type;
                        transport.Capacity = item.Capacity;
                        transport.Cost = item.Cost;
                        transport.Status = "Hoạt động";
                        await client.From<Transport>().Update(transport);
                    }

                    resolved.Add(transport);
                    continue;
                }

                var created = new Transport
                {
                    Name = item.Name,
                    Type = item.Type,
                    Capacity = item.Capacity,
                    Cost = item.Cost,
                    Status = "Hoạt động"
                };

                var insertResponse = await client.From<Transport>().Insert(created);
                var inserted = insertResponse.Models.FirstOrDefault() ?? created;
                if (inserted.Id <= 0)
                {
                    var lookup = await client.From<Transport>()
                        .Where(x => x.Name == created.Name)
                        .Order(x => x.Id, Postgrest.Constants.Ordering.Descending)
                        .Range(0, 0)
                        .Get();
                    inserted = lookup.Models.FirstOrDefault() ?? inserted;
                }

                existingByName[key] = inserted;
                resolved.Add(inserted);
            }

            _allTransports = existingByName.Values.ToList();
            return resolved;
        }

        private async Task<List<Hotel>> UpsertHotelsAsync(
            Supabase.Client client,
            IReadOnlyList<HotelPlanItem> planItems)
        {
            var existing = _allHotels.Any()
                ? _allHotels.ToList()
                : ((await client.From<Hotel>().Range(0, 4999).Get()).Models ?? new List<Hotel>()).ToList();
            var existingByName = existing
                .GroupBy(x => NormalizeKey(x.Name))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());
            var resolved = new List<Hotel>();

            foreach (var item in planItems)
            {
                var key = NormalizeKey(item.Name);
                if (existingByName.TryGetValue(key, out var hotel))
                {
                    var shouldUpdate =
                        !string.Equals(hotel.Address, item.Address, StringComparison.Ordinal) ||
                        hotel.StarRating != item.StarRating ||
                        hotel.CostPerNight != item.CostPerNight ||
                        !string.Equals(hotel.Status, "Hoạt động", StringComparison.Ordinal);

                    if (shouldUpdate)
                    {
                        hotel.Address = item.Address;
                        hotel.StarRating = item.StarRating;
                        hotel.CostPerNight = item.CostPerNight;
                        hotel.Status = "Hoạt động";
                        await client.From<Hotel>().Update(hotel);
                    }

                    resolved.Add(hotel);
                    continue;
                }

                var created = new Hotel
                {
                    Name = item.Name,
                    Address = item.Address,
                    StarRating = item.StarRating,
                    CostPerNight = item.CostPerNight,
                    Status = "Hoạt động"
                };

                var insertResponse = await client.From<Hotel>().Insert(created);
                var inserted = insertResponse.Models.FirstOrDefault() ?? created;
                if (inserted.Id <= 0)
                {
                    var lookup = await client.From<Hotel>()
                        .Where(x => x.Name == created.Name)
                        .Order(x => x.Id, Postgrest.Constants.Ordering.Descending)
                        .Range(0, 0)
                        .Get();
                    inserted = lookup.Models.FirstOrDefault() ?? inserted;
                }

                existingByName[key] = inserted;
                resolved.Add(inserted);
            }

            _allHotels = existingByName.Values.ToList();
            return resolved;
        }

        private async Task<List<Attraction>> UpsertAttractionsAsync(
            Supabase.Client client,
            IReadOnlyList<AttractionPlanItem> planItems)
        {
            var existing = _allAttractions.Any()
                ? _allAttractions.ToList()
                : ((await client.From<Attraction>().Range(0, 4999).Get()).Models ?? new List<Attraction>()).ToList();
            var existingByName = existing
                .GroupBy(x => NormalizeKey(x.Name))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());
            var resolved = new List<Attraction>();

            foreach (var item in planItems)
            {
                var key = NormalizeKey(item.Name);
                if (existingByName.TryGetValue(key, out var attraction))
                {
                    var shouldUpdate =
                        !string.Equals(attraction.Address, item.Address, StringComparison.Ordinal) ||
                        attraction.TicketPrice != item.TicketPrice ||
                        !string.Equals(attraction.Status, "Hoạt động", StringComparison.Ordinal);

                    if (shouldUpdate)
                    {
                        attraction.Address = item.Address;
                        attraction.TicketPrice = item.TicketPrice;
                        attraction.Status = "Hoạt động";
                        await client.From<Attraction>().Update(attraction);
                    }

                    resolved.Add(attraction);
                    continue;
                }

                var created = new Attraction
                {
                    Name = item.Name,
                    Address = item.Address,
                    TicketPrice = item.TicketPrice,
                    Status = "Hoạt động"
                };

                var insertResponse = await client.From<Attraction>().Insert(created);
                var inserted = insertResponse.Models.FirstOrDefault() ?? created;
                if (inserted.Id <= 0)
                {
                    var lookup = await client.From<Attraction>()
                        .Where(x => x.Name == created.Name)
                        .Order(x => x.Id, Postgrest.Constants.Ordering.Descending)
                        .Range(0, 0)
                        .Get();
                    inserted = lookup.Models.FirstOrDefault() ?? inserted;
                }

                existingByName[key] = inserted;
                resolved.Add(inserted);
            }

            _allAttractions = existingByName.Values.ToList();
            return resolved;
        }

        private static string BuildTransportPlanText(IEnumerable<TourTransport> tourTransports)
        {
            if (tourTransports == null)
            {
                return string.Empty;
            }

            return string.Join(
                Environment.NewLine,
                tourTransports
                    .Where(x => x.Transport != null)
                    .Select(x =>
                        $"{x.Transport!.Name} | {x.Transport.Type} | {Math.Max(0, x.Transport.Capacity)} | {x.Transport.Cost:0}"));
        }

        private static string BuildHotelPlanText(IEnumerable<TourHotel> tourHotels)
        {
            if (tourHotels == null)
            {
                return string.Empty;
            }

            return string.Join(
                Environment.NewLine,
                tourHotels
                    .Where(x => x.Hotel != null)
                    .Select(x =>
                        $"{x.Hotel!.Name} | {x.Hotel.Address} | {Math.Clamp(x.Hotel.StarRating, 1, 5)} | {x.Hotel.CostPerNight:0}"));
        }

        private static string BuildAttractionPlanText(IEnumerable<TourAttraction> tourAttractions)
        {
            if (tourAttractions == null)
            {
                return string.Empty;
            }

            return string.Join(
                Environment.NewLine,
                tourAttractions
                    .OrderBy(x => x.OrderIndex)
                    .Where(x => x.Attraction != null)
                    .Select(x => $"{x.Attraction!.Name} | {x.Attraction.Address} | {x.Attraction.TicketPrice:0}"));
        }

        private static bool TryParseTransportPlan(
            string raw,
            out List<TransportPlanItem> items,
            out string errorMessage)
        {
            items = new List<TransportPlanItem>();
            errorMessage = string.Empty;
            var lines = (raw ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split('|').Select(x => x.Trim()).ToArray();
                var name = parts.ElementAtOrDefault(0) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    errorMessage = $"Dòng {i + 1}: phương tiện thiếu tên. Định dạng: Tên | Loại | Sức chứa | Chi phí";
                    return false;
                }

                var type = parts.ElementAtOrDefault(1);
                if (string.IsNullOrWhiteSpace(type))
                {
                    type = "Khác";
                }

                var capacityRaw = parts.ElementAtOrDefault(2) ?? "0";
                if (!TryParseNonNegativeInt(capacityRaw, out var capacity))
                {
                    errorMessage = $"Dòng {i + 1}: sức chứa không hợp lệ.";
                    return false;
                }

                var costRaw = parts.ElementAtOrDefault(3) ?? "0";
                if (!TryParseNonNegativeDecimal(costRaw, out var cost))
                {
                    errorMessage = $"Dòng {i + 1}: chi phí phương tiện không hợp lệ.";
                    return false;
                }

                items.Add(new TransportPlanItem(name, type, capacity, cost));
            }

            items = items
                .GroupBy(x => NormalizeKey(x.Name))
                .Select(g => g.Last())
                .ToList();
            return true;
        }

        private static bool TryParseHotelPlan(
            string raw,
            out List<HotelPlanItem> items,
            out string errorMessage)
        {
            items = new List<HotelPlanItem>();
            errorMessage = string.Empty;
            var lines = (raw ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split('|').Select(x => x.Trim()).ToArray();
                var name = parts.ElementAtOrDefault(0) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    errorMessage = $"Dòng {i + 1}: khách sạn thiếu tên. Định dạng: Tên | Địa chỉ | Sao | Giá/đêm";
                    return false;
                }

                var address = parts.ElementAtOrDefault(1);
                if (string.IsNullOrWhiteSpace(address))
                {
                    address = "Đang cập nhật";
                }

                var starRaw = parts.ElementAtOrDefault(2) ?? "3";
                if (!TryParseNonNegativeInt(starRaw, out var star))
                {
                    errorMessage = $"Dòng {i + 1}: số sao khách sạn không hợp lệ.";
                    return false;
                }

                star = Math.Clamp(star, 1, 5);

                var costRaw = parts.ElementAtOrDefault(3) ?? "0";
                if (!TryParseNonNegativeDecimal(costRaw, out var costPerNight))
                {
                    errorMessage = $"Dòng {i + 1}: giá khách sạn không hợp lệ.";
                    return false;
                }

                items.Add(new HotelPlanItem(name, address, star, costPerNight));
            }

            items = items
                .GroupBy(x => NormalizeKey(x.Name))
                .Select(g => g.Last())
                .ToList();
            return true;
        }

        private static bool TryParseAttractionPlan(
            string raw,
            out List<AttractionPlanItem> items,
            out string errorMessage)
        {
            items = new List<AttractionPlanItem>();
            errorMessage = string.Empty;
            var lines = (raw ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split('|').Select(x => x.Trim()).ToArray();
                var name = parts.ElementAtOrDefault(0) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    errorMessage = $"Dòng {i + 1}: địa danh thiếu tên. Định dạng: Tên | Địa chỉ | Giá vé";
                    return false;
                }

                var address = parts.ElementAtOrDefault(1);
                if (string.IsNullOrWhiteSpace(address))
                {
                    address = "Đang cập nhật";
                }

                var ticketRaw = parts.ElementAtOrDefault(2) ?? "0";
                if (!TryParseNonNegativeDecimal(ticketRaw, out var ticketPrice))
                {
                    errorMessage = $"Dòng {i + 1}: giá vé địa danh không hợp lệ.";
                    return false;
                }

                items.Add(new AttractionPlanItem(name, address, ticketPrice));
            }

            items = items
                .GroupBy(x => NormalizeKey(x.Name))
                .Select(g => g.Last())
                .ToList();
            return true;
        }

        private static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool TryParseNonNegativeInt(string rawInput, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return true;
            }

            var cleaned = rawInput
                .Trim()
                .Replace(" ", string.Empty)
                .Replace(".", string.Empty)
                .Replace(",", string.Empty);

            return int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
        }

        private static bool TryParseNonNegativeDecimal(string rawInput, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return true;
            }

            var cleaned = rawInput
                .Trim()
                .Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("vnd", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty)
                .Replace(".", string.Empty)
                .Replace(",", string.Empty);

            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0;
        }

        private readonly record struct TransportPlanItem(string Name, string Type, int Capacity, decimal Cost);
        private readonly record struct HotelPlanItem(string Name, string Address, int StarRating, decimal CostPerNight);
        private readonly record struct AttractionPlanItem(string Name, string Address, decimal TicketPrice);

        private static bool TryParsePositiveMoney(string rawInput, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return false;
            }

            var cleaned = rawInput
                .Trim()
                .Replace("đ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("vnd", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty)
                .Replace(".", string.Empty)
                .Replace(",", string.Empty);

            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value > 0;
        }

        [RelayCommand]
        private async Task DeleteTourAsync(Tour tour)
        {
            if (tour == null) return;

            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa tour \"{tour.Name}\"?\nHành động này không thể hoàn tác.",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                await client.From<Tour>().Where(t => t.Id == tour.Id).Delete();
                await LoadToursAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi xóa tour: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

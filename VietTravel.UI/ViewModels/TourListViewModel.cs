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
    public partial class TourListViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly CloudinaryImageService _cloudinaryImageService = new();

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private ObservableCollection<Tour> _tours = new();
        [ObservableProperty] private ObservableCollection<Tour> _filteredTours = new();
        [ObservableProperty] private bool _isLoading = false;

        // Form fields
        [ObservableProperty] private string _formName = string.Empty;
        [ObservableProperty] private string _formDescription = string.Empty;
        [ObservableProperty] private string _formDestination = string.Empty;
        [ObservableProperty] private string _formBasePrice = string.Empty;
        [ObservableProperty] private string _formDurationDays = string.Empty;
        [ObservableProperty] private string _formImageUrl = string.Empty;
        [ObservableProperty] private bool _isUploadingImage = false;
        [ObservableProperty] private bool _isFormVisible = false;
        [ObservableProperty] private bool _isEditing = false;
        [ObservableProperty] private string _formTitle = "Thêm Tour Mới";

        private Tour? _editingTour;

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

        partial void OnIsUploadingImageChanged(bool value)
        {
            OnPropertyChanged(nameof(UploadImageButtonText));
            UploadTourImageCommand.NotifyCanExecuteChanged();
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredTours = new ObservableCollection<Tour>(Tours);
            }
            else
            {
                var lower = SearchText.ToLower();
                FilteredTours = new ObservableCollection<Tour>(
                    Tours.Where(t =>
                        t.Name.ToLower().Contains(lower) ||
                        t.Destination.ToLower().Contains(lower) ||
                        t.Description.ToLower().Contains(lower))
                );
            }
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
                var response = await client.From<Tour>().Get();
                var models = response.Models ?? new List<Tour>();

                Tours.Clear();
                foreach (var tour in models)
                {
                    Tours.Add(tour);
                }
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
            FormBasePrice = string.Empty;
            FormDurationDays = string.Empty;
            FormImageUrl = string.Empty;
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
            FormBasePrice = tour.BasePrice.ToString("0");
            FormDurationDays = tour.DurationDays.ToString();
            FormImageUrl = tour.ImageUrl;
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

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                if (IsEditing && _editingTour != null)
                {
                    _editingTour.Name = name;
                    _editingTour.Description = description;
                    _editingTour.Destination = destination;
                    _editingTour.BasePrice = price;
                    _editingTour.DurationDays = days;
                    _editingTour.ImageUrl = imageUrl;
                    await client.From<Tour>().Update(_editingTour);
                }
                else
                {
                    var newTour = new Tour
                    {
                        Name = name,
                        Description = description,
                        Destination = destination,
                        BasePrice = price,
                        DurationDays = days,
                        ImageUrl = imageUrl
                    };

                    await client.From<Tour>().Insert(newTour);
                }

                IsFormVisible = false;
                await LoadToursAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu tour: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

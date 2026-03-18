using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VietTravel.Core.Models;
using VietTravel.Data;

namespace VietTravel.UI.ViewModels
{
    public partial class TourListViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

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
        [ObservableProperty] private bool _isFormVisible = false;
        [ObservableProperty] private bool _isEditing = false;
        [ObservableProperty] private string _formTitle = "Thêm Tour Mới";

        private Tour? _editingTour;

        public bool HasNoData => !IsLoading && FilteredTours.Count == 0;

        public TourListViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            LoadToursCommand.Execute(null);
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
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

                Tours.Clear();
                foreach (var tour in response.Models)
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
            IsEditing = true;
            _editingTour = tour;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void CancelForm()
        {
            IsFormVisible = false;
        }

        [RelayCommand]
        private async Task SaveTourAsync()
        {
            // Validate
            if (string.IsNullOrWhiteSpace(FormName))
            {
                MessageBox.Show("Vui lòng nhập tên tour.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(FormDestination))
            {
                MessageBox.Show("Vui lòng nhập điểm đến.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!decimal.TryParse(FormBasePrice, out decimal price) || price <= 0)
            {
                MessageBox.Show("Giá cơ bản phải là số dương.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(FormDurationDays, out int days) || days <= 0)
            {
                MessageBox.Show("Số ngày phải là số nguyên dương.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                if (IsEditing && _editingTour != null)
                {
                    _editingTour.Name = FormName;
                    _editingTour.Description = FormDescription;
                    _editingTour.Destination = FormDestination;
                    _editingTour.BasePrice = price;
                    _editingTour.DurationDays = days;
                    await client.From<Tour>().Update(_editingTour);
                }
                else
                {
                    var newTour = new Tour
                    {
                        Name = FormName,
                        Description = FormDescription,
                        Destination = FormDestination,
                        BasePrice = price,
                        DurationDays = days
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

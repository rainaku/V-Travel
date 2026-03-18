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
    public partial class DepartureListViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private ObservableCollection<Departure> _departures = new();
        [ObservableProperty] private ObservableCollection<Departure> _filteredDepartures = new();
        [ObservableProperty] private ObservableCollection<Tour> _availableTours = new();
        [ObservableProperty] private bool _isLoading = false;

        // Form
        [ObservableProperty] private bool _isFormVisible = false;
        [ObservableProperty] private bool _isEditing = false;
        [ObservableProperty] private string _formTitle = "Thêm Lịch Khởi Hành";
        [ObservableProperty] private Tour? _formSelectedTour;
        [ObservableProperty] private DateTime _formStartDate = DateTime.Now.AddDays(7);
        [ObservableProperty] private string _formMaxSlots = string.Empty;
        [ObservableProperty] private string _formAvailableSlots = string.Empty;
        [ObservableProperty] private string _formStatus = "Mở bán";

        private Departure? _editingDeparture;

        public bool HasNoData => !IsLoading && FilteredDepartures.Count == 0;

        public DepartureListViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = LoadDataAsync();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredDepartures = new ObservableCollection<Departure>(Departures);
            }
            else
            {
                var lower = SearchText.ToLower();
                FilteredDepartures = new ObservableCollection<Departure>(
                    Departures.Where(d =>
                        (d.Tour?.Name?.ToLower().Contains(lower) ?? false) ||
                        d.StartDate.ToString("dd/MM/yyyy").Contains(lower) ||
                        d.Status.ToLower().Contains(lower))
                );
            }
            OnPropertyChanged(nameof(HasNoData));
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            OnPropertyChanged(nameof(HasNoData));
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();

                var toursResponse = await client.From<Tour>().Get();
                AvailableTours = new ObservableCollection<Tour>(toursResponse.Models);

                var response = await client.From<Departure>().Get();
                Departures.Clear();

                foreach (var dep in response.Models)
                {
                    dep.Tour = AvailableTours.FirstOrDefault(t => t.Id == dep.TourId);
                    Departures.Add(dep);
                }
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải lịch khởi hành: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
            FormTitle = "Thêm Lịch Khởi Hành";
            FormSelectedTour = null;
            FormStartDate = DateTime.Now.AddDays(7);
            FormMaxSlots = string.Empty;
            FormAvailableSlots = string.Empty;
            FormStatus = "Mở bán";
            IsEditing = false;
            _editingDeparture = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void ShowEditForm(Departure departure)
        {
            if (departure == null) return;
            FormTitle = "Chỉnh sửa Lịch Khởi Hành";
            FormSelectedTour = AvailableTours.FirstOrDefault(t => t.Id == departure.TourId);
            FormStartDate = departure.StartDate;
            FormMaxSlots = departure.MaxSlots.ToString();
            FormAvailableSlots = departure.AvailableSlots.ToString();
            FormStatus = departure.Status;
            IsEditing = true;
            _editingDeparture = departure;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void CancelForm() => IsFormVisible = false;

        [RelayCommand]
        private async Task SaveDepartureAsync()
        {
            if (FormSelectedTour == null)
            {
                MessageBox.Show("Vui lòng chọn tour.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(FormMaxSlots, out int maxSlots) || maxSlots <= 0)
            {
                MessageBox.Show("Tổng chỗ phải là số nguyên dương.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(FormAvailableSlots, out int availSlots) || availSlots < 0)
            {
                MessageBox.Show("Chỗ còn lại phải là số >= 0.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                if (IsEditing && _editingDeparture != null)
                {
                    _editingDeparture.TourId = FormSelectedTour.Id;
                    _editingDeparture.StartDate = FormStartDate;
                    _editingDeparture.MaxSlots = maxSlots;
                    _editingDeparture.AvailableSlots = availSlots;
                    _editingDeparture.Status = FormStatus;
                    _editingDeparture.Tour = null; // Clear reference for update
                    await client.From<Departure>().Update(_editingDeparture);
                }
                else
                {
                    var dep = new Departure
                    {
                        TourId = FormSelectedTour.Id,
                        StartDate = FormStartDate,
                        MaxSlots = maxSlots,
                        AvailableSlots = availSlots,
                        Status = FormStatus
                    };
                    await client.From<Departure>().Insert(dep);
                }
                IsFormVisible = false;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu lịch khởi hành: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteDepartureAsync(Departure departure)
        {
            if (departure == null) return;
            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa lịch khởi hành này?",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                await client.From<Departure>().Where(d => d.Id == departure.Id).Delete();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi xóa: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

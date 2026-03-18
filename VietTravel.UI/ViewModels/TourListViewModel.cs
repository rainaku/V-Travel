using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
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
        
        [ObservableProperty] private ObservableCollection<Tour> _tours = new ObservableCollection<Tour>();
        [ObservableProperty] private bool _isLoading = false;
        
        // Compute properties for UI
        public bool HasNoData => !IsLoading && Tours.Count == 0;

        public TourListViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            LoadToursCommand.Execute(null);
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
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách tour: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoData));
            }
        }
    }
}

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
    public partial class PaymentListViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private ObservableCollection<Payment> _payments = new();
        [ObservableProperty] private ObservableCollection<Payment> _filteredPayments = new();
        [ObservableProperty] private bool _isLoading = false;

        // Stats
        [ObservableProperty] private int _unpaidCount = 0;
        [ObservableProperty] private int _depositCount = 0;
        [ObservableProperty] private int _paidCount = 0;

        public bool HasNoData => !IsLoading && FilteredPayments.Count == 0;

        public PaymentListViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = LoadDataAsync();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredPayments = new ObservableCollection<Payment>(Payments);
            }
            else
            {
                var lower = SearchText.ToLower();
                FilteredPayments = new ObservableCollection<Payment>(
                    Payments.Where(p =>
                        p.Status.ToLower().Contains(lower) ||
                        p.PaymentMethod.ToLower().Contains(lower) ||
                        p.BookingId.ToString().Contains(lower))
                );
            }
            OnPropertyChanged(nameof(HasNoData));
        }

        private void UpdateStats()
        {
            UnpaidCount = Payments.Count(p => p.Status == "Chưa thanh toán");
            DepositCount = Payments.Count(p => p.Status == "Đã cọc");
            PaidCount = Payments.Count(p => p.Status == "Đã thanh toán");
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
                var response = await client.From<Payment>().Get();
                Payments.Clear();
                foreach (var p in response.Models) Payments.Add(p);
                ApplyFilter();
                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải thanh toán: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoData));
            }
        }

        [RelayCommand]
        private async Task MarkAsDepositAsync(Payment payment)
        {
            if (payment == null) return;
            try
            {
                payment.Status = "Đã cọc";
                payment.Booking = null;
                var client = await SupabaseClientFactory.GetClientAsync();
                await client.From<Payment>().Update(payment);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task MarkAsPaidAsync(Payment payment)
        {
            if (payment == null) return;
            try
            {
                payment.Status = "Đã thanh toán";
                payment.PaidAmount = payment.TotalAmount;
                payment.PaymentDate = DateTime.Now;
                payment.Booking = null;
                var client = await SupabaseClientFactory.GetClientAsync();
                await client.From<Payment>().Update(payment);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VietTravel.Core.Models;
using VietTravel.Data;
using VietTravel.UI.Services;

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
            UnpaidCount = Payments.Count(p => p.Status == "Chưa thanh toán" || p.Status == "Đợi xác nhận");
            DepositCount = Payments.Count(p => p.Status == "Đã cọc");
            PaidCount = Payments.Count(p => p.Status == "Đã thanh toán đủ" || p.Status == "Đã thanh toán");
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
                var sortedPayments = response.Models
                    .OrderByDescending(p => p.Id)
                    .ToList();
                Payments.Clear();
                foreach (var p in sortedPayments) Payments.Add(p);
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
                var previousStatus = payment.Status;
                if (payment.Status == "Đã thanh toán đủ" || payment.Status == "Đã thanh toán")
                {
                    return;
                }

                if (payment.Status == "Đợi xác nhận")
                {
                    MessageBox.Show(
                        "Khoản thanh toán này đang ở trạng thái đợi xác nhận. Vui lòng dùng nút xác nhận thanh toán đủ.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                payment.Status = "Đã cọc";
                if (payment.PaidAmount <= 0)
                {
                    payment.PaidAmount = Math.Round(payment.TotalAmount * 0.3m, 0);
                }
                payment.PaymentDate = DateTime.Now;
                payment.Booking = null;

                var client = await SupabaseClientFactory.GetClientAsync();
                await client.From<Payment>().Update(payment);
                var booking = await UpdateBookingStatusAsync(client, payment.BookingId, "Chờ xử lý");
                await NotificationCenterService.Instance.NotifyPaymentStatusChangedAsync(payment, previousStatus, booking?.UserId);
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
                if (payment.Status == "Đã thanh toán đủ" || payment.Status == "Đã thanh toán")
                {
                    return;
                }

                var previousStatus = payment.Status;
                payment.Status = "Đã thanh toán đủ";
                payment.PaidAmount = payment.TotalAmount;
                payment.PaymentDate = DateTime.Now;
                payment.Booking = null;

                var client = await SupabaseClientFactory.GetClientAsync();
                await client.From<Payment>().Update(payment);
                var booking = await UpdateBookingStatusAsync(client, payment.BookingId, "Đã xác nhận");
                await NotificationCenterService.Instance.NotifyPaymentStatusChangedAsync(payment, previousStatus, booking?.UserId);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static async Task<Booking?> UpdateBookingStatusAsync(Supabase.Client client, int bookingId, string targetStatus)
        {
            var bookingResp = await client.From<Booking>().Get();
            var booking = bookingResp.Models.FirstOrDefault(b => b.Id == bookingId);
            if (booking == null) return null;
            if (booking.Status == "Đã hủy" || booking.Status == "Hủy") return booking;
            if (booking.Status == targetStatus) return booking;

            booking.Status = targetStatus;
            booking.Customer = null;
            booking.Departure = null;
            booking.User = null;
            await client.From<Booking>().Update(booking);
            return booking;
        }
    }
}

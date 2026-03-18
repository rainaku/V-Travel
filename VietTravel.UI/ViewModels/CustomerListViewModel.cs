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
    public partial class CustomerListViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private ObservableCollection<Customer> _customers = new();
        [ObservableProperty] private ObservableCollection<Customer> _filteredCustomers = new();
        [ObservableProperty] private bool _isLoading = false;

        // Stats
        [ObservableProperty] private int _totalCustomers = 0;
        [ObservableProperty] private int _newThisMonth = 0;

        // Form
        [ObservableProperty] private bool _isFormVisible = false;
        [ObservableProperty] private bool _isEditing = false;
        [ObservableProperty] private string _formTitle = "Thêm Khách Hàng";
        [ObservableProperty] private string _formFullName = string.Empty;
        [ObservableProperty] private string _formPhone = string.Empty;
        [ObservableProperty] private string _formEmail = string.Empty;
        [ObservableProperty] private string _formAddress = string.Empty;

        private Customer? _editingCustomer;

        public bool HasNoData => !IsLoading && FilteredCustomers.Count == 0;

        public CustomerListViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = LoadDataAsync();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredCustomers = new ObservableCollection<Customer>(Customers);
            }
            else
            {
                var lower = SearchText.ToLower();
                FilteredCustomers = new ObservableCollection<Customer>(
                    Customers.Where(c =>
                        c.FullName.ToLower().Contains(lower) ||
                        c.PhoneNumber.ToLower().Contains(lower) ||
                        c.Email.ToLower().Contains(lower))
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
                var response = await client.From<Customer>().Get();
                Customers.Clear();
                foreach (var c in response.Models) Customers.Add(c);
                TotalCustomers = Customers.Count;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải khách hàng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
            FormTitle = "Thêm Khách Hàng";
            FormFullName = string.Empty;
            FormPhone = string.Empty;
            FormEmail = string.Empty;
            FormAddress = string.Empty;
            IsEditing = false;
            _editingCustomer = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void ShowEditForm(Customer customer)
        {
            if (customer == null) return;
            FormTitle = "Chỉnh sửa Khách Hàng";
            FormFullName = customer.FullName;
            FormPhone = customer.PhoneNumber;
            FormEmail = customer.Email;
            FormAddress = customer.Address;
            IsEditing = true;
            _editingCustomer = customer;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void CancelForm() => IsFormVisible = false;

        [RelayCommand]
        private async Task SaveCustomerAsync()
        {
            if (string.IsNullOrWhiteSpace(FormFullName))
            {
                MessageBox.Show("Vui lòng nhập họ tên.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                if (IsEditing && _editingCustomer != null)
                {
                    _editingCustomer.FullName = FormFullName;
                    _editingCustomer.PhoneNumber = FormPhone;
                    _editingCustomer.Email = FormEmail;
                    _editingCustomer.Address = FormAddress;
                    await client.From<Customer>().Update(_editingCustomer);
                }
                else
                {
                    var c = new Customer
                    {
                        FullName = FormFullName,
                        PhoneNumber = FormPhone,
                        Email = FormEmail,
                        Address = FormAddress
                    };
                    await client.From<Customer>().Insert(c);
                }
                IsFormVisible = false;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu khách hàng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteCustomerAsync(Customer customer)
        {
            if (customer == null) return;
            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa khách hàng \"{customer.FullName}\"?",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                await client.From<Customer>().Where(c => c.Id == customer.Id).Delete();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi xóa: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

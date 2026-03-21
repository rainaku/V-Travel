using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VietTravel.Core.Models;
using VietTravel.Data;
using VietTravel.UI.Models;

namespace VietTravel.UI.ViewModels
{
    public partial class UserManagementViewModel : PaginatedListViewModelBase<UserRoleItem>
    {
        private readonly MainViewModel _mainViewModel;
        private readonly Dictionary<int, User> _userCache = new();

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private ObservableCollection<UserRoleItem> _users = new();
        [ObservableProperty] private ObservableCollection<UserRoleItem> _filteredUsers = new();
        [ObservableProperty] private ObservableCollection<string> _roleOptions =
            new(new[] { "Admin", "Employee", "Guide", "Customer" });

        // Filters
        [ObservableProperty] private ObservableCollection<string> _filterRoles = new() { "Tất cả", "Admin", "Employee", "Guide", "Customer" };
        [ObservableProperty] private string _selectedFilterRole = "Tất cả";

        [ObservableProperty] private int _totalUsers;
        [ObservableProperty] private int _totalGuides;

        public bool CanManageRoles =>
            string.Equals(_mainViewModel.CurrentUser?.Role, "Admin", StringComparison.OrdinalIgnoreCase);

        public bool HasNoData => !IsLoading && FilteredUsers.Count == 0;

        public UserManagementViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = LoadDataAsync();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedFilterRoleChanged(string value) => ApplyFilter();

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            if (IsLoading)
            {
                return;
            }

            IsLoading = true;
            OnPropertyChanged(nameof(HasNoData));
            OnPropertyChanged(nameof(CanManageRoles));

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var response = await client.From<User>().Get();
                var models = response.Models
                    .OrderBy(x => x.FullName)
                    .ThenBy(x => x.Username)
                    .ToList();

                _userCache.Clear();
                foreach (var user in models)
                {
                    _userCache[user.Id] = user;
                }

                Users = new ObservableCollection<UserRoleItem>(
                    models.Select(u => new UserRoleItem
                    {
                        Id = u.Id,
                        FullName = u.FullName,
                        Username = u.Username,
                        Role = u.Role,
                        IsActive = u.IsActive
                    }));

                TotalUsers = Users.Count;
                TotalGuides = Users.Count(x => string.Equals(x.Role, "Guide", StringComparison.OrdinalIgnoreCase));
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách user: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoData));
            }
        }

        [RelayCommand]
        private async Task SaveUserRoleAsync(UserRoleItem? userItem)
        {
            if (userItem == null)
            {
                return;
            }

            if (!CanManageRoles)
            {
                MessageBox.Show("Chỉ Admin mới được phép gán role cho user.", "Không đủ quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_userCache.TryGetValue(userItem.Id, out var user))
            {
                MessageBox.Show("Không tìm thấy dữ liệu user để cập nhật.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var normalizedRole = NormalizeRole(userItem.Role);
            if (string.IsNullOrWhiteSpace(normalizedRole) || !RoleOptions.Contains(normalizedRole))
            {
                MessageBox.Show("Role không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_mainViewModel.CurrentUser != null &&
                _mainViewModel.CurrentUser.Id == userItem.Id &&
                !string.Equals(normalizedRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Bạn không thể tự hạ quyền khỏi Admin.", "Không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                await client
                    .From<User>()
                    .Set(u => u.Role, normalizedRole)
                    .Set(u => u.IsActive, userItem.IsActive)
                    .Where(u => u.Id == userItem.Id)
                    .Update();

                var verifyResponse = await client
                    .From<User>()
                    .Where(u => u.Id == userItem.Id)
                    .Get();
                var verifiedUser = verifyResponse.Models.FirstOrDefault();

                if (verifiedUser == null)
                {
                    MessageBox.Show("Không đọc lại được dữ liệu user sau khi lưu.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                user.Role = verifiedUser.Role;
                user.IsActive = verifiedUser.IsActive;
                userItem.Role = verifiedUser.Role;
                userItem.IsActive = verifiedUser.IsActive;

                if (_mainViewModel.CurrentUser != null && _mainViewModel.CurrentUser.Id == verifiedUser.Id)
                {
                    _mainViewModel.CurrentUser.Role = verifiedUser.Role;
                    _mainViewModel.CurrentUser.IsActive = verifiedUser.IsActive;
                }

                TotalGuides = Users.Count(x => string.Equals(x.Role, "Guide", StringComparison.OrdinalIgnoreCase));
                MessageBox.Show($"Đã cập nhật role: {verifiedUser.Role}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi cập nhật role: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string NormalizeRole(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim() switch
            {
                var role when role.Equals("admin", StringComparison.OrdinalIgnoreCase) => "Admin",
                var role when role.Equals("employee", StringComparison.OrdinalIgnoreCase) => "Employee",
                var role when role.Equals("guide", StringComparison.OrdinalIgnoreCase) => "Guide",
                var role when role.Equals("customer", StringComparison.OrdinalIgnoreCase) => "Customer",
                _ => value.Trim()
            };
        }

        private void ApplyFilter()
        {
            var isSearchEmpty = string.IsNullOrWhiteSpace(SearchText);
            var lower = isSearchEmpty ? string.Empty : SearchText.Trim().ToLowerInvariant();
            var filterRole = SelectedFilterRole == "Tất cả" ? null : SelectedFilterRole;

            var filtered = Users.Where(u =>
                    (isSearchEmpty ||
                     u.FullName.ToLowerInvariant().Contains(lower) ||
                     u.Username.ToLowerInvariant().Contains(lower) ||
                     u.Role.ToLowerInvariant().Contains(lower)) &&
                    (filterRole == null || string.Equals(u.Role, filterRole, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            SetPagedItems(filtered, FilteredUsers);

            OnPropertyChanged(nameof(HasNoData));
        }
    }
}

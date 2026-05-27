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
using VietTravel.Data.Services;
using VietTravel.UI.Models;

namespace VietTravel.UI.ViewModels
{
    public partial class UserManagementViewModel : PaginatedListViewModelBase<UserRoleItem>
    {
        private readonly MainViewModel _mainViewModel;
        private readonly Dictionary<int, User> _userCache = new();
        private const string SuperAdminUsername = "GL_13";

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
        [ObservableProperty] private int _totalBanned;

        // Edit form
        [ObservableProperty] private bool _isEditFormVisible;
        [ObservableProperty] private UserRoleItem? _editingUser;
        [ObservableProperty] private string _editFullName = string.Empty;
        [ObservableProperty] private string _editUsername = string.Empty;
        [ObservableProperty] private string _editRole = "Employee";
        [ObservableProperty] private bool _editIsActive = true;
        [ObservableProperty] private string _editNewPassword = string.Empty;
        [ObservableProperty] private bool _isSaving;

        public bool CanManageRoles =>
            string.Equals(_mainViewModel.CurrentUser?.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            IsSuperAdmin;

        public bool IsSuperAdmin =>
            string.Equals(_mainViewModel.CurrentUser?.Username, SuperAdminUsername, StringComparison.OrdinalIgnoreCase);

        public bool HasNoData => !IsLoading && FilteredUsers.Count == 0;

        public string EditFormTitle => EditingUser != null ? $"Chỉnh sửa tài khoản #{EditingUser.Id}" : "Chỉnh sửa tài khoản";

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
                TotalBanned = Users.Count(x => !x.IsActive);
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
                // Audit log for role change
                AuditLogService.LogFireAndForget(
                    client: client,
                    userId: _mainViewModel.CurrentUser?.Id ?? 0,
                    action: "USER_ROLE_CHANGE",
                    entityType: "User",
                    entityId: userItem.Id,
                    details: $"Target: {userItem.Username}, NewRole: {verifiedUser.Role}");
                MessageBox.Show($"Đã cập nhật role: {verifiedUser.Role}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi cập nhật role: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ShowEditForm(UserRoleItem? userItem)
        {
            if (userItem == null || !CanManageRoles) return;

            EditingUser = userItem;
            EditFullName = userItem.FullName;
            EditUsername = userItem.Username;
            EditRole = userItem.Role;
            EditIsActive = userItem.IsActive;
            EditNewPassword = string.Empty;
            IsEditFormVisible = true;
            OnPropertyChanged(nameof(EditFormTitle));
        }

        [RelayCommand]
        private void CloseEditForm()
        {
            IsEditFormVisible = false;
            EditingUser = null;
            EditNewPassword = string.Empty;
        }

        [RelayCommand]
        private async Task SaveEditAsync()
        {
            if (EditingUser == null || IsSaving) return;

            if (string.IsNullOrWhiteSpace(EditFullName))
            {
                MessageBox.Show("Họ tên không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(EditUsername))
            {
                MessageBox.Show("Username không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var normalizedRole = NormalizeRole(EditRole);
            if (string.IsNullOrWhiteSpace(normalizedRole) || !RoleOptions.Contains(normalizedRole))
            {
                MessageBox.Show("Role không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Prevent self-demotion
            if (_mainViewModel.CurrentUser != null &&
                _mainViewModel.CurrentUser.Id == EditingUser.Id &&
                !string.Equals(normalizedRole, _mainViewModel.CurrentUser.Role, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Bạn không thể tự thay đổi role của chính mình.", "Không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Prevent self-ban
            if (_mainViewModel.CurrentUser != null &&
                _mainViewModel.CurrentUser.Id == EditingUser.Id &&
                !EditIsActive)
            {
                MessageBox.Show("Bạn không thể tự khóa tài khoản của chính mình.", "Không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSaving = true;
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var adminName = _mainViewModel.CurrentUser?.Username ?? "Unknown";

                // Update user fields
                var query = client
                    .From<User>()
                    .Set(u => u.FullName, EditFullName.Trim())
                    .Set(u => u.Username, EditUsername.Trim())
                    .Set(u => u.Role, normalizedRole)
                    .Set(u => u.IsActive, EditIsActive);

                // Track ban info
                if (!EditIsActive)
                {
                    query = query
                        .Set(u => u.BannedBy, adminName)
                        .Set(u => u.BannedAt, DateTime.UtcNow.ToString("o"));
                }
                else
                {
                    query = query
                        .Set(u => u.BannedBy, string.Empty);
                }

                await query
                    .Where(u => u.Id == EditingUser.Id)
                    .Update();

                // Reset password if provided
                if (!string.IsNullOrWhiteSpace(EditNewPassword))
                {
                    if (EditNewPassword.Length < 8)
                    {
                        MessageBox.Show("Mật khẩu mới phải có ít nhất 8 ký tự.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        IsSaving = false;
                        return;
                    }

                    if (!EditNewPassword.Any(char.IsUpper) || !EditNewPassword.Any(char.IsLower) || !EditNewPassword.Any(char.IsDigit))
                    {
                        MessageBox.Show("Mật khẩu phải chứa ít nhất 1 chữ hoa, 1 chữ thường và 1 số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        IsSaving = false;
                        return;
                    }

                    var hashedPassword = AuthService.HashPassword(EditNewPassword);
                    await client
                        .From<User>()
                        .Set(u => u.PasswordHash, hashedPassword)
                        .Where(u => u.Id == EditingUser.Id)
                        .Update();

                    // Audit log for password reset
                    AuditLogService.LogFireAndForget(
                        client: client,
                        userId: _mainViewModel.CurrentUser?.Id ?? 0,
                        action: "USER_PASSWORD_RESET",
                        entityType: "User",
                        entityId: EditingUser.Id,
                        details: $"Target: {EditingUser.Username}, By: {adminName}");
                }

                // Verify and update local state
                var verifyResponse = await client
                    .From<User>()
                    .Where(u => u.Id == EditingUser.Id)
                    .Get();
                var verifiedUser = verifyResponse.Models.FirstOrDefault();

                if (verifiedUser != null)
                {
                    if (_userCache.TryGetValue(EditingUser.Id, out var cached))
                    {
                        cached.FullName = verifiedUser.FullName;
                        cached.Username = verifiedUser.Username;
                        cached.Role = verifiedUser.Role;
                        cached.IsActive = verifiedUser.IsActive;
                    }

                    EditingUser.FullName = verifiedUser.FullName;
                    EditingUser.Username = verifiedUser.Username;
                    EditingUser.Role = verifiedUser.Role;
                    EditingUser.IsActive = verifiedUser.IsActive;

                    // Update current user if editing self
                    if (_mainViewModel.CurrentUser != null && _mainViewModel.CurrentUser.Id == verifiedUser.Id)
                    {
                        _mainViewModel.CurrentUser.FullName = verifiedUser.FullName;
                        _mainViewModel.CurrentUser.Username = verifiedUser.Username;
                        _mainViewModel.CurrentUser.Role = verifiedUser.Role;
                        _mainViewModel.CurrentUser.IsActive = verifiedUser.IsActive;
                    }
                }

                UpdateStats();
                // Audit log for user edit
                AuditLogService.LogFireAndForget(
                    client: client,
                    userId: _mainViewModel.CurrentUser?.Id ?? 0,
                    action: "USER_EDIT",
                    entityType: "User",
                    entityId: EditingUser?.Id ?? 0,
                    details: $"Target: {EditingUser?.Username}, Role: {normalizedRole}, Active: {EditIsActive}, By: {adminName}");
                MessageBox.Show("Đã cập nhật tài khoản thành công.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                CloseEditForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi cập nhật tài khoản: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        [RelayCommand]
        private async Task ToggleBanAsync(UserRoleItem? userItem)
        {
            if (userItem == null || !CanManageRoles) return;

            // Prevent self-ban
            if (_mainViewModel.CurrentUser != null && _mainViewModel.CurrentUser.Id == userItem.Id)
            {
                MessageBox.Show("Bạn không thể tự khóa tài khoản của chính mình.", "Không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Prevent banning users of equal or higher role (hierarchy protection)
            var currentRoleLevel = GetRoleLevel(_mainViewModel.CurrentUser?.Role);
            var targetRoleLevel = GetRoleLevel(userItem.Role);
            if (targetRoleLevel >= currentRoleLevel)
            {
                MessageBox.Show(
                    $"Bạn không thể khóa tài khoản có cùng cấp hoặc cao hơn ({userItem.Role}).",
                    "Không đủ quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Protect SuperAdmin account
            if (string.Equals(userItem.Username, SuperAdminUsername, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Không thể khóa tài khoản Super Admin.", "Không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newStatus = !userItem.IsActive;
            var action = newStatus ? "mở khóa" : "khóa";
            var result = MessageBox.Show(
                $"Bạn có chắc muốn {action} tài khoản \"{userItem.FullName}\" ({userItem.Username})?",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var adminName = _mainViewModel.CurrentUser?.Username ?? "Unknown";

                if (newStatus)
                {
                    // Unban: clear banned info
                    await client
                        .From<User>()
                        .Set(u => u.IsActive, true)
                        .Set(u => u.BannedBy, string.Empty)
                        .Where(u => u.Id == userItem.Id)
                        .Update();
                }
                else
                {
                    // Ban: record who banned
                    await client
                        .From<User>()
                        .Set(u => u.IsActive, false)
                        .Set(u => u.BannedBy, adminName)
                        .Set(u => u.BannedAt, DateTime.UtcNow.ToString("o"))
                        .Where(u => u.Id == userItem.Id)
                        .Update();
                }

                // Audit log
                AuditLogService.LogFireAndForget(
                    client: client,
                    userId: _mainViewModel.CurrentUser?.Id ?? 0,
                    action: newStatus ? "USER_UNBAN" : "USER_BAN",
                    entityType: "User",
                    entityId: userItem.Id,
                    details: $"Target: {userItem.Username}, By: {adminName}");

                userItem.IsActive = newStatus;
                if (_userCache.TryGetValue(userItem.Id, out var cached))
                {
                    cached.IsActive = newStatus;
                    cached.BannedBy = newStatus ? string.Empty : adminName;
                    cached.BannedAt = newStatus ? string.Empty : DateTime.UtcNow.ToString("o");
                }

                UpdateStats();
                MessageBox.Show($"Đã {action} tài khoản \"{userItem.Username}\".", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStats()
        {
            TotalUsers = Users.Count;
            TotalGuides = Users.Count(x => string.Equals(x.Role, "Guide", StringComparison.OrdinalIgnoreCase));
            TotalBanned = Users.Count(x => !x.IsActive);
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

        private static int GetRoleLevel(string? role)
        {
            return (role ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "customer" => 0,
                "guide" => 1,
                "employee" => 2,
                "admin" => 3,
                "superadmin" => 4,
                "owner" => 5,
                _ => 0
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

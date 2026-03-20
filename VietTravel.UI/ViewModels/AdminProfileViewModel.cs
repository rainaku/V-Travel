using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VietTravel.Core.Models;
using VietTravel.Data;
using VietTravel.Data.Services;

namespace VietTravel.UI.ViewModels
{
    public partial class AdminProfileViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly CloudinaryImageService _cloudinaryImageService = new();
        private Customer? _customerProfile;

        [ObservableProperty] private string _fullName = string.Empty;
        [ObservableProperty] private string _phoneNumber = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _address = string.Empty;
        [ObservableProperty] private string _avatarUrl = string.Empty;
        [ObservableProperty] private bool _isSaving;
        [ObservableProperty] private bool _isUploading;
        [ObservableProperty] private bool _isLoading;

        public string UserRole => _mainViewModel.CurrentUser?.Role ?? "Admin";
        public string Username => _mainViewModel.CurrentUser?.Username ?? "N/A";
        public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
        public string ChangeAvatarButtonText => IsUploading ? "Đang tải..." : "Đổi ảnh đại diện";

        public AdminProfileViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            try
            {
                var user = _mainViewModel.CurrentUser;
                if (user != null)
                {
                    FullName = user.FullName;
                    AvatarUrl = user.AvatarUrl;

                    // Try to find matching customer profile for extra info (Phone, Email, Address)
                    var client = await SupabaseClientFactory.GetClientAsync();
                    var customerResponse = await client.From<Customer>()
                        .Where(c => c.FullName == user.FullName)
                        .Get();
                    
                    _customerProfile = customerResponse.Models.FirstOrDefault();
                    if (_customerProfile != null)
                    {
                        PhoneNumber = _customerProfile.PhoneNumber;
                        Email = _customerProfile.Email;
                        Address = _customerProfile.Address;
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ChangeAvatarAsync()
        {
            if (IsUploading || _mainViewModel.CurrentUser == null) return;

            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg|All files (*.*)|*.*",
                Title = "Chọn ảnh đại diện"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                IsUploading = true;
                try
                {
                    var newAvatarUrl = await _cloudinaryImageService.UploadAvatarAsync(openFileDialog.FileName, _mainViewModel.CurrentUser.Id);
                    if (!string.IsNullOrEmpty(newAvatarUrl))
                    {
                        AvatarUrl = newAvatarUrl;
                        
                        // Update User table in DB
                        var client = await SupabaseClientFactory.GetClientAsync();
                        await client.From<User>()
                            .Set(u => u.AvatarUrl, AvatarUrl)
                            .Where(u => u.Id == _mainViewModel.CurrentUser.Id)
                            .Update();

                        _mainViewModel.CurrentUser.AvatarUrl = AvatarUrl;
                        _mainViewModel.RefreshCurrentUser();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi tải ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsUploading = false;
                }
            }
        }

        [RelayCommand]
        private async Task SaveProfileAsync()
        {
            if (IsSaving || _mainViewModel.CurrentUser == null) return;

            if (string.IsNullOrWhiteSpace(FullName))
            {
                MessageBox.Show("Họ tên không được để trống.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSaving = true;
            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                
                // 1. Update User table (FullName only, as other fields don't exist in users table)
                await client.From<User>()
                    .Set(u => u.FullName, FullName)
                    .Where(u => u.Id == _mainViewModel.CurrentUser.Id)
                    .Update();

                // 2. Update Customer table if profile exists
                if (_customerProfile != null)
                {
                    await client.From<Customer>()
                        .Set(c => c.FullName, FullName)
                        .Set(c => c.PhoneNumber, PhoneNumber)
                        .Set(c => c.Email, Email)
                        .Set(c => c.Address, Address)
                        .Where(c => c.Id == _customerProfile.Id)
                        .Update();
                }

                // Refresh local state
                _mainViewModel.CurrentUser.FullName = FullName;
                _mainViewModel.RefreshCurrentUser();

                MessageBox.Show("Cập nhật hồ sơ thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu hồ sơ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}

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
    public partial class GuideManagementViewModel : ObservableObject
    {
        private const int PageSize = 12;
        private readonly MainViewModel _mainViewModel;
        private readonly Dictionary<int, int> _tourDurationByTourId = new();
        private readonly Dictionary<int, int> _departureDurationByDepartureId = new();
        private List<GuideContactItem> _guideFilteredSource = new();
        private List<GuideScheduleItem> _assignmentFilteredSource = new();

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _guideSearchText = string.Empty;
        [ObservableProperty] private string _scheduleSearchText = string.Empty;

        [ObservableProperty] private ObservableCollection<GuideContactItem> _guides = new();
        [ObservableProperty] private ObservableCollection<GuideContactItem> _filteredGuides = new();

        [ObservableProperty] private ObservableCollection<GuideScheduleItem> _assignments = new();
        [ObservableProperty] private ObservableCollection<GuideScheduleItem> _filteredAssignments = new();

        // Filters
        [ObservableProperty] private ObservableCollection<string> _scheduleStatuses = new() { "Tất cả" };
        [ObservableProperty] private string _selectedScheduleStatus = "Tất cả";

        [ObservableProperty] private ObservableCollection<User> _availableGuides = new();
        [ObservableProperty] private ObservableCollection<Departure> _availableDepartures = new();
        [ObservableProperty] private ObservableCollection<string> _assignmentStatusOptions =
            new(new[] { "Đang phân công", "Đã hoàn thành", "Đã hủy" });

        [ObservableProperty] private User? _formSelectedGuide;
        [ObservableProperty] private Departure? _formSelectedDeparture;
        [ObservableProperty] private DateTime _formWorkStart = DateTime.Now.AddDays(1);
        [ObservableProperty] private DateTime _formWorkEnd = DateTime.Now.AddDays(2);
        [ObservableProperty] private string _formAssignmentStatus = "Đang phân công";
        [ObservableProperty] private string _formAssignmentNotes = string.Empty;

        [ObservableProperty] private int _totalGuides;
        [ObservableProperty] private int _totalAssignments;
        [ObservableProperty] private int _unassignedActiveTours;
        [ObservableProperty] private int _guideCurrentPage = 1;
        [ObservableProperty] private int _guideTotalPages = 1;
        [ObservableProperty] private int _guideTotalItems;
        [ObservableProperty] private int _assignmentCurrentPage = 1;
        [ObservableProperty] private int _assignmentTotalPages = 1;
        [ObservableProperty] private int _assignmentTotalItems;

        public bool HasNoGuides => !IsLoading && FilteredGuides.Count == 0;
        public bool HasNoAssignments => !IsLoading && FilteredAssignments.Count == 0;
        public bool CanAdminEdit => string.Equals(_mainViewModel.CurrentUser?.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        public bool IsGuideViewOnly => string.Equals(_mainViewModel.CurrentUser?.Role, "Guide", StringComparison.OrdinalIgnoreCase);
        public bool ShowManagementSections => !IsGuideViewOnly;
        public string PageTitle => IsGuideViewOnly ? "Lịch làm việc của tôi" : "Quản lý Hướng Dẫn Viên";
        public string PageSubtitle => IsGuideViewOnly
            ? "Theo dõi các tour bạn được phân công"
            : "Phân công guide cho tour, lịch làm việc và thông tin liên hệ";
        public bool CanGoToPreviousGuidePage => GuideCurrentPage > 1;
        public bool CanGoToNextGuidePage => GuideCurrentPage < GuideTotalPages;
        public bool ShowGuidePagination => GuideTotalItems > PageSize;
        public string GuidePaginationSummary => GuideTotalItems == 0
            ? "0 mục"
            : $"Trang {GuideCurrentPage}/{GuideTotalPages} • {GuideTotalItems} mục";
        public bool CanGoToPreviousAssignmentPage => AssignmentCurrentPage > 1;
        public bool CanGoToNextAssignmentPage => AssignmentCurrentPage < AssignmentTotalPages;
        public bool ShowAssignmentPagination => AssignmentTotalItems > PageSize;
        public string AssignmentPaginationSummary => AssignmentTotalItems == 0
            ? "0 mục"
            : $"Trang {AssignmentCurrentPage}/{AssignmentTotalPages} • {AssignmentTotalItems} mục";

        public GuideManagementViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ = LoadDataAsync();
        }

        partial void OnGuideSearchTextChanged(string value) => ApplyGuideFilter();
        partial void OnScheduleSearchTextChanged(string value) => ApplyScheduleFilter();
        partial void OnSelectedScheduleStatusChanged(string value) => ApplyScheduleFilter();
        partial void OnGuideCurrentPageChanged(int value) => RefreshGuidePage();
        partial void OnAssignmentCurrentPageChanged(int value) => RefreshAssignmentPage();

        partial void OnFormSelectedDepartureChanged(Departure? value)
        {
            if (value == null)
            {
                return;
            }

            var existingAssignment = Assignments.FirstOrDefault(a => a.DepartureId == value.Id);
            if (existingAssignment != null)
            {
                FormWorkStart = existingAssignment.WorkStart;
                FormWorkEnd = existingAssignment.WorkEnd;
                FormAssignmentStatus = string.IsNullOrWhiteSpace(existingAssignment.Status)
                    ? "Đang phân công"
                    : existingAssignment.Status;
                FormAssignmentNotes = existingAssignment.Notes ?? string.Empty;

                var matchedGuide = AvailableGuides.FirstOrDefault(g => g.Id == existingAssignment.GuideUserId);
                if (matchedGuide != null)
                {
                    FormSelectedGuide = matchedGuide;
                }

                return;
            }

            FormWorkStart = value.StartDate.AddHours(-4);
            var duration = 1;
            if (_tourDurationByTourId.TryGetValue(value.TourId, out var resolvedDuration))
            {
                duration = Math.Max(1, resolvedDuration);
            }

            FormWorkEnd = value.StartDate.AddDays(duration);
            FormAssignmentStatus = "Đang phân công";
            FormAssignmentNotes = string.Empty;
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            if (IsLoading)
            {
                return;
            }

            IsLoading = true;
            OnPropertyChanged(nameof(HasNoGuides));
            OnPropertyChanged(nameof(HasNoAssignments));
            OnPropertyChanged(nameof(CanAdminEdit));
            OnPropertyChanged(nameof(IsGuideViewOnly));
            OnPropertyChanged(nameof(ShowManagementSections));
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(PageSubtitle));

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var users = (await client.From<User>().Get()).Models.ToList();
                var tours = (await client.From<Tour>().Get()).Models.ToList();
                var departures = (await client.From<Departure>().Get()).Models.ToList();
                var profiles = (await client.From<GuideProfile>().Get()).Models.ToList();
                var assignments = (await client.From<TourGuideAssignment>().Get()).Models.ToList();

                if (IsGuideViewOnly && _mainViewModel.CurrentUser != null)
                {
                    var currentGuideId = _mainViewModel.CurrentUser.Id;
                    users = users.Where(u => u.Id == currentGuideId).ToList();
                    profiles = profiles.Where(p => p.UserId == currentGuideId).ToList();
                    assignments = assignments.Where(a => a.GuideUserId == currentGuideId).ToList();
                }

                BuildCollections(users, tours, departures, profiles, assignments);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải dữ liệu hướng dẫn viên: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoGuides));
                OnPropertyChanged(nameof(HasNoAssignments));
            }
        }

        [RelayCommand]
        private async Task SaveGuideContactAsync(GuideContactItem? guide)
        {
            if (guide == null)
            {
                return;
            }

            if (!CanAdminEdit)
            {
                MessageBox.Show("Chỉ Admin được chỉnh sửa dữ liệu hướng dẫn viên.", "Không đủ quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var profile = new GuideProfile
                {
                    Id = guide.ProfileId,
                    UserId = guide.UserId,
                    PhoneNumber = guide.PhoneNumber?.Trim() ?? string.Empty,
                    Email = guide.Email?.Trim() ?? string.Empty,
                    EmergencyContact = guide.EmergencyContact?.Trim() ?? string.Empty,
                    Notes = guide.Notes?.Trim() ?? string.Empty,
                    UpdatedAt = DateTime.Now
                };

                if (guide.ProfileId > 0)
                {
                    profile.User = null;
                    await client.From<GuideProfile>().Update(profile);
                }
                else
                {
                    var inserted = await client.From<GuideProfile>().Insert(profile);
                    guide.ProfileId = inserted.Models.FirstOrDefault()?.Id ?? 0;
                }

                MessageBox.Show("Đã lưu thông tin liên hệ hướng dẫn viên.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu thông tin liên hệ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task AssignGuideAsync()
        {
            if (!CanAdminEdit)
            {
                MessageBox.Show("Chỉ Admin được tạo phân công hướng dẫn viên.", "Không đủ quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (FormSelectedGuide == null)
            {
                MessageBox.Show("Vui lòng chọn hướng dẫn viên.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (FormSelectedDeparture == null)
            {
                MessageBox.Show("Vui lòng chọn lịch khởi hành của tour.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (FormWorkEnd <= FormWorkStart)
            {
                MessageBox.Show("Thời gian kết thúc phải sau thời gian bắt đầu.", "Dữ liệu không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var existingAssignmentItem = Assignments.FirstOrDefault(a => a.DepartureId == FormSelectedDeparture.Id);
                var targetDuration = Math.Max(1, _tourDurationByTourId.TryGetValue(FormSelectedDeparture.TourId, out var resolvedDuration)
                    ? resolvedDuration
                    : 1);
                if (HasWorkScheduleConflict(
                        FormSelectedGuide.Id,
                        FormWorkStart,
                        FormWorkEnd,
                        FormSelectedDeparture.StartDate,
                        targetDuration,
                        existingAssignmentItem?.AssignmentId,
                        out var conflictTourName,
                        out var conflictWorkStart,
                        out var conflictWorkEnd))
                {
                    MessageBox.Show(
                        $"Hướng dẫn viên đã có lịch công tác tour \"{conflictTourName}\" trong khoảng {conflictWorkStart:dd/MM/yyyy} - {conflictWorkEnd:dd/MM/yyyy}.\nKhông được trùng ngày khởi hành hoặc ngày kết thúc. Nếu xếp tour sau, phải bắt đầu từ {conflictWorkEnd.Date.AddDays(1):dd/MM/yyyy}.",
                        "Trùng lịch công tác",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (existingAssignmentItem != null)
                {
                    var existingResponse = await client.From<TourGuideAssignment>()
                        .Where(x => x.Id == existingAssignmentItem.AssignmentId)
                        .Get();
                    var existingAssignment = existingResponse.Models.FirstOrDefault();

                    if (existingAssignment == null)
                    {
                        MessageBox.Show("Không tìm thấy phân công hiện tại để thay thế.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    existingAssignment.GuideUserId = FormSelectedGuide.Id;
                    existingAssignment.DepartureId = FormSelectedDeparture.Id;
                    existingAssignment.WorkStart = FormWorkStart;
                    existingAssignment.WorkEnd = FormWorkEnd;
                    existingAssignment.Status = string.IsNullOrWhiteSpace(FormAssignmentStatus) ? "Đang phân công" : FormAssignmentStatus.Trim();
                    existingAssignment.Notes = FormAssignmentNotes?.Trim() ?? string.Empty;
                    existingAssignment.AssignedAt = DateTime.Now;
                    existingAssignment.Departure = null;
                    existingAssignment.Guide = null;

                    await client.From<TourGuideAssignment>().Update(existingAssignment);
                    MessageBox.Show("Đã thay thế phân công hiện có.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var assignment = new TourGuideAssignment
                    {
                        GuideUserId = FormSelectedGuide.Id,
                        DepartureId = FormSelectedDeparture.Id,
                        WorkStart = FormWorkStart,
                        WorkEnd = FormWorkEnd,
                        Status = string.IsNullOrWhiteSpace(FormAssignmentStatus) ? "Đang phân công" : FormAssignmentStatus.Trim(),
                        Notes = FormAssignmentNotes?.Trim() ?? string.Empty,
                        AssignedAt = DateTime.Now
                    };

                    await client.From<TourGuideAssignment>().Insert(assignment);
                    MessageBox.Show("Đã tạo phân công mới.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                ClearAssignForm();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi phân công hướng dẫn viên: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SaveAssignmentAsync(GuideScheduleItem? assignmentItem)
        {
            if (assignmentItem == null)
            {
                return;
            }

            if (!CanAdminEdit)
            {
                MessageBox.Show("Chỉ Admin được sửa lịch làm việc hướng dẫn viên.", "Không đủ quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (assignmentItem.WorkEnd <= assignmentItem.WorkStart)
            {
                MessageBox.Show("Thời gian kết thúc phải sau thời gian bắt đầu.", "Dữ liệu không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                var assignmentResponse = await client.From<TourGuideAssignment>().Get();
                var assignment = assignmentResponse.Models.FirstOrDefault(x => x.Id == assignmentItem.AssignmentId);

                if (assignment == null)
                {
                    MessageBox.Show("Không tìm thấy phân công cần cập nhật.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var targetDuration = ResolveDepartureDurationDays(assignmentItem.DepartureId);
                var targetDepartureStart = assignmentItem.DepartureStartDate == DateTime.MinValue
                    ? assignmentItem.WorkStart
                    : assignmentItem.DepartureStartDate;
                if (HasWorkScheduleConflict(
                        assignment.GuideUserId,
                        assignmentItem.WorkStart,
                        assignmentItem.WorkEnd,
                        targetDepartureStart,
                        targetDuration,
                        assignment.Id,
                        out var conflictTourName,
                        out var conflictWorkStart,
                        out var conflictWorkEnd))
                {
                    MessageBox.Show(
                        $"Hướng dẫn viên đã có lịch công tác tour \"{conflictTourName}\" trong khoảng {conflictWorkStart:dd/MM/yyyy} - {conflictWorkEnd:dd/MM/yyyy}.\nKhông được trùng ngày khởi hành hoặc ngày kết thúc. Nếu xếp tour sau, phải bắt đầu từ {conflictWorkEnd.Date.AddDays(1):dd/MM/yyyy}.",
                        "Trùng lịch công tác",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                assignment.WorkStart = assignmentItem.WorkStart;
                assignment.WorkEnd = assignmentItem.WorkEnd;
                assignment.Status = string.IsNullOrWhiteSpace(assignmentItem.Status)
                    ? "Đang phân công"
                    : assignmentItem.Status.Trim();
                assignment.Notes = assignmentItem.Notes?.Trim() ?? string.Empty;
                assignment.Departure = null;
                assignment.Guide = null;

                await client.From<TourGuideAssignment>().Update(assignment);
                MessageBox.Show("Đã cập nhật lịch làm việc.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi cập nhật lịch làm việc: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteAssignmentAsync(GuideScheduleItem? assignment)
        {
            if (assignment == null)
            {
                return;
            }

            if (!CanAdminEdit)
            {
                MessageBox.Show("Chỉ Admin được xóa phân công hướng dẫn viên.", "Không đủ quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Xóa phân công {assignment.GuideName} cho tour \"{assignment.TourName}\"?",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var client = await SupabaseClientFactory.GetClientAsync();
                await client.From<TourGuideAssignment>().Where(x => x.Id == assignment.AssignmentId).Delete();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi xóa phân công: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildCollections(
            List<User> users,
            List<Tour> tours,
            List<Departure> departures,
            List<GuideProfile> profiles,
            List<TourGuideAssignment> assignments)
        {
            _tourDurationByTourId.Clear();
            foreach (var t in tours)
            {
                _tourDurationByTourId[t.Id] = t.DurationDays;
            }

            _departureDurationByDepartureId.Clear();
            foreach (var dep in departures)
            {
                var duration = _tourDurationByTourId.TryGetValue(dep.TourId, out var resolvedDuration)
                    ? Math.Max(1, resolvedDuration)
                    : 1;
                _departureDurationByDepartureId[dep.Id] = duration;
            }

            var guideUsers = users
                .Where(IsGuideUser)
                .OrderBy(x => x.FullName)
                .ToList();

            var profileByUserId = profiles
                .GroupBy(p => p.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            var tourById = tours.ToDictionary(t => t.Id);
            foreach (var dep in departures)
            {
                dep.Tour = tourById.TryGetValue(dep.TourId, out var tour) ? tour : null;
            }

            var activeDepartures = departures
                .Where(IsActiveDeparture)
                .OrderBy(d => d.StartDate)
                .ToList();

            AvailableGuides = new ObservableCollection<User>(guideUsers);
            AvailableDepartures = new ObservableCollection<Departure>(activeDepartures);

            Guides = new ObservableCollection<GuideContactItem>(
                guideUsers.Select(u =>
                {
                    profileByUserId.TryGetValue(u.Id, out var profile);
                    return new GuideContactItem
                    {
                        UserId = u.Id,
                        ProfileId = profile?.Id ?? 0,
                        FullName = u.FullName,
                        Username = u.Username,
                        PhoneNumber = profile?.PhoneNumber ?? string.Empty,
                        Email = profile?.Email ?? string.Empty,
                        EmergencyContact = profile?.EmergencyContact ?? string.Empty,
                        Notes = profile?.Notes ?? string.Empty
                    };
                }));

            var guideById = guideUsers.ToDictionary(g => g.Id);
            var departureById = departures.ToDictionary(d => d.Id);

            Assignments = new ObservableCollection<GuideScheduleItem>(
                assignments
                    .OrderBy(a => a.WorkStart)
                    .Select(a =>
                    {
                        departureById.TryGetValue(a.DepartureId, out var departure);
                        guideById.TryGetValue(a.GuideUserId, out var guide);
                        var tourName = departure?.Tour?.Name ?? "N/A";

                        return new GuideScheduleItem
                        {
                            AssignmentId = a.Id,
                            GuideUserId = a.GuideUserId,
                            DepartureId = a.DepartureId,
                            GuideName = guide?.FullName ?? $"Guide #{a.GuideUserId}",
                            TourName = tourName,
                            DepartureStartDate = departure?.StartDate ?? DateTime.MinValue,
                            WorkStart = a.WorkStart,
                            WorkEnd = a.WorkEnd,
                            Status = a.Status,
                            Notes = a.Notes
                        };
                    })
            );

            var assignedActiveDepartureIds = Assignments
                .Where(a => activeDepartures.Any(d => d.Id == a.DepartureId))
                .Select(x => x.DepartureId)
                .Distinct()
                .ToHashSet();

            TotalGuides = Guides.Count;
            TotalAssignments = Assignments.Count;
            UnassignedActiveTours = Math.Max(0, activeDepartures.Count(d => !assignedActiveDepartureIds.Contains(d.Id)));

            if (AvailableGuides.Count > 0)
            {
                var selectedGuideId = FormSelectedGuide?.Id ?? 0;
                FormSelectedGuide = AvailableGuides.FirstOrDefault(g => g.Id == selectedGuideId) ?? AvailableGuides[0];
            }
            else
            {
                FormSelectedGuide = null;
            }

            if (AvailableDepartures.Count > 0)
            {
                var selectedDepartureId = FormSelectedDeparture?.Id ?? 0;
                FormSelectedDeparture = AvailableDepartures.FirstOrDefault(d => d.Id == selectedDepartureId) ?? AvailableDepartures[0];
            }
            else
            {
                FormSelectedDeparture = null;
            }

            var distinctStatuses = Assignments.Select(a => a.Status).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
            var currentStatus = SelectedScheduleStatus;
            ScheduleStatuses.Clear();
            ScheduleStatuses.Add("Tất cả");
            foreach (var status in distinctStatuses)
            {
                ScheduleStatuses.Add(status);
            }
            if (ScheduleStatuses.Contains(currentStatus))
                SelectedScheduleStatus = currentStatus;
            else
                SelectedScheduleStatus = "Tất cả";

            ApplyGuideFilter();
            ApplyScheduleFilter();
        }

        private void ApplyGuideFilter()
        {
            _guideFilteredSource = string.IsNullOrWhiteSpace(GuideSearchText)
                ? Guides.ToList()
                : Guides.Where(g =>
                        g.FullName.ToLowerInvariant().Contains(GuideSearchText.Trim().ToLowerInvariant()) ||
                        g.Username.ToLowerInvariant().Contains(GuideSearchText.Trim().ToLowerInvariant()) ||
                        g.PhoneNumber.ToLowerInvariant().Contains(GuideSearchText.Trim().ToLowerInvariant()) ||
                        g.Email.ToLowerInvariant().Contains(GuideSearchText.Trim().ToLowerInvariant()))
                    .ToList();

            GuideTotalItems = _guideFilteredSource.Count;
            GuideTotalPages = Math.Max(1, (int)Math.Ceiling(GuideTotalItems / (double)PageSize));
            GuideCurrentPage = 1;
            RefreshGuidePage();
            OnPropertyChanged(nameof(HasNoGuides));
        }

        private void ApplyScheduleFilter()
        {
            var isSearchEmpty = string.IsNullOrWhiteSpace(ScheduleSearchText);
            var lower = isSearchEmpty ? string.Empty : ScheduleSearchText.Trim().ToLowerInvariant();
            var filterStatus = SelectedScheduleStatus == "Tất cả" ? null : SelectedScheduleStatus;

            _assignmentFilteredSource = Assignments.Where(a =>
                    (isSearchEmpty ||
                     a.GuideName.ToLowerInvariant().Contains(lower) ||
                     a.TourName.ToLowerInvariant().Contains(lower) ||
                     (a.Status ?? string.Empty).ToLowerInvariant().Contains(lower) ||
                     a.WorkStart.ToString("dd/MM/yyyy").Contains(lower)) &&
                    (filterStatus == null || string.Equals(a.Status, filterStatus, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            AssignmentTotalItems = _assignmentFilteredSource.Count;
            AssignmentTotalPages = Math.Max(1, (int)Math.Ceiling(AssignmentTotalItems / (double)PageSize));
            AssignmentCurrentPage = 1;
            RefreshAssignmentPage();
            OnPropertyChanged(nameof(HasNoAssignments));
        }

        [RelayCommand(CanExecute = nameof(CanGoToPreviousGuidePage))]
        private void PreviousGuidePage()
        {
            if (!CanGoToPreviousGuidePage)
            {
                return;
            }

            GuideCurrentPage--;
        }

        [RelayCommand(CanExecute = nameof(CanGoToNextGuidePage))]
        private void NextGuidePage()
        {
            if (!CanGoToNextGuidePage)
            {
                return;
            }

            GuideCurrentPage++;
        }

        [RelayCommand(CanExecute = nameof(CanGoToPreviousAssignmentPage))]
        private void PreviousAssignmentPage()
        {
            if (!CanGoToPreviousAssignmentPage)
            {
                return;
            }

            AssignmentCurrentPage--;
        }

        [RelayCommand(CanExecute = nameof(CanGoToNextAssignmentPage))]
        private void NextAssignmentPage()
        {
            if (!CanGoToNextAssignmentPage)
            {
                return;
            }

            AssignmentCurrentPage++;
        }

        private void RefreshGuidePage()
        {
            FilteredGuides.Clear();
            foreach (var item in _guideFilteredSource
                         .Skip((Math.Max(GuideCurrentPage, 1) - 1) * PageSize)
                         .Take(PageSize))
            {
                FilteredGuides.Add(item);
            }

            OnPropertyChanged(nameof(CanGoToPreviousGuidePage));
            OnPropertyChanged(nameof(CanGoToNextGuidePage));
            OnPropertyChanged(nameof(ShowGuidePagination));
            OnPropertyChanged(nameof(GuidePaginationSummary));
            PreviousGuidePageCommand.NotifyCanExecuteChanged();
            NextGuidePageCommand.NotifyCanExecuteChanged();
        }

        private void RefreshAssignmentPage()
        {
            FilteredAssignments.Clear();
            foreach (var item in _assignmentFilteredSource
                         .Skip((Math.Max(AssignmentCurrentPage, 1) - 1) * PageSize)
                         .Take(PageSize))
            {
                FilteredAssignments.Add(item);
            }

            OnPropertyChanged(nameof(CanGoToPreviousAssignmentPage));
            OnPropertyChanged(nameof(CanGoToNextAssignmentPage));
            OnPropertyChanged(nameof(ShowAssignmentPagination));
            OnPropertyChanged(nameof(AssignmentPaginationSummary));
            PreviousAssignmentPageCommand.NotifyCanExecuteChanged();
            NextAssignmentPageCommand.NotifyCanExecuteChanged();
        }

        private void ClearAssignForm()
        {
            FormAssignmentNotes = string.Empty;
            FormAssignmentStatus = "Đang phân công";
            if (FormSelectedDeparture != null)
            {
                OnFormSelectedDepartureChanged(FormSelectedDeparture);
            }
        }

        private bool HasWorkScheduleConflict(
            int guideUserId,
            DateTime workStart,
            DateTime workEnd,
            DateTime departureStartDate,
            int departureDurationDays,
            int? excludeAssignmentId,
            out string conflictTourName,
            out DateTime conflictWorkStart,
            out DateTime conflictWorkEnd)
        {
            var targetDepartureStart = departureStartDate == DateTime.MinValue
                ? workStart.Date
                : departureStartDate.Date;
            var targetStart = workStart.Date < targetDepartureStart ? workStart.Date : targetDepartureStart;
            var targetEnd = workEnd.Date;
            var targetPlannedEnd = ResolvePlannedEndDate(targetDepartureStart, departureDurationDays);
            if (targetEnd < targetPlannedEnd)
            {
                targetEnd = targetPlannedEnd;
            }

            foreach (var item in Assignments)
            {
                if (item.GuideUserId != guideUserId)
                {
                    continue;
                }

                if (excludeAssignmentId.HasValue && item.AssignmentId == excludeAssignmentId.Value)
                {
                    continue;
                }

                if (string.Equals(item.Status, "Đã hủy", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var existingDuration = ResolveDepartureDurationDays(item.DepartureId);
                var existingDepartureStart = item.DepartureStartDate == DateTime.MinValue
                    ? item.WorkStart.Date
                    : item.DepartureStartDate.Date;
                var existingStart = item.WorkStart.Date < existingDepartureStart ? item.WorkStart.Date : existingDepartureStart;
                var existingEnd = item.WorkEnd.Date;
                var existingPlannedEnd = ResolvePlannedEndDate(existingDepartureStart, existingDuration);
                if (existingEnd < existingPlannedEnd)
                {
                    existingEnd = existingPlannedEnd;
                }

                var isOverlapping = targetStart <= existingEnd && targetEnd >= existingStart;
                if (!isOverlapping)
                {
                    continue;
                }

                conflictTourName = item.TourName;
                conflictWorkStart = existingStart;
                conflictWorkEnd = existingEnd;
                return true;
            }

            conflictTourName = string.Empty;
            conflictWorkStart = DateTime.MinValue;
            conflictWorkEnd = DateTime.MinValue;
            return false;
        }

        private int ResolveDepartureDurationDays(int departureId)
        {
            return _departureDurationByDepartureId.TryGetValue(departureId, out var duration)
                ? Math.Max(1, duration)
                : 1;
        }

        private static DateTime ResolvePlannedEndDate(DateTime departureStartDate, int durationDays)
        {
            return departureStartDate.Date.AddDays(Math.Max(1, durationDays) - 1);
        }

        private static bool IsGuideUser(User user)
        {
            return user.IsActive && string.Equals(user.Role, "Guide", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsActiveDeparture(Departure departure)
        {
            return departure.StartDate.Date >= DateTime.Today
                   && !string.Equals(departure.Status, "Đóng", StringComparison.OrdinalIgnoreCase);
        }
    }
}

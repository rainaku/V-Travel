# Tổng hợp các Security Fixes trong Session

## Từ báo cáo đầu tiên (Issues #9, #10, #11)

---

### Fix #9: Mã giảm giá "Khách mới" bị lạm dụng

**File:** `VietTravel.Data/Services/PromoCodeService.cs`

**Vấn đề:** Logic cũ chỉ loại trừ booking có status "Đã hủy" → khách tạo booking, hủy, rồi dùng lại mã "khách mới" vô hạn.

**Fix:** Thay đổi logic kiểm tra "khách mới" thành 2 tầng:
1. Kiểm tra có booking nào ở trạng thái "Đã xác nhận" → chắc chắn không phải khách mới
2. Kiểm tra có payment nào đã có `payment_date` (đã thanh toán thực tế) → ngăn trường hợp booking bị hủy sau khi đã thanh toán

```csharp
// TRƯỚC (dễ bị lạm dụng):
var hasPriorBooking = bookings.Any(b =>
    !CancelledBookingStatuses.Any(s => string.Equals(s, b.Status, ...)));

// SAU (chặt hơn):
var hasConfirmedBooking = bookings.Any(b =>
    string.Equals(b.Status, BookingStatuses.Confirmed, StringComparison.OrdinalIgnoreCase));

// + kiểm tra payment đã thanh toán
var payments = (await client.From<Payment>()
    .Filter("booking_id", Operator.In, bookingIds).Get()).Models;
var hasCompletedPayment = payments.Any(p => p.PaymentDate != null);
```

---

### Fix #10: Không giới hạn trên cho số khách

**File:** `VietTravel.UI/ViewModels/BookingListViewModel.cs`

**Vấn đề:** Chỉ check `guests > 0` và hardcoded `> 500`, không check `AvailableSlots`.

**Fix:** Thêm early validation so sánh với `FormDeparture.AvailableSlots` ngay trước khi gọi DB:

```csharp
// Validate against departure available slots (early check before DB re-fetch)
if (FormDeparture.AvailableSlots > 0 && guests > FormDeparture.AvailableSlots)
{
    MessageBox.Show($"Số khách ({guests}) vượt quá số chỗ còn trống ({FormDeparture.AvailableSlots})...");
    return;
}
```

---

### Fix #11: Phân quyền RBAC không đầy đủ

**File:** `VietTravel.UI/ViewModels/AdminShellViewModel.cs`

**Fix:** Thêm hệ thống permission granular:

```csharp
public bool IsEmployeeOrHigher => GetRoleLevel(UserRole) >= GetRoleLevel("Employee");
public bool CanManageTours => IsAdminOrHigher;
public bool CanEditPricing => IsAdminOrHigher;
public bool CanCreateBooking => IsEmployeeOrHigher;
public bool CanCancelBooking => IsAdminOrHigher;
public bool CanManageCustomers => IsAdminOrHigher;
public bool CanManageUsers => IsAdminOrHigher;
public bool CanManagePayments => IsAdminOrHigher;
public bool CanManagePromotions => IsAdminOrHigher;
public bool CanViewReports => IsEmployeeOrHigher;
public bool CanManageDepartures => IsAdminOrHigher;
```

---

## Từ báo cáo thứ hai (Issues #1–#10)

---

### Fix #1: Employee truy cập Tours, Departures, Debug

**Files:** `AdminShellViewModel.cs` + `AdminShellView.xaml`

**Vấn đề:** Chỉ 4 trang bị chặn (Customers, Users, Payments, Promotions). Employee vẫn vào được Tours, Departures, Debug.

**Fix ViewModel:** Mở rộng `IsAdminOnlyPage()` và thêm `IsEmployeeOrHigherPage()`:

```csharp
private static bool IsAdminOnlyPage(string? pageName)
{
    return string.Equals(pageName, "Customers", ...)
           || string.Equals(pageName, "Users", ...)
           || string.Equals(pageName, "Payments", ...)
           || string.Equals(pageName, "Promotions", ...)
           || string.Equals(pageName, "Tours", ...)        // MỚI
           || string.Equals(pageName, "Departures", ...)   // MỚI
           || string.Equals(pageName, "Debug", ...);       // MỚI
}

private static bool IsEmployeeOrHigherPage(string? pageName)
{
    return string.Equals(pageName, "Bookings", ...)
           || string.Equals(pageName, "Reports", ...)
           || string.Equals(pageName, "Notifications", ...);
}
```

**Fix XAML — Ẩn menu items:** Thêm `Visibility` binding cho từng mục sidebar:

| Menu item | Binding | Ai thấy |
|---|---|---|
| Quản lý Tour | `CanAccessAdminOnlyModules` | Admin+ |
| Lịch Khởi Hành | `CanAccessAdminOnlyModules` | Admin+ |
| Quản lý Booking | `IsEmployeeOrHigher` | Employee+ |
| Thông báo | `IsEmployeeOrHigher` | Employee+ |
| Báo Cáo | `IsEmployeeOrHigher` | Employee+ |
| Header "THỐNG KÊ" | `IsEmployeeOrHigher` | Employee+ |
| Debug section | `IsDebugMenuVisible` AND `IsAdminOrHigher` | Admin+ (MultiDataTrigger) |

---

### Fix #2: AvailableSlots có thể vượt MaxSlots khi sửa Departure

**File:** `VietTravel.UI/ViewModels/DepartureListViewModel.cs`

**Vấn đề:** Không kiểm tra `availSlots <= maxSlots`.

**Fix:**

```csharp
if (availSlots > maxSlots)
{
    MessageBox.Show($"Chỗ còn lại ({availSlots}) không được vượt quá tổng chỗ ({maxSlots}).", ...);
    return;
}
```

---

### Fix #3: Admin có thể ban Admin khác (không bảo vệ cấp bậc)

**File:** `VietTravel.UI/ViewModels/UserManagementViewModel.cs`

**Vấn đề:** Admin A có thể ban Admin B. Không bảo vệ SuperAdmin.

**Fix:** Thêm role hierarchy check + SuperAdmin protection:

```csharp
// Prevent banning users of equal or higher role
var currentRoleLevel = GetRoleLevel(_mainViewModel.CurrentUser?.Role);
var targetRoleLevel = GetRoleLevel(userItem.Role);
if (targetRoleLevel >= currentRoleLevel)
{
    MessageBox.Show("Bạn không thể khóa tài khoản có cùng cấp hoặc cao hơn...");
    return;
}

// Protect SuperAdmin account
if (string.Equals(userItem.Username, SuperAdminUsername, StringComparison.OrdinalIgnoreCase))
{
    MessageBox.Show("Không thể khóa tài khoản Super Admin.", ...);
    return;
}
```

---

### Fix #4: Reset mật khẩu chỉ yêu cầu 4 ký tự

**File:** `VietTravel.UI/ViewModels/UserManagementViewModel.cs`

**Vấn đề:** Admin reset password cho user chỉ cần 4 ký tự, trong khi đăng ký yêu cầu 8 + chữ hoa + chữ thường + số.

**Fix:** Nâng lên 8 ký tự + yêu cầu complexity (nhất quán với đăng ký):

```csharp
if (EditNewPassword.Length < 8)
{
    MessageBox.Show("Mật khẩu mới phải có ít nhất 8 ký tự.", ...);
    return;
}
if (!EditNewPassword.Any(char.IsUpper) || !EditNewPassword.Any(char.IsLower) || !EditNewPassword.Any(char.IsDigit))
{
    MessageBox.Show("Mật khẩu phải chứa ít nhất 1 chữ hoa, 1 chữ thường và 1 số.", ...);
    return;
}
```

---

### Fix #5: Không có audit log cho thao tác quản lý user

**File:** `VietTravel.UI/ViewModels/UserManagementViewModel.cs`

**Vấn đề:** Ban/unban, đổi role, reset password, sửa user — không ghi log.

**Fix:** Thêm `AuditLogService.LogFireAndForget()` cho tất cả thao tác:

```csharp
// Ban/Unban
AuditLogService.LogFireAndForget(client, userId, "USER_BAN"/"USER_UNBAN", "User", targetId, details);

// Role change
AuditLogService.LogFireAndForget(client, userId, "USER_ROLE_CHANGE", "User", targetId, details);

// Edit user
AuditLogService.LogFireAndForget(client, userId, "USER_EDIT", "User", targetId, details);

// Password reset
AuditLogService.LogFireAndForget(client, userId, "USER_PASSWORD_RESET", "User", targetId, details);
```

---

### Fix #6: Tour giá không giới hạn trên

**File:** `VietTravel.UI/ViewModels/TourListViewModel.cs`

**Vấn đề:** `TryParsePositiveMoney()` chỉ check `> 0`, có thể set giá 999 tỷ hoặc 1 VND.

**Fix:** Thêm upper limit 10 tỷ VND:

```csharp
const decimal MaxTourPrice = 10_000_000_000m;
if (value > MaxTourPrice)
{
    value = 0;
    return false;
}
```

---

### Fix #7: Departure có thể tạo với ngày trong quá khứ

**File:** `VietTravel.UI/ViewModels/DepartureListViewModel.cs`

**Vấn đề:** Không kiểm tra `FormStartDate > DateTime.Now`.

**Fix:**

```csharp
if (!IsEditing && FormStartDate.Date <= DateTime.Today)
{
    MessageBox.Show("Ngày khởi hành phải là ngày trong tương lai.", ...);
    return;
}
```

---

### Fix #8: Tắt promo code active không cảnh báo

**File:** `VietTravel.UI/ViewModels/PromoCodeManagementViewModel.cs`

**Vấn đề:** Promo code active bị toggle off mà không cảnh báo ảnh hưởng booking pending.

**Fix:** Thêm confirmation dialog khi deactivate:

```csharp
if (item.PromoCode.IsActive)
{
    var confirm = MessageBox.Show(
        $"Mã \"{item.Code}\" đang hoạt động. Tắt mã sẽ ảnh hưởng đến các booking đang pending...",
        "Xác nhận tắt mã giảm giá", MessageBoxButton.YesNo, MessageBoxImage.Warning);
    if (confirm != MessageBoxResult.Yes) return;
}
```

---

### Fix #9 (báo cáo 2): Debug menu cho non-admin

**File:** `AdminShellViewModel.cs` + `AdminShellView.xaml`

**Fix ViewModel:** Thêm "Debug" vào `IsAdminOnlyPage()`.

**Fix XAML:** Dùng `MultiDataTrigger` yêu cầu cả `IsDebugMenuVisible=True` VÀ `IsAdminOrHigher=True`:

```xml
<MultiDataTrigger>
    <MultiDataTrigger.Conditions>
        <Condition Binding="{Binding IsDebugMenuVisible}" Value="True"/>
        <Condition Binding="{Binding IsAdminOrHigher}" Value="True"/>
    </MultiDataTrigger.Conditions>
    <Setter Property="Visibility" Value="Visible"/>
</MultiDataTrigger>
```

---

### Fix #10 (báo cáo 2): Double-cancel booking (race condition)

**File:** `VietTravel.UI/ViewModels/BookingListViewModel.cs`

**Vấn đề:** `CancelBookingAsync()` check status trên local data (stale). Nếu 2 admin cancel cùng lúc → release slots 2 lần.

**Fix:** Re-fetch booking từ DB trước khi cancel:

```csharp
// Re-fetch booking from DB to prevent double-cancel race condition
var bookingResp = await client.From<Booking>().Where(b => b.Id == booking.Id).Get();
var freshBooking = bookingResp.Models.FirstOrDefault();
if (freshBooking == null) { /* error */ return; }
if (BookingStatuses.IsCancelled(freshBooking.Status))
{
    MessageBox.Show("Booking này đã được hủy trước đó.", ...);
    await LoadDataAsync();
    return;
}
// Proceed with cancel using freshBooking data
```

---

## Tổng kết

| # | Severity | Lỗ hổng | File chính |
|---|----------|---------|------------|
| 1 | 🔴 Critical | Employee truy cập Tours/Departures/Debug | AdminShellViewModel + XAML |
| 2 | 🔴 Critical | AvailableSlots > MaxSlots | DepartureListViewModel |
| 3 | 🔴 Critical | Admin ban Admin khác | UserManagementViewModel |
| 4 | 🔴 Critical | Password reset 4 ký tự | UserManagementViewModel |
| 5 | 🔴 Critical | Không audit log | UserManagementViewModel |
| 6 | 🟠 High | Giá tour không giới hạn | TourListViewModel |
| 7 | 🟠 High | Departure ngày quá khứ | DepartureListViewModel |
| 8 | 🟠 High | Tắt promo không cảnh báo | PromoCodeManagementViewModel |
| 9 | 🟠 High | Debug menu non-admin | AdminShellViewModel + XAML |
| 10 | 🟠 High | Double-cancel booking | BookingListViewModel |
| 11 | 🟠 High | Promo "khách mới" lạm dụng | PromoCodeService |
| 12 | 🟠 High | Guest count > available slots | BookingListViewModel |
| 13 | 🟠 High | RBAC thiếu granular permissions | AdminShellViewModel |

**Tất cả fixes đã compile thành công (0 errors, 0 diagnostics).**

# KẾ HOẠCH DỰ ÁN VIET-TRAVEL (WPF)

## MÔ TẢ PHẠM VI
- 3 Vai trò: Admin, Employee, Customer
- Quy trình: Tạo tour → Mở lịch khởi hành → Khách đặt tour → Thanh toán → Quản lý khách.
- Các module: Quản lý Tour, Lịch trình, Booking, Khách hàng, Thanh toán cơ bản, Báo cáo cơ bản.

## Giai đoạn 1: Khởi tạo Project & Cấu trúc Base (HOÀN THÀNH)
- [x] Khởi tạo hệ thống Project Solution (`dotnet new sln`)
- [x] Tạo các project con: `VietTravel.Core`, `VietTravel.Data`, `VietTravel.UI`
- [x] Link các project lại với nhau
- [x] Cài đặt các nuget packages cơ bản (CommunityToolkit.Mvvm, Entity Framework Core, MaterialDesignThemes, LiveChartsCore)

## Giai đoạn 2: Thiết kế Database & Models (Supabase) (HOÀN THÀNH)
- [x] Định nghĩa các Entities với Postgrest Attributes: `User`, `Tour`, `Departure`, `Customer`, `Booking`, `Payment`
- [x] Thiết lập Connection/Khởi tạo Supabase Client.
- [x] Đồng bộ Table structure lên Supabase Dashboard.

## Giai đoạn 3: Xây dựng UI Framework & Architecture (ĐANG THỰC HIỆN)
- [x] Custom `App.xaml` cho Material Design
- [x] Thiết kế UI base: Nền trắng, Bo góc, Title xanh có đổ bóng Blur như yêu cầu
- [ ] Tạo Navigation Service và Main Window Shell
- [ ] Màn hình Login phân quyền (Admin, Employee)
- [ ] Layout Dashboard hiển thị menu bên trái

## Giai đoạn 4: Module Quản lý Tour & Lịch Trình
- [ ] Quản lý Tour (CRUD, View List)
- [ ] Quản lý Lịch Trình (Ngày đi, Số chỗ tối đa, Trạng thái đóng/mở bán)

## Giai đoạn 5: Module Khách Hàng & Đặt Tour (Booking)
- [ ] Quản lý Khách hàng (Lưu thông tin, Lịch sử)
- [ ] Flow Đặt Tour: Chọn Tour → Chọn Lịch Trình → Tạo Booking, giảm số chỗ còn lại

## Giai đoạn 6: Module Thanh Toán & Báo Cáo
- [ ] Quản lý Thanh toán mô phỏng (Chưa thanh toán, Đã cọc, Đã thanh toán)
- [ ] Màn hình Báo cáo (Tổng số tour, booking, doanh thu tạm tính, tour hot nhất)

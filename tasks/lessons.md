# LESSONS LEARNED

## Cấu trúc Kiến trúc
- Sử dụng MVVM pattern để tách biệt UI (`VietTravel.UI`) và Data/Logic (`VietTravel.Core`, `VietTravel.Data`).
- Database Context được quản lý ở Data layer. Mọi query đến DB thông qua EF Core.

## Quản lý UI
- Tránh Code-Behind, mọi behavior/command đặt ở ViewModels.
- UI components được setup qua MaterialDesignThemes.

## Sửa Lỗi Ngăn Ngừa
- (Chưa có lỗi nào được ghi nhận)

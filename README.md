# 🇻🇳 Viet Travel - Hệ Thống Quản Lý Tour Du Lịch

Ứng dụng quản lý tour du lịch cao cấp được xây dựng trên nền tảng **WPF (.NET 10)** với phong cách thiết kế **Apple/macOS** hiện đại, tinh tế và tối ưu trải nghiệm người dùng.

![Apple UI Design](https://img.shields.io/badge/Design-Apple%20Style-blue)
![Framework](https://img.shields.io/badge/Framework-WPF%20.NET%2010-blueviolet)
![Database](https://img.shields.io/badge/Backend-Supabase-green)

---

## ✨ Tính Năng Nổi Bật

### 🍎 Trải Nghiệm Người Dùng (UX/UI)
- **Glassmorphism:** Hiệu ứng kính mờ tinh tế trên toàn bộ giao diện.
- **Smooth Animations:** Chuyển động mượt mà, viền bo tròn (`16px`) nhất quán.
- **Responsive Design:** Tối ưu hiển thị cho nhiều độ phân giải màn hình.
- **Clean Typography:** Sử dụng hệ thống font chữ hiện đại (Apple-style system fonts).

### 🛠️ Chức Năng Chính
- **Hệ Thống Đăng Ký/Đăng Nhập:** Bảo mật cao, hỗ trợ phân quyền Admin, Employee và Customer.
- **Quản Lý Tour:** Dashboard trực quan, thống kê doanh thu bằng biểu đồ real-time.
- **Đặt Tour & Thanh Toán:** Quy trình đặt tour linh hoạt dành cho khách hàng.
- **Quản Lý Khách Hàng:** Hệ thống quản lý thông tin khách hàng chuyên nghiệp.
- **Báo Cáo & Thống Kê:** Xuất dữ liệu và biểu thị trực quan các chỉ số kinh doanh.

## 🚀 Công Nghệ Sử Dụng

- **Frontend:** WPF (.NET 10), Material Design In XAML Toolkit.
- **Pattern:** MVVM (CommunityToolkit.Mvvm).
- **Backend:** Supabase (PostgreSQL, Authentication).
- **Charts:** LiveCharts2 cho hiển thị dữ liệu mạnh mẽ.
- **Styling:** Custom CSS/XAML Design System (AppleDesignSystem.xaml).

## 📥 Cài Đặt & Chạy Ứng Dụng

1. **Yêu Cầu Hệ Thống:**
   - Windows 10/11.
   - .NET 10 SDK hoặc mới hơn.

2. **Cấu Hình Môi Trường:**
   - Tạo file `.env` tại thư mục gốc với các thông tin sau:
     ```env
     SUPABASE_URL=YOUR_SUPABASE_URL
     SUPABASE_KEY=YOUR_SUPABASE_ANON_KEY
     ```

3. **Chạy Ứng Dụng:**
   - Sử dụng script PowerShell đã chuẩn bị sẵn để build và chạy nhanh:
     ```powershell
     .\r.ps1
     ```

## 📂 Cấu Trúc Dự Án

- `VietTravel.UI`: Chứa mã nguồn giao diện (Views & ViewModels).
- `VietTravel.Data`: Xử lý tương tác database và service (Supabase integration).
- `VietTravel.Core`: Chứa các model dữ liệu dùng chung.
- `Themes`: Hệ thống design system trung tâm.

---

**Viet Travel** - *Hành trình khám phá vẻ đẹp Việt Nam qua từng nhấp chuột.* 🇻🇳

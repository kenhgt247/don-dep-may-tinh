# PC Cleaner v1.2.1 — Sửa lỗi tạo biểu tượng Windows

**Bản sửa lỗi từ v1.2.0** · Windows 10/11 · Tiếng Việt / English

## 🛠️ Đã sửa / Fixed

- **Sửa lỗi “Exception has been thrown by the target of an invocation”** khi nhấn **Tạo biểu tượng**.
- Thay phương pháp tạo shortcut dùng **WScript.Shell + Reflection** bằng **Windows Shell Link API** gốc, hỗ trợ đường dẫn Unicode và thư mục Desktop chuyển hướng qua OneDrive.
- Tạo shortcut riêng tại **Desktop** và **Start Menu**: nếu một vị trí bị từ chối quyền, vẫn tạo ở vị trí còn lại và hiển thị thông báo cụ thể.
- Sửa chung cơ chế tạo shortcut của tùy chọn **chạy ngầm cùng Windows**.
- Bổ sung tự kiểm tra tạo file **.lnk** thật trong thư mục tạm trên Windows và xác thực header định dạng shortcut.

## ✅ Giữ nguyên các tính năng 1.2.0

- Chuyển **🇻🇳 Tiếng Việt / 🇬🇧 English** và ghi nhớ ngôn ngữ.
- Dọn rác, phân tích ổ đĩa, file trùng, chuyển và khôi phục file, dọn dữ liệu trình duyệt và cảnh báo ổ gần đầy.
- Không thay đổi cơ chế bảo vệ file cá nhân, tin nhắn Zalo và xác nhận trước khi xóa.

## ⬇️ Cách cài / Install

1. Tải **`DonDepMayTinh.zip`** trong phần **Assets** bên dưới.
2. Giải nén file ZIP rồi chạy **`Don Dep May Tinh.exe`** (Windows 10/11).
3. Mở tab **Tự động → Tạo biểu tượng** để tạo shortcut trên Desktop và trong Start Menu.

**English:** Fixes the shortcut creation error by using Windows native Shell Link API. Supports Unicode paths and OneDrive-redirected Desktop; reports Desktop and Start Menu results separately. Download the ZIP, extract and run the EXE.

**Lưu ý:** File EXE chưa có chữ ký số; Windows SmartScreen có thể cảnh báo. Hãy chỉ tải từ trang GitHub chính thức của dự án.

Nguồn: https://github.com/kenhgt247/don-dep-may-tinh

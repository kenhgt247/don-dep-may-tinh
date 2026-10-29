# PC Cleaner v1.3.1 — Smart Folder cho phép quét ổ D:\\ và ổ dữ liệu

## 🛠️ Đã sửa

- Sửa lỗi **“Thư mục hệ thống, Zalo, ứng dụng và đồng bộ đám mây không được hỗ trợ”** khi chọn trực tiếp ổ dữ liệu `D:\\` hoặc ổ USB.
- Sửa chuẩn hóa đường dẫn gốc (`D:\\` khác `D:` trong Windows), ngăn tạo đường dẫn đích không chính xác.
- Vẫn **chặn quét trực tiếp ổ chứa Windows (thường là `C:\\`)**, các thư mục hệ thống, dữ liệu Zalo, ứng dụng, OneDrive/Drive và đường dẫn liên kết.
- Khi chọn toàn ổ: **xác nhận trước khi quét**, và **không tự đánh dấu file để di chuyển**. Có nút **Chọn đề xuất / Bỏ chọn hết** nếu người dùng muốn tự chọn hàng loạt.
- Cảnh báo khi danh sách quét chạm giới hạn **20.000 file** (kết quả chưa đầy đủ).
- Giới hạn tính SHA-256 cho file tối đa 64 MB, tổng dữ liệu băm tối đa 512 MB để tránh quá tải khi quét cả ổ.

## 🛡️ An toàn

Không tự xóa file, không ghi đè file đích, giữ nguyên file không rõ loại và có hoàn tác qua nhật ký. Khi chọn ổ dữ liệu, xem kỹ danh sách file nguồn → đích trước khi xác nhận. Để thận trọng, nên thử trước với một thư mục nhỏ.

## ⬇️ Cài đặt

Tải `DonDepMayTinh.zip` ở mục Assets, giải nén, chạy `Don Dep May Tinh.exe` trên Windows 10/11, vào **Smart Folder → Chọn thư mục → D:\\ → Quét & đề xuất**.

**English:** Smart Folder v1.3.1 allows scanning a non-system data drive root (e.g. D:\\) or a USB drive, while still blocking the Windows system drive. Whole-drive scans require explicit confirmation, start with no files selected, and never delete or overwrite files.

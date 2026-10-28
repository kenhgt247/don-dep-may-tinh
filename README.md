# Dọn Dẹp Máy Tính

Phần mềm **miễn phí** giúp máy tính Windows hết cảnh "ổ C đầy": dọn rác, tìm thứ đang chiếm chỗ, và chuyển bớt file lên **Google Drive** hoặc **ổ cứng ngoài** — luôn hỏi bạn trước khi làm.

## ⬇️ Tải về

👉 **[Tải bản mới nhất tại đây](https://github.com/kenhgt247/don-dep-may-tinh/releases/latest)** → bấm vào file `DonDepMayTinh.zip`.

1. Giải nén ra (ví dụ ra Desktop).
2. Bấm đúp **Don Dep May Tinh.exe**.
3. Nếu Windows hiện cảnh báo màu xanh *"Windows protected your PC"*: bấm **More info → Run anyway**.

Chạy trên Windows 10 / 11. Không cần cài đặt thêm gì.

Phần mềm hoàn toàn miễn phí. Ký số miễn phí bởi [SignPath.io](https://about.signpath.io), chứng chỉ của [SignPath Foundation](https://signpath.org) (đang chờ duyệt — xem [Chính sách ký số](CODE_SIGNING_POLICY.md)). [Chính sách quyền riêng tư](PRIVACY.md).

## Tính năng

| Tab | Làm gì |
|---|---|
| **Dọn dẹp** | Quét rác hệ thống (file tạm, bộ nhớ đệm trình duyệt/Zalo/Teams, thùng rác, gói cập nhật cũ), ảnh/video Zalo cũ, file cũ trong Downloads, file lớn. Xóa rác hoặc chuyển file đi. |
| **Bản đồ ổ đĩa** | Xem thư mục nào chiếm nhiều chỗ nhất, đi sâu từng cấp, chọn để chuyển đi. |
| **File trùng** | Tìm các file giống hệt nhau, giữ lại 1 bản, bản thừa vào Thùng rác. |
| **Đã chuyển đi** | Danh sách file đã chuyển; lấy về đúng chỗ cũ bất cứ lúc nào. |
| **Tự động** | Chạy ngầm cùng Windows, báo khi ổ sắp đầy, tự dọn rác hệ thống theo lịch. |

## An toàn

- Không làm gì khi bạn chưa bấm **Yes** ở bảng xác nhận (bảng này cho biết sẽ xóa gì, chuyển gì, ổ còn trống bao nhiêu sau khi dọn).
- Không cho chuyển nếu nơi nhận không đủ chỗ.
- File chỉ bị xóa khỏi máy **sau khi** đã chép xong và kiểm tra đúng.
- Không bao giờ đụng tới dữ liệu tin nhắn Zalo, thư mục Windows và chương trình.
- Với Google Drive, app chỉ thấy được những file do chính nó tải lên (dùng công cụ mã nguồn mở [rclone](https://rclone.org)).
- Toàn bộ mã nguồn công khai trong thư mục [`app/`](app/) — ai cũng kiểm tra được.

## Câu chuyện

Tôi quá khó chịu vì ổ cứng lúc nào cũng đầy — máy chậm, Zalo không mở được, lưu file thì báo hết chỗ. Mấy phần mềm dọn rác ngoài kia thì xóa lung tung hoặc bắt trả tiền. Nên tôi tự làm công cụ này, và chia sẻ miễn phí cho ai cũng gặp chuyện giống mình.

## Góp ý

Gặp lỗi hoặc muốn thêm tính năng? Vào mục **[Issues](https://github.com/kenhgt247/don-dep-may-tinh/issues)** → **New issue**, kèm ảnh chụp màn hình nếu được.

## Dành cho người phát triển

- Mã nguồn: `app/DonDep.ps1` (PowerShell 5.1 + Windows Forms), `app/Launcher.cs` (file .exe khởi động).
- Phát hành bản mới: sửa `$Script:AppVersion` trong `app/DonDep.ps1`, rồi vào tab **Actions** → **Đóng gói và phát hành** → **Run workflow**. GitHub tự tạo `.exe`, đóng gói `.zip` và đăng lên trang Releases với thẻ `v<số phiên bản>`.

Giấy phép: [MIT](LICENSE) · [Code signing policy](CODE_SIGNING_POLICY.md) · [Privacy policy](PRIVACY.md)

---

**English:** Free, open-source Windows tool that frees disk space: cleans junk files, shows what is using the disk, finds duplicate files, and moves files to the user's own Google Drive or an external drive — always asking for confirmation first. Free code signing provided by SignPath.io, certificate by SignPath Foundation (application pending).

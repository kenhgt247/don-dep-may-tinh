# Dọn Dẹp Máy Tính

Phần mềm **miễn phí** giúp máy tính Windows hết cảnh "ổ C đầy": dọn rác, tìm thứ đang chiếm chỗ, và chuyển bớt file lên **Google Drive** hoặc **ổ cứng ngoài** — luôn hỏi bạn trước khi làm.

## ⬇️ Tải về

👉 **[Tải bản mới nhất tại đây](https://github.com/kenhgt247/don-dep-may-tinh/releases/latest)** → bấm vào file `DonDepMayTinh.zip`.

1. Giải nén ra (ví dụ ra Desktop).
2. Bấm đúp **Don Dep May Tinh.exe**.
3. Nếu Windows hiện cảnh báo màu xanh *"Windows protected your PC"*: bấm **More info → Run anyway**.

Chạy trên Windows 10 / 11. Chỉ một file `.exe`, không cần cài đặt thêm gì.

Phần mềm hoàn toàn miễn phí, mã nguồn mở. File `.exe` hiện **chưa có chữ ký số** (dự án mới, sẽ đăng ký lại khi có nhiều người dùng) nên Windows có thể hiện cảnh báo lần đầu mở — xem [Chính sách ký số](CODE_SIGNING_POLICY.md). [Chính sách quyền riêng tư](PRIVACY.md).

⭐ Thấy hữu ích? Bấm **Star** ở góc trên bên phải trang này để ủng hộ dự án.

## 🌐 Ngôn ngữ / Languages (v1.2.0)

- **Tiếng Việt** là ngôn ngữ mặc định. Nhấn nút **🇬🇧 English** ở góc phải thanh trạng thái để chuyển sang tiếng Anh.
- Trong giao diện tiếng Anh, nhấn **🇻🇳 Tiếng Việt** để chuyển lại. Lựa chọn được lưu trong `Documents\\DonDepMayTinh\\caidat.json` (khóa `Language`).
- Đổi ngôn ngữ không xóa hoặc di chuyển bất kỳ file nào. Giao diện được tải lại, vì vậy kết quả quét chưa xử lý cần quét lại sau khi chuyển.
- **Lưu ý:** Chức năng song ngữ có trong mã nguồn v1.2.0; để tải bản ZIP mới, cần hoàn tất kiểm tra và phát hành v1.2.0 trong GitHub Actions. Liên kết `releases/latest` có thể vẫn trỏ tới bản cũ trước khi phát hành.

**English:** Use **🇬🇧 English / 🇻🇳 Tiếng Việt** in the bottom-right status bar to switch language. The preference is saved. Switching language refreshes the UI but does not delete or move files.

## Tính năng

| Tab | Làm gì |
|---|---|
| **Dọn dẹp** | Quét rác hệ thống (file tạm, bộ nhớ đệm trình duyệt/Zalo/Teams, thùng rác, gói cập nhật cũ), ảnh/video Zalo cũ, file cũ trong Downloads, file lớn. Rác thì xóa; với file cá nhân, **bạn chọn từng file: chuyển lên Drive / ổ ngoài hay xóa vĩnh viễn** (app gợi ý sẵn: file cài đặt → xóa, ảnh/video → chuyển). |
| **Bản đồ ổ đĩa** | Xem thư mục nào chiếm nhiều chỗ nhất, đi sâu từng cấp. Bấm vào một mục để biết **đó là gì và có xóa được không** (rác / dữ liệu cá nhân / cẩn thận / không được xóa). Chuột phải để xóa (vào Thùng rác hoặc vĩnh viễn) hoặc chuyển lên Drive. Thư mục hệ thống bị chặn, không xóa nhầm được. |
| **File trùng** | Tìm các file giống hệt nhau, giữ lại 1 bản, bản thừa vào Thùng rác. |
| **Đã chuyển đi** | Danh sách file đã chuyển; lấy về đúng chỗ cũ bất cứ lúc nào. |
| **Trình duyệt** | Xem Chrome, Edge, Cốc Cốc, Brave, Opera, Firefox chiếm bao nhiêu chỗ theo từng tài khoản. Dọn **bộ nhớ đệm** (an toàn, chọn sẵn), **lịch sử** và **cookie** (tự chọn; xóa cookie sẽ bị đăng xuất — app cảnh báo 2 lần). Không bao giờ đụng mật khẩu đã lưu, dấu trang, tiện ích. |
| **Tự động** | Chạy ngầm cùng Windows, báo khi ổ sắp đầy, tự dọn rác hệ thống theo lịch. |

## An toàn

- Không làm gì khi bạn chưa bấm **Yes** ở bảng xác nhận (bảng này cho biết sẽ xóa gì, chuyển gì, ổ còn trống bao nhiêu sau khi dọn).
- Không cho chuyển nếu nơi nhận không đủ chỗ.
- File chỉ bị xóa khỏi máy **sau khi** đã chép xong và kiểm tra đúng.
- Không bao giờ đụng tới dữ liệu tin nhắn Zalo, thư mục Windows và chương trình.
- Với Google Drive, app chỉ thấy được những file do chính nó tải lên (dùng công cụ mã nguồn mở [rclone](https://rclone.org)).
- Toàn bộ mã nguồn công khai trong thư mục [`src/`](src/) — ai cũng kiểm tra được.

## Câu chuyện

Tôi quá khó chịu vì ổ cứng lúc nào cũng đầy — máy chậm, Zalo không mở được, lưu file thì báo hết chỗ. Mấy phần mềm dọn rác ngoài kia thì xóa lung tung hoặc bắt trả tiền. Nên tôi tự làm công cụ này, và chia sẻ miễn phí cho ai cũng gặp chuyện giống mình.

## Góp ý

Gặp lỗi hoặc muốn thêm tính năng? Vào mục **[Issues](https://github.com/kenhgt247/don-dep-may-tinh/issues)** → **New issue**, kèm ảnh chụp màn hình nếu được.

## Dành cho người phát triển

- Viết bằng C# (Windows Forms, .NET Framework 4.8 — có sẵn trên Windows 10/11). Mã nguồn trong [`src/`](src/).
- Biên dịch: `dotnet build src/DonDepMayTinh.csproj -c Release`.
- Phát hành bản mới: sửa `Version` trong `src/Core.cs` và `src/DonDepMayTinh.csproj`, rồi vào tab **Actions** → **Đóng gói và phát hành** → **Run workflow**. GitHub tự biên dịch, chạy thử (`--selftest`), đóng gói `.zip` và đăng lên trang Releases.
- Bản 0.x cũ viết bằng PowerShell vẫn còn trong lịch sử Git.

Giấy phép: [MIT](LICENSE) · [Code signing policy](CODE_SIGNING_POLICY.md) · [Privacy policy](PRIVACY.md)

---

**English:** Free, open-source Windows tool that frees disk space: cleans junk files, shows what is using the disk, finds duplicate files, and moves files to the user's own Google Drive or an external drive — always asking for confirmation first. Releases are currently unsigned; the project plans to re-apply to the SignPath Foundation program once it has a wider user base.

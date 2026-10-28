# Privacy Policy / Chính sách quyền riêng tư

**Dọn Dẹp Máy Tính** does not collect, store or send any personal data to the author.
There is no account, no analytics, no advertising and no telemetry.

## Network connections made by the app

| When | Where | What is sent | Why |
|---|---|---|---|
| Each time the app opens, and when the user clicks "Kiểm tra bản mới" | `api.github.com` | A standard web request (your IP address and the app version, as with any website visit) | To check whether a newer version exists |
| Only if the user chooses Google Drive and agrees to download it | `downloads.rclone.org` | A standard download request | To download the free rclone tool used to talk to Google Drive |
| Only after the user signs in to Google and approves | Google Drive (`googleapis.com`) | The files the user explicitly chose to move | To store those files in the user's own Google Drive |

- Google Drive access uses the `drive.file` permission: the app can only see files it uploaded itself, never other files in the user's Drive.
- The Google sign-in token is stored only on the user's computer (`rclone.conf`), never sent to the author.
- Settings, logs and the list of moved files are stored only on the user's computer, in `Documents\DonDepMayTinh`.

## Tiếng Việt

Phần mềm **không thu thập và không gửi bất kỳ dữ liệu cá nhân nào** về cho tác giả. Không có tài khoản, không quảng cáo, không theo dõi.

- Mỗi lần mở, app hỏi GitHub xem có bản mới không (giống việc mở một trang web).
- Chỉ khi bạn chọn Google Drive: app tải công cụ rclone từ rclone.org và tải lên Drive **của chính bạn** những file bạn đã chọn. App chỉ thấy được file do nó tải lên.
- Thiết lập, nhật ký và danh sách file đã chuyển chỉ nằm trên máy bạn (thư mục `Documents\DonDepMayTinh`).

Questions / Câu hỏi: https://github.com/kenhgt247/don-dep-may-tinh/issues

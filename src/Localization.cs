// Vietnamese/English display localization. Internal file paths and commands are never translated.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DonDep
{
    static class UiLanguage
    {
        public static string Code = "vi";
        public static bool English => Code == "en";
        static readonly Dictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Dọn Dẹp Máy Tính", "PC Cleaner" },
            { "  —  quyền quản trị", "  —  administrator" },
            { "Sẵn sàng.", "Ready." },
            { "Dọn dẹp", "Cleanup" },
            { "Bản đồ ổ đĩa", "Disk map" },
            { "File trùng", "Duplicate files" },
            { "Đã chuyển đi", "Moved files" },
            { "Trình duyệt", "Browsers" },
            { "Tự động", "Automation" },
            { "Rác hệ thống", "System junk" },
            { "File lớn", "Large files" },
            { "Tự chọn", "Custom" },
            { "Ổ đĩa", "Drive" },
            { "Ổ đĩa:", "Drive:" },
            { "Quét những gì", "What to scan" },
            { "Rác hệ thống (file tạm, bộ nhớ đệm, bản cập nhật cũ…)", "System junk (temporary files, caches, old updates…)" },
            { "Ảnh / video / file Zalo cũ", "Old Zalo photos / videos / files" },
            { "Thư mục Downloads (file cũ)", "Downloads folder (old files)" },
            { "File lớn (video, file nén, ISO…) — tự chọn từng file", "Large files (videos, archives, ISO…) — review individually" },
            { "Thiết lập", "Settings" },
            { "Coi là \"cũ\" nếu quá (ngày):", "Consider old after (days):" },
            { "File lớn từ (MB):", "Large files from (MB):" },
            { "Giữ trống tối thiểu (GB):", "Minimum free space (GB):" },
            { "Thêm thư mục Zalo khác…", "Add another Zalo folder…" },
            { "Chọn thư mục dữ liệu Zalo (ví dụ D:\\Zalo Data). App chỉ lấy ảnh, video, tài liệu — không đụng tới dữ liệu tin nhắn.", "Choose a Zalo data folder (e.g. D:\\Zalo Data). Only media and documents are scanned, never message databases." },
            { "Loại", "Category" },
            { "Tên", "Name" },
            { "Dung lượng", "Size" },
            { "Ngày sửa", "Modified" },
            { "Thư mục", "Folder" },
            { "Xử lý", "Action" },
            { "Xóa vĩnh viễn", "Delete permanently" },
            { "🗑  Xóa vĩnh viễn", "🗑  Permanently delete" },
            { "☁  Chuyển lên Drive / ổ ngoài", "☁  Move to Drive / external disk" },
            { "Mở thư mục chứa file", "Open containing folder" },
            { "File cần chuyển đi sẽ được lưu ở", "Where to store moved files" },
            { "Kết nối Google Drive", "Connect Google Drive" },
            { "Kết nối lại", "Reconnect" },
            { "Chưa kết nối", "Not connected" },
            { "Đã kết nối", "Connected" },
            { "Ổ cứng ngoài / USB", "External disk / USB" },
            { "Chọn…", "Browse…" },
            { "Chọn thư mục trên ổ cứng ngoài / USB để lưu file chuyển đi", "Select a folder on the external disk / USB for moved files" },
            { "Quét máy", "Scan PC" },
            { "Dọn dẹp ngay", "Clean now" },
            { "Chọn tất cả", "Select all" },
            { "Bỏ chọn hết", "Clear selection" },
            { "Mở nhật ký", "Open logs" },
            { "Quyền admin", "Run as admin" },
            { "Đang là admin", "Running as admin" },
            { "Bấm \"Quét máy\" để bắt đầu.", "Click \"Scan PC\" to start." },
            { "🗑 Xóa (rác)", "🗑 Delete junk" },
            { "🗑 Xóa vĩnh viễn", "🗑 Delete permanently" },
            { "☁ Chuyển đi", "☁ Move" },
            { "  — chưa chọn sẵn, hãy tự đánh dấu file muốn chuyển", "  — not preselected; manually select files to move" },
            { "  — thêm từ tab Bản đồ ổ đĩa", "  — added from Disk map" },
            { "Bắt đầu quét...", "Starting scan..." },
            { "Đang tính: ", "Calculating: " },
            { "Đang tính: Thùng rác", "Calculating: Recycle Bin" },
            { "Thùng rác (Recycle Bin)", "Recycle Bin" },
            { "Thùng rác", "Recycle Bin" },
            { "Tắt chế độ ngủ đông (hiberfil.sys) — chỉ chọn nếu bạn không dùng Hibernate", "Disable hibernation (hiberfil.sys) — select only if you never use Hibernate" },
            { "Không tìm thấy thư mục Zalo. Nếu bạn đã chuyển dữ liệu Zalo sang chỗ khác, bấm \"Thêm thư mục Zalo khác\".", "No Zalo folder was found. If you moved Zalo data, click \"Add another Zalo folder\"." },
            { "Quét xong. Hãy xem danh sách, bỏ chọn những gì muốn giữ, rồi bấm \"Dọn dẹp ngay\".", "Scan complete. Review the list, deselect what you want to keep, then click \"Clean now\"." },
            { "Bạn chưa chọn mục nào. Hãy bấm \"Quét máy\" rồi đánh dấu những gì muốn dọn.", "Nothing selected. Click \"Scan PC\" and check items to clean." },
            { "Đã hủy.", "Cancelled." },
            { "Có lỗi xảy ra: ", "An error occurred: " },
            { "LỖI: ", "ERROR: " },
            { "Đang kiểm tra dung lượng Google Drive...", "Checking Google Drive free space..." },
            { "Hãy bấm \"Kết nối Google Drive\" trước, hoặc chọn \"Ổ cứng ngoài / USB\".", "Connect Google Drive first, or select \"External disk / USB\"." },
            { "Google Drive không đủ chỗ.", "Not enough space on Google Drive." },
            { "Còn trống: ", "Available: " },
            { "Cần: ", "Required: " },
            { "Hãy bỏ bớt file hoặc chọn ổ cứng ngoài.", "Deselect some files or choose an external disk." },
            { "Hãy bấm \"Chọn…\" để chọn thư mục trên ổ cứng ngoài / USB.", "Click \"Browse…\" to choose a folder on the external disk / USB." },
            { "không đủ chỗ an toàn", "does not have enough free space" },
            { "Mức giữ trống tối thiểu: ", "Minimum free space: " },
            { "Bạn sắp:", "You are about to:" },
            { "XÓA VĨNH VIỄN", "PERMANENTLY DELETE" },
            { "Tiếp tục?", "Continue?" },
            { "Không thể khôi phục", "Cannot be recovered" },
            { "Đã chọn ", "Selected " },
            { " mục", " items" },
            { "   •   Xóa rác: ", "   •   Delete junk: " },
            { "   •   Xóa vĩnh viễn: ", "   •   Permanently delete: " },
            { "   •   Chuyển đi: ", "   •   Move: " },
            { "   còn trống ", "   free " },
            { "   ⚠ sắp đầy", "   ⚠ almost full" },
            { "  → sau khi chuyển còn ", "  → after moving " },
            { "Chuyển đi hay xóa vĩnh viễn?", "Move or permanently delete?" },
            { "Xem trước", "Preview" },
            { "Đã kết nối — còn trống ", "Connected — available " },
            { "Phân tích ổ đĩa", "Analyze disk" },
            { "⬆ Lên trên", "⬆ Up" },
            { "Mở trong Explorer", "Open in Explorer" },
            { "Thêm vào danh sách chuyển đi", "Add to move list" },
            { "Bấm vào một mục để biết đó là gì. Chuột phải để xóa hoặc chuyển đi.", "Click an item to learn what it is. Right-click to delete or move." },
            { "Chọn ổ rồi bấm \"Phân tích ổ đĩa\" để xem thư mục nào chiếm nhiều chỗ nhất.", "Select a drive and click \"Analyze disk\" to see the largest folders." },
            { "% ổ đĩa", "% of drive" },
            { "Số file", "Files" },
            { "Đây là gì", "What is this" },
            { "Chọn một mục", "Select an item" },
            { "Bấm vào một thư mục hoặc file trong danh sách, app sẽ cho biết đó là gì và có xóa được không.", "Click a folder or file to see what it is and whether deleting it is safe." },
            { "Xóa (vào Thùng rác)", "Delete (to Recycle Bin)" },
            { "Chuyển lên Drive / ổ ngoài", "Move to Drive / external disk" },
            { "Đây là gì?", "What is this?" },
            { "Đây là gì?  —  ", "What is this?  —  " },
            { "Bắt đầu phân tích ổ ", "Analyzing drive " },
            { "Bấm đúp vào thư mục để xem bên trong. Bấm vào một mục để biết đó là gì; chuột phải để xóa hoặc chuyển đi.", "Double-click a folder to browse. Click an item for details; right-click to delete or move." },
            { "  (một phần không đọc được)", "  (partially inaccessible)" },
            { " thuộc file hệ thống không đọc được", " belongs to inaccessible system files" },
            { " (thử Quyền admin)", " (try Run as admin)" },
            { "Thư mục: ", "Folder: " },
            { "\nDung lượng: ", "\nSize: " },
            { "Đang chọn ", "Selecting " },
            { " mục, tổng ", " items, total " },
            { "Hãy chọn thư mục / file muốn xóa trước.", "Select folders / files to delete first." },
            { "Những mục sau KHÔNG ĐƯỢC XÓA:", "These items MUST NOT be deleted:" },
            { "XÓA VĨNH VIỄN — không khôi phục được:", "PERMANENT DELETE — cannot be undone:" },
            { "Chuyển vào Thùng rác (còn khôi phục được):", "Move to Recycle Bin (can be restored):" },
            { "⚠️ CHÚ Ý:", "⚠️ WARNING:" },
            { "Lưu ý: ổ đĩa chỉ trống thêm sau khi dọn Thùng rác. File quá lớn có thể bị Windows hỏi xóa hẳn.", "Note: disk space is freed after emptying the Recycle Bin. Windows may permanently delete oversized files." },
            { "Bạn chắc chắn chứ?", "Are you sure?" },
            { "Xóa vĩnh viễn ", "Permanently delete " },
            { "Các mục sẽ vào Thùng rác.", "Items will go to the Recycle Bin." },
            { "Đang xóa: ", "Deleting: " },
            { "Đã xóa ", "Deleted " },
            { "vĩnh viễn", "permanently" },
            { "vào Thùng rác", "to Recycle Bin" },
            { "Đã xóa xong ", "Deleted " },
            { "Một số file đang được dùng nên chưa xóa được. Hãy tắt phần mềm đang dùng chúng rồi thử lại.", "Some files are in use. Close their applications and try again." },
            { "Đã giải phóng:", "Freed:" },
            { "Các mục đang nằm trong Thùng rác. Dọn Thùng rác (tab Dọn dẹp) để thật sự lấy lại dung lượng.", "Items are in Recycle Bin. Empty it from the Cleanup tab to actually reclaim the space." },
            { "Đã xóa xong.", "Delete complete." },
            { "Không có file nào mới để thêm.", "No new files to add." },
            { "Đã thêm ", "Added " },
            { "Sẽ thêm ", "Add " },
            { " vào nhóm Tự chọn.", " to Custom group." },
            { "Tìm file giống hệt nhau trong các thư mục:", "Find identical files in folders:" },
            { "Thêm thư mục…", "Add folder…" },
            { "Bỏ thư mục", "Remove folder" },
            { "Chỉ xét file từ (MB):", "Minimum file size (MB):" },
            { "Tìm file trùng", "Find duplicates" },
            { "Xóa bản trùng đã chọn", "Delete selected duplicates" },
            { "Gợi ý", "Suggestion" },
            { "Chọn thư mục để tìm file trùng", "Choose a folder to search for duplicates" },
            { "Hãy thêm ít nhất một thư mục để tìm file trùng.", "Add at least one folder for duplicate scanning." },
            { "Nhóm ", "Group " },
            { "giữ lại", "keep" },
            { "bản trùng", "duplicate" },
            { "Tìm thấy ", "Found " },
            { " nhóm file trùng — bản thừa chiếm ", " duplicate groups — redundant copies use " },
            { "Mỗi nhóm đã chọn sẵn để giữ lại 1 bản.", "Each group is preselected to keep one copy." },
            { "Xem lại các bản được chọn rồi bấm \"Xóa bản trùng đã chọn\".", "Review selected copies, then click \"Delete selected duplicates\"." },
            { "Chưa chọn bản trùng nào.", "No duplicates selected." },
            { "Các nhóm sau đang bị chọn hết tất cả các bản:", "Every copy in these groups was selected:" },
            { "Hãy bỏ chọn ít nhất 1 bản trong mỗi nhóm để giữ lại.", "Uncheck at least one copy in each group." },
            { "bản giống hệt nhau", "identical copies" },
            { "Tải lại danh sách", "Reload list" },
            { "Lấy về chỗ cũ", "Restore to original location" },
            { "Thời gian", "Time" },
            { "Đường dẫn gốc", "Original path" },
            { "Nơi lưu mới", "New location" },
            { "Trạng thái", "Status" },
            { "Tổng cộng ", "Total " },
            { " file đã từng chuyển đi. Đánh dấu file muốn lấy về rồi bấm \"Lấy về chỗ cũ\".", " files moved. Select files to restore and click \"Restore to original location\"." },
            { "Hãy đánh dấu những file (đang ở nơi lưu) muốn lấy về.", "Select the files to restore." },
            { "Đang lấy về: ", "Restoring: " },
            { "Đã lấy về", "Restored" },
            { "Đã có trên máy", "Already on PC" },
            { "Đang ở nơi lưu", "At backup location" },
            { "Chạy ngầm cùng Windows", "Run in background with Windows" },
            { "Bật: tự chạy ngầm khi mở máy (biểu tượng nhỏ ở góc phải thanh Taskbar)", "Enable: run in background at startup (notification-area icon)" },
            { "Cảnh báo khi một ổ còn trống dưới (GB):", "Warn when a drive has less than (GB):" },
            { "Tự động dọn RÁC HỆ THỐNG theo lịch (không bao giờ tự chuyển hay xóa file cá nhân)", "Automatically clean SYSTEM JUNK on schedule (never move or delete personal files)" },
            { "Dọn mỗi (ngày):", "Clean every (days):" },
            { "chưa lần nào", "never" },
            { "Lần tự dọn gần nhất: ", "Last automatic cleanup: " },
            { "Lưu và bật chạy ngầm ngay", "Save and start background mode" },
            { "Lưu thiết lập", "Save settings" },
            { "Biểu tượng", "Shortcuts" },
            { "Tạo biểu tượng \"Dọn Dẹp Máy Tính\" trên Desktop và trong menu Start để mở app nhanh.", "Create a PC Cleaner shortcut on Desktop and Start menu." },
            { "Tạo biểu tượng", "Create shortcut" },
            { "Phiên bản", "Version" },
            { "Bạn đang dùng bản ", "You are using version " },
            { ". App tự kiểm tra bản mới mỗi lần mở. Trang chủ: github.com/", ". The app automatically checks for updates on launch. Homepage: github.com/" },
            { "Kiểm tra bản mới", "Check for updates" },
            { "Mở trang chủ", "Open homepage" },
            { "Đã tạo biểu tượng trên Desktop và menu Start.", "Shortcuts created on Desktop and Start menu." },
            { "Quét trình duyệt", "Scan browsers" },
            { "Dọn trình duyệt đã chọn", "Clean selected browser data" },
            { "Chỉ chọn bộ nhớ đệm", "Select caches only" },
            { "Không bao giờ đụng mật khẩu đã lưu, dấu trang (bookmark) và tiện ích mở rộng. Nên tắt trình duyệt trước khi dọn.", "Saved passwords, bookmarks and extensions are never touched. Close browsers before cleaning." },
            { "Bấm \"Quét trình duyệt\" để xem mỗi trình duyệt đang chiếm bao nhiêu chỗ.", "Click \"Scan browsers\" to see storage usage for each browser." },
            { "Mục", "Item" },
            { "Tài khoản (profile)", "Profile" },
            { "Xóa thì sao?", "What happens if deleted?" },
            { "Bộ nhớ đệm (cache)", "Cache" },
            { "Lịch sử duyệt web", "Browsing history" },
            { "Cookie (dữ liệu đăng nhập trang web)", "Cookies (website sign-in data)" },
            { "An toàn — trang web tự tải lại khi cần", "Safe — websites recreate cache as needed" },
            { "Mất danh sách trang đã xem và lịch sử tải về", "Removes visited pages and download history" },
            { "⚠ Sẽ bị ĐĂNG XUẤT khỏi Facebook, Gmail, Zalo web…", "⚠ You will be SIGNED OUT of Facebook, Gmail, Zalo Web…" },
            { "Có chọn Cookie: sẽ bị đăng xuất khỏi các trang web", "Cookies selected: you will be signed out of websites" },
            { "Đang quét trình duyệt...", "Scanning browsers..." },
            { "Không tìm thấy trình duyệt nào.", "No browsers found." },
            { "Quét xong. Bộ nhớ đệm đã được chọn sẵn; lịch sử và cookie bạn tự chọn nếu muốn.", "Scan complete. Caches are preselected; history and cookies are optional." },
            { "Bạn chưa chọn mục nào. Bấm \"Quét trình duyệt\" rồi đánh dấu những gì muốn dọn.", "Nothing selected. Click \"Scan browsers\", then check the items to clean." },
            { "Hãy tắt hẳn các trình duyệt sau rồi bấm Retry:", "Close these browsers completely, then click Retry:" },
            { "Trình duyệt đang mở sẽ giữ khóa một số file nên không dọn hết được.", "Open browsers lock some files, preventing complete cleanup." },
            { "Bấm Ignore để vẫn dọn phần dọn được.", "Click Ignore to clean what is available." },
            { "Bạn sắp dọn ", "You are about to clean " },
            { "⚠ LỊCH SỬ: mất danh sách trang web đã xem và lịch sử tải về.", "⚠ HISTORY: visited pages and download history will be lost." },
            { "⚠ COOKIE: bạn sẽ bị ĐĂNG XUẤT khỏi các trang web (Facebook, Gmail, Zalo web, ngân hàng…) trong các tài khoản đã chọn. Hãy chắc chắn bạn nhớ mật khẩu.", "⚠ COOKIES: you will be SIGNED OUT of websites (Facebook, Gmail, Zalo Web, banking…) in the selected profiles. Make sure you know your passwords." },
            { "Mật khẩu đã lưu, dấu trang và tiện ích mở rộng KHÔNG bị xóa.", "Saved passwords, bookmarks and extensions will NOT be deleted." },
            { "Xác nhận lần nữa: xóa Cookie của ", "Confirm again: delete cookies for " },
            { " tài khoản trình duyệt?", " browser profiles?" },
            { "Bạn sẽ phải đăng nhập lại các trang web.", "You will need to sign in to websites again." },
            { "Đang dọn: ", "Cleaning: " },
            { "Đã dọn xong trình duyệt! Giải phóng khoảng ", "Browser cleanup complete! Freed approximately " },
            { "Đã dọn xong trình duyệt.", "Browser cleanup complete." },
            { " chưa xóa được vì trình duyệt đang dùng. Tắt trình duyệt rồi dọn lại để xóa hết.", " could not be deleted because the browser is running. Close it and retry." },
            { "Mở Dọn Dẹp Máy Tính", "Open PC Cleaner" },
            { "Dọn rác hệ thống ngay", "Clean system junk now" },
            { "Tắt chạy ngầm", "Exit background mode" },
            { "Đang dọn rác hệ thống...", "Cleaning system junk..." },
            { "Đã dọn xong, giải phóng khoảng ", "Cleanup complete, freed approximately " },
            { "Đã tự dọn rác theo lịch, giải phóng khoảng ", "Scheduled cleanup complete, freed approximately " },
            { "LỖI KHÔNG MONG MUỐN: ", "UNEXPECTED ERROR: " },
            { "Chi tiết đã được ghi vào nhật ký", "Details were written to the log" },
            { " sắp đầy!", " almost full!" },
            { " trống. Bấm vào đây để mở Dọn Dẹp Máy Tính.", " free. Click here to open PC Cleaner." },
            { "Đang quét: ", "Scanning: " },
            { "Đang phân tích (", "Analyzing (" },
            { " giây): ", " seconds): " },
            { "Phân tích xong ổ ", "Finished analyzing drive " },
            { "Đã thấy ", "Found " },
            { " file, đang so sánh...", " files, comparing..." },
            { "⛔ KHÔNG ĐƯỢC XÓA", "⛔ DO NOT DELETE" },
            { "✅ RÁC — xóa an toàn", "✅ JUNK — safe to delete" },
            { "🟢 Thường xóa được", "🟢 Usually safe to delete" },
            { "🔵 Dữ liệu cá nhân", "🔵 Personal data" },
            { "⚠️ CẨN THẬN", "⚠️ CAUTION" },
            { "❔ Chưa rõ là gì", "❔ Unknown item" },
            { "Không được xóa", "Do not delete" },
            { "Rác", "Junk" },
            { "Xóa được", "Safe to delete" },
            { "Cá nhân", "Personal" },
            { "Cẩn thận", "Caution" },
            { "Chưa rõ", "Unknown" },
            { "Chưa rõ là gì", "Unknown item" },
            { "Tin nhắn Zalo", "Zalo messages" },
            { "Dữ liệu Zalo", "Zalo data" },
            { "Bộ nhớ đệm Zalo", "Zalo cache" },
            { "Bộ nhớ đệm", "Cache" },
            { "File tạm", "Temporary files" },
            { "File tạm của Windows", "Windows temporary files" },
            { "File tạm của người dùng", "User temporary files" },
            { "Dữ liệu cá nhân của bạn", "Your personal data" },
            { "Dữ liệu trên ổ phụ", "Data on secondary drive" },
            { "Phần mềm đã cài", "Installed software" },
            { "File cài đặt phần mềm", "Software installer" },
            { "Bản Windows cũ (Windows.old)", "Previous Windows installation (Windows.old)" },
            { "Gói cập nhật Windows đã tải", "Downloaded Windows updates" },
            { "File ngủ đông (Hibernate)", "Hibernation file (Hibernate)" },
            { "Bộ nhớ ảo của Windows", "Windows virtual memory" },
            { "Dữ liệu hệ thống / khôi phục", "System / recovery data" },
            { "Dữ liệu ứng dụng", "Application data" },
            { "Dữ liệu của một phần mềm", "Application data" },
            { "Thư viện lập trình (node_modules)", "Development libraries (node_modules)" },
            { "Máy ảo / giả lập Android", "Virtual machine / Android emulator" },
            { "Ổ đĩa ảo", "Virtual disk" },
            { "Hộp thư Outlook", "Outlook mailbox" },
            { "Chương trình", "Program" },
            { "File ảnh đĩa (ISO)", "Disk image (ISO)" },
            { "File tạm / nhật ký / bản sao cũ", "Temporary files / logs / old backups" },
            { "File nén đã tải về", "Downloaded archives" },
            { "Đừng xóa trực tiếp ở đây.", "Do not delete this directly." },
            { "Không xóa.", "Do not delete." },
            { "Xóa an toàn", "Safe to delete" },
            { "Nên tắt", "Close" },
            { "Không xóa cả thư mục", "Do not delete the entire folder" },
            { "Xóa là mất", "Deleting will lose" },
            { "Xóa có thể làm", "Deleting may cause" },
            { "để còn khôi phục được", "so it can be restored" },
            { "Không đủ chỗ trống để tải công cụ kết nối Google Drive", "Not enough free space to download Google Drive connection utility" },
            { "Đang tải rclone...", "Downloading rclone..." },
            { "Tải về không thành công (kiểm tra kết nối mạng).", "Download failed (check network connection)." },
            { "Không tìm thấy rclone.exe trong file tải về.", "rclone.exe was not found in the downloaded archive." },
            { "Mở trình duyệt để đăng nhập Google Drive...", "Open browser to sign in to Google Drive..." },
            { "Đã kết nối Google Drive.", "Google Drive connected." },
            { "Kết nối Google Drive thất bại: ", "Google Drive connection failed: " },
        };
        static readonly KeyValuePair<string, string>[] Phrases = En.OrderByDescending(p => p.Key.Length).ToArray();

        public static string T(string text)
        {
            if (!English || string.IsNullOrEmpty(text)) return text;
            string exact;
            if (En.TryGetValue(text, out exact)) return exact;
            // Longer phrases first: preserves inserted file paths, counts and disk sizes.
            foreach (var phrase in Phrases)
                if (phrase.Key.Length >= 5 && text.Contains(phrase.Key))
                    text = text.Replace(phrase.Key, phrase.Value);
            return text;
        }

        public static void Attach(Control root)
        {
            if (root == null) return;
            if (!(root is TextBoxBase) && !(root is ListView) && !(root is ComboBox))
            {
                root.Text = T(root.Text);
                root.TextChanged += (s, e) =>
                {
                    if (!English) return;
                    var c = (Control)s;
                    string translated = T(c.Text);
                    if (c.Text != translated) c.Text = translated;
                };
            }
            var list = root as ListView;
            if (list != null)
            {
                foreach (ColumnHeader col in list.Columns) col.Text = T(col.Text);
                foreach (ListViewGroup group in list.Groups) group.Header = T(group.Header);
            }
            if (root.ContextMenuStrip != null) TranslateMenu(root.ContextMenuStrip.Items);
            root.ControlAdded += (s, e) => Attach(e.Control);
            foreach (Control child in root.Controls) Attach(child);
        }

        static void TranslateMenu(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                item.Text = T(item.Text);
                var drop = item as ToolStripDropDownItem;
                if (drop != null) TranslateMenu(drop.DropDownItems);
            }
        }

        public static void TranslateRows(ListView list, params int[] columns)
        {
            if (!English) return;
            foreach (ListViewItem item in list.Items)
                foreach (int column in columns)
                    if (column >= 0 && column < item.SubItems.Count)
                        item.SubItems[column].Text = T(item.SubItems[column].Text);
            foreach (ListViewGroup group in list.Groups) group.Header = T(group.Header);
        }
    }

    // MessageBox proxy translates confirmations and warnings without changing their safety behavior.
    static class LocalizedMessageBox
    {
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        { return MessageBox.Show(owner, UiLanguage.T(text), UiLanguage.T(caption), buttons, icon); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        { return MessageBox.Show(owner, UiLanguage.T(text), UiLanguage.T(caption), buttons, icon, defaultButton); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        { return MessageBox.Show(UiLanguage.T(text), UiLanguage.T(caption), buttons, icon); }
    }
}

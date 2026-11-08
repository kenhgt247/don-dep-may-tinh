// Dọn Dẹp Máy Tính — tab Trình duyệt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MessageBox = DonDep.LocalizedMessageBox;

namespace DonDep
{
    partial class MainForm
    {
        ListView brList;
        Label lblBrSum;

        void BuildBrowserTab(TabPage tab)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 92 };
            var btnScan = Btn("Quét trình duyệt", 8, 10, 190, 40);
            btnScan.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            var btnClean = Btn("Dọn trình duyệt đã chọn", 206, 10, 230, 40);
            btnClean.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnClean.BackColor = Yellow;
            var btnCacheOnly = Btn("Chỉ chọn bộ nhớ đệm", 444, 14, 170, 32);
            var btnNone = Btn("Bỏ chọn hết", 620, 14, 120, 32);
            var btnKeep = Btn(UiLanguage.L("🔐 Giữ đăng nhập…", "🔐 Keep sign-ins…"), 748, 14, 170, 32, false);
            btnKeep.Click += (s, e) => EditKeepCookies();
            top.Controls.Add(btnKeep);
            top.Controls.Add(Lbl("Không bao giờ đụng mật khẩu đã lưu, dấu trang (bookmark) và tiện ích mở rộng. Nên tắt trình duyệt trước khi dọn.", 928, 8, 400, 44));
            Browsers.KeepDomains = cfg.KeepCookies.Select(CookieKeeper.Normalize).Where(x => x != null).Distinct().ToList();
            lblBrSum = Lbl("Bấm \"Quét trình duyệt\" để xem mỗi trình duyệt đang chiếm bao nhiêu chỗ.", 8, 60, 1150, 24);
            lblBrSum.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            top.Controls.AddRange(new Control[] { btnScan, btnClean, btnCacheOnly, btnNone, lblBrSum });

            brList = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill, ShowGroups = true };
            brList.Columns.Add("Mục", 260);
            brList.Columns.Add("Tài khoản (profile)", 260);
            brList.Columns.Add("Dung lượng", 100, HorizontalAlignment.Right);
            brList.Columns.Add("Xóa thì sao?", 420);
            brList.ItemChecked += (s, e) => UpdateBrowserSummary();
            tab.Controls.Add(brList);
            tab.Controls.Add(top);

            btnScan.Click += async (s, e) => await Guard(ScanBrowsersAsync);
            btnClean.Click += async (s, e) => await Guard(CleanBrowsersAsync);
            btnCacheOnly.Click += (s, e) =>
            {
                suspendSummary = true;
                foreach (ListViewItem it in brList.Items) it.Checked = ((BrowserItem)it.Tag).Kind == BrowserItemKind.Cache;
                suspendSummary = false;
                UpdateBrowserSummary();
            };
            btnNone.Click += (s, e) => SetAllChecked(brList, false);
        }

        void UpdateBrowserSummary()
        {
            if (suspendSummary || brList.Items.Count == 0) return;
            long sum = 0; int n = 0; bool cookies = false;
            foreach (ListViewItem it in brList.CheckedItems) { var b = (BrowserItem)it.Tag; sum += b.Size; n++; if (b.Kind == BrowserItemKind.Cookies) cookies = true; }
            lblBrSum.Text = "Đã chọn " + n + " mục — " + Fmt.Size(sum) + (cookies ? "   •   ⚠ Có chọn Cookie: sẽ bị đăng xuất khỏi các trang web" : "");
            lblBrSum.ForeColor = cookies ? Color.Firebrick : SystemColors.ControlText;
        }

        void EditKeepCookies()
        {
            using (var f = new Form { Text = UiLanguage.L("Giữ đăng nhập khi xóa cookie", "Keep sign-ins when deleting cookies"), ClientSize = new Size(560, 470),
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, Font = Font, Icon = Icon })
            {
                var info = new Label { Location = new Point(16, 12), Size = new Size(528, 92), UseMnemonic = false,
                    Text = Sqlite.Available
                        ? UiLanguage.L("Khi xóa cookie, bạn vẫn GIỮ ĐĂNG NHẬP các trang dưới đây (mỗi dòng một trang, ví dụ facebook.com). Tên miền con cũng được giữ (google.com giữ cả mail.google.com).\nĐể trống danh sách = xóa hết cookie như trước.",
                                       "When cookies are deleted you STAY SIGNED IN to the sites below (one per line, e.g. facebook.com). Subdomains are kept too (google.com keeps mail.google.com).\nEmpty list = delete all cookies as before.")
                        : UiLanguage.L("Windows trên máy này quá cũ nên không hỗ trợ giữ đăng nhập — xóa cookie sẽ xóa hết.", "This Windows version is too old to keep sign-ins — deleting cookies removes all of them.") };
                var box = new TextBox { Location = new Point(16, 110), Size = new Size(528, 290), Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true,
                    Text = string.Join(Environment.NewLine, cfg.KeepCookies) };
                var ok = new Button { Text = UiLanguage.L("Lưu", "Save"), Location = new Point(300, 414), Size = new Size(120, 36), DialogResult = DialogResult.OK };
                var def = new Button { Text = UiLanguage.L("Mặc định", "Defaults"), Location = new Point(16, 414), Size = new Size(120, 36) };
                var cancel = new Button { Text = UiLanguage.L("Hủy", "Cancel"), Location = new Point(424, 414), Size = new Size(120, 36), DialogResult = DialogResult.Cancel };
                def.Click += (s, e) => box.Text = string.Join(Environment.NewLine, CookieKeeper.Defaults);
                f.Controls.AddRange(new Control[] { info, box, ok, def, cancel });
                f.AcceptButton = null; f.CancelButton = cancel;
                if (f.ShowDialog(this) != DialogResult.OK) return;
                var lines = box.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                var good = lines.Select(CookieKeeper.Normalize).Where(x => x != null).Distinct().ToList();
                var bad = lines.Where(l => CookieKeeper.Normalize(l) == null).ToList();
                cfg.KeepCookies = good;
                cfg.Save();
                Browsers.KeepDomains = good;
                if (brList.Items.Count > 0) FillBrowserList(brList.Items.Cast<ListViewItem>().Select(i => (BrowserItem)i.Tag).ToList());
                Status(UiLanguage.L("Đã lưu: giữ đăng nhập " + good.Count + " trang.", "Saved: keeping sign-ins for " + good.Count + " sites."));
                if (bad.Count > 0) Msg(UiLanguage.P(UiLanguage.L("Bỏ qua các dòng không phải tên trang web:\n", "Ignored lines that are not website names:\n") + string.Join("\n", bad)));
            }
        }

        async Task ScanBrowsersAsync()
        {
            SetBusy(true);
            Status("Đang quét trình duyệt...");
            var items = await Task.Run(() => Browsers.Detect());
            FillBrowserList(items);
            Status(items.Count == 0 ? "Không tìm thấy trình duyệt nào." : "Quét xong. Bộ nhớ đệm đã được chọn sẵn; lịch sử và cookie bạn tự chọn nếu muốn.");
        }

        void FillBrowserList(List<BrowserItem> items)
        {
            suspendSummary = true;
            brList.BeginUpdate();
            brList.Items.Clear(); brList.Groups.Clear();
            foreach (var g in items.GroupBy(i => i.Browser))
            {
                var lg = new ListViewGroup(g.Key, g.Key + " — " + Fmt.Size(g.Sum(i => i.Size)));
                brList.Groups.Add(lg);
                foreach (var b in g.OrderBy(i => i.Profile).ThenBy(i => i.Kind))
                {
                    var it = new ListViewItem(b.KindText);
                    it.SubItems.Add(b.Profile);
                    it.SubItems.Add(Fmt.Size(b.Size));
                    it.SubItems.Add(b.Note);
                    it.UseItemStyleForSubItems = false;
                    it.SubItems[3].ForeColor = b.Kind == BrowserItemKind.Cookies ? Color.Firebrick : b.Kind == BrowserItemKind.History ? Color.DarkOrange : Color.ForestGreen;
                    it.Checked = b.Kind == BrowserItemKind.Cache && b.Size > 0;
                    it.Tag = b;
                    it.Group = lg;
                    brList.Items.Add(it);
                }
            }
            brList.EndUpdate();
            UiLanguage.TranslateRows(brList, 0, 3);
            suspendSummary = false;
            UpdateBrowserSummary();
            if (items.Count > 0)
                App.Log("Trình duyệt: " + items.Select(i => i.Browser).Distinct().Count() + " trình duyệt, tổng " + Fmt.Size(items.Sum(i => i.Size)));
        }

        async Task CleanBrowsersAsync()
        {
            var sel = brList.CheckedItems.Cast<ListViewItem>().Select(i => (BrowserItem)i.Tag).ToList();
            if (sel.Count == 0) { Msg("Bạn chưa chọn mục nào. Bấm \"Quét trình duyệt\" rồi đánh dấu những gì muốn dọn."); return; }
            var cookies = sel.Where(b => b.Kind == BrowserItemKind.Cookies).ToList();
            var hist = sel.Where(b => b.Kind == BrowserItemKind.History).ToList();

            var running = Browsers.RunningBrowsers(sel);
            while (running.Count > 0)
            {
                var r = MessageBox.Show(this, "Hãy tắt hẳn các trình duyệt sau rồi bấm Retry:\n\n• " + string.Join("\n• ", running) +
                    "\n\n(Trình duyệt đang mở sẽ giữ khóa một số file nên không dọn hết được.)\nBấm Ignore để vẫn dọn phần dọn được.",
                    App.Name, MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Warning);
                if (r == DialogResult.Abort) { Status("Đã hủy."); return; }
                if (r == DialogResult.Ignore) break;
                running = Browsers.RunningBrowsers(sel);
            }

            var msg = new StringBuilder("Bạn sắp dọn " + sel.Count + " mục (" + Fmt.Size(sel.Sum(b => b.Size)) + "):\n");
            foreach (var g in sel.GroupBy(b => b.Browser))
                msg.Append("\n• ").Append(g.Key).Append(": ").Append(string.Join(", ", g.Select(b => b.KindText.Split('(')[0].Trim()).Distinct()));
            if (hist.Count > 0) msg.Append("\n\n⚠ LỊCH SỬ: mất danh sách trang web đã xem và lịch sử tải về.");
            if (cookies.Count > 0 && Browsers.KeepActive)
                msg.Append(UiLanguage.L("\n\n🔐 COOKIE: vẫn GIỮ ĐĂNG NHẬP các trang: " + string.Join(", ", Browsers.KeepDomains) + ".\nCác trang khác sẽ bị đăng xuất. (Sửa danh sách bằng nút \"Giữ đăng nhập…\")",
                                        "\n\n🔐 COOKIES: you STAY SIGNED IN to: " + string.Join(", ", Browsers.KeepDomains) + ".\nOther sites will be signed out. (Edit the list with \"Keep sign-ins…\")"));
            else if (cookies.Count > 0) msg.Append("\n\n⚠ COOKIE: bạn sẽ bị ĐĂNG XUẤT khỏi các trang web (Facebook, Gmail, Zalo web, ngân hàng…) trong các tài khoản đã chọn. Hãy chắc chắn bạn nhớ mật khẩu.");
            msg.Append("\n\nMật khẩu đã lưu, dấu trang và tiện ích mở rộng KHÔNG bị xóa.\n\nTiếp tục?");
            if (!Confirm(msg.ToString())) { Status("Đã hủy."); return; }
            if (cookies.Count > 0 && !Browsers.KeepActive && !Confirm("Xác nhận lần nữa: xóa Cookie của " + cookies.Count + " tài khoản trình duyệt?\n\nBạn sẽ phải đăng nhập lại các trang web.")) { Status("Đã hủy."); return; }

            SetBusy(true);
            var freeBefore = Fs.FreeMap();
            long left = await Task.Run(() =>
            {
                long l = 0;
                foreach (var b in sel) { status.Report("Đang dọn: " + b.Browser + " — " + b.KindText); l += Browsers.Clean(b); }
                return l;
            });
            var items = brList.Items.Cast<ListViewItem>().Select(i => (BrowserItem)i.Tag).ToList();
            FillBrowserList(items);
            UpdateDrives();
            var freeAfter = Fs.FreeMap();
            long freed = 0;
            foreach (var k in freeAfter.Keys) { long b; if (freeBefore.TryGetValue(k, out b) && freeAfter[k] > b) freed += freeAfter[k] - b; }
            AppState.AddFreed(freed);
            History.Add("trinhduyet", sel.Count, freed, sel.Select(b => b.Browser + " (" + b.Profile + ") — " + b.KindText));
            UpdateStatsLabel();
            string done = "Đã dọn xong trình duyệt! Giải phóng khoảng " + Fmt.Size(freed) + ".";
            if (left > 10 * Fmt.MB) done += "\n\nCòn " + Fmt.Size(left) + " chưa xóa được vì trình duyệt đang dùng. Tắt trình duyệt rồi dọn lại để xóa hết.";
            Status("Đã dọn xong trình duyệt.");
            Msg(done);
        }
    }
}

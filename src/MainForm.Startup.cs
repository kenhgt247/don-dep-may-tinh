// Dọn Dẹp Máy Tính — tab Khởi động (chương trình chạy cùng Windows).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MessageBox = DonDep.LocalizedMessageBox;

namespace DonDep
{
    partial class MainForm
    {
        ListView startList;
        Label lblStart;
        bool startLoaded;

        void BuildStartupTab(TabPage tab)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 96 };
            var btnReload = Btn(UiLanguage.L("Làm mới", "Refresh"), 8, 10, 130, 40);
            var btnOff = Btn(UiLanguage.L("Tắt mục đã chọn", "Disable selected"), 146, 10, 200, 40);
            btnOff.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnOff.BackColor = Yellow;
            var btnOn = Btn(UiLanguage.L("Bật lại mục đã chọn", "Enable selected"), 354, 10, 200, 40);
            var btnFolder = Btn(UiLanguage.L("Mở vị trí file", "Open file location"), 562, 14, 170, 32, false);
            top.Controls.Add(Lbl(UiLanguage.L("Tắt bớt chương trình chạy cùng Windows giúp máy bật nhanh hơn. Tắt chỉ là tạm ngưng (giống Task Manager), không xóa gì — bật lại lúc nào cũng được.",
                                              "Disabling startup programs makes Windows start faster. Disabling only pauses them (like Task Manager) — nothing is deleted; re-enable any time."), 744, 8, 430, 44));
            lblStart = Lbl("", 8, 62, 1150, 26);
            lblStart.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            top.Controls.AddRange(new Control[] { btnReload, btnOff, btnOn, btnFolder, lblStart });

            startList = new ListView { View = View.Details, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill, HideSelection = false };
            startList.Columns.Add(UiLanguage.L("Chương trình", "Program"), 250);
            startList.Columns.Add(UiLanguage.L("Trạng thái", "Status"), 90);
            startList.Columns.Add(UiLanguage.L("Nhà phát hành", "Publisher"), 180);
            startList.Columns.Add(UiLanguage.L("Nguồn", "Source"), 170);
            startList.Columns.Add(UiLanguage.L("Ghi chú", "Note"), 260);
            startList.Columns.Add(UiLanguage.L("Lệnh chạy", "Command"), 420);
            tab.Controls.Add(startList);
            tab.Controls.Add(top);

            btnReload.Click += async (s, e) => await Guard(LoadStartupAsync);
            btnOff.Click += async (s, e) => await Guard(() => ToggleStartupAsync(false));
            btnOn.Click += async (s, e) => await Guard(() => ToggleStartupAsync(true));
            btnFolder.Click += (s, e) =>
            {
                if (startList.SelectedItems.Count == 0) return;
                var x = (StartupEntry)startList.SelectedItems[0].Tag;
                if (File.Exists(x.Target)) Fs.OpenInExplorer(x.Target);
                else Msg(UiLanguage.L("Không tìm thấy file của mục này.", "The file for this item was not found."));
            };
            startList.DoubleClick += (s, e) => btnFolder.PerformClick();
            tabs.SelectedIndexChanged += async (s, e) => { if (tabs.SelectedTab == tab && !startLoaded) await Guard(LoadStartupAsync); };
        }

        async Task LoadStartupAsync()
        {
            SetBusy(true);
            Status(UiLanguage.L("Đang đọc danh sách chương trình chạy cùng Windows...", "Reading startup programs..."));
            var items = await Task.Run(() => StartupItems.Scan());
            startLoaded = true;
            FillStartup(items);
            Status(UiLanguage.L("Sẵn sàng.", "Ready."));
        }

        void FillStartup(List<StartupEntry> items)
        {
            startList.BeginUpdate();
            startList.Items.Clear();
            foreach (var x in items)
            {
                var it = new ListViewItem(x.Name) { Tag = x };
                it.SubItems.Add(x.Enabled ? UiLanguage.L("Đang bật", "Enabled") : UiLanguage.L("Đã tắt", "Disabled"));
                it.SubItems.Add(x.Publisher);
                it.SubItems.Add(x.Source);
                it.SubItems.Add(x.Hint);
                it.SubItems.Add(x.Command);
                it.UseItemStyleForSubItems = false;
                it.SubItems[1].ForeColor = x.Enabled ? Color.ForestGreen : Color.DimGray;
                if (x.KeepAdvised) it.SubItems[4].ForeColor = Color.DarkOrange;
                else if (x.Hint.Length > 0) it.SubItems[4].ForeColor = Color.SeaGreen;
                if (!x.Enabled) it.ForeColor = Color.DimGray;
                startList.Items.Add(it);
            }
            startList.EndUpdate();
            int on = items.Count(x => x.Enabled);
            lblStart.Text = UiLanguage.L(items.Count + " chương trình chạy cùng Windows — " + on + " đang bật, " + (items.Count - on) + " đã tắt. Chọn một hoặc nhiều mục rồi bấm Tắt / Bật.",
                                         items.Count + " startup programs — " + on + " enabled, " + (items.Count - on) + " disabled. Select items, then Disable / Enable.");
        }

        async Task ToggleStartupAsync(bool enable)
        {
            var sel = startList.SelectedItems.Cast<ListViewItem>().Select(i => (StartupEntry)i.Tag).Where(x => x.Enabled != enable).ToList();
            if (sel.Count == 0) { Msg(UiLanguage.L("Hãy chọn mục cần " + (enable ? "bật lại" : "tắt") + " trong danh sách.", "Select the items to " + (enable ? "enable" : "disable") + " in the list.")); return; }
            if (!enable)
            {
                var keep = sel.Where(x => x.KeepAdvised).ToList();
                if (keep.Count > 0 && !Confirm(UiLanguage.P(UiLanguage.L(
                    "Các mục sau nên GIỮ chạy cùng Windows (driver, bảo mật, bộ gõ tiếng Việt…):\n\n• " + string.Join("\n• ", keep.Select(x => x.Name)) + "\n\nTắt có thể làm mất âm thanh, bàn di chuột, bộ gõ… cho tới khi bật lại. Vẫn tắt?",
                    "These items should KEEP running at startup (drivers, security, Vietnamese keyboard…):\n\n• " + string.Join("\n• ", keep.Select(x => x.Name)) + "\n\nDisabling them may break sound, touchpad, typing… until re-enabled. Disable anyway?")))) return;
            }
            var ok = new List<StartupEntry>(); bool needAdmin = false;
            foreach (var x in sel)
            {
                try { StartupItems.SetEnabled(x, enable); ok.Add(x); }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is System.Security.SecurityException) { needAdmin = true; }
                catch (Exception ex) { App.Log("Không đổi được trạng thái " + x.Name + ": " + ex.Message); }
            }
            if (ok.Count > 0) History.Add("khoidong", ok.Count, 0, ok.Select(x => (enable ? "✓ " : "⏸ ") + x.Name + "  —  " + x.Command), enable ? UiLanguage.L("Bật lại", "Enabled") : UiLanguage.L("Tắt", "Disabled"));
            await LoadStartupAsync();
            string m = UiLanguage.L((enable ? "Đã bật lại " : "Đã tắt ") + ok.Count + " mục. Có hiệu lực từ lần khởi động máy tiếp theo.",
                                    (enable ? "Enabled " : "Disabled ") + ok.Count + " items. Takes effect at the next sign-in.");
            if (needAdmin) m += UiLanguage.L("\n\nMột số mục thuộc \"Mọi tài khoản\" cần quyền quản trị: vào tab Dọn dẹp, bấm \"Quyền admin\" rồi thử lại.",
                                             "\n\nSome \"All users\" items need administrator rights: in the Cleanup tab click \"Run as admin\", then try again.");
            Msg(UiLanguage.P(m));
        }
    }
}

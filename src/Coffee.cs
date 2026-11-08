// Dọn Dẹp Máy Tính — "Ủng hộ tác giả 1 ly cà phê": thẻ trong Cài đặt + lời mời thỉnh thoảng sau khi dọn xong.
// Chỉ hiển thị mã QR, không thu tiền, không khóa tính năng nào.
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace DonDep
{
    static class Coffee
    {
        static Image qr;
        public static Image Qr()
        {
            if (qr != null) return qr;
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("coffee-qr.png"))
                    if (s != null) qr = new Bitmap(Image.FromStream(s));
            }
            catch { }
            return qr;
        }

        public static string Title => UiLanguage.L("☕ Ủng hộ tác giả 1 ly cà phê", "☕ Buy the author a coffee");
        public static string Body => UiLanguage.L(
            "PC Cleaner miễn phí hoàn toàn và sẽ luôn miễn phí — không quảng cáo, không gói VIP.\n\n" +
            "Nếu phần mềm giúp ích cho bạn, bạn có thể mời tác giả 1 ly cà phê. Mỗi đóng góp, dù nhỏ, đều là động lực để mình làm thêm tính năng mới và sửa lỗi nhanh hơn.\n\nCảm ơn bạn rất nhiều! ❤",
            "PC Cleaner is completely free and will stay free — no ads, no premium tier.\n\n" +
            "If it helped you, you can buy the author a coffee. Every contribution, however small, keeps new features and fixes coming.\n\nThank you so much! ❤");
        public static string ScanHint => UiLanguage.L("Quét mã bằng app ngân hàng bất kỳ (VietQR · Napas 247).", "Scan with any Vietnamese banking app (VietQR · Napas 247).");

        /// Cửa sổ xem mã QR cỡ lớn (để điện thoại quét dễ hơn).
        public static void ShowLarge(IWin32Window owner, string headline = null, bool offerStop = false)
        {
            var img = Qr();
            using (var f = new Form
            {
                Text = Title.Replace("☕ ", ""), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, ClientSize = new Size(760, 600), Icon = App.AppIcon, Font = new Font("Segoe UI", 10f)
            })
            {
                var pic = new PictureBox { Image = img, SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(24, 24), Size = new Size(400, 506), BackColor = Color.White };
                var head = new Label { Text = headline ?? Title, Location = new Point(448, 26), Size = new Size(292, 64), Font = new Font("Segoe UI Semibold", 13f), UseMnemonic = false };
                var body = new Label { Text = Body, Location = new Point(448, 96), Size = new Size(292, 300), UseMnemonic = false };
                var hint = new Label { Text = ScanHint, Location = new Point(448, 404), Size = new Size(292, 48), UseMnemonic = false };
                var close = new Button { Text = UiLanguage.L("Đóng", "Close"), Location = new Point(448, 490), Size = new Size(140, 38), DialogResult = DialogResult.Cancel };
                f.Controls.AddRange(new Control[] { pic, head, body, hint, close });
                if (offerStop)
                {
                    var stop = new Button { Text = UiLanguage.L("Không nhắc lại", "Don't show again"), Location = new Point(600, 490), Size = new Size(140, 38) };
                    stop.Click += (s, e) => { AppState.SetCoffeeOff(); f.Close(); };
                    f.Controls.Add(stop);
                    close.Text = UiLanguage.L("Để lúc khác", "Maybe later");
                }
                f.CancelButton = close;
                f.ShowDialog(owner);
            }
        }

        /// Lời mời nhẹ nhàng sau khi dọn xong: chỉ khi vừa giải phóng từ 1 GB, đã dọn ít nhất 3 lần,
        /// tối đa 1 lần mỗi 30 ngày, và người dùng chưa bấm "Không nhắc lại".
        public static void MaybeAsk(IWin32Window owner, long freedNow)
        {
            try
            {
                AppState.Load();
                if (AppState.CoffeeOff || freedNow < Fmt.GB || AppState.Cleanups < 3 || Qr() == null) return;
                DateTime last;
                if (DateTime.TryParse(AppState.CoffeeLast, out last) && (DateTime.Now - last).TotalDays < 30) return;
                AppState.MarkCoffee(DateTime.Now);
                string head = UiLanguage.L("🎉 Đã giải phóng " + Fmt.Size(freedNow) + "!\nTổng cộng: " + Fmt.Size(AppState.TotalFreed),
                                           "🎉 " + Fmt.Size(freedNow) + " freed!\nTotal so far: " + Fmt.Size(AppState.TotalFreed));
                ShowLarge(owner, head, true);
            }
            catch { }
        }
    }
}

// Dọn Dẹp Máy Tính — giao diện tối hiện đại. CHỈ thay đổi cách hiển thị, không đổi chức năng nào.
// Tô lại màu / vẽ lại các control có sẵn, thêm thanh tiêu đề + thanh điều hướng bên trái (bấm = chọn tab như cũ).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DonDep
{
    static class Theme
    {
        // ---------------------------------------------------------------- bảng màu
        public static readonly Color Bg = Color.FromArgb(13, 22, 38);          // nền cửa sổ
        public static readonly Color Side = Color.FromArgb(9, 16, 29);         // thanh bên
        public static readonly Color Surface = Color.FromArgb(17, 28, 47);     // vùng nội dung
        public static readonly Color Card = Color.FromArgb(22, 36, 60);        // thẻ / khung nhóm
        public static readonly Color CardHi = Color.FromArgb(29, 46, 76);
        public static readonly Color Input = Color.FromArgb(11, 19, 33);
        public static readonly Color Border = Color.FromArgb(40, 58, 90);
        public static readonly Color Fore = Color.FromArgb(230, 237, 247);
        public static readonly Color Muted = Color.FromArgb(143, 160, 186);
        public static readonly Color Accent = Color.FromArgb(56, 152, 255);
        public static readonly Color Accent2 = Color.FromArgb(34, 211, 238);
        public static readonly Color Red = Color.FromArgb(248, 113, 113);
        public static readonly Color Green = Color.FromArgb(52, 211, 153);
        public static readonly Color Amber = Color.FromArgb(251, 191, 36);
        public static readonly Color Blue = Color.FromArgb(96, 165, 250);

        static readonly Color OldYellow = Color.FromArgb(255, 222, 89);   // nút chính cũ

        // ---------------------------------------------------------------- Win32
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string id);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadLibrary(string name);
        [DllImport("kernel32.dll")] static extern IntPtr GetProcAddress(IntPtr mod, IntPtr ordinal);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int SetPreferredAppModeFn(int mode);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate bool AllowDarkModeForWindowFn(IntPtr h, bool allow);
        static AllowDarkModeForWindowFn allowDark;
        static bool appModeSet;

        static int Build { get { try { return Environment.OSVersion.Version.Major >= 10 ? Environment.OSVersion.Version.Build : 0; } catch { return 0; } } }

        /// Cho phép thanh cuộn / tiêu đề cửa sổ dạng tối (Windows 10 1809 trở lên). Lỗi thì bỏ qua.
        static void EnableAppDarkMode()
        {
            if (appModeSet) return;
            appModeSet = true;
            if (Build < 17763) return;
            try
            {
                var ux = LoadLibrary("uxtheme.dll");
                if (ux == IntPtr.Zero) return;
                var p135 = GetProcAddress(ux, (IntPtr)135);
                if (p135 != IntPtr.Zero) ((SetPreferredAppModeFn)Marshal.GetDelegateForFunctionPointer(p135, typeof(SetPreferredAppModeFn)))(1); // AllowDark
                var p133 = GetProcAddress(ux, (IntPtr)133);
                if (p133 != IntPtr.Zero) allowDark = (AllowDarkModeForWindowFn)Marshal.GetDelegateForFunctionPointer(p133, typeof(AllowDarkModeForWindowFn));
            }
            catch { }
        }

        static void DarkWindow(Control c, string theme = "DarkMode_Explorer")
        {
            Action go = () =>
            {
                try { allowDark?.Invoke(c.Handle, true); } catch { }
                try { SetWindowTheme(c.Handle, theme, null); } catch { }
            };
            if (c.IsHandleCreated) go(); else c.HandleCreated += (s, e) => go();
        }

        public static void DarkTitleBar(Form f)
        {
            Action go = () =>
            {
                try
                {
                    int on = 1;
                    if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
                    int cap = ColorTranslator.ToWin32(Side);
                    DwmSetWindowAttribute(f.Handle, 35, ref cap, 4);   // Windows 11: màu thanh tiêu đề
                }
                catch { }
            };
            if (f.IsHandleCreated) go(); else f.HandleCreated += (s, e) => go();
        }

        // ---------------------------------------------------------------- màu cũ → màu hợp nền tối
        public static Color Map(Color c)
        {
            if (c.A == 0) return c;
            int argb = c.ToArgb();
            if (argb == Color.Firebrick.ToArgb() || argb == Color.Red.ToArgb() || argb == Color.DarkRed.ToArgb() || argb == Color.Crimson.ToArgb() || argb == Color.Maroon.ToArgb()) return Red;
            if (argb == Color.ForestGreen.ToArgb() || argb == Color.Green.ToArgb() || argb == Color.SeaGreen.ToArgb() || argb == Color.DarkGreen.ToArgb() || argb == Color.FromArgb(22, 101, 52).ToArgb()) return Green;
            if (argb == Color.DarkOrange.ToArgb() || argb == Color.Orange.ToArgb() || argb == Color.Chocolate.ToArgb() || argb == Color.DarkGoldenrod.ToArgb()) return Amber;
            if (argb == Color.DimGray.ToArgb() || argb == Color.Gray.ToArgb() || argb == SystemColors.GrayText.ToArgb() || argb == Color.DarkGray.ToArgb() || argb == Color.SlateGray.ToArgb()) return Muted;
            if (argb == Color.Black.ToArgb() || argb == SystemColors.ControlText.ToArgb() || argb == SystemColors.WindowText.ToArgb()) return Fore;
            if (argb == Color.SteelBlue.ToArgb() || argb == Color.Blue.ToArgb() || argb == Color.DarkBlue.ToArgb() || argb == Color.RoyalBlue.ToArgb() || argb == Color.Navy.ToArgb() ) return Blue;
            if (argb == Color.FromArgb(31, 56, 95).ToArgb() || argb == Color.FromArgb(51, 65, 85).ToArgb() || argb == Color.FromArgb(26, 86, 173).ToArgb()) return Fore;
            // Màu tối còn lại: làm sáng lên giữ nguyên sắc.
            float l = c.GetBrightness();
            if (l < 0.5f)
            {
                double k = 0.6;
                return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * k), (int)(c.G + (255 - c.G) * k), (int)(c.B + (255 - c.B) * k));
            }
            return c;
        }

        static bool IsLightBack(Color c) { return c.A > 0 && c.GetBrightness() > 0.6f; }

        // ---------------------------------------------------------------- áp dụng cho cửa sổ chính
        static Font fontUi, fontBold, fontTitle, fontIcon, fontNav;
        static string IconFamily()
        {
            foreach (var n in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
                if (FontFamily.Families.Any(f => f.Name == n)) return n;
            return null;
        }

        static readonly string[] Glyphs = { "", "", "", "", "", "", "", "", "", "" };

        public static void ApplyMain(Form form, TabControl tabs, StatusStrip strip)
        {
            EnableAppDarkMode();
            fontUi = fontUi ?? new Font("Segoe UI", 9.5f);
            fontBold = fontBold ?? new Font("Segoe UI Semibold", 9.5f);
            fontTitle = fontTitle ?? new Font("Segoe UI Semibold", 15f);
            fontNav = fontNav ?? new Font("Segoe UI", 10.5f);
            string icons = IconFamily();
            if (fontIcon == null && icons != null) fontIcon = new Font(icons, 12f);

            form.BackColor = Bg;
            form.ForeColor = Fore;
            DarkTitleBar(form);

            // Ẩn hàng tab gốc — điều hướng bằng thanh bên.
            tabs.Appearance = TabAppearance.FlatButtons;
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.ItemSize = new Size(0, 1);
            EventHandler clip = (s, e) => { try { tabs.Region = new Region(tabs.DisplayRectangle); } catch { } };
            tabs.Resize += clip; tabs.HandleCreated += clip; clip(null, null);

            // Thanh tiêu đề
            var header = new HeaderPanel { Dock = DockStyle.Top, Height = 64 };
            // Thanh bên
            var side = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = Side, Padding = new Padding(10, 10, 10, 10) };
            var navs = new List<NavItem>();
            for (int i = tabs.TabPages.Count - 1; i >= 0; i--)
            {
                int idx = i;
                string text = tabs.TabPages[i].Text.TrimStart('⚙', ' ');
                var n = new NavItem { Text = text, Glyph = i < Glyphs.Length ? Glyphs[i] : "", IconFont = fontIcon, Font = fontNav, Dock = DockStyle.Top, Height = 44, Index = idx, Tabs = tabs };
                n.Click += (s, e) => { if (tabs.SelectedIndex != idx) tabs.SelectedIndex = idx; };
                navs.Add(n);
                side.Controls.Add(n);
            }
            var spacer = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Side };
            side.Controls.Add(spacer);
            var foot = new Label
            {
                Dock = DockStyle.Bottom, Height = 54, ForeColor = Muted, BackColor = Side, UseMnemonic = false,
                Font = new Font("Segoe UI", 8.5f), TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(6, 0, 0, 6),
                Text = "v" + App.Version + "\n" + UiLanguage.L("Miễn phí · Mã nguồn mở", "Free · Open source")
            };
            side.Controls.Add(foot);
            spacer.SendToBack();
            tabs.SelectedIndexChanged += (s, e) => { foreach (var n in navs) n.Invalidate(); header.Invalidate(); };
            header.Tabs = tabs;

            form.Controls.Add(side);
            form.Controls.Add(header);
            header.SendToBack();          // Top dock trước (rộng hết cửa sổ)
            strip.SendToBack();           // Status dưới cùng rộng hết
            tabs.BringToFront();          // Fill phải ở trên cùng thứ tự

            // Thanh trạng thái + menu chuột phải
            ToolStripManager.Renderer = new DarkRenderer();
            strip.Renderer = new DarkRenderer();
            strip.BackColor = Side;
            strip.ForeColor = Muted;
            strip.SizingGrip = false;
            foreach (ToolStripItem it in strip.Items) it.ForeColor = Muted;

            foreach (TabPage p in tabs.TabPages) { p.UseVisualStyleBackColor = false; p.BackColor = Bg; p.Padding = new Padding(8); }
            Style(tabs);
            InstallDialogHook(form);
        }

        // ---------------------------------------------------------------- tô màu từng control (đệ quy, cả control thêm về sau)
        public static void Style(Control root)
        {
            foreach (Control c in root.Controls) StyleOne(c);
        }

        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> done = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();

        static void StyleOne(Control c)
        {
            if (c is NavItem || c is HeaderPanel) return;
            object tmp;
            if (done.TryGetValue(c, out tmp)) { Style(c); return; }
            done.Add(c, null);
            c.ControlAdded += (s, e) => { StyleOne(e.Control); };

            if (c is Button) StyleButton((Button)c);
            else if (c is GroupBox) StyleGroup((GroupBox)c);
            else if (c is ListView) StyleList((ListView)c);
            else if (c is TextBoxBase)
            {
                c.BackColor = Input; c.ForeColor = Fore;
                var tb = c as TextBox; if (tb != null && tb.BorderStyle == BorderStyle.Fixed3D) tb.BorderStyle = BorderStyle.FixedSingle;
                if (tb != null && tb.Multiline) DarkWindow(c);
            }
            else if (c is NumericUpDown) { c.BackColor = Input; c.ForeColor = Fore; ((NumericUpDown)c).BorderStyle = BorderStyle.FixedSingle; }
            else if (c is ComboBox) { var cb = (ComboBox)c; cb.FlatStyle = FlatStyle.Flat; cb.BackColor = Input; cb.ForeColor = Fore; DarkWindow(c, "DarkMode_CFD"); }
            else if (c is ListBox) { var lb = (ListBox)c; lb.BackColor = Input; lb.ForeColor = Fore; lb.BorderStyle = BorderStyle.FixedSingle; DarkWindow(c); }
            else if (c is TreeView) { var tv = (TreeView)c; tv.BackColor = Card; tv.ForeColor = Fore; tv.LineColor = Muted; DarkWindow(c); }
            else if (c is ProgressBar) StyleProgress((ProgressBar)c);
            else if (c is CheckBox || c is RadioButton)
            {
                c.ForeColor = Fore;
                c.BackColor = Color.Transparent;
                c.ForeColorChanged += Remap;
            }
            else if (c is LinkLabel) { var ll = (LinkLabel)c; ll.LinkColor = Accent2; ll.ActiveLinkColor = Accent; ll.VisitedLinkColor = Accent2; ll.ForeColor = Fore; }
            else if (c is Label)
            {
                if (IsLightBack(c.BackColor)) c.BackColor = Card;
                if (c.BackColor != Color.Transparent && c.BackColor == SystemColors.Control) c.BackColor = Color.Transparent;
                c.ForeColor = Map(c.ForeColor);
                c.ForeColorChanged += Remap;
            }
            else if (c is PictureBox) { if (IsLightBack(c.BackColor)) c.BackColor = Input; }
            else if (c is SplitContainer) { c.BackColor = Border; var sc = (SplitContainer)c; sc.Panel1.BackColor = Bg; sc.Panel2.BackColor = Bg; }
            else if (c is TabControl) { foreach (TabPage p in ((TabControl)c).TabPages) { p.UseVisualStyleBackColor = false; p.BackColor = Bg; } }
            else if (c is ScrollableControl)   // Panel, FlowLayoutPanel, TabPage…
            {
                if (c is TabPage) { ((TabPage)c).UseVisualStyleBackColor = false; c.BackColor = Bg; }
                else if (c.BackColor.ToArgb() == Color.White.ToArgb()) c.BackColor = Card;
                else if (IsLightBack(c.BackColor)) c.BackColor = Bg;
                else if (c.BackColor == SystemColors.Control) c.BackColor = Color.Transparent;
                c.ForeColor = Fore;
                if (((ScrollableControl)c).AutoScroll) DarkWindow(c);
            }
            Style(c);
        }

        static bool remapping;
        static void Remap(object sender, EventArgs e)
        {
            if (remapping) return;
            var c = (Control)sender;
            var m = Map(c.ForeColor);
            if (m != c.ForeColor) { remapping = true; try { c.ForeColor = m; } finally { remapping = false; } }
        }

        // ---------------------------------------------------------------- nút bấm bo góc
        static void StyleButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Cursor = Cursors.Hand;
            bool hover = false, down = false;
            b.MouseEnter += (s, e) => { hover = true; b.Invalidate(); };
            b.MouseLeave += (s, e) => { hover = false; down = false; b.Invalidate(); };
            b.MouseDown += (s, e) => { down = true; b.Invalidate(); };
            b.MouseUp += (s, e) => { down = false; b.Invalidate(); };
            b.EnabledChanged += (s, e) => b.Invalidate();
            b.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Effective(b.Parent));
                bool primary = b.BackColor.ToArgb() == OldYellow.ToArgb();
                // Nút có nền xanh nhạt (vd. ngôn ngữ đang chọn, nút Lưu) = trạng thái "đang chọn".
                bool selected = !primary && b.BackColor.A > 0 && b.BackColor.GetBrightness() > 0.8f && b.BackColor.B > b.BackColor.R + 10;
                var r = new Rectangle(1, 1, b.Width - 3, b.Height - 3);
                using (var path = Round(r, Math.Min(10, b.Height / 2)))
                {
                    if (primary)
                    {
                        Color c1 = b.Enabled ? (down ? Color.FromArgb(37, 120, 220) : hover ? Color.FromArgb(80, 170, 255) : Accent) : Color.FromArgb(40, 66, 104);
                        Color c2 = b.Enabled ? (down ? Color.FromArgb(20, 160, 190) : hover ? Color.FromArgb(70, 225, 245) : Accent2) : Color.FromArgb(40, 70, 96);
                        using (var br = new LinearGradientBrush(r, c1, c2, LinearGradientMode.Horizontal)) g.FillPath(br, path);
                    }
                    else
                    {
                        Color f = !b.Enabled ? Color.FromArgb(20, 32, 52) : down ? Color.FromArgb(24, 40, 66) : selected ? Color.FromArgb(24, 54, 96) : hover ? CardHi : Card;
                        using (var br = new SolidBrush(f)) g.FillPath(br, path);
                        using (var pen = new Pen(selected ? Accent : hover && b.Enabled ? Color.FromArgb(70, 110, 170) : Border, selected ? 2f : 1f)) g.DrawPath(pen, path);
                    }
                }
                Color tc = !b.Enabled ? Color.FromArgb(95, 112, 140) : primary ? Color.White : Fore;
                TextRenderer.DrawText(g, b.Text, b.Font, new Rectangle(6, 0, b.Width - 12, b.Height), tc,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
        }

        static Color Effective(Control c)
        {
            while (c != null && (c.BackColor.A == 0)) c = c.Parent;
            return c?.BackColor ?? Bg;
        }

        static GraphicsPath Round(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            int d = rad * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ---------------------------------------------------------------- khung nhóm = thẻ bo góc
        static void StyleGroup(GroupBox g)
        {
            g.BackColor = Card;
            g.ForeColor = Fore;
            g.Paint += (s, e) =>
            {
                var gr = e.Graphics;
                gr.SmoothingMode = SmoothingMode.AntiAlias;
                gr.Clear(Effective(g.Parent));
                var r = new Rectangle(0, 0, g.Width - 1, g.Height - 1);
                using (var path = Round(r, 10))
                {
                    using (var br = new SolidBrush(Card)) gr.FillPath(br, path);
                    using (var pen = new Pen(Border)) gr.DrawPath(pen, path);
                }
                using (var f = new Font("Segoe UI Semibold", 9.5f))
                    TextRenderer.DrawText(gr, g.Text, f, new Point(12, 3), Accent2, TextFormatFlags.NoPrefix);
            };
        }

        // ---------------------------------------------------------------- bảng danh sách
        static void StyleList(ListView lv)
        {
            lv.BackColor = Card;
            lv.ForeColor = Fore;
            lv.BorderStyle = BorderStyle.None;
            lv.GridLines = false;
            if (lv.View == View.Details)
            {
                lv.OwnerDraw = true;
                lv.DrawColumnHeader += (s, e) =>
                {
                    using (var br = new SolidBrush(Surface)) e.Graphics.FillRectangle(br, e.Bounds);
                    using (var pen = new Pen(Border))
                    {
                        e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                        e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 6, e.Bounds.Right - 1, e.Bounds.Bottom - 6);
                    }
                    var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
                    var al = e.Header != null ? e.Header.TextAlign : HorizontalAlignment.Left;
                    flags |= al == HorizontalAlignment.Right ? TextFormatFlags.Right : al == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;
                    var rect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", fontBold ?? lv.Font, rect, Muted, flags);
                };
                lv.DrawItem += (s, e) => { e.DrawDefault = true; };
                lv.DrawSubItem += (s, e) =>
                {
                    // Đổi màu chữ cũ (đỏ đậm, xanh đậm…) sang màu sáng dễ đọc trên nền tối.
                    try
                    {
                        if (e.SubItem != null)
                        {
                            var m = Map(e.SubItem.ForeColor);
                            if (m != e.SubItem.ForeColor) e.SubItem.ForeColor = m;
                            if (IsLightBack(e.SubItem.BackColor) && e.SubItem.BackColor != lv.BackColor) e.SubItem.BackColor = Card;
                        }
                        if (e.ColumnIndex == 0 && e.Item != null)
                        {
                            var mi = Map(e.Item.ForeColor);
                            if (mi != e.Item.ForeColor) e.Item.ForeColor = mi;
                        }
                    }
                    catch { }
                    e.DrawDefault = true;
                };
            }
            DarkWindow(lv);
            lv.HandleCreated += (s, e) => DarkHeader(lv);
            if (lv.IsHandleCreated) DarkHeader(lv);
        }

        static void DarkHeader(ListView lv)
        {
            try
            {
                var h = SendMessage(lv.Handle, 0x101F /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero);
                if (h != IntPtr.Zero) { try { allowDark?.Invoke(h, true); } catch { } SetWindowTheme(h, "DarkMode_ItemsView", null); }
            }
            catch { }
        }

        static void StyleProgress(ProgressBar pb)
        {
            Action go = () =>
            {
                try
                {
                    SetWindowTheme(pb.Handle, "", "");
                    SendMessage(pb.Handle, 0x0409 /* PBM_SETBARCOLOR */, IntPtr.Zero, (IntPtr)ColorTranslator.ToWin32(Accent));
                    SendMessage(pb.Handle, 0x2001 /* PBM_SETBKCOLOR */, IntPtr.Zero, (IntPtr)ColorTranslator.ToWin32(Input));
                }
                catch { }
            };
            if (pb.IsHandleCreated) go(); else pb.HandleCreated += (s, e) => go();
        }

        // ---------------------------------------------------------------- hộp thoại phụ (xem trước, chọn tàn dư…)
        static bool hooked;
        static void InstallDialogHook(Form main)
        {
            if (hooked) return;
            hooked = true;
            var seen = new HashSet<Form>();
            Application.Idle += (s, e) =>
            {
                try
                {
                    foreach (Form f in Application.OpenForms)
                    {
                        if (seen.Contains(f) || f is MainForm) continue;
                        seen.Add(f);
                        f.FormClosed += (a, b) => seen.Remove(f);
                        f.BackColor = Bg; f.ForeColor = Fore;
                        DarkTitleBar(f);
                        Style(f);
                        f.Invalidate(true);
                    }
                }
                catch { }
            };
        }

        // ---------------------------------------------------------------- vẽ thanh trạng thái / menu
        class DarkColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Card;
            public override Color ImageMarginGradientBegin => Card;
            public override Color ImageMarginGradientMiddle => Card;
            public override Color ImageMarginGradientEnd => Card;
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => Accent;
            public override Color MenuItemSelected => CardHi;
            public override Color MenuItemSelectedGradientBegin => CardHi;
            public override Color MenuItemSelectedGradientEnd => CardHi;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Border;
            public override Color StatusStripGradientBegin => Side;
            public override Color StatusStripGradientEnd => Side;
            public override Color ToolStripBorder => Side;
        }

        class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? (e.Item.Owner is StatusStrip ? Muted : Fore) : Color.FromArgb(95, 112, 140);
                base.OnRenderItemText(e);
            }
            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Muted; base.OnRenderArrow(e); }
        }

        // ---------------------------------------------------------------- thanh tiêu đề trên cùng
        class HeaderPanel : Panel
        {
            public TabControl Tabs;
            static Image logo;
            public HeaderPanel() { DoubleBuffered = true; ResizeRedraw = true; BackColor = Side; }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                using (var br = new LinearGradientBrush(ClientRectangle, Color.FromArgb(12, 24, 44), Color.FromArgb(16, 40, 74), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, ClientRectangle);
                using (var pen = new Pen(Border)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
                if (logo == null) { try { logo = ShareCard.Logo(); } catch { } }
                if (logo != null) g.DrawImage(logo, new Rectangle(18, 10, 44, 44));
                using (var f1 = new Font("Segoe UI Semibold", 15f))
                using (var f2 = new Font("Segoe UI", 9f))
                {
                    TextRenderer.DrawText(g, "PC Cleaner", f1, new Point(72, 9), Fore, TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(g, UiLanguage.L("Dọn Dẹp Máy Tính · bộ công cụ tối ưu ổ cứng cho Windows", "Disk cleanup & storage toolkit for Windows"), f2, new Point(74, 38), Muted, TextFormatFlags.NoPrefix);
                }
                // Tên trang hiện tại + nhãn phiên bản (chỉ để hiển thị)
                string page = Tabs != null && Tabs.SelectedTab != null ? Tabs.SelectedTab.Text.TrimStart('⚙', ' ') : "";
                using (var fp = new Font("Segoe UI Semibold", 12f))
                using (var fb = new Font("Segoe UI Semibold", 8.5f))
                {
                    string badge = UiLanguage.L("MIỄN PHÍ", "FREE");
                    var bs = TextRenderer.MeasureText(badge, fb);
                    var vs = TextRenderer.MeasureText("v" + App.Version, fb);
                    int x = Width - 20 - bs.Width - 18;
                    var rb = new Rectangle(x, 20, bs.Width + 18, 24);
                    using (var path = Round(rb, 12))
                    using (var br = new LinearGradientBrush(rb, Accent, Accent2, LinearGradientMode.Horizontal)) g.FillPath(br, path);
                    TextRenderer.DrawText(g, badge, fb, rb, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    var rv = new Rectangle(x - vs.Width - 26, 20, vs.Width + 18, 24);
                    using (var path = Round(rv, 12))
                    {
                        using (var br = new SolidBrush(Card)) g.FillPath(br, path);
                        using (var pen = new Pen(Border)) g.DrawPath(pen, path);
                    }
                    TextRenderer.DrawText(g, "v" + App.Version, fb, rv, Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    var ps = TextRenderer.MeasureText(page, fp);
                    int px = rv.X - ps.Width - 28;
                    if (px > 520) TextRenderer.DrawText(g, page, fp, new Point(px, 20), Fore, TextFormatFlags.NoPrefix);
                }
            }
        }

        // ---------------------------------------------------------------- một mục trên thanh bên
        class NavItem : Control
        {
            public string Glyph;
            public Font IconFont;
            public int Index;
            public TabControl Tabs;
            bool hover;

            public NavItem()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
                BackColor = Side;
                Margin = new Padding(0, 0, 0, 4);
            }
            protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Side);
                bool sel = Tabs != null && Tabs.SelectedIndex == Index;
                var r = new Rectangle(0, 2, Width - 1, Height - 5);
                if (sel || hover)
                    using (var path = Round(r, 9))
                    {
                        if (sel)
                            using (var br = new LinearGradientBrush(r, Color.FromArgb(32, 64, 112), Color.FromArgb(22, 44, 80), LinearGradientMode.Horizontal)) g.FillPath(br, path);
                        else using (var br = new SolidBrush(Color.FromArgb(18, 30, 52))) g.FillPath(br, path);
                        if (sel) using (var pen = new Pen(Color.FromArgb(52, 96, 160))) g.DrawPath(pen, path);
                    }
                if (sel)
                    using (var br = new LinearGradientBrush(new Rectangle(0, 10, 4, Height - 20), Accent, Accent2, LinearGradientMode.Vertical))
                        g.FillRectangle(br, 0, 10, 4, Height - 20);
                Color fg = sel ? Color.White : hover ? Fore : Muted;
                int tx = 14;
                if (IconFont != null && !string.IsNullOrEmpty(Glyph))
                {
                    TextRenderer.DrawText(g, Glyph, IconFont, new Rectangle(12, 0, 26, Height), sel ? Accent2 : fg, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
                    tx = 46;
                }
                TextRenderer.DrawText(g, Text, Font, new Rectangle(tx, 0, Width - tx - 6, Height), fg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }
}

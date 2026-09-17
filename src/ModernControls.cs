using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WeixinAntiRevoke
{
    internal static class Visual
    {
        internal static readonly Color Background = Color.FromArgb(10, 17, 30);
        internal static readonly Color Surface = Color.FromArgb(17, 29, 47);
        internal static readonly Color Text = Color.FromArgb(237, 245, 255);
        internal static readonly Color Muted = Color.FromArgb(149, 171, 195);
        internal static readonly Color Accent = Color.FromArgb(86, 226, 232);
        internal static readonly Color Border = Color.FromArgb(40, 62, 83);
        internal static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure();
            return path;
        }
        internal static void Label(Graphics g, string text, Font font, Color color, Rectangle rect, TextFormatFlags extra)
        {
            TextRenderer.DrawText(g, text, font, rect, color, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | extra);
        }
        internal static void Shield(Graphics g, float x, float y, float size, Color color)
        {
            using (GraphicsPath p = new GraphicsPath())
            using (Pen pen = new Pen(color, Math.Max(1.5f, size * .045f)))
            {
                p.AddLines(new[] { new PointF(x + size * .5f, y), new PointF(x + size * .88f, y + size * .14f), new PointF(x + size * .84f, y + size * .6f) });
                p.AddBezier(x + size * .84f, y + size * .6f, x + size * .77f, y + size * .8f, x + size * .61f, y + size * .93f, x + size * .5f, y + size);
                p.AddBezier(x + size * .5f, y + size, x + size * .39f, y + size * .93f, x + size * .23f, y + size * .8f, x + size * .16f, y + size * .6f);
                p.AddLine(x + size * .16f, y + size * .6f, x + size * .12f, y + size * .14f); p.CloseFigure();
                using (SolidBrush b = new SolidBrush(Color.FromArgb(14, color))) g.FillPath(b, p);
                g.DrawPath(pen, p);
                pen.StartCap = pen.EndCap = LineCap.Round;
                g.DrawLines(pen, new[] { new PointF(x + size * .32f, y + size * .49f), new PointF(x + size * .45f, y + size * .62f), new PointF(x + size * .69f, y + size * .36f) });
            }
        }
    }

    internal sealed class ModernButton : Button
    {
        internal bool Primary, Quiet;
        private bool hover;
        internal ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold); Cursor = Cursors.Hand;
            BackColor = Visual.Background; ForeColor = Visual.Text;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent == null ? Visual.Background : Parent.BackColor);
            float s = DeviceDpi / 96F;
            RectangleF r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (GraphicsPath path = Visual.Round(r, 12 * s))
            {
                Color left = !Enabled ? Color.FromArgb(27, 42, 58) : Primary ? (hover ? Color.FromArgb(111, 242, 231) : Color.FromArgb(74, 226, 213)) : Quiet ? Visual.Background : hover ? Color.FromArgb(30, 49, 70) : Visual.Surface;
                Color right = !Enabled ? left : Primary ? Color.FromArgb(97, 178, 255) : left;
                using (LinearGradientBrush b = new LinearGradientBrush(r, left, right, 0F)) g.FillPath(b, path);
                if (!Primary && !Quiet) using (Pen p = new Pen(hover ? Visual.Accent : Visual.Border)) g.DrawPath(p, path);
                if (Focused && ShowFocusCues) using (Pen p = new Pen(Visual.Accent, 2 * s)) { p.DashStyle = DashStyle.Dot; g.DrawPath(p, path); }
            }
            Color text = !Enabled ? Color.FromArgb(103, 126, 148) : Primary ? Color.FromArgb(8, 30, 45) : Quiet ? (hover ? Visual.Accent : Visual.Muted) : Visual.Text;
            Visual.Label(g, Text, Font, text, ClientRectangle, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    internal sealed class EffectCard : Button
    {
        internal string Title, Description, Footnote;
        internal bool Selected, Available = true;
        private bool hover;
        internal EffectCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
            BackColor = Visual.Background; AccessibleRole = AccessibleRole.RadioButton;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Visual.Background);
            float s = DeviceDpi / 96F;
            RectangleF rect = new RectangleF(1, 1, Width - 3, Height - 3);
            using (GraphicsPath p = Visual.Round(rect, 14 * s))
            {
                using (SolidBrush b = new SolidBrush(Selected ? Color.FromArgb(17, 47, 61) : Visual.Surface)) g.FillPath(b, p);
                using (Pen pen = new Pen(Selected ? Visual.Accent : hover && Enabled ? Color.FromArgb(77, 108, 136) : Visual.Border, Selected ? 1.5F * s : 1F)) g.DrawPath(pen, p);
                if (Focused && ShowFocusCues) using (Pen pen = new Pen(Visual.Accent)) { pen.DashStyle = DashStyle.Dot; g.DrawPath(pen, p); }
            }
            Color main = Available ? Visual.Text : Visual.Muted;
            RectangleF ring = new RectangleF(22 * s, 23 * s, 18 * s, 18 * s);
            using (Pen pen = new Pen(Selected ? Visual.Accent : Color.FromArgb(101, 126, 151), 1.6F * s)) g.DrawEllipse(pen, ring);
            if (Selected) using (SolidBrush b = new SolidBrush(Visual.Accent)) g.FillEllipse(b, 27 * s, 28 * s, 8 * s, 8 * s);
            using (Font title = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold))
            using (Font desc = new Font("Microsoft YaHei UI", 9.5F))
            using (Font foot = new Font("Microsoft YaHei UI", 8.5F))
            {
                Visual.Label(g, Title, title, main, new Rectangle((int)(52 * s), (int)(18 * s), Width - (int)(68 * s), (int)(29 * s)), TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                Visual.Label(g, Available ? Description : "这版微信暂时不支持此选项", desc, Visual.Muted, new Rectangle((int)(22 * s), (int)(53 * s), Width - (int)(40 * s), (int)(22 * s)), TextFormatFlags.SingleLine);
                Visual.Label(g, Footnote, foot, Selected ? Color.FromArgb(127, 208, 215) : Color.FromArgb(115, 140, 166), new Rectangle((int)(22 * s), (int)(80 * s), Width - (int)(40 * s), (int)(18 * s)), TextFormatFlags.SingleLine);
            }
        }
    }

    internal sealed class StatusCard : Panel
    {
        internal bool Working;
        internal float Phase;
        internal Color Signal = Visual.Accent;
        internal StatusCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Visual.Surface;
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Visual.Background);
            float s = DeviceDpi / 96F;
            RectangleF r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (GraphicsPath path = Visual.Round(r, 19 * s))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(20, 38, 57), Color.FromArgb(15, 28, 46), 15F)) g.FillPath(b, path);
                using (Pen p = new Pen(Color.FromArgb(50, 85, 109))) g.DrawPath(p, path);
            }
            float cx = Width - 93 * s, cy = Height / 2F;
            for (int i = 3; i >= 1; i--) using (Pen p = new Pen(Color.FromArgb(25 + (3 - i) * 10, Signal), s)) g.DrawEllipse(p, cx - i * 20 * s, cy - i * 20 * s, i * 40 * s, i * 40 * s);
            using (Pen p = new Pen(Color.FromArgb(150, Signal), 2 * s)) { g.DrawArc(p, cx - 60 * s, cy - 60 * s, 120 * s, 120 * s, Working ? Phase : 218, 56); }
            Visual.Shield(g, cx - 22 * s, cy - 26 * s, 44 * s, Signal);
            using (SolidBrush b = new SolidBrush(Signal)) g.FillEllipse(b, cx + 48 * s, cy + 24 * s, 5 * s, 5 * s);
        }
    }
}

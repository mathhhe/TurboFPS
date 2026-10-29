using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TurboFPS
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(12, 14, 19);
        public static readonly Color Panel = Color.FromArgb(17, 20, 27);
        public static readonly Color Card = Color.FromArgb(23, 27, 36);
        public static readonly Color CardHover = Color.FromArgb(30, 35, 47);
        public static readonly Color CardSelected = Color.FromArgb(16, 38, 33);
        public static readonly Color Border = Color.FromArgb(37, 42, 55);
        public static readonly Color Text = Color.FromArgb(232, 235, 242);
        public static readonly Color Muted = Color.FromArgb(134, 142, 160);
        public static readonly Color Faint = Color.FromArgb(82, 89, 106);
        public static readonly Color Accent = Color.FromArgb(0, 214, 143);
        public static readonly Color AccentHover = Color.FromArgb(46, 234, 170);
        public static readonly Color AccentDim = Color.FromArgb(0, 104, 72);
        public static readonly Color Cyan = Color.FromArgb(34, 211, 238);
        public static readonly Color Low = Color.FromArgb(34, 197, 94);
        public static readonly Color Mid = Color.FromArgb(245, 158, 11);
        public static readonly Color High = Color.FromArgb(239, 68, 68);

        public static readonly Font Title = new Font("Segoe UI Semibold", 17f);
        public static readonly Font H2 = new Font("Segoe UI Semibold", 14f);
        public static readonly Font Body = new Font("Segoe UI", 9.75f);
        public static readonly Font BodyBold = new Font("Segoe UI Semibold", 10.5f);
        public static readonly Font Button = new Font("Segoe UI Semibold", 9.75f);
        public static readonly Font Small = new Font("Segoe UI", 9f);
        public static readonly Font Tiny = new Font("Segoe UI Semibold", 7.5f);
        public static readonly Font Icon = new Font("Segoe MDL2 Assets", 12f);
        public static readonly Font IconSmall = new Font("Segoe MDL2 Assets", 9f);
        public static readonly Font IconBig = new Font("Segoe MDL2 Assets", 18f);
        public static readonly Font Mono = new Font("Consolas", 9f);

        public const TextFormatFlags NoPad = TextFormatFlags.NoPadding;
        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

        public static float Scale = 1f;

        public static void InitScale()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = Math.Max(1f, g.DpiX / 96f);
        }

        public static int S(int px) { return (int)Math.Round(px * Scale); }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color fill, Color? border = null)
        {
            using (var path = Round(r, radius))
            using (var b = new SolidBrush(fill))
            {
                g.FillPath(b, path);
                if (border.HasValue)
                    using (var p = new Pen(border.Value)) g.DrawPath(p, path);
            }
        }

        public static Color RiskColor(Risk r)
        {
            return r == Risk.Alto ? High : r == Risk.Medio ? Mid : Low;
        }

        public static string RiskText(Risk r)
        {
            return r == Risk.Alto ? "ALTO" : r == Risk.Medio ? "MÉDIO" : "BAIXO";
        }

        /// <summary>Desenha uma etiqueta arredondada alinhada à direita; retorna o X da borda esquerda.</summary>
        public static int Pill(Graphics g, string text, Color c, int right, int top)
        {
            var sz = TextRenderer.MeasureText(g, text, Tiny, Size.Empty, NoPad);
            var r = new Rectangle(right - sz.Width - S(16), top, sz.Width + S(16), sz.Height + S(8));
            FillRound(g, r, r.Height / 2, Color.FromArgb(38, c));
            TextRenderer.DrawText(g, text, Tiny, new Point(r.X + S(8), r.Y + S(4)), c, NoPad);
            return r.X;
        }

        public static void DrawCheck(Graphics g, Rectangle r, bool on, bool enabled)
        {
            if (on)
            {
                FillRound(g, r, S(5), enabled ? Accent : AccentDim);
                using (var p = new Pen(Bg, Math.Max(2f, S(2) * 1.1f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(p, new[]
                    {
                        new PointF(r.X + r.Width * 0.24f, r.Y + r.Height * 0.52f),
                        new PointF(r.X + r.Width * 0.43f, r.Y + r.Height * 0.70f),
                        new PointF(r.X + r.Width * 0.76f, r.Y + r.Height * 0.32f)
                    });
            }
            else
            {
                using (var path = Round(r, S(5)))
                using (var p = new Pen(enabled ? Muted : Faint, Math.Max(1.5f, S(1) * 1.5f)))
                    g.DrawPath(p, path);
            }
        }
    }

    class Canvas : Panel
    {
        public Canvas()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    abstract class PaintedControl : Control
    {
        protected bool Hover;

        protected PaintedControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            // Cada clique conta (sem isso, o segundo clique rápido vira "duplo clique" e é ignorado).
            SetStyle(ControlStyles.StandardDoubleClick, false);
        }

        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected void Prepare(Graphics g)
        {
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
        }
    }

    sealed class FlatButton : PaintedControl
    {
        public bool Primary;
        public string Glyph;

        public FlatButton(string text, bool primary)
        {
            Text = text;
            Primary = primary;
            Font = Theme.Button;
            Cursor = Cursors.Hand;
            Size = new Size(Theme.S(150), Theme.S(36));
        }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Prepare(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill, border, fore;
            if (Primary)
            {
                fill = !Enabled ? Theme.AccentDim : Hover ? Theme.AccentHover : Theme.Accent;
                border = fill;
                fore = Enabled ? Theme.Bg : Color.FromArgb(150, Theme.Bg);
            }
            else
            {
                fill = Hover && Enabled ? Theme.CardHover : Theme.Card;
                border = Theme.Border;
                fore = Enabled ? Theme.Text : Theme.Faint;
            }
            Theme.FillRound(g, r, Theme.S(8), fill, border);

            if (string.IsNullOrEmpty(Glyph))
            {
                TextRenderer.DrawText(g, Text, Font, r, fore, Theme.Center | TextFormatFlags.EndEllipsis);
                return;
            }
            var ts = TextRenderer.MeasureText(g, Text, Font, Size.Empty, Theme.NoPad);
            var gs = TextRenderer.MeasureText(g, Glyph, Theme.IconSmall, Size.Empty, Theme.NoPad);
            int gap = Theme.S(8);
            int x = (Width - ts.Width - gs.Width - gap) / 2;
            TextRenderer.DrawText(g, Glyph, Theme.IconSmall, new Rectangle(x, 0, gs.Width, Height), fore, TextFormatFlags.VerticalCenter | Theme.NoPad);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x + gs.Width + gap, 0, ts.Width + 2, Height), fore, TextFormatFlags.VerticalCenter | Theme.NoPad);
        }
    }

    sealed class CheckToggle : PaintedControl
    {
        public bool Checked = true;

        public CheckToggle(string text)
        {
            Text = text;
            Font = Theme.Small;
            Cursor = Cursors.Hand;
            var ts = TextRenderer.MeasureText(text, Font, Size.Empty, Theme.NoPad);
            Size = new Size(Theme.S(30) + ts.Width, Math.Max(Theme.S(22), ts.Height));
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            Checked = !Checked;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Prepare(g);
            int box = Theme.S(18);
            Theme.DrawCheck(g, new Rectangle(1, (Height - box) / 2, box, box), Checked, Enabled);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Theme.S(30), 0, Width - Theme.S(30), Height),
                Enabled ? (Hover ? Theme.Text : Theme.Muted) : Theme.Faint, TextFormatFlags.VerticalCenter | Theme.NoPad);
        }
    }

    sealed class NavItem : PaintedControl
    {
        public readonly CategoryInfo Info;
        public bool Active;
        public string Badge = "";

        public NavItem(CategoryInfo info)
        {
            Info = info;
            Cursor = Cursors.Hand;
            Size = new Size(Theme.S(208), Theme.S(38));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Prepare(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Active) Theme.FillRound(g, r, Theme.S(8), Theme.Card);
            else if (Hover) Theme.FillRound(g, r, Theme.S(8), Color.FromArgb(22, 26, 34));
            if (Active)
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillRectangle(b, 0, Theme.S(9), Theme.S(3), Height - Theme.S(18));

            TextRenderer.DrawText(g, Info.Glyph, Theme.Icon, new Rectangle(Theme.S(12), 0, Theme.S(26), Height),
                Active ? Theme.Accent : Theme.Muted, Theme.Center);
            TextRenderer.DrawText(g, Info.Name, Theme.Body, new Rectangle(Theme.S(46), 0, Width - Theme.S(100), Height),
                Active || Hover ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | Theme.NoPad);
            if (!string.IsNullOrEmpty(Badge))
            {
                var bs = TextRenderer.MeasureText(g, Badge, Theme.Small, Size.Empty, Theme.NoPad);
                TextRenderer.DrawText(g, Badge, Theme.Small, new Rectangle(Width - bs.Width - Theme.S(12), 0, bs.Width + 1, Height),
                    Active ? Theme.Muted : Theme.Faint, TextFormatFlags.VerticalCenter | Theme.NoPad);
            }
        }
    }

    sealed class SectionHeader : PaintedControl
    {
        public readonly CategoryInfo Info;
        public string Counter = "";

        public SectionHeader(CategoryInfo info)
        {
            Info = info;
            Height = Theme.S(34);
            Margin = new Padding(0, Theme.S(8), 0, Theme.S(2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Prepare(g);
            int y = Height - Theme.S(22);
            TextRenderer.DrawText(g, Info.Glyph, Theme.IconSmall, new Point(Theme.S(2), y + Theme.S(2)), Theme.Accent, Theme.NoPad);
            string caption = Info.Name.ToUpperInvariant();
            var cs = TextRenderer.MeasureText(g, caption, Theme.Tiny, Size.Empty, Theme.NoPad);
            TextRenderer.DrawText(g, caption, Theme.Tiny, new Point(Theme.S(24), y + Theme.S(2)), Theme.Accent, Theme.NoPad);
            int x = Theme.S(24) + cs.Width + Theme.S(10);
            if (!string.IsNullOrEmpty(Counter))
            {
                var ks = TextRenderer.MeasureText(g, Counter, Theme.Tiny, Size.Empty, Theme.NoPad);
                TextRenderer.DrawText(g, Counter, Theme.Tiny, new Point(x, y + Theme.S(2)), Theme.Faint, Theme.NoPad);
                x += ks.Width + Theme.S(10);
            }
            using (var p = new Pen(Theme.Border))
                g.DrawLine(p, x, y + Theme.S(8), Width, y + Theme.S(8));
        }
    }

    /// <summary>Cartão de uma otimização (com caixa de seleção) ou de uma ferramenta (com botão Executar).</summary>
    sealed class TweakRow : PaintedControl
    {
        public readonly Tweak Tweak;
        public readonly ToolAction Tool;
        public bool Selected, Available = true, Applied, Known, Busy;
        public event EventHandler SelectionChanged;
        public event EventHandler RunRequested;
        Rectangle runRect;
        bool hoverRun;

        public TweakRow(Tweak tweak) : this() { Tweak = tweak; }
        public TweakRow(ToolAction tool) : this() { Tool = tool; Known = true; }

        TweakRow()
        {
            Margin = new Padding(0, 0, 0, Theme.S(8));
        }

        string Title { get { return Tweak != null ? Tweak.Name : Tool.Name; } }
        string Desc { get { return Tweak != null ? Tweak.Description : Tool.Description; } }
        int TextLeft { get { return Theme.S(Tweak != null ? 54 : 22); } }
        int RightWidth { get { return Theme.S(Tweak != null ? 210 : 160); } }
        bool CanToggle { get { return Tweak != null && Known && Available && !Busy; } }

        public void FitWidth(int width)
        {
            int textW = Math.Max(Theme.S(120), width - TextLeft - RightWidth);
            var sz = TextRenderer.MeasureText(Desc, Theme.Small, new Size(textW, int.MaxValue), TextFormatFlags.WordBreak | Theme.NoPad);
            Size = new Size(width, Math.Max(Theme.S(74), Theme.S(42) + sz.Height + Theme.S(16)));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool h = Tool != null && runRect.Contains(e.Location);
            if (h != hoverRun) { hoverRun = h; Invalidate(); }
            Cursor = (Tool != null ? h && !Busy : CanToggle) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (Tool != null)
            {
                if (runRect.Contains(e.Location) && !Busy && RunRequested != null) RunRequested(this, EventArgs.Empty);
                return;
            }
            if (!CanToggle) return;
            Selected = !Selected;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Prepare(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            bool dim = Tweak != null && Known && !Available;
            Color fill = Selected ? Theme.CardSelected : (Hover && (CanToggle || Tool != null)) ? Theme.CardHover : Theme.Card;
            Theme.FillRound(g, r, Theme.S(10), fill, Selected ? Theme.AccentDim : Theme.Border);

            if (Tweak != null)
            {
                int box = Theme.S(20);
                Theme.DrawCheck(g, new Rectangle(Theme.S(18), (Height - box) / 2, box, box), Selected, !dim);
            }

            int textW = Width - TextLeft - RightWidth;
            TextRenderer.DrawText(g, Title, Theme.BodyBold, new Rectangle(TextLeft, Theme.S(14), textW, Theme.S(24)),
                dim ? Theme.Faint : Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | Theme.NoPad);
            TextRenderer.DrawText(g, Desc, Theme.Small, new Rectangle(TextLeft, Theme.S(42), textW, Height - Theme.S(46)),
                dim ? Theme.Faint : Theme.Muted, TextFormatFlags.WordBreak | Theme.NoPad);

            int right = Width - Theme.S(18);
            if (Tweak != null) PaintTweakStatus(g, right);
            else PaintRunButton(g, right);
        }

        void PaintTweakStatus(Graphics g, int right)
        {
            int x = Theme.Pill(g, "RISCO " + Theme.RiskText(Tweak.Risk), Theme.RiskColor(Tweak.Risk), right, Theme.S(14));
            if (Tweak.NeedsRestart) Theme.Pill(g, "REINÍCIO", Theme.Muted, x - Theme.S(6), Theme.S(14));

            string status;
            Color color;
            if (Busy && Selected) { status = "Processando..."; color = Theme.Cyan; }
            else if (!Known) { status = "Verificando..."; color = Theme.Faint; }
            else if (!Available) { status = "Indisponível neste PC"; color = Theme.Faint; }
            else if (Applied) { status = "Ativo"; color = Theme.Accent; }
            else { status = "Não aplicado"; color = Theme.Muted; }

            var ss = TextRenderer.MeasureText(g, status, Theme.Small, Size.Empty, Theme.NoPad);
            int y = Theme.S(46);
            TextRenderer.DrawText(g, status, Theme.Small, new Point(right - ss.Width, y), color, Theme.NoPad);
            if (Known && Available && !(Busy && Selected))
            {
                int d = Theme.S(8);
                var dot = new Rectangle(right - ss.Width - d - Theme.S(7), y + (ss.Height - d) / 2, d, d);
                if (Applied)
                    using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, dot);
                else
                    using (var p = new Pen(Theme.Muted, Math.Max(1.2f, Theme.Scale * 1.2f))) g.DrawEllipse(p, dot);
            }
        }

        void PaintRunButton(Graphics g, int right)
        {
            int w = Theme.S(118), h = Theme.S(34);
            runRect = new Rectangle(right - w, (Height - h) / 2, w, h);
            if (Busy)
                Theme.FillRound(g, runRect, Theme.S(8), Theme.Card, Theme.Border);
            else if (hoverRun)
                Theme.FillRound(g, runRect, Theme.S(8), Theme.Accent);
            else
                Theme.FillRound(g, runRect, Theme.S(8), Color.FromArgb(30, Theme.Accent), Theme.AccentDim);
            TextRenderer.DrawText(g, Busy ? "Executando..." : "Executar", Theme.Button, runRect,
                Busy ? Theme.Muted : hoverRun ? Theme.Bg : Theme.Accent, Theme.Center);
        }
    }
}

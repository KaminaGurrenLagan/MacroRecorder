using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MacroRecorderPro.UI
{
    // Custom Gradient Panel
    public class GradientPanel : Panel
    {
        public Color GradientColorStart { get; set; } = Color.Purple;
        public Color GradientColorEnd { get; set; } = Color.Blue;
        public float GradientAngle { get; set; } = 45f;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(
                ClientRectangle,
                GradientColorStart,
                GradientColorEnd,
                GradientAngle))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }
    }

    // Custom Gradient Button
    public class GradientButton : Button
    {
        public Color GradientColorStart { get; set; } = Color.Purple;
        public Color GradientColorEnd { get; set; } = Color.Blue;
        public int BorderRadius { get; set; } = 12;
        private bool isHovered = false;

        public GradientButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            GraphicsPath path = GetRoundedRectangle(rect, BorderRadius);

            // Gradient background
            Color startColor = isHovered ? ControlPaint.Light(GradientColorStart, 0.2f) : GradientColorStart;
            Color endColor = isHovered ? ControlPaint.Light(GradientColorEnd, 0.2f) : GradientColorEnd;

            using (LinearGradientBrush brush = new LinearGradientBrush(
                rect, startColor, endColor, 135f))
            {
                g.FillPath(brush, path);
            }

            // Shadow effect
            if (Enabled)
            {
                using (Pen shadowPen = new Pen(Color.FromArgb(50, GradientColorEnd), 2))
                {
                    Rectangle shadowRect = new Rectangle(2, 2, Width - 5, Height - 5);
                    GraphicsPath shadowPath = GetRoundedRectangle(shadowRect, BorderRadius);
                    g.DrawPath(shadowPen, shadowPath);
                }
            }

            // Text
            TextRenderer.DrawText(g, Text, Font, rect, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }

    // Factory Pattern для создания UI элементов (SRP)
    public static class UIFactory
    {
        public static GradientButton CreateGradientButton(string text, int x, int y,
            Color colorStart, Color colorEnd)
        {
            var btn = new GradientButton
            {
                Text = text,
                Location = new Point(x + 10, y),
                Size = new Size(440, 50),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                GradientColorStart = colorStart,
                GradientColorEnd = colorEnd,
                BorderRadius = 15
            };
            return btn;
        }

        public static Button CreateSmallButton(string text, int x, int y, Color color)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(135, 38),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.BorderColor = ColorScheme.AccentLight;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.1f);

            // Rounded corners effect
            System.Drawing.Region region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0,
                btn.Width, btn.Height, 10, 10));
            btn.Region = region;

            return btn;
        }

        public static CheckBox CreateCheckBox(string text, int x, int y)
        {
            return new CheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(200, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = ColorScheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
        }

        public static Label CreateLabel(string text, int x, int y, int width = 60)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = ColorScheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        public static NumericUpDown CreateNumericUpDown(int x, int y)
        {
            var numeric = new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(75, 28),
                Minimum = 1,
                Maximum = 9999,
                Value = 1,
                BackColor = ColorScheme.Surface,
                ForeColor = ColorScheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9)
            };
            return numeric;
        }

        public static TrackBar CreateTrackBar(int x, int y)
        {
            var track = new TrackBar
            {
                Location = new Point(x, y),
                Size = new Size(170, 40),
                Minimum = 10,
                Maximum = 500,
                Value = 100,
                TickFrequency = 50,
                BackColor = ColorScheme.Background,
                Cursor = Cursors.Hand
            };
            return track;
        }

        // P/Invoke для скругленных углов
        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);
    }

    // Централизованная цветовая схема в стиле iOS (фиолетовые тона)
    public static class ColorScheme
    {
        // Основные фоны
        public static readonly Color Background = Color.FromArgb(15, 10, 25);        // Очень темный фиолетовый
        public static readonly Color Surface = Color.FromArgb(30, 20, 45);           // Темный фиолетовый
        public static readonly Color SurfaceLight = Color.FromArgb(45, 30, 65);      // Средний фиолетовый

        // Акцентные цвета (фиолетовый градиент)
        public static readonly Color AccentDark = Color.FromArgb(88, 28, 135);       // Темный пурпурный
        public static readonly Color Accent = Color.FromArgb(126, 34, 206);          // Яркий пурпурный
        public static readonly Color AccentLight = Color.FromArgb(167, 139, 250);    // Светлый фиолетовый
        public static readonly Color AccentGlow = Color.FromArgb(196, 181, 253);     // Светящийся фиолетовый

        // Функциональные цвета
        public static readonly Color SuccessDark = Color.FromArgb(20, 83, 45);       // Темный зеленый
        public static readonly Color Success = Color.FromArgb(34, 197, 94);          // Зеленый
        public static readonly Color SuccessGlow = Color.FromArgb(134, 239, 172);    // Светлый зеленый

        public static readonly Color DangerDark = Color.FromArgb(127, 29, 29);       // Темный красный
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);           // Красный
        public static readonly Color DangerGlow = Color.FromArgb(252, 165, 165);     // Светлый красный

        public static readonly Color Warning = Color.FromArgb(251, 146, 60);         // Оранжевый
        public static readonly Color Info = Color.FromArgb(59, 130, 246);            // Синий

        // Текстовые цвета
        public static readonly Color TextPrimary = Color.FromArgb(243, 244, 246);    // Почти белый
        public static readonly Color TextSecondary = Color.FromArgb(156, 163, 175);  // Серый
        public static readonly Color TextMuted = Color.FromArgb(107, 114, 128);      // Темно-серый
    }
}
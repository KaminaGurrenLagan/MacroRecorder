using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MacroRecorderPro.UI
{
    // Custom Gradient Panel with 3-color support
    public class GradientPanel : Panel
    {
        public Color GradientColorStart { get; set; } = Color.Purple;
        public Color GradientColorMiddle { get; set; } = Color.Magenta;
        public Color GradientColorEnd { get; set; } = Color.Blue;
        public float GradientAngle { get; set; } = 45f;
        public bool UseThreeColorGradient { get; set; } = false;

        public GradientPanel()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (UseThreeColorGradient)
            {
                // Создаем трехцветный градиент
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    ClientRectangle,
                    GradientColorStart,
                    GradientColorEnd,
                    GradientAngle))
                {
                    ColorBlend blend = new ColorBlend();
                    blend.Colors = new[] { GradientColorStart, GradientColorMiddle, GradientColorEnd };
                    blend.Positions = new[] { 0f, 0.5f, 1f };
                    brush.InterpolationColors = blend;
                    e.Graphics.FillRectangle(brush, ClientRectangle);
                }
            }
            else
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
    }

    // Custom Gradient Button with glass morphism
    public class GradientButton : Button
    {
        public Color GradientColorStart { get; set; } = Color.Purple;
        public Color GradientColorMiddle { get; set; } = Color.Magenta;
        public Color GradientColorEnd { get; set; } = Color.Blue;
        public int BorderRadius { get; set; } = 18;
        private bool isHovered = false;
        private bool isPressed = false;

        public GradientButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            DoubleBuffered = true;
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
            isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            isPressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            GraphicsPath path = GetRoundedRectangle(rect, BorderRadius);

            // Эффект нажатия
            float scale = isPressed ? 0.95f : 1f;
            if (isPressed)
            {
                g.TranslateTransform(Width * (1 - scale) / 2, Height * (1 - scale) / 2);
                g.ScaleTransform(scale, scale);
            }

            // Тень
            if (!isPressed && Enabled)
            {
                Rectangle shadowRect = new Rectangle(3, 3, Width - 6, Height - 6);
                GraphicsPath shadowPath = GetRoundedRectangle(shadowRect, BorderRadius);
                using (PathGradientBrush shadowBrush = new PathGradientBrush(shadowPath))
                {
                    shadowBrush.CenterColor = Color.FromArgb(80, 0, 0, 0);
                    shadowBrush.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
                    g.FillPath(shadowBrush, shadowPath);
                }
            }

            // Градиентный фон с 3 цветами
            Color startColor = isHovered ? ControlPaint.Light(GradientColorStart, 0.15f) : GradientColorStart;
            Color middleColor = isHovered ? ControlPaint.Light(GradientColorMiddle, 0.15f) : GradientColorMiddle;
            Color endColor = isHovered ? ControlPaint.Light(GradientColorEnd, 0.15f) : GradientColorEnd;

            using (LinearGradientBrush brush = new LinearGradientBrush(rect, startColor, endColor, 135f))
            {
                ColorBlend blend = new ColorBlend();
                blend.Colors = new[] { startColor, middleColor, endColor };
                blend.Positions = new[] { 0f, 0.5f, 1f };
                brush.InterpolationColors = blend;
                g.FillPath(brush, path);
            }

            // Стеклянный эффект (glass morphism)
            Rectangle glassRect = new Rectangle(0, 0, Width, Height / 2);
            GraphicsPath glassPath = GetRoundedRectangle(glassRect, BorderRadius);
            using (LinearGradientBrush glassBrush = new LinearGradientBrush(
                glassRect,
                Color.FromArgb(40, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                90f))
            {
                g.FillPath(glassBrush, glassPath);
            }

            // Граница с градиентом
            if (!isPressed && Enabled)
            {
                using (LinearGradientBrush borderBrush = new LinearGradientBrush(
                    rect,
                    Color.FromArgb(100, 255, 255, 255),
                    Color.FromArgb(30, 255, 255, 255),
                    135f))
                {
                    using (Pen borderPen = new Pen(borderBrush, 1.5f))
                    {
                        g.DrawPath(borderPen, path);
                    }
                }
            }

            // Текст с тенью
            Rectangle textRect = new Rectangle(1, 1, Width, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Color.FromArgb(100, 0, 0, 0),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            textRect = new Rectangle(0, 0, Width, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, ForeColor,
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

    // Modern Button (для маленьких кнопок)
    public class ModernButton : Button
    {
        private bool isHovered = false;

        public ModernButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            BackColor = ColorScheme.Surface;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9, FontStyle.Bold);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHovered = true;
            BackColor = ColorScheme.SurfaceLight;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            BackColor = ColorScheme.Surface;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Скругленные углы
            using (GraphicsPath path = GetRoundedRect(rect, 12))
            {
                g.FillPath(new SolidBrush(BackColor), path);

                // Граница
                using (Pen pen = new Pen(Color.FromArgb(50, ColorScheme.AccentLight), 1))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Текст
            TextRenderer.DrawText(g, Text, Font, rect, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private GraphicsPath GetRoundedRect(Rectangle rect, int radius)
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

    // Modern CheckBox
    public class ModernCheckBox : CheckBox
    {
        public ModernCheckBox()
        {
            FlatStyle = FlatStyle.Flat;
            Cursor = Cursors.Hand;
            ForeColor = ColorScheme.TextPrimary;
            Font = new Font("Segoe UI", 9);
        }
    }

    // Modern NumericUpDown
    public class ModernNumericUpDown : NumericUpDown
    {
        public ModernNumericUpDown()
        {
            BackColor = ColorScheme.SurfaceLight;
            ForeColor = ColorScheme.TextPrimary;
            BorderStyle = BorderStyle.None;
            Font = new Font("Segoe UI", 9);
        }
    }

    // Modern TrackBar
    public class ModernTrackBar : TrackBar
    {
        public ModernTrackBar()
        {
            BackColor = ColorScheme.Surface;
            Cursor = Cursors.Hand;
        }
    }

    // Factory Pattern для создания UI элементов
    public static class UIFactory
    {
        public static GradientButton CreateGradientButton(string text, int x, int y,
            Color colorStart, Color colorMiddle, Color colorEnd)
        {
            var btn = new GradientButton
            {
                Text = text,
                Location = new Point(x + 10, y),
                Size = new Size(450, 55),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                GradientColorStart = colorStart,
                GradientColorMiddle = colorMiddle,
                GradientColorEnd = colorEnd,
                BorderRadius = 18
            };
            return btn;
        }

        public static ModernButton CreateModernButton(string text, int x, int y)
        {
            var btn = new ModernButton
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(145, 42)
            };
            return btn;
        }

        public static ModernCheckBox CreateCheckBox(string text, int x, int y)
        {
            return new ModernCheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(220, 25)
            };
        }

        public static Label CreateLabel(string text, int x, int y, int width = 70)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = ColorScheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
        }

        public static ModernNumericUpDown CreateNumericUpDown(int x, int y)
        {
            var numeric = new ModernNumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(80, 30),
                Minimum = 1,
                Maximum = 9999,
                Value = 1
            };
            ApplyRoundedCorners(numeric, 8);
            return numeric;
        }

        public static ModernTrackBar CreateTrackBar(int x, int y)
        {
            var track = new ModernTrackBar
            {
                Location = new Point(x, y),
                Size = new Size(300, 40),
                Minimum = 10,
                Maximum = 500,
                Value = 100,
                TickFrequency = 50
            };
            return track;
        }

        public static void ApplyRoundedCorners(Control control, int radius)
        {
            try
            {
                IntPtr ptr = CreateRoundRectRgn(0, 0, control.Width, control.Height, radius, radius);
                control.Region = System.Drawing.Region.FromHrgn(ptr);
                DeleteObject(ptr);
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "DeleteObject")]
        private static extern bool DeleteObject(IntPtr hObject);
    }

    // Цветовая схема в стиле iOS 18 / HyperOS 3
    public static class ColorScheme
    {
        // Фоны - глубокий темный с фиолетовым оттенком
        public static readonly Color Background = Color.FromArgb(10, 5, 20);         // Почти черный с фиолетовым
        public static readonly Color Surface = Color.FromArgb(25, 15, 40);           // Темный фиолетовый
        public static readonly Color SurfaceLight = Color.FromArgb(40, 25, 60);      // Средний фиолетовый

        // Акцентные цвета - яркий фиолетово-розовый градиент
        public static readonly Color AccentDark = Color.FromArgb(75, 0, 130);        // Глубокий индиго
        public static readonly Color Accent = Color.FromArgb(138, 43, 226);          // Яркий фиолетовый
        public static readonly Color AccentLight = Color.FromArgb(186, 85, 211);     // Светлый orchid
        public static readonly Color AccentGlow = Color.FromArgb(218, 112, 214);     // Розово-фиолетовый

        // Success - зеленый градиент
        public static readonly Color SuccessDark = Color.FromArgb(0, 100, 80);       // Темный изумрудный
        public static readonly Color Success = Color.FromArgb(16, 185, 129);         // Яркий изумрудный
        public static readonly Color SuccessGlow = Color.FromArgb(110, 231, 183);    // Светлый мятный

        // Danger - красный градиент
        public static readonly Color DangerDark = Color.FromArgb(153, 27, 27);       // Темный красный
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);           // Яркий красный
        public static readonly Color DangerGlow = Color.FromArgb(252, 165, 165);     // Розовый

        // Дополнительные
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);         // Янтарный
        public static readonly Color Info = Color.FromArgb(59, 130, 246);            // Синий

        // Текст
        public static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);    // Почти белый
        public static readonly Color TextSecondary = Color.FromArgb(203, 213, 225);  // Светло-серый
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);      // Серый
    }
}
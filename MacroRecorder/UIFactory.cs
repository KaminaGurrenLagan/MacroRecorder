using System.Drawing;
using System.Windows.Forms;

namespace MacroRecorderPro.UI
{
    // Factory Pattern для создания UI элементов (SRP)
    public static class UIFactory
    {
        public static Button CreateButton(string text, int x, int y, Color color)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x + 10, y),
                Size = new Size(410, 45),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.1f);
            return btn;
        }

        public static Button CreateSmallButton(string text, int x, int y, Color color)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(125, 32),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.1f);
            return btn;
        }

        public static CheckBox CreateCheckBox(string text, int x, int y)
        {
            return new CheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(180, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
        }

        public static Label CreateLabel(string text, int x, int y, int width = 50)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        public static NumericUpDown CreateNumericUpDown(int x, int y)
        {
            return new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(70, 25),
                Minimum = 1,
                Maximum = 9999,
                Value = 1,
                BackColor = ColorScheme.Surface,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        public static TrackBar CreateTrackBar(int x, int y)
        {
            return new TrackBar
            {
                Location = new Point(x, y),
                Size = new Size(180, 35),
                Minimum = 10,
                Maximum = 500,
                Value = 100,
                TickFrequency = 50,
                BackColor = ColorScheme.Background
            };
        }
    }

    // Централизованная цветовая схема (SRP)
    public static class ColorScheme
    {
        public static readonly Color Background = Color.FromArgb(24, 24, 27);
        public static readonly Color Surface = Color.FromArgb(39, 39, 42);
        public static readonly Color Primary = Color.FromArgb(59, 130, 246);
        public static readonly Color Success = Color.FromArgb(34, 197, 94);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        public static readonly Color Warning = Color.FromArgb(251, 146, 60);
        public static readonly Color Secondary = Color.FromArgb(71, 85, 105);
        public static readonly Color Accent = Color.FromArgb(168, 85, 247);
        public static readonly Color Gray = Color.FromArgb(156, 163, 175);
    }
}
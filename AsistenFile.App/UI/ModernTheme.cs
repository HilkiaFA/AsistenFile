using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AsistenFile.App.UI
{
    public static class ModernTheme
    {
        public static readonly Color SidebarBg = Color.FromArgb(15, 23, 42);       // Slate 900
        public static readonly Color SidebarItemHover = Color.FromArgb(30, 41, 59); // Slate 800
        public static readonly Color SidebarItemActive = Color.FromArgb(51, 65, 85);// Slate 700
        public static readonly Color Primary = Color.FromArgb(37, 99, 235);        // Blue 600
        public static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216);   // Blue 700
        public static readonly Color AccentSuccess = Color.FromArgb(16, 185, 129);  // Emerald 500
        public static readonly Color AccentWarning = Color.FromArgb(245, 158, 11);  // Amber 500
        public static readonly Color AccentDanger = Color.FromArgb(239, 68, 68);    // Red 500

        public static readonly Color ContentBg = Color.FromArgb(248, 250, 252);     // Slate 50
        public static readonly Color CardBg = Color.White;
        public static readonly Color BorderColor = Color.FromArgb(226, 232, 240);   // Slate 200
        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);      // Slate 900
        public static readonly Color TextSecondary = Color.FromArgb(71, 85, 105);   // Slate 600
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);     // Slate 400

        public static readonly Font HeaderFont = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
        public static readonly Font SubHeaderFont = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        public static readonly Font BodyFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        public static readonly Font SmallFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        public static readonly Font CodeFont = new Font("Consolas", 9f, FontStyle.Regular);

        public static void ApplyCardStyle(Panel panel)
        {
            panel.BackColor = CardBg;
            panel.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderColor, 1);
                var rect = panel.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                e.Graphics.DrawRectangle(pen, rect);
            };
        }

        public static void StyleButton(Button btn, bool isPrimary = true)
        {
            btn.UseMnemonic = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;

            if (isPrimary)
            {
                btn.BackColor = Primary;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.MouseOverBackColor = PrimaryHover;
            }
            else
            {
                btn.BackColor = Color.FromArgb(241, 245, 249);
                btn.ForeColor = TextPrimary;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
            }
        }

        public static Color GetBadgeColorForExtension(string ext)
        {
            return (ext?.ToLowerInvariant()) switch
            {
                ".pdf" => Color.FromArgb(220, 38, 38),   // Red
                ".docx" or ".doc" => Color.FromArgb(37, 99, 235), // Blue
                ".xlsx" or ".xls" or ".csv" => Color.FromArgb(16, 185, 129), // Emerald
                ".eml" or ".msg" => Color.FromArgb(139, 92, 246), // Purple
                ".png" or ".jpg" or ".jpeg" => Color.FromArgb(245, 158, 11), // Orange
                _ => Color.FromArgb(100, 116, 139)       // Slate
            };
        }
    }
}

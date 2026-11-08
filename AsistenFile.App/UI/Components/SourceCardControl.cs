using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AsistenFile.App.UI;
using AsistenFile.Core.Models;

namespace AsistenFile.App.UI.Components
{
    public class SourceCardControl : UserControl
    {
        private readonly SearchResultItem _source;

        public SourceCardControl(SearchResultItem source)
        {
            _source = source;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.BackColor = ModernTheme.CardBg;
            this.Font = ModernTheme.BodyFont;
            this.Margin = new Padding(0, 0, 0, 10);
            this.Padding = new Padding(12);
            this.Height = 150;
            this.Dock = DockStyle.Top;

            // Border painting
            this.Paint += (s, e) =>
            {
                using var pen = new Pen(ModernTheme.BorderColor, 1);
                var rect = this.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                e.Graphics.DrawRectangle(pen, rect);
            };

            // Top Header Panel
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.Transparent
            };

            // Badge Extension
            var badgeExt = new Label
            {
                Text = _source.Extension.TrimStart('.').ToUpperInvariant(),
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ModernTheme.GetBadgeColorForExtension(_source.Extension),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(46, 22),
                Location = new Point(0, 4)
            };

            // File Name Label
            var lblFileName = new Label
            {
                Text = _source.FileName,
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                AutoEllipsis = true,
                Location = new Point(54, 4),
                Size = new Size(400, 24),
                Cursor = Cursors.Hand
            };
            lblFileName.Click += (s, e) => OpenFile();

            // Section / Page Label
            var lblSection = new Label
            {
                Text = $"📍 {_source.SectionTitle}",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(460, 8)
            };

            // Relevance Badge
            int pct = (int)Math.Clamp(_source.RelevanceScore * 100, 10, 100);
            var lblScore = new Label
            {
                Text = $"⭐ {pct}% Cocok",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.Primary,
                BackColor = Color.FromArgb(239, 246, 255),
                Padding = new Padding(4, 2, 4, 2),
                AutoSize = true,
                Dock = DockStyle.Right
            };

            headerPanel.Controls.Add(lblScore);
            headerPanel.Controls.Add(lblSection);
            headerPanel.Controls.Add(lblFileName);
            headerPanel.Controls.Add(badgeExt);

            // Action Buttons Panel
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = Color.Transparent
            };

            var btnOpenFile = new Button
            {
                Text = "📖 Buka File",
                Size = new Size(95, 26),
                Location = new Point(0, 2)
            };
            ModernTheme.StyleButton(btnOpenFile, isPrimary: true);
            btnOpenFile.Click += (s, e) => OpenFile();

            var btnOpenFolder = new Button
            {
                Text = "📂 Buka Folder",
                Size = new Size(105, 26),
                Location = new Point(105, 2)
            };
            ModernTheme.StyleButton(btnOpenFolder, isPrimary: false);
            btnOpenFolder.Click += (s, e) => OpenContainingFolder();

            var btnCopy = new Button
            {
                Text = "📋 Salin Kutipan",
                Size = new Size(110, 26),
                Location = new Point(220, 2)
            };
            ModernTheme.StyleButton(btnCopy, isPrimary: false);
            btnCopy.Click += (s, e) =>
            {
                Clipboard.SetText($"\"{_source.Snippet}\"\n(Sumber: {_source.FileName}, {_source.SectionTitle})");
                MessageBox.Show("Kutipan berhasil disalin ke clipboard!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var lblModified = new Label
            {
                Text = $"Dimodifikasi: {_source.FileLastModified:dd/MM/yyyy HH:mm}",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextMuted,
                AutoSize = true,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight
            };

            bottomPanel.Controls.Add(lblModified);
            bottomPanel.Controls.Add(btnCopy);
            bottomPanel.Controls.Add(btnOpenFolder);
            bottomPanel.Controls.Add(btnOpenFile);

            // Snippet Box (Middle)
            var snippetBox = new TextBox
            {
                Text = _source.Snippet,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = ModernTheme.TextSecondary,
                Font = ModernTheme.BodyFont,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill
            };

            var snippetContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(248, 250, 252)
            };
            snippetContainer.Controls.Add(snippetBox);

            this.Controls.Add(snippetContainer);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(headerPanel);
        }

        private void OpenFile()
        {
            try
            {
                if (File.Exists(_source.FilePath))
                {
                    Process.Start(new ProcessStartInfo(_source.FilePath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show($"File tidak ditemukan di disk:\n{_source.FilePath}", "File Tidak Ditemukan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membuka file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenContainingFolder()
        {
            try
            {
                if (File.Exists(_source.FilePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{_source.FilePath}\"");
                }
                else if (Directory.Exists(Path.GetDirectoryName(_source.FilePath)))
                {
                    Process.Start("explorer.exe", $"\"{Path.GetDirectoryName(_source.FilePath)}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membuka folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

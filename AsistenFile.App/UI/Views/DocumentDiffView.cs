using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AsistenFile.App.UI;
using AsistenFile.Core.Features;
using AsistenFile.Core.Models;

namespace AsistenFile.App.UI.Views
{
    public class DocumentDiffView : UserControl
    {
        private readonly IDocumentDiffService _diffService;

        private TextBox _txtFileA = null!;
        private TextBox _txtFileB = null!;
        private Button _btnBrowseA = null!;
        private Button _btnBrowseB = null!;
        private Button _btnCompare = null!;
        private Button _btnCopySummary = null!;
        private TextBox _txtSummary = null!;
        private FlowLayoutPanel _flowChanges = null!;
        private Label _lblStatus = null!;

        private DocumentDiffResult? _currentResult;

        public DocumentDiffView(IDocumentDiffService diffService)
        {
            _diffService = diffService;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.BackColor = ModernTheme.ContentBg;
            this.Dock = DockStyle.Fill;
            this.Font = ModernTheme.BodyFont;
            this.Padding = new Padding(20);

            // 1. Top Card: File Selectors
            var topCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 160,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(16),
                Margin = new Padding(0, 0, 0, 16)
            };
            ModernTheme.ApplyCardStyle(topCard);

            var lblHeader = new Label
            {
                Text = "📑 Bandingkan Dua Dokumen (Document Diff AI)",
                Font = ModernTheme.HeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblSub = new Label
            {
                Text = "Bandingkan dua versi kontrak, proposal, atau laporan kerja. AI otomatis mendeteksi pasal yang dihapus/ditambahkan serta selisih angka dan biaya.",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextSecondary,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 10)
            };

            // Selectors Rows
            var selectorsPanel = new Panel { Dock = DockStyle.Bottom, Height = 74 };

            // Row 1: File A
            var lblA = new Label { Text = "Dokumen Lama (A):", Location = new Point(0, 5), AutoSize = true, Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold) };
            _txtFileA = new TextBox { Location = new Point(140, 2), Size = new Size(420, 24), ReadOnly = true };
            _btnBrowseA = new Button { Text = "Pilih File A...", Location = new Point(568, 1), Size = new Size(110, 26) };
            ModernTheme.StyleButton(_btnBrowseA, isPrimary: false);
            _btnBrowseA.Click += (s, e) => BrowseFile(p => _txtFileA.Text = p);

            // Row 2: File B & Compare Button
            var lblB = new Label { Text = "Dokumen Baru (B):", Location = new Point(0, 40), AutoSize = true, Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold) };
            _txtFileB = new TextBox { Location = new Point(140, 37), Size = new Size(420, 24), ReadOnly = true };
            _btnBrowseB = new Button { Text = "Pilih File B...", Location = new Point(568, 36), Size = new Size(110, 26) };
            ModernTheme.StyleButton(_btnBrowseB, isPrimary: false);
            _btnBrowseB.Click += (s, e) => BrowseFile(p => _txtFileB.Text = p);

            _btnCompare = new Button { Text = "⚡ Bandingkan Sekarang", Location = new Point(690, 10), Size = new Size(200, 52) };
            ModernTheme.StyleButton(_btnCompare, isPrimary: true);
            _btnCompare.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
            _btnCompare.Click += async (s, e) => await ExecuteComparisonAsync();

            selectorsPanel.Controls.Add(_btnCompare);
            selectorsPanel.Controls.Add(_btnBrowseB);
            selectorsPanel.Controls.Add(_txtFileB);
            selectorsPanel.Controls.Add(lblB);
            selectorsPanel.Controls.Add(_btnBrowseA);
            selectorsPanel.Controls.Add(_txtFileA);
            selectorsPanel.Controls.Add(lblA);

            topCard.Controls.Add(selectorsPanel);
            topCard.Controls.Add(lblSub);
            topCard.Controls.Add(lblHeader);

            // 2. Middle Split Container: Summary (Top/Left) + Change Cards (Bottom/Right)
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 210,
                BackColor = ModernTheme.BorderColor
            };

            // Summary Panel (Top)
            var summaryPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(14)
            };
            ModernTheme.ApplyCardStyle(summaryPanel);

            var summaryHeader = new Panel { Dock = DockStyle.Top, Height = 28 };
            var lblSumTitle = new Label
            {
                Text = "📊 Rangkuman Eksekutif Perbedaan:",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Left,
                AutoSize = true
            };

            _btnCopySummary = new Button
            {
                Text = "📋 Salin Rangkuman",
                Size = new Size(130, 26),
                Dock = DockStyle.Right
            };
            ModernTheme.StyleButton(_btnCopySummary, isPrimary: false);
            _btnCopySummary.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_txtSummary.Text))
                {
                    Clipboard.SetText(_txtSummary.Text);
                    MessageBox.Show("Rangkuman perbandingan disalin ke clipboard!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            summaryHeader.Controls.Add(lblSumTitle);
            summaryHeader.Controls.Add(_btnCopySummary);

            _txtSummary = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = ModernTheme.TextPrimary,
                Font = new Font("Segoe UI", 9.5f),
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Text = "Pilih dua dokumen di atas lalu klik 'Bandingkan Sekarang' untuk melihat hasil komparasi klausul dan angka."
            };

            var summaryBoxContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(248, 250, 252)
            };
            summaryBoxContainer.Controls.Add(_txtSummary);

            summaryPanel.Controls.Add(summaryBoxContainer);
            summaryPanel.Controls.Add(summaryHeader);

            splitContainer.Panel1.Controls.Add(summaryPanel);

            // Change Cards Flow Panel (Bottom)
            var cardsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(14)
            };
            ModernTheme.ApplyCardStyle(cardsPanel);

            var lblCardsTitle = new Label
            {
                Text = "🔍 Rincian Perubahan Klausul & Angka Terdeteksi:",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 8)
            };

            _flowChanges = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            cardsPanel.Controls.Add(_flowChanges);
            cardsPanel.Controls.Add(lblCardsTitle);

            splitContainer.Panel2.Controls.Add(cardsPanel);

            // Bottom Status Label
            _lblStatus = new Label
            {
                Text = "Status: Siap membandingkan dokumen.",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextSecondary,
                Dock = DockStyle.Bottom,
                Height = 22
            };

            var middleContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 10)
            };
            middleContainer.Controls.Add(splitContainer);

            this.Controls.Add(middleContainer);
            this.Controls.Add(_lblStatus);
            this.Controls.Add(topCard);
        }

        private void BrowseFile(Action<string> onSelected)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Dokumen Pendukung (*.pdf;*.docx;*.txt;*.md;*.xlsx)|*.pdf;*.docx;*.txt;*.md;*.xlsx|Semua File (*.*)|*.*",
                Title = "Pilih Dokumen untuk Dibandingkan"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                onSelected(ofd.FileName);
            }
        }

        private async Task ExecuteComparisonAsync()
        {
            var fileA = _txtFileA.Text.Trim();
            var fileB = _txtFileB.Text.Trim();

            if (string.IsNullOrEmpty(fileA) || string.IsNullOrEmpty(fileB))
            {
                MessageBox.Show("Silakan pilih kedua file (File A dan File B) terlebih dahulu.", "Peringatan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnCompare.Enabled = false;
            _lblStatus.Text = "⏳ Menganalisis perbedaan kedua dokumen (mengekstrak klausul & membandingkan nilai)...";
            _flowChanges.Controls.Clear();

            try
            {
                _currentResult = await _diffService.CompareDocumentsAsync(fileA, fileB);
                _txtSummary.Text = _currentResult.ExecutiveSummary;

                _lblStatus.Text = $"✅ Analisis selesai: Ditemukan {_currentResult.Changes.Count} perubahan antara {_currentResult.FileAName} dan {_currentResult.FileBName}.";

                // Render change cards
                foreach (var c in _currentResult.Changes)
                {
                    var card = CreateDiffChangeCard(c);
                    _flowChanges.Controls.Add(card);
                }

                if (_currentResult.Changes.Count == 0)
                {
                    var lblNoChange = new Label
                    {
                        Text = "Tidak ditemukan perbedaan klausul, tanggal, atau nilai biaya yang signifikan di antara kedua dokumen.",
                        ForeColor = ModernTheme.TextSecondary,
                        AutoSize = true,
                        Padding = new Padding(10)
                    };
                    _flowChanges.Controls.Add(lblNoChange);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membandingkan dokumen: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _lblStatus.Text = "Error saat memproses perbandingan.";
            }
            finally
            {
                _btnCompare.Enabled = true;
            }
        }

        private Panel CreateDiffChangeCard(DiffChangeItem change)
        {
            var p = new Panel
            {
                Size = new Size(860, 85),
                BackColor = Color.FromArgb(248, 250, 252),
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(12)
            };
            p.Paint += (s, e) =>
            {
                using var pen = new Pen(ModernTheme.BorderColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };

            Color badgeColor;
            string categoryText;
            switch (change.Category)
            {
                case DiffCategory.Financial:
                    badgeColor = Color.FromArgb(16, 185, 129); // Emerald
                    categoryText = "BIAYA / NILAI";
                    break;
                case DiffCategory.Duration:
                    badgeColor = Color.FromArgb(245, 158, 11); // Amber
                    categoryText = "DURASI / WAKTU";
                    break;
                case DiffCategory.Clause:
                    badgeColor = Color.FromArgb(99, 102, 241); // Indigo
                    categoryText = "PASAL / KLAUSUL";
                    break;
                default:
                    badgeColor = Color.FromArgb(100, 116, 139);
                    categoryText = "UMUM";
                    break;
            }

            var lblBadge = new Label
            {
                Text = categoryText,
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = badgeColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(120, 20),
                Location = new Point(12, 10)
            };

            var lblTitle = new Label
            {
                Text = change.Title,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = ModernTheme.TextPrimary,
                Location = new Point(140, 10),
                Size = new Size(680, 22)
            };

            var lblDesc = new Label
            {
                Text = change.Description,
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextSecondary,
                Location = new Point(12, 36),
                Size = new Size(820, 20)
            };

            var lblDiffLine = new Label
            {
                Text = $"Lama: {change.OldValue}   ➜   Baru: {change.NewValue}",
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Regular),
                ForeColor = ModernTheme.Primary,
                Location = new Point(12, 58),
                Size = new Size(820, 20)
            };

            p.Controls.Add(lblDiffLine);
            p.Controls.Add(lblDesc);
            p.Controls.Add(lblTitle);
            p.Controls.Add(lblBadge);

            return p;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AsistenFile.App.UI;
using AsistenFile.Core.Features;
using AsistenFile.Core.Models;

namespace AsistenFile.App.UI.Views
{
    public class DeadlineTrackerView : UserControl
    {
        private readonly IDeadlineTrackerService _deadlineService;

        private DataGridView _grid = null!;
        private ComboBox _cmbFilter = null!;
        private Button _btnRefresh = null!;
        private Button _btnOpenFile = null!;
        private TextBox _txtSnippet = null!;
        private Label _lblSummary = null!;

        private List<DocumentDeadlineItem> _allDeadlines = new();
        private List<DocumentDeadlineItem> _displayedDeadlines = new();

        public event Action<string>? OnCriticalDeadlineDetected;

        public DeadlineTrackerView(IDeadlineTrackerService deadlineService)
        {
            _deadlineService = deadlineService;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.BackColor = ModernTheme.ContentBg;
            this.Dock = DockStyle.Fill;
            this.Font = ModernTheme.BodyFont;
            this.Padding = new Padding(20);

            // 1. Top Card (Header & Filter)
            var topCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(16),
                Margin = new Padding(0, 0, 0, 16)
            };
            ModernTheme.ApplyCardStyle(topCard);

            var lblHeader = new Label
            {
                Text = "📅 Pelacak Jatuh Tempo & Agenda Dokumen",
                Font = ModernTheme.HeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblSub = new Label
            {
                Text = "Memindai otomatis tanggal jatuh tempo kontrak sewa, batas bayar invoice, dan perpanjangan langganan langsung dari isi dokumen Anda.",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextSecondary,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 10)
            };

            var filterRow = new Panel { Dock = DockStyle.Bottom, Height = 32 };

            var lblFilter = new Label { Text = "Filter Status:", AutoSize = true, Location = new Point(0, 6) };
            _cmbFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(200, 26),
                Location = new Point(80, 3)
            };
            _cmbFilter.Items.AddRange(new object[]
            {
                "Semua Dokumen",
                "⚠️ Segera Jatuh Tempo (<= 30 Hari)",
                "🚨 Kritis (<= 7 Hari)",
                "🔴 Sudah Lewat Jatuh Tempo",
                "📜 Khusus Kontrak Sewa / PKS",
                "🧾 Khusus Invoice / Tagihan"
            });
            _cmbFilter.SelectedIndex = 0;
            _cmbFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

            _btnRefresh = new Button
            {
                Text = "🔄 Pindai Ulang Dokumen",
                Size = new Size(180, 30),
                Location = new Point(290, 1)
            };
            ModernTheme.StyleButton(_btnRefresh, isPrimary: true);
            _btnRefresh.Click += async (s, e) => await ScanDeadlinesAsync();

            _lblSummary = new Label
            {
                Text = "Memuat data jatuh tempo...",
                AutoSize = true,
                Location = new Point(480, 7),
                ForeColor = ModernTheme.TextSecondary
            };

            filterRow.Controls.Add(_lblSummary);
            filterRow.Controls.Add(_btnRefresh);
            filterRow.Controls.Add(_cmbFilter);
            filterRow.Controls.Add(lblFilter);

            topCard.Controls.Add(filterRow);
            topCard.Controls.Add(lblSub);
            topCard.Controls.Add(lblHeader);

            // 2. Bottom Preview & Actions
            var bottomCard = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 140,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(14),
                Margin = new Padding(0, 14, 0, 0)
            };
            ModernTheme.ApplyCardStyle(bottomCard);

            var bottomHeader = new Panel { Dock = DockStyle.Top, Height = 28 };
            var lblPreviewTitle = new Label
            {
                Text = "📄 Klausul / Kalimat Terkait Jatuh Tempo:",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Left,
                AutoSize = true
            };

            _btnOpenFile = new Button
            {
                Text = "📖 Buka File Dokumen",
                Size = new Size(140, 26),
                Dock = DockStyle.Right
            };
            ModernTheme.StyleButton(_btnOpenFile, isPrimary: true);
            _btnOpenFile.Click += (s, e) => OpenSelectedDocument();

            var btnOpenFolder = new Button
            {
                Text = "📂 Buka Folder",
                Size = new Size(110, 26),
                Dock = DockStyle.Right
            };
            ModernTheme.StyleButton(btnOpenFolder, isPrimary: false);
            btnOpenFolder.Click += (s, e) => OpenSelectedFolder();

            bottomHeader.Controls.Add(lblPreviewTitle);
            bottomHeader.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
            bottomHeader.Controls.Add(btnOpenFolder);
            bottomHeader.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
            bottomHeader.Controls.Add(_btnOpenFile);

            _txtSnippet = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = ModernTheme.TextPrimary,
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
            snippetContainer.Controls.Add(_txtSnippet);

            bottomCard.Controls.Add(snippetContainer);
            bottomCard.Controls.Add(bottomHeader);

            // 3. Middle DataGridView
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = ModernTheme.ContentBg,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            _grid.Columns.Add("Status", "Status Urgensi");
            _grid.Columns.Add("SisaWaktu", "Sisa Waktu");
            _grid.Columns.Add("Tanggal", "Jatuh Tempo");
            _grid.Columns.Add("Kategori", "Kategori");
            _grid.Columns.Add("NamaFile", "Nama File");
            _grid.Columns.Add("Bagian", "Halaman/Bagian");

            _grid.Columns["Status"].Width = 140;
            _grid.Columns["SisaWaktu"].Width = 130;
            _grid.Columns["Tanggal"].Width = 120;
            _grid.Columns["Kategori"].Width = 100;
            _grid.Columns["NamaFile"].Width = 260;

            _grid.SelectionChanged += (s, e) => UpdateSnippetPreview();
            _grid.CellDoubleClick += (s, e) => OpenSelectedDocument();

            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 10)
            };
            gridContainer.Controls.Add(_grid);

            this.Controls.Add(gridContainer);
            this.Controls.Add(bottomCard);
            this.Controls.Add(topCard);
        }

        public async Task ScanDeadlinesAsync()
        {
            _btnRefresh.Enabled = false;
            _lblSummary.Text = "⏳ Memindai isi dokumen...";

            try
            {
                _allDeadlines = await _deadlineService.ScanDeadlinesAsync();
                ApplyFilter();

                // Check for critical deadlines and trigger notification
                var critical = _deadlineService.GetCriticalUpcomingDeadlines(_allDeadlines, 30);
                if (critical.Count > 0)
                {
                    var first = critical[0];
                    string msg = first.DaysRemaining == 0
                        ? $"Hari ini jatuh tempo: {first.FileName}!"
                        : $"{first.FileName} jatuh tempo dalam {first.DaysRemaining} hari lagi ({first.DueDate:dd MMMM yyyy})!";
                    OnCriticalDeadlineDetected?.Invoke(msg);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal memindai jatuh tempo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnRefresh.Enabled = true;
            }
        }

        private void ApplyFilter()
        {
            string filterKey = _cmbFilter.SelectedIndex switch
            {
                1 => "segera",
                2 => "kritis",
                3 => "lewat",
                4 => "kontrak",
                5 => "invoice",
                _ => "semua"
            };

            _displayedDeadlines = _deadlineService.FilterDeadlines(_allDeadlines, filterKey);
            _grid.Rows.Clear();

            int overdueCount = _allDeadlines.Count(x => x.DaysRemaining < 0);
            int upcomingCount = _allDeadlines.Count(x => x.DaysRemaining >= 0 && x.DaysRemaining <= 30);
            _lblSummary.Text = $"Total: {_allDeadlines.Count} dokumen terdeteksi | ⚠️ {upcomingCount} segera | 🔴 {overdueCount} lewat";

            foreach (var item in _displayedDeadlines)
            {
                string statusText;
                string sisaText;

                if (item.DaysRemaining < 0)
                {
                    statusText = "🔴 LEWAT TEMPO";
                    sisaText = $"Terlewat {Math.Abs(item.DaysRemaining)} hari";
                }
                else if (item.DaysRemaining <= 7)
                {
                    statusText = "🚨 KRITIS (<= 7 Hari)";
                    sisaText = item.DaysRemaining == 0 ? "HARI INI!" : $"{item.DaysRemaining} hari lagi";
                }
                else if (item.DaysRemaining <= 30)
                {
                    statusText = "⚠️ SEGERA (<= 30 Hari)";
                    sisaText = $"{item.DaysRemaining} hari lagi";
                }
                else
                {
                    statusText = "🟢 Normal / Aman";
                    sisaText = $"{item.DaysRemaining} hari lagi";
                }

                int rowIndex = _grid.Rows.Add(
                    statusText,
                    sisaText,
                    item.DueDate.ToString("dd MMMM yyyy"),
                    item.DocumentCategory,
                    item.FileName,
                    item.SectionTitle
                );

                // Highlight urgent rows
                if (item.DaysRemaining < 0)
                {
                    _grid.Rows[rowIndex].Cells["Status"].Style.ForeColor = Color.Red;
                }
                else if (item.DaysRemaining <= 7)
                {
                    _grid.Rows[rowIndex].Cells["Status"].Style.ForeColor = Color.FromArgb(234, 88, 12);
                }
                else if (item.DaysRemaining <= 30)
                {
                    _grid.Rows[rowIndex].Cells["Status"].Style.ForeColor = Color.FromArgb(217, 119, 6);
                }
            }

            if (_displayedDeadlines.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                UpdateSnippetPreview();
            }
            else
            {
                _txtSnippet.Text = "Tidak ada dokumen yang sesuai dengan filter ini.";
            }
        }

        private void UpdateSnippetPreview()
        {
            if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Index < _displayedDeadlines.Count)
            {
                var item = _displayedDeadlines[_grid.SelectedRows[0].Index];
                _txtSnippet.Text = $"Dokumen: {item.FileName}\n" +
                                   $"Jatuh Tempo: {item.DueDate:dddd, dd MMMM yyyy} (Sisa: {item.DaysRemaining} hari)\n" +
                                   $"Kategori: {item.DocumentCategory} | Lokasi: {item.SectionTitle}\n\n" +
                                   $"Kutipan: \"{item.Snippet}\"";
            }
        }

        private void OpenSelectedDocument()
        {
            if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Index >= _displayedDeadlines.Count) return;
            var item = _displayedDeadlines[_grid.SelectedRows[0].Index];

            try
            {
                if (File.Exists(item.FilePath))
                {
                    Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("File tidak ditemukan di disk. Kemungkinan file telah dipindahkan atau dihapus.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membuka file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenSelectedFolder()
        {
            if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Index >= _displayedDeadlines.Count) return;
            var item = _displayedDeadlines[_grid.SelectedRows[0].Index];

            try
            {
                if (File.Exists(item.FilePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
                }
                else
                {
                    MessageBox.Show("File tidak ditemukan di disk. Kemungkinan file telah dipindahkan atau dihapus.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membuka folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

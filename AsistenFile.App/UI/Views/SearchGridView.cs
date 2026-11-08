using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AsistenFile.App.UI;
using AsistenFile.Core.Models;
using AsistenFile.Core.Search;
using AsistenFile.Core.Storage;

namespace AsistenFile.App.UI.Views
{
    public class SearchGridView : UserControl
    {
        private readonly IHybridSearchEngine _searchEngine;
        private readonly IQueryParser _queryParser;
        private readonly IDatabaseService _databaseService;

        private TextBox _txtSearch = null!;
        private ComboBox _cmbFileType = null!;
        private CheckBox _chkDateFilter = null!;
        private DateTimePicker _dtpFrom = null!;
        private DateTimePicker _dtpTo = null!;
        private Button _btnSearch = null!;
        private Button _btnExportCsv = null!;
        private DataGridView _grid = null!;
        private TextBox _txtPreview = null!;
        private Label _lblCount = null!;

        private List<SearchResultItem> _currentResults = new();

        public SearchGridView(IHybridSearchEngine searchEngine, IQueryParser queryParser, IDatabaseService databaseService)
        {
            _searchEngine = searchEngine;
            _queryParser = queryParser;
            _databaseService = databaseService;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.BackColor = ModernTheme.ContentBg;
            this.Dock = DockStyle.Fill;
            this.Font = ModernTheme.BodyFont;

            // 1. Top Filter Panel
            var filterPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(16, 12, 16, 12)
            };
            ModernTheme.ApplyCardStyle(filterPanel);

            // Row 1: Search Box & Search Button
            var row1 = new Panel { Dock = DockStyle.Top, Height = 32 };
            _txtSearch = new TextBox
            {
                Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
                Dock = DockStyle.Fill
            };
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _ = ExecuteSearchAsync();
                }
            };

            _btnSearch = new Button
            {
                Text = "🔍 Cari",
                Size = new Size(100, 32),
                Dock = DockStyle.Right
            };
            ModernTheme.StyleButton(_btnSearch, isPrimary: true);
            _btnSearch.Click += (s, e) => _ = ExecuteSearchAsync();

            _btnExportCsv = new Button
            {
                Text = "📊 Ekspor CSV",
                Size = new Size(110, 32),
                Dock = DockStyle.Right
            };
            ModernTheme.StyleButton(_btnExportCsv, isPrimary: false);
            _btnExportCsv.Click += (s, e) => ExportCsv();

            row1.Controls.Add(_txtSearch);
            row1.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
            row1.Controls.Add(_btnSearch);
            row1.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
            row1.Controls.Add(_btnExportCsv);

            // Row 2: Filters (File Type, Date Checkbox, Date Pickers)
            var row2 = new Panel { Dock = DockStyle.Bottom, Height = 28 };

            var lblType = new Label { Text = "Tipe File:", AutoSize = true, Location = new Point(0, 5) };
            _cmbFileType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(130, 24),
                Location = new Point(60, 2)
            };
            _cmbFileType.Items.AddRange(new object[] { "Semua Tipe", "PDF (.pdf)", "Word (.docx)", "Excel (.xlsx, .csv)", "Email (.eml)", "Teks (.txt, .md)" });
            _cmbFileType.SelectedIndex = 0;
            _cmbFileType.SelectedIndexChanged += (s, e) => _ = ExecuteSearchAsync();

            _chkDateFilter = new CheckBox
            {
                Text = "Filter Rentang Tanggal:",
                AutoSize = true,
                Location = new Point(210, 5)
            };
            _chkDateFilter.CheckedChanged += (s, e) =>
            {
                _dtpFrom.Enabled = _chkDateFilter.Checked;
                _dtpTo.Enabled = _chkDateFilter.Checked;
                _ = ExecuteSearchAsync();
            };

            _dtpFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Size = new Size(105, 24),
                Location = new Point(370, 2),
                Value = DateTime.Now.AddYears(-1),
                Enabled = false
            };

            var lblTo = new Label { Text = "s/d", AutoSize = true, Location = new Point(485, 5) };

            _dtpTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Size = new Size(105, 24),
                Location = new Point(515, 2),
                Value = DateTime.Now,
                Enabled = false
            };

            _lblCount = new Label
            {
                Text = "Hasil: 0 dokumen",
                AutoSize = true,
                Location = new Point(635, 5),
                ForeColor = ModernTheme.TextSecondary
            };

            row2.Controls.Add(lblType);
            row2.Controls.Add(_cmbFileType);
            row2.Controls.Add(_chkDateFilter);
            row2.Controls.Add(_dtpFrom);
            row2.Controls.Add(lblTo);
            row2.Controls.Add(_dtpTo);
            row2.Controls.Add(_lblCount);

            filterPanel.Controls.Add(row1);
            filterPanel.Controls.Add(row2);

            // 2. Bottom Preview Panel
            var previewPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 160,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(12)
            };
            ModernTheme.ApplyCardStyle(previewPanel);

            var previewHeader = new Panel { Dock = DockStyle.Top, Height = 26 };
            var lblPreviewTitle = new Label
            {
                Text = "📄 Pratinjau Cuplikan Dokumen Terpilih:",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Left,
                AutoSize = true
            };

            var btnOpenSelected = new Button
            {
                Text = "📖 Buka File Terpilih",
                Size = new Size(130, 24),
                Dock = DockStyle.Right
            };
            ModernTheme.StyleButton(btnOpenSelected, isPrimary: true);
            btnOpenSelected.Click += (s, e) => OpenSelectedFile();

            previewHeader.Controls.Add(lblPreviewTitle);
            previewHeader.Controls.Add(btnOpenSelected);

            _txtPreview = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 250, 252),
                Font = ModernTheme.BodyFont,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill
            };

            var previewContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(248, 250, 252)
            };
            previewContainer.Controls.Add(_txtPreview);

            previewPanel.Controls.Add(previewContainer);
            previewPanel.Controls.Add(previewHeader);

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

            _grid.Columns.Add("Tipe", "Tipe");
            _grid.Columns.Add("NamaFile", "Nama File");
            _grid.Columns.Add("Bagian", "Halaman/Bagian");
            _grid.Columns.Add("Tanggal", "Dimodifikasi");
            _grid.Columns.Add("Skor", "Relevansi");
            _grid.Columns.Add("Cuplikan", "Cuplikan Singkat");

            _grid.Columns["Tipe"].Width = 60;
            _grid.Columns["NamaFile"].Width = 200;
            _grid.Columns["Bagian"].Width = 110;
            _grid.Columns["Tanggal"].Width = 120;
            _grid.Columns["Skor"].Width = 80;

            _grid.SelectionChanged += (s, e) =>
            {
                if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Index < _currentResults.Count)
                {
                    var item = _currentResults[_grid.SelectedRows[0].Index];
                    _txtPreview.Text = $"File: {item.FilePath}\n" +
                                       $"Bagian: {item.SectionTitle} | Tanggal: {item.FileLastModified:yyyy-MM-dd HH:mm}\n\n" +
                                       $"{item.Snippet}";
                }
            };

            _grid.CellDoubleClick += (s, e) => OpenSelectedFile();

            this.Controls.Add(_grid);
            this.Controls.Add(previewPanel);
            this.Controls.Add(filterPanel);
        }

        private async Task ExecuteSearchAsync()
        {
            var text = _txtSearch.Text.Trim();
            var parsed = _queryParser.Parse(text);

            // Override extension filter if combo selected
            switch (_cmbFileType.SelectedIndex)
            {
                case 1: parsed.FileTypeFilter = ".pdf"; break;
                case 2: parsed.FileTypeFilter = ".docx"; break;
                case 3: parsed.FileTypeFilter = ".xlsx"; break;
                case 4: parsed.FileTypeFilter = ".eml"; break;
                case 5: parsed.FileTypeFilter = ".txt"; break;
            }

            // Override date filter if checked
            if (_chkDateFilter.Checked)
            {
                parsed.DateFrom = _dtpFrom.Value.Date;
                parsed.DateTo = _dtpTo.Value.Date.AddDays(1).AddSeconds(-1);
            }

            parsed.Limit = 50;

            _btnSearch.Enabled = false;
            try
            {
                _currentResults = await _searchEngine.SearchAsync(parsed);
                _grid.Rows.Clear();

                foreach (var item in _currentResults)
                {
                    int pct = (int)Math.Clamp(item.RelevanceScore * 100, 10, 100);
                    _grid.Rows.Add(
                        item.Extension.TrimStart('.').ToUpperInvariant(),
                        item.FileName,
                        item.SectionTitle,
                        item.FileLastModified.ToString("dd/MM/yyyy HH:mm"),
                        $"{pct}%",
                        item.Snippet.Replace("\r", " ").Replace("\n", " ")
                    );
                }

                _lblCount.Text = $"Hasil: {_currentResults.Count} dokumen";
                if (_currentResults.Count > 0)
                {
                    _grid.Rows[0].Selected = true;
                }
                else
                {
                    _txtPreview.Text = "Tidak ada hasil yang sesuai dengan kriteria filter.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error pencarian: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnSearch.Enabled = true;
            }
        }

        private void OpenSelectedFile()
        {
            if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Index >= _currentResults.Count) return;
            var item = _currentResults[_grid.SelectedRows[0].Index];

            try
            {
                if (File.Exists(item.FilePath))
                {
                    Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("File tidak ditemukan di disk.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membuka file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportCsv()
        {
            if (_currentResults.Count == 0)
            {
                MessageBox.Show("Tidak ada data untuk diekspor.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = $"HasilPencarian_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("NamaFile,Tipe,Bagian,TanggalModifikasi,SkorRelevansi,FilePath,Cuplikan");

                    foreach (var r in _currentResults)
                    {
                        sb.AppendLine($"\"{EscapeCsv(r.FileName)}\",\"{r.Extension}\",\"{EscapeCsv(r.SectionTitle)}\",\"{r.FileLastModified:yyyy-MM-dd HH:mm}\",\"{r.RelevanceScore}\",\"{EscapeCsv(r.FilePath)}\",\"{EscapeCsv(r.Snippet)}\"");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                    MessageBox.Show("Hasil pencarian berhasil diekspor ke CSV!", "Sukses", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Tidak dapat menyimpan file:\n{ex.Message}\n\nPastikan file tidak sedang dibuka di aplikasi lain.", "File Terkunci", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal mengekspor file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string EscapeCsv(string s) => s?.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") ?? "";
    }
}

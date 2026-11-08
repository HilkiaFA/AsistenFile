using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AsistenFile.App.UI;
using AsistenFile.Core.Features;
using AsistenFile.Core.Models;

namespace AsistenFile.App.UI.Views
{
    public class InvoiceRekapView : UserControl
    {
        private readonly IInvoiceExtractorService _invoiceService;

        private DataGridView _grid = null!;
        private Button _btnExtractAll = null!;
        private Button _btnExportExcel = null!;
        private TextBox _txtSearch = null!;
        private Label _lblTotalAmount = null!;
        private Label _lblInvoiceCount = null!;
        private Label _lblPaidAmount = null!;
        private Label _lblUnpaidAmount = null!;

        private List<ExtractedInvoiceItem> _allInvoices = new();
        private List<ExtractedInvoiceItem> _displayedInvoices = new();

        public InvoiceRekapView(IInvoiceExtractorService invoiceService)
        {
            _invoiceService = invoiceService;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.BackColor = ModernTheme.ContentBg;
            this.Dock = DockStyle.Fill;
            this.Font = ModernTheme.BodyFont;
            this.Padding = new Padding(20);

            // 1. Top Card: Header & Action Buttons
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
                Text = "📊 Rekap Invoice & Ekstraksi Tabel Otomatis",
                Font = ModernTheme.HeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblSub = new Label
            {
                Text = "Ekstrak otomatis tumpukan invoice, tagihan, dan kwitansi dari PDF, Excel, dan Email ke tabel terstruktur siap ekspor ke Excel/CSV.",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextSecondary,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 10)
            };

            // Stat Cards Panel
            var statsPanel = new Panel { Dock = DockStyle.Top, Height = 48 };

            var stat1 = CreateStatBox("Total Nilai Tagihan", "Rp 0", Color.FromArgb(37, 99, 235), 0, out _lblTotalAmount);
            var stat2 = CreateStatBox("Jumlah Invoice", "0 File", Color.FromArgb(71, 85, 105), 210, out _lblInvoiceCount);
            var stat3 = CreateStatBox("Sudah Lunas", "Rp 0", Color.FromArgb(16, 185, 129), 390, out _lblPaidAmount);
            var stat4 = CreateStatBox("Belum Dibayar", "Rp 0", Color.FromArgb(239, 68, 68), 580, out _lblUnpaidAmount);

            statsPanel.Controls.Add(stat4);
            statsPanel.Controls.Add(stat3);
            statsPanel.Controls.Add(stat2);
            statsPanel.Controls.Add(stat1);

            // Action Row Panel
            var actionRow = new Panel { Dock = DockStyle.Bottom, Height = 34 };

            _btnExtractAll = new Button
            {
                Text = "⚡ Rekap Semua Invoice ke Tabel",
                Size = new Size(230, 32),
                Location = new Point(0, 1)
            };
            ModernTheme.StyleButton(_btnExtractAll, isPrimary: true);
            _btnExtractAll.Click += async (s, e) => await ExtractInvoicesAsync();

            _btnExportExcel = new Button
            {
                Text = "📊 Ekspor ke Excel (.csv)",
                Size = new Size(180, 32),
                Location = new Point(238, 1)
            };
            ModernTheme.StyleButton(_btnExportExcel, isPrimary: false);
            _btnExportExcel.Click += async (s, e) => await ExportToExcelDialogAsync();

            var lblSearch = new Label { Text = "Cari Vendor/No:", AutoSize = true, Location = new Point(435, 7) };
            _txtSearch = new TextBox
            {
                Location = new Point(540, 4),
                Size = new Size(180, 24)
            };
            _txtSearch.TextChanged += (s, e) => ApplySearchFilter();

            actionRow.Controls.Add(_txtSearch);
            actionRow.Controls.Add(lblSearch);
            actionRow.Controls.Add(_btnExportExcel);
            actionRow.Controls.Add(_btnExtractAll);

            topCard.Controls.Add(actionRow);
            topCard.Controls.Add(statsPanel);
            topCard.Controls.Add(lblSub);
            topCard.Controls.Add(lblHeader);

            // 2. Middle DataGridView
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

            _grid.Columns.Add("NoInvoice", "No. Invoice");
            _grid.Columns.Add("Tanggal", "Tanggal");
            _grid.Columns.Add("Vendor", "Nama Vendor / Klien");
            _grid.Columns.Add("Uraian", "Uraian Transaksi");
            _grid.Columns.Add("Nilai", "Nilai Tagihan (IDR)");
            _grid.Columns.Add("Status", "Status");
            _grid.Columns.Add("FileSumber", "File Dokumen");

            _grid.Columns["NoInvoice"].Width = 140;
            _grid.Columns["Tanggal"].Width = 100;
            _grid.Columns["Vendor"].Width = 180;
            _grid.Columns["Uraian"].Width = 200;
            _grid.Columns["Nilai"].Width = 140;
            _grid.Columns["Nilai"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["Status"].Width = 100;

            _grid.CellDoubleClick += (s, e) => OpenSelectedFile();

            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0)
            };
            gridContainer.Controls.Add(_grid);

            this.Controls.Add(gridContainer);
            this.Controls.Add(topCard);
        }

        private Panel CreateStatBox(string title, string initialValue, Color valueColor, int left, out Label lblValueOut)
        {
            var p = new Panel
            {
                Location = new Point(left, 0),
                Size = new Size(175, 46),
                BackColor = Color.FromArgb(248, 250, 252)
            };
            p.Paint += (s, e) =>
            {
                using var pen = new Pen(ModernTheme.BorderColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };

            var lblT = new Label
            {
                Text = title,
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextMuted,
                Location = new Point(6, 4),
                AutoSize = true
            };

            var lblV = new Label
            {
                Text = initialValue,
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                ForeColor = valueColor,
                Location = new Point(6, 20),
                AutoSize = true
            };

            p.Controls.Add(lblV);
            p.Controls.Add(lblT);
            lblValueOut = lblV;
            return p;
        }

        public async Task ExtractInvoicesAsync()
        {
            _btnExtractAll.Enabled = false;
            try
            {
                _allInvoices = await _invoiceService.ExtractAllInvoicesAsync();
                ApplySearchFilter();

                var stats = _invoiceService.CalculateStats(_allInvoices);
                _lblTotalAmount.Text = $"Rp {stats.TotalAmount:N0}";
                _lblInvoiceCount.Text = $"{stats.TotalInvoices} Transaksi";
                _lblPaidAmount.Text = $"Rp {stats.PaidAmount:N0}";
                _lblUnpaidAmount.Text = $"Rp {stats.UnpaidAmount:N0}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal merekap invoice: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnExtractAll.Enabled = true;
            }
        }

        private void ApplySearchFilter()
        {
            var q = _txtSearch.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q))
            {
                _displayedInvoices = new List<ExtractedInvoiceItem>(_allInvoices);
            }
            else
            {
                _displayedInvoices = _allInvoices.Where(x =>
                    x.VendorOrClient.ToLowerInvariant().Contains(q) ||
                    x.InvoiceNumber.ToLowerInvariant().Contains(q) ||
                    x.Description.ToLowerInvariant().Contains(q) ||
                    x.FileName.ToLowerInvariant().Contains(q)
                ).ToList();
            }

            _grid.Rows.Clear();
            foreach (var inv in _displayedInvoices)
            {
                int rIdx = _grid.Rows.Add(
                    inv.InvoiceNumber,
                    inv.InvoiceDate?.ToString("dd/MM/yyyy") ?? "-",
                    inv.VendorOrClient,
                    inv.Description,
                    $"Rp {inv.Amount:N0}",
                    inv.Status,
                    inv.FileName
                );

                if (inv.Status == "Lunas")
                {
                    _grid.Rows[rIdx].Cells["Status"].Style.ForeColor = ModernTheme.AccentSuccess;
                }
                else
                {
                    _grid.Rows[rIdx].Cells["Status"].Style.ForeColor = ModernTheme.AccentDanger;
                }
            }
        }

        private async Task ExportToExcelDialogAsync()
        {
            if (_displayedInvoices.Count == 0)
            {
                MessageBox.Show("Belum ada data invoice yang diekstrak untuk diekspor.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel CSV File (*.csv)|*.csv",
                FileName = $"Rekap_Invoice_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await _invoiceService.ExportToCsvAsync(_displayedInvoices, sfd.FileName);
                    var res = MessageBox.Show($"File berhasil diekspor ke:\n{sfd.FileName}\n\nBuka file sekarang?", "Sukses", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (res == DialogResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                    }
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Tidak dapat menyimpan file:\n{ex.Message}\n\nPastikan file tidak sedang dibuka di Excel atau program lain.", "File Terkunci", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal mengekspor file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OpenSelectedFile()
        {
            if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Index >= _displayedInvoices.Count) return;
            var item = _displayedInvoices[_grid.SelectedRows[0].Index];

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
    }
}

using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AsistenFile.App.UI;
using AsistenFile.Core.Indexing;
using AsistenFile.Core.Storage;

namespace AsistenFile.App.UI.Views
{
    public class FolderManagerView : UserControl
    {
        private readonly IDatabaseService _databaseService;
        private readonly IIndexingService _indexingService;
        private readonly IFileWatcherService _fileWatcherService;

        private ListView _listViewFolders = null!;
        private Button _btnAddFolder = null!;
        private Button _btnRemoveFolder = null!;
        private Button _btnReindexAll = null!;
        private ProgressBar _progressBar = null!;
        private Label _lblProgressStatus = null!;
        private Label _lblStats = null!;

        public event Action? OnIndexingCompleted;

        public FolderManagerView(IDatabaseService databaseService, IIndexingService indexingService, IFileWatcherService fileWatcherService)
        {
            _databaseService = databaseService;
            _indexingService = indexingService;
            _fileWatcherService = fileWatcherService;
            InitializeComponent();
            SetupEvents();
        }

        private void InitializeComponent()
        {
            this.BackColor = ModernTheme.ContentBg;
            this.Dock = DockStyle.Fill;
            this.Font = ModernTheme.BodyFont;
            this.Padding = new Padding(20);

            // 1. Top Card: Info & Action Buttons
            var topCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 130,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(16),
                Margin = new Padding(0, 0, 0, 16)
            };
            ModernTheme.ApplyCardStyle(topCard);

            var lblHeader = new Label
            {
                Text = "📁 Kelola Folder Sumber Dokumen",
                Font = ModernTheme.HeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblSub = new Label
            {
                Text = "Pilih folder di komputer Anda yang ingin diindeks (misalnya Documents, Unduhan, Folder Proyek).\n" +
                       "Aplikasi hanya membaca folder yang Anda tentukan secara eksplisit. Folder lain tidak disentuh.",
                Font = ModernTheme.BodyFont,
                ForeColor = ModernTheme.TextSecondary,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 10)
            };

            var actionRow = new Panel { Dock = DockStyle.Bottom, Height = 34 };

            _btnAddFolder = new Button
            {
                Text = "➕ Tambah Folder",
                Size = new Size(140, 34),
                Location = new Point(0, 0)
            };
            ModernTheme.StyleButton(_btnAddFolder, isPrimary: true);
            _btnAddFolder.Click += (s, e) => AddFolderDialog();

            _btnRemoveFolder = new Button
            {
                Text = "🗑️ Hapus Folder",
                Size = new Size(130, 34),
                Location = new Point(148, 0)
            };
            ModernTheme.StyleButton(_btnRemoveFolder, isPrimary: false);
            _btnRemoveFolder.Click += (s, e) => RemoveSelectedFolder();

            _btnReindexAll = new Button
            {
                Text = "🔄 Scan & Indeks Ulang Semua",
                Size = new Size(210, 34),
                Location = new Point(286, 0)
            };
            ModernTheme.StyleButton(_btnReindexAll, isPrimary: false);
            _btnReindexAll.Click += (s, e) => _ = ReindexAllAsync();

            actionRow.Controls.Add(_btnAddFolder);
            actionRow.Controls.Add(_btnRemoveFolder);
            actionRow.Controls.Add(_btnReindexAll);

            topCard.Controls.Add(actionRow);
            topCard.Controls.Add(lblSub);
            topCard.Controls.Add(lblHeader);

            // 2. Bottom Card: Background Progress Bar & Index Stats
            var bottomCard = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 110,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(16),
                Margin = new Padding(0, 16, 0, 0)
            };
            ModernTheme.ApplyCardStyle(bottomCard);

            _lblStats = new Label
            {
                Text = "Statistik: Memuat...",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Top,
                AutoSize = true
            };

            _lblProgressStatus = new Label
            {
                Text = "Status: Siap memindai.",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextSecondary,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 4)
            };

            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Style = ProgressBarStyle.Continuous,
                Value = 0
            };

            bottomCard.Controls.Add(_progressBar);
            bottomCard.Controls.Add(_lblProgressStatus);
            bottomCard.Controls.Add(_lblStats);

            // 3. Middle ListView: Folders List
            _listViewFolders = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BorderStyle = BorderStyle.None,
                BackColor = ModernTheme.CardBg,
                Font = ModernTheme.BodyFont
            };

            _listViewFolders.Columns.Add("Path Folder", 450);
            _listViewFolders.Columns.Add("Status Pemantauan", 140);
            _listViewFolders.Columns.Add("Jumlah File", 100);
            _listViewFolders.Columns.Add("Terakhir Dipindai", 160);

            var listContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 14, 0, 14)
            };
            listContainer.Controls.Add(_listViewFolders);

            this.Controls.Add(listContainer);
            this.Controls.Add(bottomCard);
            this.Controls.Add(topCard);
        }

        private void SetupEvents()
        {
            _indexingService.ProgressChanged += (s, e) =>
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() => UpdateProgressUI(e)));
                }
                else
                {
                    UpdateProgressUI(e);
                }
            };

            _indexingService.StatusChanged += (s, msg) =>
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() => _lblProgressStatus.Text = $"Status: {msg}"));
                }
                else
                {
                    _lblProgressStatus.Text = $"Status: {msg}";
                }
            };
        }

        private void UpdateProgressUI(IndexingProgressEventArgs e)
        {
            _progressBar.Value = Math.Clamp(e.PercentComplete, 0, 100);
            _lblProgressStatus.Text = $"Memproses: {e.CurrentFileName} ({e.ProcessedFiles}/{e.TotalFiles} file | {e.IndexedFiles} baru, {e.SkippedFiles} terlewati, {e.ErrorFiles} error)";

            if (e.IsCompleted)
            {
                _progressBar.Value = 100;
                _ = RefreshStatsAsync();
                _ = RefreshFolderListAsync();
                OnIndexingCompleted?.Invoke();
            }
        }

        public async Task LoadInitialDataAsync()
        {
            await RefreshFolderListAsync();
            await RefreshStatsAsync();
        }

        private async Task RefreshFolderListAsync()
        {
            _listViewFolders.Items.Clear();
            var folders = await _databaseService.GetFoldersAsync();

            foreach (var f in folders)
            {
                var item = new ListViewItem(f.FolderPath) { Tag = f.Id };
                item.SubItems.Add(f.IsActive ? "🟢 Aktif (Live Watch)" : "⚪ Nonaktif");
                item.SubItems.Add(f.FileCount.ToString());
                item.SubItems.Add(f.LastScannedAt.HasValue ? f.LastScannedAt.Value.ToString("dd/MM/yyyy HH:mm") : "Belum dipindai");
                _listViewFolders.Items.Add(item);
            }
        }

        private async Task RefreshStatsAsync()
        {
            var totalFiles = await _databaseService.GetTotalIndexedFilesCountAsync();
            var totalChunks = await _databaseService.GetTotalIndexedChunksCountAsync();
            _lblStats.Text = $"📊 Total Terindeks: {totalFiles} File Dokumen ({totalChunks} Potongan Teks Vektor)";
        }

        private void AddFolderDialog()
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "Pilih folder dokumen untuk diindeks oleh Asisten File:",
                UseDescriptionForTitle = true
            };

            if (fbd.ShowDialog() == DialogResult.OK)
            {
                var path = fbd.SelectedPath;
                if (!Directory.Exists(path)) return;

                _ = AddFolderAndScanAsync(path);
            }
        }

        private async Task AddFolderAndScanAsync(string folderPath)
        {
            try
            {
                _btnAddFolder.Enabled = false;
                _lblProgressStatus.Text = $"Menambahkan folder '{folderPath}' dan memulai indeksasi...";

                await _indexingService.ScanAndIndexFolderAsync(folderPath);
                await _fileWatcherService.RefreshWatchersAsync();
                await RefreshFolderListAsync();
                await RefreshStatsAsync();

                MessageBox.Show($"Folder berhasil ditambahkan dan diindeks:\n{folderPath}", "Sukses", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal mengindeks folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnAddFolder.Enabled = true;
            }
        }

        private void RemoveSelectedFolder()
        {
            if (_listViewFolders.SelectedItems.Count == 0)
            {
                MessageBox.Show("Pilih folder yang ingin dihapus dari indeks.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = _listViewFolders.SelectedItems[0];
            long folderId = (long)selectedItem.Tag!;
            string path = selectedItem.Text;

            var res = MessageBox.Show($"Hapus folder ini dari daftar indeks?\n\n{path}\n\n(Catatan: File fisik di komputer Anda TIDAK akan dihapus, hanya indeks lokalnya yang dibersihkan).", "Konfirmasi", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                _ = RemoveFolderAsync(folderId);
            }
        }

        private async Task RemoveFolderAsync(long folderId)
        {
            await _databaseService.RemoveFolderAsync(folderId);
            await _fileWatcherService.RefreshWatchersAsync();
            await RefreshFolderListAsync();
            await RefreshStatsAsync();
            MessageBox.Show("Folder berhasil dihapus dari indeks.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task ReindexAllAsync()
        {
            if (_indexingService.IsIndexing)
            {
                MessageBox.Show("Proses pengindeksan sedang berjalan di latar belakang.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _btnReindexAll.Enabled = false;
            try
            {
                await _indexingService.ScanAndIndexAllAsync();
                await RefreshFolderListAsync();
                await RefreshStatsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error indeksasi: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnReindexAll.Enabled = true;
            }
        }
    }
}

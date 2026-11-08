using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AsistenFile.App.UI.Views;
using AsistenFile.Core.AI;
using AsistenFile.Core.Chunking;
using AsistenFile.Core.Config;
using AsistenFile.Core.Embeddings;
using AsistenFile.Core.Extractors;
using AsistenFile.Core.Features;
using AsistenFile.Core.Indexing;
using AsistenFile.Core.Models;
using AsistenFile.Core.Ocr;
using AsistenFile.Core.Search;
using AsistenFile.Core.Storage;

namespace AsistenFile.App.UI
{
    public class MainForm : Form
    {
        // Core Services
        private readonly ISettingsService _settingsService;
        private readonly IDatabaseService _databaseService;
        private readonly IOcrService _ocrService;
        private readonly IExtractorFactory _extractorFactory;
        private readonly ITextChunker _chunker;
        private readonly IEmbeddingService _embeddingService;
        private readonly IIndexingService _indexingService;
        private readonly IFileWatcherService _fileWatcherService;
        private readonly IQueryParser _queryParser;
        private readonly IHybridSearchEngine _searchEngine;
        private readonly ILanguageModelService _ollamaService;
        private readonly ILocalSynthesizer _localSynthesizer;
        private readonly IRagOrchestrator _ragOrchestrator;
        private readonly IOllamaInstallerService _installerService;
        private readonly IDeadlineTrackerService _deadlineService;
        private readonly IInvoiceExtractorService _invoiceService;
        private readonly IDocumentDiffService _diffService;

        // UI Views
        private ChatView _chatView = null!;
        private SearchGridView _searchGridView = null!;
        private DeadlineTrackerView _deadlineTrackerView = null!;
        private InvoiceRekapView _invoiceRekapView = null!;
        private DocumentDiffView _documentDiffView = null!;
        private FolderManagerView _folderManagerView = null!;
        private SettingsView _settingsView = null!;

        // Shell Controls
        private Panel _sidebarPanel = null!;
        private Panel _headerPanel = null!;
        private Panel _contentPanel = null!;
        private Label _lblHeaderTitle = null!;
        private Label _lblHeaderStats = null!;
        private Label _lblHeaderAiStatus = null!;
        private Button _btnNavChat = null!;
        private Button _btnNavSearch = null!;
        private Button _btnNavDeadlines = null!;
        private Button _btnNavInvoices = null!;
        private Button _btnNavDiff = null!;
        private Button _btnNavFolders = null!;
        private Button _btnNavSettings = null!;
        private NotifyIcon _notifyIcon = null!;

        public MainForm()
        {
            // Initialize Core Dependency Graph
            _settingsService = new SettingsService();
            _databaseService = new SqliteDatabaseService();
            _ocrService = new WindowsMediaOcrService();
            _extractorFactory = new ExtractorFactory(_ocrService);
            _chunker = new SmartTextChunker();
            _embeddingService = new HybridEmbeddingService();
            _indexingService = new IndexingService(_databaseService, _extractorFactory, _chunker, _embeddingService, _settingsService);
            _fileWatcherService = new FileWatcherService(_databaseService, _indexingService);
            _queryParser = new NaturalLanguageQueryParser();
            _searchEngine = new HybridSearchEngine(_databaseService, _embeddingService);
            _ollamaService = new OllamaClientService();
            _localSynthesizer = new LocalExtractiveSynthesizer();
            _ragOrchestrator = new RagOrchestrator(_queryParser, _searchEngine, _ollamaService, _localSynthesizer, _settingsService);
            _installerService = new OllamaInstallerService();
            _deadlineService = new DeadlineTrackerService(_databaseService);
            _invoiceService = new InvoiceExtractorService(_databaseService);
            _diffService = new DocumentDiffService(_extractorFactory, _ollamaService);

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Asisten File & Dokumen Lokal - AI On-Device";
            this.Size = new Size(1280, 840);
            this.MinimumSize = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ModernTheme.ContentBg;
            this.Font = ModernTheme.BodyFont;

            // 1. Left Sidebar
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 240,
                BackColor = ModernTheme.SidebarBg
            };

            // App Brand Header
            var brandPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                Padding = new Padding(20, 18, 16, 10),
                BackColor = Color.Transparent
            };

            var lblLogo = new Label
            {
                Text = "⚡ ASISTEN FILE",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = Color.White,
                UseMnemonic = false,
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblSubtitle = new Label
            {
                Text = "Dokumen & RAG On-Device",
                Font = ModernTheme.SmallFont,
                ForeColor = Color.FromArgb(148, 163, 184),
                UseMnemonic = false,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 0)
            };

            brandPanel.Controls.Add(lblSubtitle);
            brandPanel.Controls.Add(lblLogo);

            // Nav Buttons
            var navButtonsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 10, 12, 10),
                BackColor = Color.Transparent
            };

            _btnNavChat = CreateNavButton("💬  Tanya & Chat", 0);
            _btnNavChat.Click += (s, e) => SwitchView(_chatView, _btnNavChat, "💬 Tanya & Chat Dokumen");

            _btnNavSearch = CreateNavButton("🔍  Pencarian File", 44);
            _btnNavSearch.Click += (s, e) => SwitchView(_searchGridView, _btnNavSearch, "🔍 Pencarian & Filter Dokumen");

            _btnNavDeadlines = CreateNavButton("📅  Jatuh Tempo", 88);
            _btnNavDeadlines.Click += (s, e) =>
            {
                SwitchView(_deadlineTrackerView, _btnNavDeadlines, "📅 Pelacak Jatuh Tempo & Agenda Dokumen");
                _ = _deadlineTrackerView.ScanDeadlinesAsync();
            };

            _btnNavInvoices = CreateNavButton("📊  Rekap Invoice", 132);
            _btnNavInvoices.Click += (s, e) =>
            {
                SwitchView(_invoiceRekapView, _btnNavInvoices, "📊 Rekap Invoice & Ekstraksi Tabel Otomatis");
                _ = _invoiceRekapView.ExtractInvoicesAsync();
            };

            _btnNavDiff = CreateNavButton("📑  Bandingkan File", 176);
            _btnNavDiff.Click += (s, e) => SwitchView(_documentDiffView, _btnNavDiff, "📑 Bandingkan Dua Dokumen (Document Diff AI)");

            _btnNavFolders = CreateNavButton("📁  Kelola Folder", 220);
            _btnNavFolders.Click += (s, e) => SwitchView(_folderManagerView, _btnNavFolders, "📁 Kelola Folder & Status Indeks");

            _btnNavSettings = CreateNavButton("⚙️  Pengaturan & AI", 264);
            _btnNavSettings.Click += (s, e) => SwitchView(_settingsView, _btnNavSettings, "⚙️ Pengaturan & Model Bahasa Lokal");

            navButtonsPanel.Controls.Add(_btnNavSettings);
            navButtonsPanel.Controls.Add(_btnNavFolders);
            navButtonsPanel.Controls.Add(_btnNavDiff);
            navButtonsPanel.Controls.Add(_btnNavInvoices);
            navButtonsPanel.Controls.Add(_btnNavDeadlines);
            navButtonsPanel.Controls.Add(_btnNavSearch);
            navButtonsPanel.Controls.Add(_btnNavChat);

            // Bottom Privacy Badge
            var privacyBadge = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                Padding = new Padding(16, 12, 16, 12),
                BackColor = Color.FromArgb(15, 23, 42)
            };

            var lblLock = new Label
            {
                Text = "🛡️ 100% Offline & Privat",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.AccentSuccess,
                UseMnemonic = false,
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblLockDesc = new Label
            {
                Text = "Data tidak pernah keluar dari komputer.",
                Font = ModernTheme.SmallFont,
                ForeColor = Color.FromArgb(148, 163, 184),
                UseMnemonic = false,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 2, 0, 0)
            };

            privacyBadge.Controls.Add(lblLockDesc);
            privacyBadge.Controls.Add(lblLock);

            _sidebarPanel.Controls.Add(navButtonsPanel);
            _sidebarPanel.Controls.Add(privacyBadge);
            _sidebarPanel.Controls.Add(brandPanel);

            // 2. Top Header Bar
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(24, 16, 24, 16)
            };
            _headerPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(ModernTheme.BorderColor, 1);
                e.Graphics.DrawLine(pen, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
            };

            _lblHeaderTitle = new Label
            {
                Text = "💬 Tanya & Chat Dokumen",
                Font = ModernTheme.HeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                UseMnemonic = false,
                Dock = DockStyle.Left,
                AutoSize = true
            };

            _lblHeaderAiStatus = new Label
            {
                Text = "⚡ Memeriksa AI...",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.Primary,
                BackColor = Color.FromArgb(239, 246, 255),
                Padding = new Padding(6, 4, 6, 4),
                UseMnemonic = false,
                Dock = DockStyle.Right,
                AutoSize = true
            };

            _lblHeaderStats = new Label
            {
                Text = "📁 Memuat data indeks...",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextSecondary,
                UseMnemonic = false,
                Dock = DockStyle.Right,
                AutoSize = true,
                Padding = new Padding(0, 4, 16, 0)
            };

            _headerPanel.Controls.Add(_lblHeaderTitle);
            _headerPanel.Controls.Add(_lblHeaderStats);
            _headerPanel.Controls.Add(_lblHeaderAiStatus);

            // 3. Central Content Area
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ModernTheme.ContentBg
            };

            // Initialize Child Views
            _chatView = new ChatView(_ragOrchestrator);
            _searchGridView = new SearchGridView(_searchEngine, _queryParser, _databaseService);
            _deadlineTrackerView = new DeadlineTrackerView(_deadlineService);
            _invoiceRekapView = new InvoiceRekapView(_invoiceService);
            _documentDiffView = new DocumentDiffView(_diffService);
            _folderManagerView = new FolderManagerView(_databaseService, _indexingService, _fileWatcherService);
            _settingsView = new SettingsView(_settingsService, _ollamaService, _installerService);

            // Setup Notifications
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Visible = true,
                Text = "Asisten File & Dokumen Lokal"
            };
            _notifyIcon.BalloonTipClicked += (s, e) =>
            {
                SwitchView(_deadlineTrackerView, _btnNavDeadlines, "📅 Pelacak Jatuh Tempo & Agenda Dokumen");
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            };

            _deadlineTrackerView.OnCriticalDeadlineDetected += (msg) =>
            {
                _notifyIcon.ShowBalloonTip(6000, "⚠️ Pengingat Jatuh Tempo Dokumen", msg, ToolTipIcon.Warning);
            };

            _folderManagerView.OnIndexingCompleted += () =>
            {
                _ = RefreshHeaderStatsAsync();
                _ = _deadlineTrackerView.ScanDeadlinesAsync();
            };

            _settingsView.OnBackgroundModelDownloadRequested += (model) => StartBackgroundModelDownload(model);

            _contentPanel.Controls.Add(_chatView);
            _contentPanel.Controls.Add(_searchGridView);
            _contentPanel.Controls.Add(_deadlineTrackerView);
            _contentPanel.Controls.Add(_invoiceRekapView);
            _contentPanel.Controls.Add(_documentDiffView);
            _contentPanel.Controls.Add(_folderManagerView);
            _contentPanel.Controls.Add(_settingsView);

            this.Controls.Add(_contentPanel);
            this.Controls.Add(_headerPanel);
            this.Controls.Add(_sidebarPanel);

            this.Load += async (s, e) => await InitializeApplicationAsync();
        }

        private Button CreateNavButton(string text, int top)
        {
            var btn = new Button
            {
                Text = text,
                UseMnemonic = false,
                Location = new Point(0, top),
                Size = new Size(216, 38),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 232, 240),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ModernTheme.SidebarItemHover;
            return btn;
        }

        private void SwitchView(UserControl view, Button activeBtn, string headerTitle)
        {
            _chatView.Visible = (view == _chatView);
            _searchGridView.Visible = (view == _searchGridView);
            _deadlineTrackerView.Visible = (view == _deadlineTrackerView);
            _invoiceRekapView.Visible = (view == _invoiceRekapView);
            _documentDiffView.Visible = (view == _documentDiffView);
            _folderManagerView.Visible = (view == _folderManagerView);
            _settingsView.Visible = (view == _settingsView);

            view.BringToFront();
            _lblHeaderTitle.Text = headerTitle;

            // Highlight active button
            _btnNavChat.BackColor = (_btnNavChat == activeBtn) ? ModernTheme.SidebarItemActive : Color.Transparent;
            _btnNavSearch.BackColor = (_btnNavSearch == activeBtn) ? ModernTheme.SidebarItemActive : Color.Transparent;
            _btnNavDeadlines.BackColor = (_btnNavDeadlines == activeBtn) ? ModernTheme.SidebarItemActive : Color.Transparent;
            _btnNavInvoices.BackColor = (_btnNavInvoices == activeBtn) ? ModernTheme.SidebarItemActive : Color.Transparent;
            _btnNavDiff.BackColor = (_btnNavDiff == activeBtn) ? ModernTheme.SidebarItemActive : Color.Transparent;
            _btnNavFolders.BackColor = (_btnNavFolders == activeBtn) ? ModernTheme.SidebarItemActive : Color.Transparent;
            _btnNavSettings.BackColor = (_btnNavSettings == activeBtn) ? ModernTheme.SidebarItemActive : Color.Transparent;
        }

        private async Task InitializeApplicationAsync()
        {
            try
            {
                await _settingsService.LoadAsync();
                await _databaseService.InitializeAsync();
                await _folderManagerView.LoadInitialDataAsync();
                await _fileWatcherService.StartWatchingAsync();
                await RefreshHeaderStatsAsync();

                // Select Chat view by default
                SwitchView(_chatView, _btnNavChat, "💬 Tanya & Chat Dokumen");

                // Check if Ollama has any model or if onboarding has never been completed
                bool hasModels = await _installerService.HasAnyModelsAsync();
                if (!_settingsService.Current.HasCompletedOnboarding || !hasModels)
                {
                    using var setupDlg = new OllamaSetupDialog(_installerService, _settingsService);
                    if (setupDlg.ShowDialog(this) == DialogResult.OK)
                    {
                        if (setupDlg.BackgroundDownloadRequested)
                        {
                            StartBackgroundModelDownload(setupDlg.SelectedModel);
                        }
                    }
                }

                await UpdateAiStatusBadgeAsync();

                // Initial background scan for deadlines
                _ = _deadlineTrackerView.ScanDeadlinesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Inisialisasi aplikasi gagal: {ex.Message}", "Error Inisialisasi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void StartBackgroundModelDownload(string modelName)
        {
            _lblHeaderAiStatus.Text = $"📥 Mengunduh {modelName} di latar belakang... (AI Ekstraktif aktif)";
            _lblHeaderAiStatus.ForeColor = ModernTheme.AccentWarning;
            _lblHeaderAiStatus.BackColor = Color.FromArgb(254, 243, 199);

            _ = Task.Run(async () =>
            {
                var progress = new Progress<ModelDownloadProgress>(p =>
                {
                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            if (p.IsCompleted)
                            {
                                _lblHeaderAiStatus.Text = $"🟢 Ollama LLM Siap ({modelName})";
                                _lblHeaderAiStatus.ForeColor = ModernTheme.AccentSuccess;
                                _lblHeaderAiStatus.BackColor = Color.FromArgb(236, 253, 245);

                                if (_ollamaService is OllamaClientService client)
                                {
                                    client.SetModel(modelName);
                                }

                                _notifyIcon.ShowBalloonTip(6000, "🎉 Unduhan AI Selesai",
                                    $"Model '{modelName}' telah selesai dipasang! Asisten File otomatis beralih menggunakan model Ollama lokal.",
                                    ToolTipIcon.Info);
                            }
                            else
                            {
                                _lblHeaderAiStatus.Text = $"📥 Mengunduh {modelName} ({p.Percent}%)... (AI Ekstraktif aktif)";
                            }
                        }));
                    }
                });

                try
                {
                    await _installerService.PullModelAsync(modelName, progress);
                }
                catch
                {
                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            _lblHeaderAiStatus.Text = "⚡ AI Ekstraktif Lokal Aktif";
                            _lblHeaderAiStatus.ForeColor = ModernTheme.Primary;
                            _lblHeaderAiStatus.BackColor = Color.FromArgb(239, 246, 255);
                        }));
                    }
                }
            });
        }

        private async Task UpdateAiStatusBadgeAsync()
        {
            var ollamaReady = _settingsService.Current.UseOllamaIfAvailable && await _ollamaService.IsAvailableAsync();
            var hasModels = await _installerService.HasAnyModelsAsync();

            if (ollamaReady && hasModels)
            {
                _lblHeaderAiStatus.Text = $"🟢 Ollama LLM Siap ({_settingsService.Current.OllamaModel})";
                _lblHeaderAiStatus.ForeColor = ModernTheme.AccentSuccess;
                _lblHeaderAiStatus.BackColor = Color.FromArgb(236, 253, 245);
            }
            else
            {
                _lblHeaderAiStatus.Text = "⚡ AI Ekstraktif Lokal Aktif";
                _lblHeaderAiStatus.ForeColor = ModernTheme.Primary;
                _lblHeaderAiStatus.BackColor = Color.FromArgb(239, 246, 255);
            }
        }

        private async Task RefreshHeaderStatsAsync()
        {
            try
            {
                var files = await _databaseService.GetTotalIndexedFilesCountAsync();
                var chunks = await _databaseService.GetTotalIndexedChunksCountAsync();
                _lblHeaderStats.Text = $"📁 {files} File Dokumen | 📑 {chunks} Potongan Teks Vektor";
            }
            catch
            {
                // Silently ignore stats update error
            }
        }
    }
}

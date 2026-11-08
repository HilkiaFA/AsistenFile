using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AsistenFile.Core.AI;
using AsistenFile.Core.Config;

namespace AsistenFile.App.UI
{
    public class OllamaSetupDialog : Form
    {
        private readonly IOllamaInstallerService _installerService;
        private readonly ISettingsService _settingsService;

        private ComboBox _cmbModels = null!;
        private Label _lblOllamaStatus = null!;
        private Button _btnInstallOllama = null!;
        private Button _btnDownloadNow = null!;
        private Button _btnDownloadBackground = null!;
        private Button _btnUseExtractiveOnly = null!;
        private ProgressBar _progressBar = null!;
        private Label _lblDownloadStatus = null!;

        private CancellationTokenSource? _downloadCts;

        public bool BackgroundDownloadRequested { get; private set; }
        public string SelectedModel { get; private set; } = "qwen2.5:1.5b";

        public OllamaSetupDialog(IOllamaInstallerService installerService, ISettingsService settingsService)
        {
            _installerService = installerService;
            _settingsService = settingsService;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Penyiapan AI Dokumen Lokal - Asisten File";
            this.Size = new Size(740, 680);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = ModernTheme.ContentBg;
            this.Font = ModernTheme.BodyFont;

            // 1. Top Header Banner
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 95,
                BackColor = ModernTheme.SidebarBg,
                Padding = new Padding(24, 18, 24, 14)
            };

            var lblTitle = new Label
            {
                Text = "🚀 Penyiapan Model AI Dokumen Lokal",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblSubtitle = new Label
            {
                Text = "Asisten File memproses semua dokumen langsung di perangkat Anda tanpa cloud.\n" +
                       "Pilih bagaimana Anda ingin menjalankan kecerdasan buatan (AI) di komputer ini.",
                Font = ModernTheme.SmallFont,
                ForeColor = Color.FromArgb(203, 213, 225),
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 0)
            };

            headerPanel.Controls.Add(lblSubtitle);
            headerPanel.Controls.Add(lblTitle);

            // 2. Middle Content Container
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                AutoScroll = true
            };

            // Status Card (Detection of Ollama)
            var statusCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(14),
                Margin = new Padding(0, 0, 0, 14)
            };
            ModernTheme.ApplyCardStyle(statusCard);

            _lblOllamaStatus = new Label
            {
                Text = "Memeriksa status instalasi Ollama...",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Location = new Point(14, 12),
                AutoSize = true
            };

            _btnInstallOllama = new Button
            {
                Text = "⚡ Pasang Ollama Otomatis",
                Size = new Size(185, 30),
                Location = new Point(480, 10),
                Visible = false
            };
            ModernTheme.StyleButton(_btnInstallOllama, isPrimary: true);
            _btnInstallOllama.Click += async (s, e) => await InstallOllamaAsync();

            statusCard.Controls.Add(_btnInstallOllama);
            statusCard.Controls.Add(_lblOllamaStatus);

            // Card 1: Ollama Model (Recommended)
            var cardOllama = new Panel
            {
                Dock = DockStyle.Top,
                Height = 270,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(16),
                Margin = new Padding(0, 14, 0, 14)
            };
            ModernTheme.ApplyCardStyle(cardOllama);

            var lblOllamaCardTitle = new Label
            {
                Text = "🌟 Opsi 1: Unduh Model AI Lokal (Ollama LLM) - Rekomendasi",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.Primary,
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblOllamaCardDesc = new Label
            {
                Text = "Model AI pintar berbasis LLM kecil on-device. Menjawab pertanyaan dalam bahasa alami secara fasih,\n" +
                       "menganalisis konteks dokumen, dan tetap berjalan 100% offline tanpa internet.",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextSecondary,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 12)
            };

            var lblModelSelect = new Label
            {
                Text = "Pilih Model yang Ingin Diunduh:",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(16, 85),
                AutoSize = true
            };

            _cmbModels = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(16, 110),
                Size = new Size(640, 26)
            };
            _cmbModels.Items.AddRange(new object[]
            {
                "qwen2.5:1.5b  —  [Sangat Cepat & Terbaik Bahasa Indonesia | Ukuran ~1.0 GB | RAM 4-8 GB]",
                "llama3.2:3b   —  [Rekomendasi Kualitas Tinggi Meta | Ukuran ~2.0 GB | RAM 8-16 GB]",
                "llama3.2:1b   —  [Super Ringan Hemat Daya Laptop | Ukuran ~1.3 GB | RAM 4 GB]"
            });
            _cmbModels.SelectedIndex = 0;

            // Action Buttons in Card 1
            _btnDownloadNow = new Button
            {
                Text = "📥 Unduh & Siapkan Sekarang",
                Size = new Size(220, 38),
                Location = new Point(16, 155)
            };
            ModernTheme.StyleButton(_btnDownloadNow, isPrimary: true);
            _btnDownloadNow.Click += async (s, e) => await DownloadModelNowAsync();

            _btnDownloadBackground = new Button
            {
                Text = "⚡ Masuk Aplikasi Sekarang & Unduh di Latar Belakang",
                Size = new Size(390, 38),
                Location = new Point(245, 155)
            };
            ModernTheme.StyleButton(_btnDownloadBackground, isPrimary: false);
            _btnDownloadBackground.Click += (s, e) => RequestBackgroundDownloadAndEnter();

            var lblBgNote = new Label
            {
                Text = "💡 Fitur Auto-Download Latar Belakang: Aplikasi langsung terbuka dan aktif menggunakan AI Ekstraktif bawaan.\n" +
                       "Anda bisa langsung indeks dan cari dokumen tanpa menunggu unduhan selesai!",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.AccentSuccess,
                Location = new Point(16, 205),
                AutoSize = true
            };

            cardOllama.Controls.Add(lblBgNote);
            cardOllama.Controls.Add(_btnDownloadBackground);
            cardOllama.Controls.Add(_btnDownloadNow);
            cardOllama.Controls.Add(_cmbModels);
            cardOllama.Controls.Add(lblModelSelect);
            cardOllama.Controls.Add(lblOllamaCardDesc);
            cardOllama.Controls.Add(lblOllamaCardTitle);

            // Card 2: Extractive AI Only (Zero download)
            var cardExtractive = new Panel
            {
                Dock = DockStyle.Top,
                Height = 115,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(16),
                Margin = new Padding(0, 14, 0, 0)
            };
            ModernTheme.ApplyCardStyle(cardExtractive);

            var lblExtractiveTitle = new Label
            {
                Text = "⚡ Opsi 2: Gunakan AI Ekstraktif Bawaan (Tanpa Download Apapun)",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblExtractiveDesc = new Label
            {
                Text = "Langsung aktif seketika tanpa perlu mengunduh file besar. Sangat cepat, hemat ruang disk dan RAM.\n" +
                       "Mengekstrak tanggal kontrak, total invoice, dan sitasi langsung dari teks dokumen.",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextSecondary,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 10)
            };

            _btnUseExtractiveOnly = new Button
            {
                Text = "Lanjut Tanpa Download (Gunakan AI Bawaan)",
                Size = new Size(300, 32),
                Location = new Point(16, 68)
            };
            ModernTheme.StyleButton(_btnUseExtractiveOnly, isPrimary: false);
            _btnUseExtractiveOnly.Click += (s, e) => UseExtractiveOnlyAndClose();

            cardExtractive.Controls.Add(_btnUseExtractiveOnly);
            cardExtractive.Controls.Add(lblExtractiveDesc);
            cardExtractive.Controls.Add(lblExtractiveTitle);

            // 3. Bottom Progress Bar Area
            var bottomProgressArea = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(20, 10, 20, 10)
            };
            bottomProgressArea.Paint += (s, e) =>
            {
                using var pen = new Pen(ModernTheme.BorderColor, 1);
                e.Graphics.DrawLine(pen, 0, 0, bottomProgressArea.Width, 0);
            };

            _lblDownloadStatus = new Label
            {
                Text = "Pilih salah satu opsi di atas untuk memulai.",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextSecondary,
                Dock = DockStyle.Top,
                Height = 22
            };

            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                Style = ProgressBarStyle.Continuous,
                Value = 0
            };

            bottomProgressArea.Controls.Add(_progressBar);
            bottomProgressArea.Controls.Add(_lblDownloadStatus);

            contentPanel.Controls.Add(cardExtractive);
            contentPanel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 14 });
            contentPanel.Controls.Add(cardOllama);
            contentPanel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 14 });
            contentPanel.Controls.Add(statusCard);

            this.Controls.Add(contentPanel);
            this.Controls.Add(bottomProgressArea);
            this.Controls.Add(headerPanel);

            this.Load += async (s, e) => await CheckSystemStatusAsync();
        }

        private async Task CheckSystemStatusAsync()
        {
            bool isInstalled = _installerService.IsOllamaInstalled();
            if (!isInstalled)
            {
                _lblOllamaStatus.Text = "⚠️ Ollama belum terpasang di komputer ini.";
                _lblOllamaStatus.ForeColor = ModernTheme.AccentWarning;
                _btnInstallOllama.Visible = true;
                _btnDownloadNow.Enabled = false;
                _btnDownloadBackground.Enabled = false;
                return;
            }

            _lblOllamaStatus.Text = "✅ Ollama terpasang. Memeriksa model lokal...";
            _lblOllamaStatus.ForeColor = ModernTheme.AccentSuccess;

            bool isRunning = await _installerService.IsOllamaRunningAsync();
            if (!isRunning)
            {
                _lblOllamaStatus.Text = "⏳ Menjalankan service Ollama lokal...";
                await _installerService.StartOllamaProcessAsync();
            }

            bool hasModels = await _installerService.HasAnyModelsAsync();
            if (hasModels)
            {
                _lblOllamaStatus.Text = "✅ Ollama aktif dan sudah memiliki model lokal yang siap digunakan!";
                _lblOllamaStatus.ForeColor = ModernTheme.AccentSuccess;
            }
            else
            {
                _lblOllamaStatus.Text = "ℹ️ Ollama aktif, namun belum ada model AI yang diunduh.";
                _lblOllamaStatus.ForeColor = ModernTheme.Primary;
            }

            _btnDownloadNow.Enabled = true;
            _btnDownloadBackground.Enabled = true;
        }

        private string GetSelectedModelName()
        {
            return _cmbModels.SelectedIndex switch
            {
                1 => "llama3.2:3b",
                2 => "llama3.2:1b",
                _ => "qwen2.5:1.5b"
            };
        }

        private async Task InstallOllamaAsync()
        {
            _btnInstallOllama.Enabled = false;
            _downloadCts = new CancellationTokenSource();

            var progress = new Progress<ModelDownloadProgress>(p =>
            {
                _progressBar.Value = Math.Clamp(p.Percent, 0, 100);
                _lblDownloadStatus.Text = p.Status;
            });

            try
            {
                await _installerService.DownloadOllamaInstallerAsync(progress, _downloadCts.Token);
                await CheckSystemStatusAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal memasang Ollama: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _lblDownloadStatus.Text = "Gagal memasang Ollama.";
                _btnInstallOllama.Enabled = true;
            }
        }

        private async Task DownloadModelNowAsync()
        {
            var modelName = GetSelectedModelName();
            SelectedModel = modelName;

            _btnDownloadNow.Enabled = false;
            _btnDownloadBackground.Enabled = false;
            _btnUseExtractiveOnly.Enabled = false;
            _cmbModels.Enabled = false;

            _downloadCts = new CancellationTokenSource();

            var progress = new Progress<ModelDownloadProgress>(p =>
            {
                _progressBar.Value = Math.Clamp(p.Percent, 0, 100);
                if (p.TotalBytes > 0)
                {
                    _lblDownloadStatus.Text = $"{p.Status} ({p.CompletedBytes / 1_048_576} MB / {p.TotalBytes / 1_048_576} MB - {p.Percent}%)";
                }
                else
                {
                    _lblDownloadStatus.Text = p.Status;
                }

                if (p.IsCompleted)
                {
                    _progressBar.Value = 100;
                    _lblDownloadStatus.Text = $"🎉 Model {modelName} berhasil dipasang!";
                }
            });

            try
            {
                await _installerService.PullModelAsync(modelName, progress, _downloadCts.Token);

                // Update settings
                var settings = _settingsService.Current;
                settings.OllamaModel = modelName;
                settings.UseOllamaIfAvailable = true;
                settings.HasCompletedOnboarding = true;
                await _settingsService.SaveAsync();

                MessageBox.Show($"Model {modelName} berhasil diunduh dan dipasang!\nAplikasi siap digunakan.", "Sukses", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal mengunduh model: {ex.Message}\nAnda tetap bisa masuk dan menggunakan AI Ekstraktif bawaan.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _btnDownloadNow.Enabled = true;
                _btnDownloadBackground.Enabled = true;
                _btnUseExtractiveOnly.Enabled = true;
                _cmbModels.Enabled = true;
            }
        }

        private void RequestBackgroundDownloadAndEnter()
        {
            SelectedModel = GetSelectedModelName();
            BackgroundDownloadRequested = true;

            var settings = _settingsService.Current;
            settings.OllamaModel = SelectedModel;
            settings.UseOllamaIfAvailable = true;
            settings.HasCompletedOnboarding = true;
            _ = _settingsService.SaveAsync();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void UseExtractiveOnlyAndClose()
        {
            var settings = _settingsService.Current;
            settings.UseOllamaIfAvailable = false;
            settings.HasCompletedOnboarding = true;
            _ = _settingsService.SaveAsync();

            BackgroundDownloadRequested = false;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}

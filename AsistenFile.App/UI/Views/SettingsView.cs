using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AsistenFile.App.UI;
using AsistenFile.Core.AI;
using AsistenFile.Core.Config;
using AsistenFile.Core.Models;

namespace AsistenFile.App.UI.Views
{
    public class SettingsView : UserControl
    {
        private readonly ISettingsService _settingsService;
        private readonly ILanguageModelService _ollamaService;
        private readonly IOllamaInstallerService _installerService;

        private TextBox _txtEndpoint = null!;
        private ComboBox _cmbModel = null!;
        private Button _btnDetectModels = null!;
        private Button _btnTestConnection = null!;
        private Button _btnOpenInstallerWizard = null!;
        private Label _lblOllamaStatus = null!;
        private CheckBox _chkUseOllama = null!;
        private CheckBox _chkEnableOcr = null!;
        private NumericUpDown _numChunkSize = null!;
        private TextBox _txtExcludedFolders = null!;
        private TextBox _txtExcludedExtensions = null!;
        private Button _btnSave = null!;

        public event Action<string>? OnBackgroundModelDownloadRequested;

        public SettingsView(ISettingsService settingsService, ILanguageModelService ollamaService, IOllamaInstallerService installerService)
        {
            _settingsService = settingsService;
            _ollamaService = ollamaService;
            _installerService = installerService;
            InitializeComponent();
            LoadSettingsToUI();
        }

        private void InitializeComponent()
        {
            this.BackColor = ModernTheme.ContentBg;
            this.Dock = DockStyle.Fill;
            this.Font = ModernTheme.BodyFont;
            this.Padding = new Padding(24);
            this.AutoScroll = true;

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 4
            };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // Section 1: AI & LLM Lokal Card
            var aiCard = CreateCard("🤖 Konfigurasi AI & Model Bahasa Lokal", 250);
            
            var lblEndpoint = new Label { Text = "Ollama Endpoint URL:", Location = new Point(16, 44), AutoSize = true };
            _txtEndpoint = new TextBox { Text = "http://localhost:11434", Location = new Point(170, 41), Size = new Size(240, 24) };

            _btnTestConnection = new Button { Text = "🔌 Uji Koneksi", Location = new Point(420, 39), Size = new Size(110, 28) };
            ModernTheme.StyleButton(_btnTestConnection, isPrimary: false);
            _btnTestConnection.Click += (s, e) => _ = TestOllamaConnectionAsync();

            _lblOllamaStatus = new Label { Text = "⚪ Belum diuji", Location = new Point(540, 44), AutoSize = true, ForeColor = ModernTheme.TextMuted };

            var lblModel = new Label { Text = "Pilihan Model LLM:", Location = new Point(16, 84), AutoSize = true };
            _cmbModel = new ComboBox { Location = new Point(170, 81), Size = new Size(240, 24) };
            _cmbModel.Items.AddRange(new object[] { "qwen2.5:1.5b", "llama3.2:3b", "llama3.2:1b", "phi3:mini", "mistral:7b" });

            _btnDetectModels = new Button { Text = "🔍 Deteksi Model", Location = new Point(420, 79), Size = new Size(125, 28) };
            ModernTheme.StyleButton(_btnDetectModels, isPrimary: false);
            _btnDetectModels.Click += (s, e) => _ = DetectModelsAsync();

            _btnOpenInstallerWizard = new Button { Text = "📥 Pasang / Unduh Model Baru", Location = new Point(555, 79), Size = new Size(200, 28) };
            ModernTheme.StyleButton(_btnOpenInstallerWizard, isPrimary: true);
            _btnOpenInstallerWizard.Click += (s, e) => OpenInstallerDialog();

            _chkUseOllama = new CheckBox
            {
                Text = "Gunakan Ollama LLM jika aktif (jika offline, otomatis beralih ke Mesin Ekstraktif Cepat)",
                Location = new Point(16, 126),
                AutoSize = true,
                Checked = true
            };

            var lblLlmNote = new Label
            {
                Text = "💡 Privasi 100% Terjamin: Baik Ollama maupun AI Ekstraktif bawaan berjalan langsung di laptop Anda.\nTidak ada sebaris pun teks dokumen yang dikirim ke internet.",
                Location = new Point(16, 160),
                AutoSize = true,
                ForeColor = ModernTheme.AccentSuccess,
                Font = ModernTheme.SmallFont
            };

            aiCard.Controls.Add(lblLlmNote);
            aiCard.Controls.Add(_chkUseOllama);
            aiCard.Controls.Add(_btnOpenInstallerWizard);
            aiCard.Controls.Add(_btnDetectModels);
            aiCard.Controls.Add(_cmbModel);
            aiCard.Controls.Add(lblModel);
            aiCard.Controls.Add(_lblOllamaStatus);
            aiCard.Controls.Add(_btnTestConnection);
            aiCard.Controls.Add(_txtEndpoint);
            aiCard.Controls.Add(lblEndpoint);

            // Section 2: Indexing & OCR Card
            var ocrCard = CreateCard("⚙️ Pengindeksan & OCR Dokumen", 140);

            _chkEnableOcr = new CheckBox
            {
                Text = "Aktifkan OCR Windows bawaan untuk membaca PDF hasil scan dan gambar (.png, .jpg)",
                Location = new Point(16, 44),
                AutoSize = true,
                Checked = true
            };

            var lblChunk = new Label { Text = "Ukuran Potongan Teks (Kata):", Location = new Point(16, 84), AutoSize = true };
            _numChunkSize = new NumericUpDown
            {
                Location = new Point(220, 82),
                Size = new Size(90, 24),
                Minimum = 150,
                Maximum = 1000,
                Value = 350
            };

            var lblChunkNote = new Label
            {
                Text = "(Rekomendasi: 300-500 kata untuk menjaga konteks kalimat dan kecepatan pencarian)",
                Location = new Point(320, 84),
                AutoSize = true,
                ForeColor = ModernTheme.TextMuted,
                Font = ModernTheme.SmallFont
            };

            ocrCard.Controls.Add(lblChunkNote);
            ocrCard.Controls.Add(_numChunkSize);
            ocrCard.Controls.Add(lblChunk);
            ocrCard.Controls.Add(_chkEnableOcr);

            // Section 3: Privacy & Exclusions Card
            var privacyCard = CreateCard("🔒 Pengaturan Privasi & Pengecualian Folder", 180);

            var lblExcFolders = new Label { Text = "Folder yang Dikecualikan (Pisahkan dengan koma):", Location = new Point(16, 44), AutoSize = true };
            _txtExcludedFolders = new TextBox
            {
                Location = new Point(16, 68),
                Size = new Size(650, 24)
            };

            var lblExcExt = new Label { Text = "Ekstensi File yang Dikecualikan (Pisahkan dengan koma):", Location = new Point(16, 106), AutoSize = true };
            _txtExcludedExtensions = new TextBox
            {
                Location = new Point(16, 130),
                Size = new Size(650, 24)
            };

            privacyCard.Controls.Add(_txtExcludedExtensions);
            privacyCard.Controls.Add(lblExcExt);
            privacyCard.Controls.Add(_txtExcludedFolders);
            privacyCard.Controls.Add(lblExcFolders);

            // Section 4: Save Button Panel
            var savePanel = new Panel
            {
                Height = 60,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 16, 0, 0)
            };

            _btnSave = new Button
            {
                Text = "💾 Simpan Pengaturan",
                Size = new Size(180, 38),
                Location = new Point(0, 10)
            };
            ModernTheme.StyleButton(_btnSave, isPrimary: true);
            _btnSave.Click += (s, e) => _ = SaveSettingsAsync();

            savePanel.Controls.Add(_btnSave);

            mainPanel.Controls.Add(aiCard);
            mainPanel.Controls.Add(ocrCard);
            mainPanel.Controls.Add(privacyCard);
            mainPanel.Controls.Add(savePanel);

            this.Controls.Add(mainPanel);
        }

        private Panel CreateCard(string title, int height)
        {
            var card = new Panel
            {
                Dock = DockStyle.Top,
                Height = height,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(16),
                Margin = new Padding(0, 0, 0, 16)
            };
            ModernTheme.ApplyCardStyle(card);

            var lblHeader = new Label
            {
                Text = title,
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Top,
                AutoSize = true
            };
            card.Controls.Add(lblHeader);

            return card;
        }

        private void LoadSettingsToUI()
        {
            var s = _settingsService.Current;
            _txtEndpoint.Text = s.OllamaEndpoint;
            _cmbModel.Text = s.OllamaModel;
            _chkUseOllama.Checked = s.UseOllamaIfAvailable;
            _chkEnableOcr.Checked = s.EnableBuiltInOcr;
            _numChunkSize.Value = Math.Clamp(s.ChunkWordSize, 150, 1000);
            _txtExcludedFolders.Text = string.Join(", ", s.ExcludedFolderNames);
            _txtExcludedExtensions.Text = string.Join(", ", s.ExcludedExtensions);
        }

        private void OpenInstallerDialog()
        {
            using var dlg = new OllamaSetupDialog(_installerService, _settingsService);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                if (dlg.BackgroundDownloadRequested)
                {
                    OnBackgroundModelDownloadRequested?.Invoke(dlg.SelectedModel);
                }
                else
                {
                    _ = DetectModelsAsync();
                }
            }
        }

        private async Task TestOllamaConnectionAsync()
        {
            _btnTestConnection.Enabled = false;
            _lblOllamaStatus.Text = "⏳ Menguji...";
            _lblOllamaStatus.ForeColor = ModernTheme.Primary;

            try
            {
                var isOk = await _ollamaService.IsAvailableAsync();
                if (isOk)
                {
                    _lblOllamaStatus.Text = "🟢 Ollama Terhubung & Siap";
                    _lblOllamaStatus.ForeColor = ModernTheme.AccentSuccess;
                }
                else
                {
                    _lblOllamaStatus.Text = "🔴 Ollama Tidak Terhubung (Gunakan AI Ekstraktif bawaan)";
                    _lblOllamaStatus.ForeColor = ModernTheme.AccentDanger;
                }
            }
            catch
            {
                _lblOllamaStatus.Text = "🔴 Gagal menghubungi Ollama";
                _lblOllamaStatus.ForeColor = ModernTheme.AccentDanger;
            }
            finally
            {
                _btnTestConnection.Enabled = true;
            }
        }

        private async Task DetectModelsAsync()
        {
            _btnDetectModels.Enabled = false;
            try
            {
                var models = await _ollamaService.GetInstalledModelsAsync();
                if (models.Count > 0)
                {
                    _cmbModel.Items.Clear();
                    foreach (var m in models)
                    {
                        _cmbModel.Items.Add(m);
                    }
                    _cmbModel.SelectedIndex = 0;
                    MessageBox.Show($"Ditemukan {models.Count} model lokal di Ollama:\n" + string.Join("\n", models), "Model Ditemukan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Tidak ada model yang terpasang di Ollama.\nKlik tombol 'Pasang / Unduh Model Baru' untuk mengunduh model otomatis.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal mendeteksi model: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnDetectModels.Enabled = true;
            }
        }

        private async Task SaveSettingsAsync()
        {
            var s = _settingsService.Current;
            s.OllamaEndpoint = _txtEndpoint.Text.Trim();
            s.OllamaModel = _cmbModel.Text.Trim();
            s.UseOllamaIfAvailable = _chkUseOllama.Checked;
            s.EnableBuiltInOcr = _chkEnableOcr.Checked;
            s.ChunkWordSize = (int)_numChunkSize.Value;

            s.ExcludedFolderNames = _txtExcludedFolders.Text
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();

            s.ExcludedExtensions = _txtExcludedExtensions.Text
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().StartsWith(".") ? x.Trim() : $".{x.Trim()}")
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();

            if (_ollamaService is OllamaClientService client)
            {
                client.SetModel(s.OllamaModel);
            }

            await _settingsService.SaveAsync();
            MessageBox.Show("Pengaturan berhasil disimpan!", "Sukses", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

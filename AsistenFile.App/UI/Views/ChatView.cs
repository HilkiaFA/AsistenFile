using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AsistenFile.App.UI;
using AsistenFile.App.UI.Components;
using AsistenFile.Core.AI;
using AsistenFile.Core.Models;

namespace AsistenFile.App.UI.Views
{
    public class ChatView : UserControl
    {
        private readonly IRagOrchestrator _ragOrchestrator;
        private readonly List<RagResponse> _conversationHistory = new();

        private Panel _chatScrollPanel = null!;
        private TextBox _txtQuery = null!;
        private Button _btnSend = null!;
        private Button _btnClear = null!;
        private Button _btnExport = null!;
        private Label _lblStatus = null!;
        private Panel _suggestedChipsPanel = null!;

        public ChatView(IRagOrchestrator ragOrchestrator)
        {
            _ragOrchestrator = ragOrchestrator;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.BackColor = ModernTheme.ContentBg;
            this.Dock = DockStyle.Fill;
            this.Font = ModernTheme.BodyFont;

            // 1. Bottom Input Container
            var bottomContainer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 100,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(20, 10, 20, 14)
            };
            bottomContainer.Paint += (s, e) =>
            {
                using var pen = new Pen(ModernTheme.BorderColor, 1);
                e.Graphics.DrawLine(pen, 0, 0, bottomContainer.Width, 0);
            };

            // Status label (e.g. "Menganalisis dokumen...")
            _lblStatus = new Label
            {
                Text = "💡 Tips: Anda bisa mencari berdasarkan tanggal ('tahun lalu', 'bulan Maret'), kata kunci, atau tipe file.",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.TextMuted,
                UseMnemonic = false,
                Dock = DockStyle.Top,
                Height = 22
            };

            // Input Row Panel
            var inputRow = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };

            _txtQuery = new TextBox
            {
                Font = new Font("Segoe UI", 11f, FontStyle.Regular),
                ForeColor = ModernTheme.TextPrimary,
                Dock = DockStyle.Fill,
                Height = 36
            };
            _txtQuery.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    _ = SendQueryAsync();
                }
            };

            _btnSend = new Button
            {
                Text = "🔍 Cari & Jawab",
                UseMnemonic = false,
                Size = new Size(135, 36),
                Dock = DockStyle.Right,
                Margin = new Padding(8, 0, 0, 0)
            };
            ModernTheme.StyleButton(_btnSend, isPrimary: true);
            _btnSend.Click += (s, e) => _ = SendQueryAsync();

            var buttonSpacer = new Panel { Dock = DockStyle.Right, Width = 8 };

            _btnClear = new Button
            {
                Text = "Bersihkan",
                UseMnemonic = false,
                Size = new Size(85, 36),
                Dock = DockStyle.Right
            };
            ModernTheme.StyleButton(_btnClear, isPrimary: false);
            _btnClear.Click += (s, e) => ClearChat();

            var buttonSpacer2 = new Panel { Dock = DockStyle.Right, Width = 8 };

            _btnExport = new Button
            {
                Text = "Ekspor",
                UseMnemonic = false,
                Size = new Size(75, 36),
                Dock = DockStyle.Right
            };
            ModernTheme.StyleButton(_btnExport, isPrimary: false);
            _btnExport.Click += (s, e) => ExportChat();

            inputRow.Controls.Add(_txtQuery);
            inputRow.Controls.Add(buttonSpacer);
            inputRow.Controls.Add(_btnSend);
            inputRow.Controls.Add(buttonSpacer2);
            inputRow.Controls.Add(_btnClear);
            inputRow.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
            inputRow.Controls.Add(_btnExport);

            bottomContainer.Controls.Add(inputRow);
            bottomContainer.Controls.Add(_lblStatus);

            // 2. Chat Scroll Area
            _chatScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ModernTheme.ContentBg,
                Padding = new Padding(24, 20, 24, 20)
            };

            // 3. Suggested Query Chips Panel (Welcome banner)
            _suggestedChipsPanel = CreateWelcomeBanner();
            _chatScrollPanel.Controls.Add(_suggestedChipsPanel);

            this.Controls.Add(_chatScrollPanel);
            this.Controls.Add(bottomContainer);
        }

        private Panel CreateWelcomeBanner()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(22),
                Margin = new Padding(0, 0, 0, 24)
            };
            ModernTheme.ApplyCardStyle(panel);

            var lblTitle = new Label
            {
                Text = "Selamat Datang di Asisten File & Dokumen Lokal",
                Font = ModernTheme.HeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                UseMnemonic = false,
                Dock = DockStyle.Top,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            var lblDesc = new Label
            {
                Text = "Tanyakan apapun seputar dokumen Anda dalam bahasa natural (PDF, Word, Excel, Email, Teks, Scan OCR).\n" +
                       "Semua proses berjalan 100% lokal di komputer Anda tanpa internet dan tanpa data keluar.",
                Font = ModernTheme.BodyFont,
                ForeColor = ModernTheme.TextSecondary,
                UseMnemonic = false,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 14)
            };

            var lblSampleHeader = new Label
            {
                Text = "💡 Contoh Pertanyaan Cepat (Klik untuk mencoba):",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.Primary,
                UseMnemonic = false,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 10)
            };

            var chipsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent
            };

            var samples = new[]
            {
                "cari kontrak sewa yang saya tanda tangani tahun lalu",
                "rangkum semua invoice bulan Maret",
                "cari bukti pembayaran atau kwitansi",
                "apa kewajiban penyewa dalam perjanjian kontrak sewa?",
                "cari dokumen proposal proyek terbaru"
            };

            foreach (var sample in samples)
            {
                var chip = new Button
                {
                    Text = $"💬 {sample}",
                    UseMnemonic = false,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 12, 12),
                    Padding = new Padding(12, 7, 12, 7)
                };
                ModernTheme.StyleButton(chip, isPrimary: false);
                chip.Click += (s, e) =>
                {
                    _txtQuery.Text = sample;
                    _ = SendQueryAsync();
                };
                chipsFlow.Controls.Add(chip);
            }

            panel.Controls.Add(chipsFlow);
            panel.Controls.Add(lblSampleHeader);
            panel.Controls.Add(lblDesc);
            panel.Controls.Add(lblTitle);

            // Dynamically calculate and enforce panel height so chips are NEVER cut off
            void UpdateBannerHeight()
            {
                int baseHeaderHeight = panel.Padding.Top + lblTitle.Height + 6 + lblDesc.Height + 14 + lblSampleHeader.Height + 10;
                int maxChildBottom = 0;
                foreach (Control c in chipsFlow.Controls)
                {
                    if (c.Bottom > maxChildBottom) maxChildBottom = c.Bottom;
                }
                int finalHeight = baseHeaderHeight + maxChildBottom + panel.Padding.Bottom + 16;
                panel.Height = Math.Max(260, finalHeight);
            }

            panel.Layout += (s, e) => UpdateBannerHeight();
            panel.Resize += (s, e) => UpdateBannerHeight();
            chipsFlow.Layout += (s, e) => UpdateBannerHeight();

            return panel;
        }

        private async Task SendQueryAsync()
        {
            var query = _txtQuery.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            _txtQuery.Clear();
            _btnSend.Enabled = false;
            _lblStatus.Text = "⏳ Sedang mencari dan menganalisis dokumen lokal...";
            _lblStatus.ForeColor = ModernTheme.Primary;

            try
            {
                var response = await _ragOrchestrator.AnswerAsync(query);
                _conversationHistory.Add(response);

                var bubble = new ChatBubbleControl(response);
                // Add to scroll panel
                _chatScrollPanel.Controls.Add(bubble);
                _chatScrollPanel.Controls.SetChildIndex(bubble, 0); // Put on top stack

                _chatScrollPanel.ScrollControlIntoView(bubble);

                _lblStatus.Text = $"✅ Jawaban selesai ({response.ProcessingTimeMs} ms) menggunakan {response.ModelUsed}.";
                _lblStatus.ForeColor = ModernTheme.AccentSuccess;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"❌ Gagal memproses: {ex.Message}";
                _lblStatus.ForeColor = ModernTheme.AccentDanger;
            }
            finally
            {
                _btnSend.Enabled = true;
                _txtQuery.Focus();
            }
        }

        private void ClearChat()
        {
            _conversationHistory.Clear();
            _chatScrollPanel.Controls.Clear();
            _chatScrollPanel.Controls.Add(_suggestedChipsPanel);
            _lblStatus.Text = "💡 Riwayat percakapan dibersihkan.";
            _lblStatus.ForeColor = ModernTheme.TextMuted;
        }

        private void ExportChat()
        {
            if (_conversationHistory.Count == 0)
            {
                MessageBox.Show("Belum ada riwayat tanya jawab untuk diekspor.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Markdown File (*.md)|*.md|Text File (*.txt)|*.txt",
                FileName = $"AsistenFile_Export_{DateTime.Now:yyyyMMdd_HHmm}.md"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# Laporan Tanya Jawab Dokumen Lokal");
                    sb.AppendLine($"Tanggal Ekspor: {DateTime.Now:dd MMMM yyyy HH:mm}\n");

                    int index = 1;
                    foreach (var item in _conversationHistory)
                    {
                        sb.AppendLine($"## #{index++} Pertanyaan: {item.Query}");
                        sb.AppendLine($"**Model:** {item.ModelUsed} ({item.ProcessingTimeMs} ms)");
                        sb.AppendLine();
                        sb.AppendLine("### Jawaban:");
                        sb.AppendLine(item.Answer);
                        sb.AppendLine();

                        if (item.Sources.Count > 0)
                        {
                            sb.AppendLine("### Sumber Terkait:");
                            foreach (var src in item.Sources)
                            {
                                sb.AppendLine($"- **{src.FileName}** ({src.SectionTitle}) - [Buka: {src.FilePath}]");
                                sb.AppendLine($"  > {src.Snippet.Replace("\n", " ")}");
                            }
                        }

                        sb.AppendLine("\n---\n");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                    MessageBox.Show("Riwayat berhasil diekspor!", "Sukses", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Tidak dapat menyimpan file:\n{ex.Message}\n\nPastikan file tidak sedang dibuka di aplikasi lain.", "File Terkunci", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal mengekspor riwayat: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;
using AsistenFile.App.UI;
using AsistenFile.Core.Models;

namespace AsistenFile.App.UI.Components
{
    public class ChatBubbleControl : UserControl
    {
        private readonly RagResponse _response;

        public ChatBubbleControl(RagResponse response)
        {
            _response = response;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.Transparent;
            this.Font = ModernTheme.BodyFont;
            this.Dock = DockStyle.Top;
            this.AutoSize = true;
            this.Margin = new Padding(0, 0, 0, 16);
            this.Padding = new Padding(10);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 3
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // 1. User Message Box
            var userBox = new Panel
            {
                BackColor = Color.FromArgb(238, 242, 255), // Indigo 50
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 10),
                AutoSize = true,
                Dock = DockStyle.Top
            };
            ModernTheme.ApplyCardStyle(userBox);

            var lblUserHeader = new Label
            {
                Text = "👤 Pertanyaan Anda:",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.Primary,
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 6)
            };

            var lblUserQuery = new Label
            {
                Text = _response.Query,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
                ForeColor = ModernTheme.TextPrimary,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 4, 0, 0)
            };

            userBox.Controls.Add(lblUserQuery);
            userBox.Controls.Add(lblUserHeader);

            // 2. Assistant Response Box
            var botBox = new Panel
            {
                BackColor = ModernTheme.CardBg,
                Padding = new Padding(14),
                Margin = new Padding(0, 0, 0, 10),
                AutoSize = true,
                Dock = DockStyle.Top
            };
            ModernTheme.ApplyCardStyle(botBox);

            var botHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.Transparent
            };

            var lblBotTitle = new Label
            {
                Text = "🤖 Asisten Dokumen",
                Font = ModernTheme.SubHeaderFont,
                ForeColor = ModernTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 4)
            };

            var lblModelBadge = new Label
            {
                Text = $"⚡ {_response.ModelUsed} ({_response.ProcessingTimeMs} ms)",
                Font = ModernTheme.SmallFont,
                ForeColor = ModernTheme.AccentSuccess,
                BackColor = Color.FromArgb(236, 253, 245),
                Padding = new Padding(4, 2, 4, 2),
                AutoSize = true,
                Location = new Point(160, 4)
            };

            botHeader.Controls.Add(lblModelBadge);
            botHeader.Controls.Add(lblBotTitle);

            // Answer Content (TextBox for auto-scroll and selectable text)
            var txtAnswer = new TextBox
            {
                Text = _response.Answer,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = ModernTheme.CardBg,
                ForeColor = ModernTheme.TextPrimary,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                Dock = DockStyle.Top,
                ScrollBars = ScrollBars.None,
                Margin = new Padding(0, 8, 0, 8)
            };
            // Measure height dynamically
            using (var g = txtAnswer.CreateGraphics())
            {
                var size = g.MeasureString(_response.Answer, txtAnswer.Font, 750);
                txtAnswer.Height = Math.Max(70, (int)size.Height + 25);
            }

            // Intent / analysis hint
            Label? lblIntent = null;
            if (!string.IsNullOrEmpty(_response.IntentExplanation))
            {
                lblIntent = new Label
                {
                    Text = $"🔍 Parameter Terdeteksi: {_response.IntentExplanation}",
                    Font = ModernTheme.SmallFont,
                    ForeColor = ModernTheme.TextMuted,
                    AutoSize = true,
                    Dock = DockStyle.Top,
                    Padding = new Padding(0, 4, 0, 6)
                };
            }

            // Bottom action panel for bot box
            var botActions = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.Transparent
            };

            var btnCopyAnswer = new Button
            {
                Text = "📋 Salin Jawaban",
                Size = new Size(115, 24),
                Location = new Point(0, 2)
            };
            ModernTheme.StyleButton(btnCopyAnswer, isPrimary: false);
            btnCopyAnswer.Click += (s, e) =>
            {
                Clipboard.SetText(_response.Answer);
                MessageBox.Show("Jawaban berhasil disalin!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            botActions.Controls.Add(btnCopyAnswer);

            botBox.Controls.Add(botActions);
            if (lblIntent != null) botBox.Controls.Add(lblIntent);
            botBox.Controls.Add(txtAnswer);
            botBox.Controls.Add(botHeader);

            // 3. Source Citations Panel
            var sourcesContainer = new Panel
            {
                BackColor = Color.Transparent,
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 10)
            };

            if (_response.Sources.Count > 0)
            {
                var lblSourcesHeader = new Label
                {
                    Text = $"📚 Sumber Dokumen Terkait ({_response.Sources.Count} Sitasi):",
                    Font = ModernTheme.SubHeaderFont,
                    ForeColor = ModernTheme.TextPrimary,
                    AutoSize = true,
                    Dock = DockStyle.Top,
                    Padding = new Padding(4, 0, 0, 6)
                };
                sourcesContainer.Controls.Add(lblSourcesHeader);

                // Add source cards in reverse order so DockStyle.Top stacks them correctly
                for (int i = _response.Sources.Count - 1; i >= 0; i--)
                {
                    var card = new SourceCardControl(_response.Sources[i]);
                    sourcesContainer.Controls.Add(card);
                }
            }

            mainLayout.Controls.Add(userBox);
            mainLayout.Controls.Add(botBox);
            mainLayout.Controls.Add(sourcesContainer);

            this.Controls.Add(mainLayout);
        }
    }
}

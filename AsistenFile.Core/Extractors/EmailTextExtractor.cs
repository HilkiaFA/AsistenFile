using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Models;
using MimeKit;

namespace AsistenFile.Core.Extractors
{
    public class EmailTextExtractor : ITextExtractor
    {
        public bool CanHandle(string extension)
        {
            return string.Equals(extension, ".eml", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".msg", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<ExtractedPage>> ExtractAsync(string filePath, CancellationToken ct = default)
        {
            var results = new List<ExtractedPage>();

            await Task.Run(() =>
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var message = MimeMessage.Load(stream);

                var sb = new StringBuilder();
                sb.AppendLine($"[Email / Pesan Masuk]");
                sb.AppendLine($"Subjek: {message.Subject}");
                sb.AppendLine($"Dari: {message.From}");
                sb.AppendLine($"Kepada: {message.To}");
                sb.AppendLine($"Tanggal: {message.Date:yyyy-MM-dd HH:mm:ss}");

                var attachmentNames = new List<string>();
                foreach (var attachment in message.Attachments)
                {
                    if (attachment is MimePart part && !string.IsNullOrWhiteSpace(part.FileName))
                    {
                        attachmentNames.Add(part.FileName);
                    }
                }

                if (attachmentNames.Count > 0)
                {
                    sb.AppendLine($"Lampiran: {string.Join(", ", attachmentNames)}");
                }

                sb.AppendLine();
                sb.AppendLine("--- Isi Email ---");

                var bodyText = message.TextBody;
                if (string.IsNullOrWhiteSpace(bodyText) && !string.IsNullOrWhiteSpace(message.HtmlBody))
                {
                    // Clean HTML tags to plain text
                    bodyText = System.Text.RegularExpressions.Regex.Replace(message.HtmlBody, "<.*?>", string.Empty);
                }

                sb.AppendLine(bodyText?.Trim() ?? "(Pesan kosong)");

                results.Add(new ExtractedPage
                {
                    PageNumber = 1,
                    SectionTitle = string.IsNullOrWhiteSpace(message.Subject) ? "Email" : message.Subject,
                    Content = sb.ToString().Trim()
                });
            }, ct);

            return results;
        }
    }
}

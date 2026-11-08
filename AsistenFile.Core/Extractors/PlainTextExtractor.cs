using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Models;

namespace AsistenFile.Core.Extractors
{
    public class PlainTextExtractor : ITextExtractor
    {
        private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".log", ".json", ".yaml", ".yml", ".ini", ".config", ".xml", ".html", ".htm"
        };

        public bool CanHandle(string extension)
        {
            return Supported.Contains(extension);
        }

        public async Task<List<ExtractedPage>> ExtractAsync(string filePath, CancellationToken ct = default)
        {
            var results = new List<ExtractedPage>();

            var text = await ReadFileWithEncodingFallbackAsync(filePath, ct);
            if (string.IsNullOrWhiteSpace(text)) return results;

            // Split into logical pages every ~400-500 words or by markdown header (# )
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            int pageNumber = 1;
            int wordCount = 0;
            string currentSection = "Halaman 1";

            foreach (var line in lines)
            {
                if (ct.IsCancellationRequested) break;

                if (line.StartsWith("# ") || line.StartsWith("## "))
                {
                    currentSection = line.TrimStart('#').Trim();
                }

                sb.AppendLine(line);
                wordCount += line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

                if (wordCount >= 450)
                {
                    results.Add(new ExtractedPage
                    {
                        PageNumber = pageNumber++,
                        SectionTitle = currentSection,
                        Content = sb.ToString().Trim()
                    });
                    sb.Clear();
                    wordCount = 0;
                }
            }

            if (sb.Length > 0)
            {
                results.Add(new ExtractedPage
                {
                    PageNumber = pageNumber,
                    SectionTitle = currentSection,
                    Content = sb.ToString().Trim()
                });
            }

            return results;
        }

        private static async Task<string> ReadFileWithEncodingFallbackAsync(string filePath, CancellationToken ct)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return await reader.ReadToEndAsync(ct);
            }
            catch
            {
                try
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream, Encoding.Default, detectEncodingFromByteOrderMarks: true);
                    return await reader.ReadToEndAsync(ct);
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }
}

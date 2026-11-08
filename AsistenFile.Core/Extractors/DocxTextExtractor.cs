using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AsistenFile.Core.Extractors
{
    public class DocxTextExtractor : ITextExtractor
    {
        public bool CanHandle(string extension)
        {
            return string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".docm", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<ExtractedPage>> ExtractAsync(string filePath, CancellationToken ct = default)
        {
            var results = new List<ExtractedPage>();

            await Task.Run(() =>
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var doc = WordprocessingDocument.Open(stream, false);
                var mainPart = doc.MainDocumentPart;
                if (mainPart?.Document?.Body == null) return;

                var sb = new StringBuilder();
                int pageEstimate = 1;
                int wordCountInCurrentPage = 0;

                foreach (var element in mainPart.Document.Body.Elements())
                {
                    if (ct.IsCancellationRequested) break;

                    if (element is Paragraph p)
                    {
                        var text = p.InnerText.Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            sb.AppendLine(text);
                            wordCountInCurrentPage += text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                        }
                    }
                    else if (element is Table table)
                    {
                        // Extract table rows cleanly
                        sb.AppendLine("\n[Tabel]");
                        foreach (var row in table.Elements<TableRow>())
                        {
                            var rowCells = new List<string>();
                            foreach (var cell in row.Elements<TableCell>())
                            {
                                rowCells.Add(cell.InnerText.Trim());
                            }
                            sb.AppendLine(string.Join(" | ", rowCells));
                            wordCountInCurrentPage += 15;
                        }
                        sb.AppendLine();
                    }

                    // Estimate logical pages around ~450 words per page if no explicit breaks
                    if (wordCountInCurrentPage > 450)
                    {
                        var pageContent = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(pageContent))
                        {
                            results.Add(new ExtractedPage
                            {
                                PageNumber = pageEstimate++,
                                SectionTitle = $"Bagian {pageEstimate - 1}",
                                Content = pageContent
                            });
                        }
                        sb.Clear();
                        wordCountInCurrentPage = 0;
                    }
                }

                if (sb.Length > 0)
                {
                    results.Add(new ExtractedPage
                    {
                        PageNumber = pageEstimate,
                        SectionTitle = $"Bagian {pageEstimate}",
                        Content = sb.ToString().Trim()
                    });
                }
            }, ct);

            return results;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Models;
using ExcelDataReader;

namespace AsistenFile.Core.Extractors
{
    public class ExcelTextExtractor : ITextExtractor
    {
        static ExcelTextExtractor()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public bool CanHandle(string extension)
        {
            return string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<ExtractedPage>> ExtractAsync(string filePath, CancellationToken ct = default)
        {
            var results = new List<ExtractedPage>();

            await Task.Run(() =>
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var ext = Path.GetExtension(filePath);
                using var reader = string.Equals(ext, ".csv", StringComparison.OrdinalIgnoreCase)
                    ? ExcelReaderFactory.CreateCsvReader(stream)
                    : ExcelReaderFactory.CreateReader(stream);

                int sheetIndex = 1;

                do
                {
                    if (ct.IsCancellationRequested) break;

                    string sheetName = reader.Name ?? $"Sheet{sheetIndex}";
                    var sb = new StringBuilder();
                    sb.AppendLine($"[Lembar Kerja: {sheetName}]");

                    var headers = new List<string>();
                    bool isHeaderRow = true;
                    int rowNumber = 0;

                    while (reader.Read())
                    {
                        if (ct.IsCancellationRequested) break;
                        rowNumber++;

                        int fieldCount = reader.FieldCount;
                        if (fieldCount == 0) continue;

                        var cellValues = new List<string>();
                        bool hasNonEmptyCell = false;

                        for (int i = 0; i < fieldCount; i++)
                        {
                            var val = reader.GetValue(i)?.ToString()?.Trim() ?? string.Empty;
                            cellValues.Add(val);
                            if (!string.IsNullOrEmpty(val)) hasNonEmptyCell = true;
                        }

                        if (!hasNonEmptyCell) continue;

                        if (isHeaderRow)
                        {
                            headers = new List<string>(cellValues);
                            isHeaderRow = false;
                            sb.AppendLine("Kolom: " + string.Join(" | ", headers));
                            continue;
                        }

                        // Format data row with column associations for optimal RAG retrieval
                        var rowBuilder = new StringBuilder();
                        rowBuilder.Append($"Baris {rowNumber}: ");
                        var items = new List<string>();

                        for (int i = 0; i < cellValues.Count; i++)
                        {
                            if (string.IsNullOrEmpty(cellValues[i])) continue;
                            string header = (i < headers.Count && !string.IsNullOrWhiteSpace(headers[i]))
                                ? headers[i]
                                : $"Kolom {i + 1}";
                            items.Add($"{header}: {cellValues[i]}");
                        }

                        if (items.Count > 0)
                        {
                            sb.AppendLine(string.Join(", ", items));
                        }
                    }

                    var content = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        results.Add(new ExtractedPage
                        {
                            PageNumber = sheetIndex,
                            SectionTitle = $"Lembar '{sheetName}'",
                            Content = content
                        });
                    }

                    sheetIndex++;
                } while (reader.NextResult());
            }, ct);

            return results;
        }
    }
}

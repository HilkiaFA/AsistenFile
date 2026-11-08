using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Models;
using AsistenFile.Core.Storage;

namespace AsistenFile.Core.Features
{
    public interface IDeadlineTrackerService
    {
        Task<List<DocumentDeadlineItem>> ScanDeadlinesAsync(CancellationToken ct = default);
        List<DocumentDeadlineItem> FilterDeadlines(List<DocumentDeadlineItem> items, string filter);
        List<DocumentDeadlineItem> GetCriticalUpcomingDeadlines(List<DocumentDeadlineItem> items, int thresholdDays = 30);
    }

    public class DeadlineTrackerService : IDeadlineTrackerService
    {
        private readonly IDatabaseService _databaseService;

        private static readonly Dictionary<string, int> IndonesianMonths = new(StringComparer.OrdinalIgnoreCase)
        {
            { "januari", 1 }, { "jan", 1 },
            { "februari", 2 }, { "feb", 2 },
            { "maret", 3 }, { "mar", 3 },
            { "april", 4 }, { "apr", 4 },
            { "mei", 5 }, { "may", 5 },
            { "juni", 6 }, { "jun", 6 },
            { "juli", 7 }, { "jul", 7 },
            { "agustus", 8 }, { "agu", 8 }, { "aug", 8 },
            { "september", 9 }, { "sep", 9 },
            { "oktober", 10 }, { "okt", 10 }, { "oct", 10 },
            { "november", 11 }, { "nov", 11 },
            { "desember", 12 }, { "des", 12 }, { "dec", 12 }
        };

        // Regex pattern to capture deadline trigger keywords followed by a date
        private static readonly Regex DeadlineRegex = new Regex(
            @"(?:jatuh\s+tempo|berlaku\s+sampai|masa\s+berlaku\s+hingga|berakhir\s+pada|tanggal\s+kedaluwarsa|sampai\s+dengan\s+tanggal|periode\s+berakhir|due\s+date|expires?\s+on|valid\s+until|expiration\s+date)\s*[:=–-]?\s*([0-9]{1,2}(?:st|nd|rd|th)?[\s\/\-\.](?:[a-zA-Z]+|[0-9]{1,2})[\s\/\-\.][0-9]{2,4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public DeadlineTrackerService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<DocumentDeadlineItem>> ScanDeadlinesAsync(CancellationToken ct = default)
        {
            var results = new List<DocumentDeadlineItem>();
            var chunks = await _databaseService.GetAllChunksForVectorSearchAsync();

            var seenDocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var chunk in chunks)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrWhiteSpace(chunk.Content)) continue;

                var matches = DeadlineRegex.Matches(chunk.Content);
                foreach (Match m in matches)
                {
                    if (m.Groups.Count > 1)
                    {
                        var dateStr = m.Groups[1].Value.Trim();
                        if (TryParseAnyDate(dateStr, out DateTime parsedDate))
                        {
                            // Avoid duplicate alerts for the same file and same date
                            var key = $"{chunk.FilePath}_{parsedDate:yyyyMMdd}";
                            if (seenDocs.Contains(key)) continue;
                            seenDocs.Add(key);

                            var category = DetermineCategory(chunk.FileName, chunk.Content);
                            var snippet = ExtractSnippetAroundMatch(chunk.Content, m.Index, m.Length);

                            results.Add(new DocumentDeadlineItem
                            {
                                FileId = chunk.FileId,
                                FilePath = chunk.FilePath,
                                FileName = chunk.FileName,
                                Extension = chunk.Extension,
                                DocumentCategory = category,
                                DueDate = parsedDate,
                                Snippet = snippet,
                                SectionTitle = chunk.SectionTitle
                            });
                        }
                    }
                }
            }

            // Order by urgency (overdue & nearest deadlines first)
            return results.OrderBy(x => x.DueDate).ToList();
        }

        public List<DocumentDeadlineItem> FilterDeadlines(List<DocumentDeadlineItem> items, string filter)
        {
            return (filter?.ToLowerInvariant()) switch
            {
                "kritis" or "segera" => items.Where(x => x.DaysRemaining >= 0 && x.DaysRemaining <= 30).ToList(),
                "lewat" or "overdue" => items.Where(x => x.DaysRemaining < 0).ToList(),
                "mendatang" or "normal" => items.Where(x => x.DaysRemaining > 30).ToList(),
                "kontrak" => items.Where(x => x.DocumentCategory.Equals("Kontrak", StringComparison.OrdinalIgnoreCase)).ToList(),
                "invoice" => items.Where(x => x.DocumentCategory.Equals("Invoice", StringComparison.OrdinalIgnoreCase)).ToList(),
                _ => items
            };
        }

        public List<DocumentDeadlineItem> GetCriticalUpcomingDeadlines(List<DocumentDeadlineItem> items, int thresholdDays = 30)
        {
            return items.Where(x => x.DaysRemaining >= 0 && x.DaysRemaining <= thresholdDays).OrderBy(x => x.DueDate).ToList();
        }

        private static string DetermineCategory(string fileName, string content)
        {
            var combined = $"{fileName} {content}".ToLowerInvariant();

            if (combined.Contains("sewa") || combined.Contains("kontrak") || combined.Contains("perjanjian") || combined.Contains("pks"))
                return "Kontrak";
            if (combined.Contains("invoice") || combined.Contains("tagihan") || combined.Contains("kwitansi") || combined.Contains("faktur"))
                return "Invoice";
            if (combined.Contains("domain") || combined.Contains("hosting") || combined.Contains("lisensi") || combined.Contains("langganan"))
                return "Langganan";
            if (combined.Contains("polis") || combined.Contains("asuransi") || combined.Contains("garansi"))
                return "Polis/Garansi";

            return "Umum";
        }

        private static string ExtractSnippetAroundMatch(string text, int index, int length)
        {
            int start = Math.Max(0, index - 60);
            int end = Math.Min(text.Length, index + length + 80);
            var sub = text.Substring(start, end - start).Trim().Replace("\r", " ").Replace("\n", " ");
            if (start > 0) sub = "..." + sub;
            if (end < text.Length) sub += "...";
            return sub;
        }

        private static bool TryParseAnyDate(string raw, out DateTime result)
        {
            result = DateTime.MinValue;
            var clean = raw.Replace("st", "").Replace("nd", "").Replace("rd", "").Replace("th", "").Trim();

            // Try standard culture parsers
            if (DateTime.TryParse(clean, new CultureInfo("id-ID"), DateTimeStyles.None, out result))
                return IsReasonableYear(result.Year);

            if (DateTime.TryParse(clean, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return IsReasonableYear(result.Year);

            // Indonesian month name parser: "15 Maret 2026"
            var parts = clean.Split(new[] { ' ', '/', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3)
            {
                if (int.TryParse(parts[0], out int day) && int.TryParse(parts[2], out int year))
                {
                    if (year < 100) year += 2000;
                    if (IndonesianMonths.TryGetValue(parts[1], out int month))
                    {
                        try
                        {
                            result = new DateTime(year, month, day);
                            return IsReasonableYear(year);
                        }
                        catch { }
                    }
                }
            }

            return false;
        }

        private static bool IsReasonableYear(int year)
        {
            // Filter out unrealistic dates (e.g. historical dates or typo numbers)
            return year >= 2020 && year <= 2040;
        }
    }
}

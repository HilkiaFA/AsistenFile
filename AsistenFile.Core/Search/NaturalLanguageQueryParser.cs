using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AsistenFile.Core.Models;

namespace AsistenFile.Core.Search
{
    public interface IQueryParser
    {
        SearchQuery Parse(string rawQuery);
    }

    public class NaturalLanguageQueryParser : IQueryParser
    {
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

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "cari", "carikan", "tolong", "bisa", "tolonglah", "temukan", "tampilkan", "lihat", "lihatkan",
            "apakah", "apa", "ada", "yang", "di", "ke", "dari", "pada", "untuk", "dengan", "dan", "atau",
            "saya", "kamu", "anda", "dia", "mereka", "kita", "kami", "ini", "itu", "tersebut",
            "semua", "seluruh", "setiap", "koleksi", "dokumen", "berkas", "file", "arsip",
            "please", "find", "search", "show", "get", "summarize", "all", "the", "in", "of", "from"
        };

        public SearchQuery Parse(string rawQuery)
        {
            var query = new SearchQuery
            {
                RawQuery = rawQuery?.Trim() ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(query.RawQuery))
            {
                return query;
            }

            var lower = query.RawQuery.ToLowerInvariant();

            // 1. Detect summarize intent
            if (lower.Contains("rangkum") || lower.Contains("ringkas") || lower.Contains("summarize") || lower.Contains("rekap") || lower.Contains("rekapitulasi"))
            {
                query.IsSummarizeRequest = true;
            }

            // 2. Detect Date filters
            var now = DateTime.Now;

            // "tahun lalu" / "last year"
            if (lower.Contains("tahun lalu") || lower.Contains("last year"))
            {
                int prevYear = now.Year - 1;
                query.DateFrom = new DateTime(prevYear, 1, 1, 0, 0, 0);
                query.DateTo = new DateTime(prevYear, 12, 31, 23, 59, 59);
            }
            // "tahun ini" / "this year"
            else if (lower.Contains("tahun ini") || lower.Contains("this year"))
            {
                query.DateFrom = new DateTime(now.Year, 1, 1, 0, 0, 0);
                query.DateTo = new DateTime(now.Year, 12, 31, 23, 59, 59);
            }
            // "bulan lalu" / "last month"
            else if (lower.Contains("bulan lalu") || lower.Contains("last month"))
            {
                var prevMonthDate = now.AddMonths(-1);
                query.DateFrom = new DateTime(prevMonthDate.Year, prevMonthDate.Month, 1);
                query.DateTo = query.DateFrom.Value.AddMonths(1).AddSeconds(-1);
            }
            // "minggu lalu" / "last week"
            else if (lower.Contains("minggu lalu") || lower.Contains("last week"))
            {
                var lastWeek = now.AddDays(-7);
                query.DateFrom = lastWeek.Date;
                query.DateTo = now.Date.AddDays(1).AddSeconds(-1);
            }
            else
            {
                // Detect specific month: e.g. "bulan maret", "bulan april 2024"
                var monthMatch = Regex.Match(lower, @"bulan\s+([a-z]+)(\s+(\d{4}))?", RegexOptions.IgnoreCase);
                if (monthMatch.Success)
                {
                    var mName = monthMatch.Groups[1].Value;
                    int targetYear = now.Year;
                    if (monthMatch.Groups[3].Success)
                    {
                        int.TryParse(monthMatch.Groups[3].Value, out targetYear);
                    }

                    if (IndonesianMonths.TryGetValue(mName, out int monthNum))
                    {
                        query.DateFrom = new DateTime(targetYear, monthNum, 1);
                        query.DateTo = query.DateFrom.Value.AddMonths(1).AddSeconds(-1);
                    }
                }
                else
                {
                    // Detect specific year like "2023", "2024", "2025"
                    var yearMatch = Regex.Match(lower, @"\b(20\d{2})\b");
                    if (yearMatch.Success)
                    {
                        if (int.TryParse(yearMatch.Groups[1].Value, out int year))
                        {
                            query.DateFrom = new DateTime(year, 1, 1);
                            query.DateTo = new DateTime(year, 12, 31, 23, 59, 59);
                        }
                    }
                }
            }

            // 3. Detect File Type filters
            if (lower.Contains("pdf")) query.FileTypeFilter = ".pdf";
            else if (lower.Contains("word") || lower.Contains("docx") || lower.Contains("dokumen word")) query.FileTypeFilter = ".docx";
            else if (lower.Contains("excel") || lower.Contains("spreadsheet") || lower.Contains("xlsx") || lower.Contains("csv") || lower.Contains("tabel")) query.FileTypeFilter = ".xlsx";
            else if (lower.Contains("email") || lower.Contains("surat masuk") || lower.Contains("eml")) query.FileTypeFilter = ".eml";
            else if (lower.Contains("catatan") || lower.Contains("notes") || lower.Contains("markdown") || lower.Contains("teks")) query.FileTypeFilter = ".txt";

            // 4. Extract cleaned keywords for BM25/FTS search
            var tokens = lower.Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']' },
                StringSplitOptions.RemoveEmptyEntries);

            var meaningfulWords = new List<string>();
            foreach (var token in tokens)
            {
                if (StopWords.Contains(token)) continue;
                if (token.Length < 2) continue;
                // Exclude temporal filter words from keywords to avoid overconstraining text matches
                if (token == "tahun" || token == "lalu" || token == "bulan" || token == "minggu" || token == "ini" || token == "kemarin") continue;
                if (IndonesianMonths.ContainsKey(token)) continue;
                if (Regex.IsMatch(token, @"^20\d{2}$")) continue;

                meaningfulWords.Add(token);
            }

            query.CleanedKeywords = string.Join(" ", meaningfulWords);
            if (string.IsNullOrWhiteSpace(query.CleanedKeywords))
            {
                query.CleanedKeywords = query.RawQuery;
            }

            return query;
        }
    }
}

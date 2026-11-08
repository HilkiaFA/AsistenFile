using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.AI;
using AsistenFile.Core.Extractors;
using AsistenFile.Core.Models;

namespace AsistenFile.Core.Features
{
    public interface IDocumentDiffService
    {
        Task<DocumentDiffResult> CompareDocumentsAsync(string fileAPath, string fileBPath, CancellationToken ct = default);
    }

    public class DocumentDiffService : IDocumentDiffService
    {
        private readonly IExtractorFactory _extractorFactory;
        private readonly ILanguageModelService _ollamaService;

        private static readonly Regex MoneyRegex = new Regex(
            @"(?:Rp\.?|IDR)?\s*([0-9]{1,3}(?:\.[0-9]{3})+(?:,[0-9]{2})?|[0-9]{5,})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DurationRegex = new Regex(
            @"\b([0-9]+)\s*(?:hari|minggu|bulan|tahun|days|weeks|months|years)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ArticleRegex = new Regex(
            @"(?:^|\n)\s*(?:Pasal\s*[0-9]+|BAB\s*[IVXLCDM]+|Article\s*[0-9]+|Section\s*[0-9]+)[^\n]*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public DocumentDiffService(IExtractorFactory extractorFactory, ILanguageModelService ollamaService)
        {
            _extractorFactory = extractorFactory;
            _ollamaService = ollamaService;
        }

        public async Task<DocumentDiffResult> CompareDocumentsAsync(string fileAPath, string fileBPath, CancellationToken ct = default)
        {
            if (!File.Exists(fileAPath)) throw new FileNotFoundException($"File A tidak ditemukan: {fileAPath}");
            if (!File.Exists(fileBPath)) throw new FileNotFoundException($"File B tidak ditemukan: {fileBPath}");

            var extractorA = _extractorFactory.GetExtractor(fileAPath) ?? throw new Exception("Format File A tidak didukung.");
            var extractorB = _extractorFactory.GetExtractor(fileBPath) ?? throw new Exception("Format File B tidak didukung.");

            var pagesA = await extractorA.ExtractAsync(fileAPath, ct);
            var pagesB = await extractorB.ExtractAsync(fileBPath, ct);

            var textA = string.Join("\n\n", pagesA.Select(p => p.Content)).Trim();
            var textB = string.Join("\n\n", pagesB.Select(p => p.Content)).Trim();

            var result = new DocumentDiffResult
            {
                FileAPath = fileAPath,
                FileBPath = fileBPath,
                FileAName = Path.GetFileName(fileAPath),
                FileBName = Path.GetFileName(fileBPath)
            };

            // 1. Check if Ollama LLM is available for rich comparative reasoning
            bool isOllamaReady = await _ollamaService.IsAvailableAsync(ct);
            if (isOllamaReady)
            {
                try
                {
                    var prompt = $@"Bandingkan kedua dokumen ini secara teliti dan temukan perbedaannya:
DOKUMEN A (VERSI LAMA):
{Truncate(textA, 3500)}

DOKUMEN B (VERSI BARU / REVISI):
{Truncate(textB, 3500)}

TUGAS ANDA:
Rangkum perbedaan utama dalam format ringkas:
1. Perubahan Biaya / Nilai Uang (sebutkan angka lama vs angka baru)
2. Perubahan Jangka Waktu / Durasi / Jadwal
3. Klausul atau Pasal yang diubah, dihapus, atau baru ditambahkan
Berikan jawaban dalam bahasa Indonesia yang terstruktur dan lugas.";

                    var aiAnalysis = await _ollamaService.GenerateAnswerAsync(prompt, "Analisis Perbandingan Dokumen Hukum & Kontrak", ct);
                    result.ExecutiveSummary = aiAnalysis;
                }
                catch
                {
                    // Fall back to rule-based comparison
                }
            }

            // 2. Perform Deterministic Structural & Numerical Diff (always runs for structured change badges)
            PerformDeterministicDiff(textA, textB, result);

            return result;
        }

        private void PerformDeterministicDiff(string textA, string textB, DocumentDiffResult result)
        {
            var changes = new List<DiffChangeItem>();

            // A. Compare Financial / Amounts
            var amountsA = ExtractDistinctAmounts(textA);
            var amountsB = ExtractDistinctAmounts(textB);

            var removedAmounts = amountsA.Except(amountsB).ToList();
            var addedAmounts = amountsB.Except(amountsA).ToList();

            var idCulture = new System.Globalization.CultureInfo("id-ID");

            if (removedAmounts.Count > 0 && addedAmounts.Count > 0)
            {
                int pairCount = Math.Min(removedAmounts.Count, addedAmounts.Count);
                for (int i = 0; i < pairCount; i++)
                {
                    var oldAmt = removedAmounts[i];
                    var newAmt = addedAmounts[i];
                    string direction = newAmt > oldAmt ? "Naik" : "Turun";
                    decimal diff = Math.Abs(newAmt - oldAmt);

                    changes.Add(new DiffChangeItem
                    {
                        Category = DiffCategory.Financial,
                        ChangeType = DiffType.Modified,
                        Title = $"Perubahan Nilai / Biaya ({direction})",
                        OldValue = $"Rp {oldAmt.ToString("N0", idCulture)}",
                        NewValue = $"Rp {newAmt.ToString("N0", idCulture)}",
                        Description = $"Nilai berubah dari Rp {oldAmt.ToString("N0", idCulture)} menjadi Rp {newAmt.ToString("N0", idCulture)} (Selisih {direction}: Rp {diff.ToString("N0", idCulture)})."
                    });
                }
            }
            else if (addedAmounts.Count > 0)
            {
                foreach (var a in addedAmounts.Take(2))
                {
                    changes.Add(new DiffChangeItem
                    {
                        Category = DiffCategory.Financial,
                        ChangeType = DiffType.Added,
                        Title = "Penambahan Nilai / Biaya Baru",
                        OldValue = "-",
                        NewValue = $"Rp {a.ToString("N0", idCulture)}",
                        Description = $"Ditemukan klausul nilai biaya baru sebesar Rp {a.ToString("N0", idCulture)}."
                    });
                }
            }

            // B. Compare Durations / Timeframes
            var durationsA = DurationRegex.Matches(textA).Select(m => m.Value.ToLowerInvariant()).Distinct().ToList();
            var durationsB = DurationRegex.Matches(textB).Select(m => m.Value.ToLowerInvariant()).Distinct().ToList();

            var removedDurations = durationsA.Except(durationsB).ToList();
            var addedDurations = durationsB.Except(durationsA).ToList();

            if (removedDurations.Count > 0 && addedDurations.Count > 0)
            {
                changes.Add(new DiffChangeItem
                {
                    Category = DiffCategory.Duration,
                    ChangeType = DiffType.Modified,
                    Title = "Perubahan Jangka Waktu / Durasi",
                    OldValue = string.Join(", ", removedDurations),
                    NewValue = string.Join(", ", addedDurations),
                    Description = $"Jangka waktu berubah dari '{string.Join(", ", removedDurations)}' menjadi '{string.Join(", ", addedDurations)}'."
                });
            }

            // C. Compare Articles / Clauses (Pasal-pasal)
            var articlesA = ExtractArticles(textA);
            var articlesB = ExtractArticles(textB);

            foreach (var artKey in articlesA.Keys)
            {
                if (!articlesB.ContainsKey(artKey))
                {
                    changes.Add(new DiffChangeItem
                    {
                        Category = DiffCategory.Clause,
                        ChangeType = DiffType.Removed,
                        Title = $"Penghapusan: {artKey}",
                        OldValue = Truncate(articlesA[artKey], 120),
                        NewValue = "(Dihapus dari dokumen revisi)",
                        Description = $"Pasal/klausul '{artKey}' yang ada di versi lama telah dihapus pada versi revisi."
                    });
                }
                else
                {
                    // Check if content modified significantly
                    var bodyA = articlesA[artKey];
                    var bodyB = articlesB[artKey];
                    if (!string.Equals(bodyA, bodyB, StringComparison.OrdinalIgnoreCase))
                    {
                        changes.Add(new DiffChangeItem
                        {
                            Category = DiffCategory.Clause,
                            ChangeType = DiffType.Modified,
                            Title = $"Modifikasi Klausul: {artKey}",
                            OldValue = Truncate(bodyA, 140),
                            NewValue = Truncate(bodyB, 140),
                            Description = $"Redaksi atau ketentuan pada '{artKey}' mengalami penyesuaian."
                        });
                    }
                }
            }

            foreach (var artKey in articlesB.Keys)
            {
                if (!articlesA.ContainsKey(artKey))
                {
                    changes.Add(new DiffChangeItem
                    {
                        Category = DiffCategory.Clause,
                        ChangeType = DiffType.Added,
                        Title = $"Klausul Baru: {artKey}",
                        OldValue = "(Belum ada di dokumen lama)",
                        NewValue = Truncate(articlesB[artKey], 140),
                        Description = $"Klausul/pasal baru '{artKey}' ditambahkan pada dokumen revisi."
                    });
                }
            }

            result.Changes = changes;

            // Generate fallback executive summary if Ollama was offline
            if (string.IsNullOrWhiteSpace(result.ExecutiveSummary))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"📊 **Hasil Analisis Perbandingan Dokumen:**");
                sb.AppendLine($"• **Dokumen Lama (A):** {result.FileAName}");
                sb.AppendLine($"• **Dokumen Revisi (B):** {result.FileBName}");
                sb.AppendLine($"• **Total Perbedaan Terdeteksi:** {changes.Count} perubahan signifikan.\n");

                if (changes.Count == 0)
                {
                    sb.AppendLine("Kedua dokumen identik atau tidak ditemukan perbedaan klausul maupun angka yang signifikan.");
                }
                else
                {
                    foreach (var c in changes)
                    {
                        string icon = c.Category switch
                        {
                            DiffCategory.Financial => "💰",
                            DiffCategory.Duration => "⏳",
                            DiffCategory.Clause => "📜",
                            _ => "📌"
                        };
                        sb.AppendLine($"{icon} **{c.Title}**");
                        sb.AppendLine($"   {c.Description}");
                        sb.AppendLine();
                    }
                }

                result.ExecutiveSummary = sb.ToString().Trim();
            }
        }

        private static List<decimal> ExtractDistinctAmounts(string text)
        {
            var amounts = new List<decimal>();
            var matches = MoneyRegex.Matches(text);
            foreach (Match m in matches)
            {
                var raw = m.Groups[1].Value.Replace(".", "").Replace(",", ".");
                if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
                {
                    if (val >= 10000 && !amounts.Contains(val))
                    {
                        amounts.Add(val);
                    }
                }
            }
            return amounts.OrderBy(x => x).ToList();
        }

        private static Dictionary<string, string> ExtractArticles(string text)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var matches = ArticleRegex.Matches(text);

            for (int i = 0; i < matches.Count; i++)
            {
                var header = matches[i].Value.Trim();
                int start = matches[i].Index + matches[i].Length;
                int end = (i + 1 < matches.Count) ? matches[i + 1].Index : text.Length;

                var body = text.Substring(start, end - start).Trim();
                dict[header] = body;
            }

            return dict;
        }

        private static string Truncate(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length > maxChars ? clean.Substring(0, maxChars - 3) + "..." : clean;
        }
    }
}

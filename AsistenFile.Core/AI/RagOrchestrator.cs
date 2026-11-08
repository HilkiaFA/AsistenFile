using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Config;
using AsistenFile.Core.Models;
using AsistenFile.Core.Search;

namespace AsistenFile.Core.AI
{
    public interface IRagOrchestrator
    {
        Task<RagResponse> AnswerAsync(string userQuestion, CancellationToken ct = default);
    }

    public class RagOrchestrator : IRagOrchestrator
    {
        private readonly IQueryParser _queryParser;
        private readonly IHybridSearchEngine _searchEngine;
        private readonly ILanguageModelService _ollamaService;
        private readonly ILocalSynthesizer _localSynthesizer;
        private readonly ISettingsService _settingsService;

        public RagOrchestrator(
            IQueryParser queryParser,
            IHybridSearchEngine searchEngine,
            ILanguageModelService ollamaService,
            ILocalSynthesizer localSynthesizer,
            ISettingsService settingsService)
        {
            _queryParser = queryParser;
            _searchEngine = searchEngine;
            _ollamaService = ollamaService;
            _localSynthesizer = localSynthesizer;
            _settingsService = settingsService;
        }

        public async Task<RagResponse> AnswerAsync(string userQuestion, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            // 1. Parse natural language query (intent, dates, file types, keywords)
            var parsedQuery = _queryParser.Parse(userQuestion);

            // 2. Hybrid search retrieval & re-ranking
            var settings = _settingsService.Current;
            var sources = await _searchEngine.SearchAsync(parsedQuery, settings.HybridKeywordWeight, ct);

            // Explain intent to user for full transparency
            var intentExplanation = BuildIntentExplanation(parsedQuery);

            // If no relevant documents found at all (honest grounded negative)
            if (sources.Count == 0 || sources[0].RelevanceScore < 0.28f)
            {
                sw.Stop();
                return new RagResponse
                {
                    Query = userQuestion,
                    Answer = $"Dokumen terkait tidak ditemukan dalam indeks lokal.\n\n" +
                             $"Pertanyaan Anda dianalisis sebagai:\n" +
                             $"{intentExplanation}\n\n" +
                             $"Silakan periksa apakah folder yang berisi file tersebut sudah ditambahkan pada menu 'Kelola Folder' dan status indeksasi telah selesai.",
                    Sources = sources,
                    IsGrounded = false,
                    ProcessingTimeMs = sw.ElapsedMilliseconds,
                    ModelUsed = "Sistem Verifikasi Lokal",
                    IntentExplanation = intentExplanation
                };
            }

            string answerText;
            string modelName;

            // 3. Check if local Ollama LLM is available and user enabled it
            bool ollamaAvailable = settings.UseOllamaIfAvailable && await _ollamaService.IsAvailableAsync(ct);

            if (ollamaAvailable)
            {
                try
                {
                    // Build context string from top 4 chunks
                    var contextBuilder = new StringBuilder();
                    foreach (var src in sources.Take(4))
                    {
                        contextBuilder.AppendLine($"[Dokumen: {src.FileName} | Halaman/Bagian: {src.SectionTitle} | Tanggal File: {src.FileLastModified:yyyy-MM-dd}]");
                        contextBuilder.AppendLine(src.Snippet);
                        contextBuilder.AppendLine();
                    }

                    answerText = await _ollamaService.GenerateAnswerAsync(userQuestion, contextBuilder.ToString(), ct);
                    modelName = _ollamaService.ServiceName;
                }
                catch
                {
                    // Fall back to local extractive synthesizer if Ollama call encounters timeout or error
                    answerText = _localSynthesizer.SynthesizeAnswer(userQuestion, parsedQuery, sources);
                    modelName = "Local Extractive Synthesizer (Fallback)";
                }
            }
            else
            {
                // High-speed, 100% offline local extractive synthesizer
                answerText = _localSynthesizer.SynthesizeAnswer(userQuestion, parsedQuery, sources);
                modelName = "Local Extractive AI (100% On-Device Offline)";
            }

            sw.Stop();

            return new RagResponse
            {
                Query = userQuestion,
                Answer = answerText,
                Sources = sources,
                IsGrounded = true,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                ModelUsed = modelName,
                IntentExplanation = intentExplanation
            };
        }

        private static string BuildIntentExplanation(SearchQuery query)
        {
            var parts = new StringBuilder();
            parts.Append($"Kata kunci: '{query.CleanedKeywords}'");

            if (query.DateFrom.HasValue && query.DateTo.HasValue)
            {
                parts.Append($" | Rentang Waktu: {query.DateFrom.Value:dd/MM/yyyy} s/d {query.DateTo.Value:dd/MM/yyyy}");
            }

            if (!string.IsNullOrEmpty(query.FileTypeFilter))
            {
                parts.Append($" | Tipe File: {query.FileTypeFilter.ToUpperInvariant()}");
            }

            if (query.IsSummarizeRequest)
            {
                parts.Append(" | Mode: Rangkuman & Rekapitulasi");
            }

            return parts.ToString();
        }
    }
}

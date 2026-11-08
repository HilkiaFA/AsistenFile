using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Embeddings;
using AsistenFile.Core.Models;
using AsistenFile.Core.Storage;

namespace AsistenFile.Core.Search
{
    public interface IHybridSearchEngine
    {
        Task<List<SearchResultItem>> SearchAsync(SearchQuery query, float hybridWeight = 0.5f, CancellationToken ct = default);
    }

    public class HybridSearchEngine : IHybridSearchEngine
    {
        private readonly IDatabaseService _databaseService;
        private readonly IEmbeddingService _embeddingService;

        public HybridSearchEngine(IDatabaseService databaseService, IEmbeddingService embeddingService)
        {
            _databaseService = databaseService;
            _embeddingService = embeddingService;
        }

        public async Task<List<SearchResultItem>> SearchAsync(SearchQuery query, float hybridWeight = 0.5f, CancellationToken ct = default)
        {
            var scoredChunks = new Dictionary<long, (DocumentChunk chunk, float ftsScore, float vectorScore)>();

            // 1. Keyword search via FTS
            var ftsChunks = await _databaseService.SearchFtsAsync(
                query.CleanedKeywords,
                limit: 30,
                from: query.DateFrom,
                to: query.DateTo,
                ext: query.FileTypeFilter
            );

            // If strict filter yielded nothing, try without date/ext constraints
            if (ftsChunks.Count == 0 && (query.DateFrom.HasValue || !string.IsNullOrEmpty(query.FileTypeFilter)))
            {
                ftsChunks = await _databaseService.SearchFtsAsync(query.CleanedKeywords, limit: 30);
            }

            for (int i = 0; i < ftsChunks.Count; i++)
            {
                var chunk = ftsChunks[i];
                // Rank-based decay score from 1.0 down to 0.2
                float score = 1.0f - (i / (float)Math.Max(ftsChunks.Count, 1)) * 0.75f;
                scoredChunks[chunk.Id] = (chunk, score, 0f);
            }

            // 2. Vector Semantic Search
            try
            {
                var queryVector = await _embeddingService.GenerateEmbeddingAsync(query.RawQuery, ct);
                var allChunks = await _databaseService.GetAllChunksForVectorSearchAsync(
                    from: query.DateFrom,
                    to: query.DateTo,
                    ext: query.FileTypeFilter
                );

                if (allChunks.Count == 0 && (query.DateFrom.HasValue || !string.IsNullOrEmpty(query.FileTypeFilter)))
                {
                    allChunks = await _databaseService.GetAllChunksForVectorSearchAsync();
                }

                foreach (var chunk in allChunks)
                {
                    if (ct.IsCancellationRequested) break;
                    if (chunk.Embedding == null || chunk.Embedding.Length == 0) continue;

                    float cosSim = _embeddingService.ComputeCosineSimilarity(queryVector, chunk.Embedding);
                    float normalizedVecScore = Math.Max(0f, cosSim);

                    if (scoredChunks.TryGetValue(chunk.Id, out var existing))
                    {
                        scoredChunks[chunk.Id] = (existing.chunk, existing.ftsScore, normalizedVecScore);
                    }
                    else if (normalizedVecScore > 0.25f)
                    {
                        scoredChunks[chunk.Id] = (chunk, 0f, normalizedVecScore);
                    }
                }
            }
            catch
            {
                // Vector search error fallback gracefully to keyword results
            }

            // 3. Re-ranking with Hybrid Formula & Contextual Boosts
            var finalResults = new List<SearchResultItem>();
            var queryWords = query.CleanedKeywords.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var kvp in scoredChunks)
            {
                var (chunk, ftsScore, vecScore) = kvp.Value;

                // Base hybrid score
                float combinedScore;
                if (ftsScore > 0 && vecScore > 0)
                {
                    combinedScore = (vecScore * hybridWeight) + (ftsScore * (1f - hybridWeight));
                }
                else if (ftsScore > 0)
                {
                    combinedScore = ftsScore * 0.85f;
                }
                else
                {
                    combinedScore = vecScore * 0.80f;
                }

                // Boost 1: Filename match
                var fileNameLower = chunk.FileName.ToLowerInvariant();
                foreach (var qw in queryWords)
                {
                    if (qw.Length > 2 && fileNameLower.Contains(qw))
                    {
                        combinedScore += 0.20f;
                    }
                }

                // Boost 2: Exact phrase in content
                if (chunk.Content.IndexOf(query.CleanedKeywords, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    combinedScore += 0.25f;
                }

                // Boost 3: Date match within filter range
                if (query.DateFrom.HasValue && query.DateTo.HasValue)
                {
                    if (chunk.FileLastModified >= query.DateFrom.Value && chunk.FileLastModified <= query.DateTo.Value)
                    {
                        combinedScore += 0.15f;
                    }
                }

                var snippet = ExtractHighlightSnippet(chunk.Content, queryWords);

                finalResults.Add(new SearchResultItem
                {
                    ChunkId = chunk.Id,
                    FileId = chunk.FileId,
                    FilePath = chunk.FilePath,
                    FileName = chunk.FileName,
                    Extension = chunk.Extension,
                    PageNumber = chunk.PageNumber,
                    SectionTitle = chunk.SectionTitle,
                    Snippet = snippet,
                    RelevanceScore = MathF.Round(combinedScore, 3),
                    FileLastModified = chunk.FileLastModified
                });
            }

            return finalResults
                .OrderByDescending(r => r.RelevanceScore)
                .Take(query.Limit)
                .ToList();
        }

        private static string ExtractHighlightSnippet(string content, string[] queryWords)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;

            int bestPos = -1;
            int maxWordMatches = 0;

            // Find window of 250 characters with highest query word density
            for (int i = 0; i < content.Length; i += 60)
            {
                int len = Math.Min(250, content.Length - i);
                var sub = content.Substring(i, len).ToLowerInvariant();
                int matches = 0;
                foreach (var w in queryWords)
                {
                    if (w.Length > 2 && sub.Contains(w)) matches++;
                }

                if (matches > maxWordMatches)
                {
                    maxWordMatches = matches;
                    bestPos = i;
                }
            }

            if (bestPos < 0) bestPos = 0;

            int snippetLen = Math.Min(320, content.Length - bestPos);
            var snippet = content.Substring(bestPos, snippetLen).Trim();
            snippet = Regex.Replace(snippet, @"\s+", " ");

            if (bestPos > 0) snippet = "..." + snippet;
            if (bestPos + snippetLen < content.Length) snippet += "...";

            return snippet;
        }
    }
}

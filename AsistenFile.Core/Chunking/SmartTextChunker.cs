using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AsistenFile.Core.Models;

namespace AsistenFile.Core.Chunking
{
    public interface ITextChunker
    {
        List<DocumentChunk> CreateChunks(DocumentFile file, List<ExtractedPage> pages, int targetWords = 350, int overlapWords = 50);
    }

    public class SmartTextChunker : ITextChunker
    {
        public List<DocumentChunk> CreateChunks(DocumentFile file, List<ExtractedPage> pages, int targetWords = 350, int overlapWords = 50)
        {
            var chunks = new List<DocumentChunk>();
            int globalChunkIndex = 0;

            foreach (var page in pages)
            {
                if (string.IsNullOrWhiteSpace(page.Content)) continue;

                var sentences = SplitIntoSentences(page.Content);
                var currentChunkWords = new List<string>();
                var currentChunkSentences = new List<string>();

                for (int i = 0; i < sentences.Count; i++)
                {
                    var sentence = sentences[i];
                    var words = sentence.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length == 0) continue;

                    // If adding this sentence exceeds targetWords and we already have words, emit chunk
                    if (currentChunkWords.Count + words.Length > targetWords && currentChunkWords.Count > 0)
                    {
                        var chunkText = string.Join(" ", currentChunkSentences).Trim();
                        if (!string.IsNullOrWhiteSpace(chunkText))
                        {
                            chunks.Add(new DocumentChunk
                            {
                                FileId = file.Id,
                                FilePath = file.FilePath,
                                FileName = file.FileName,
                                Extension = file.Extension,
                                PageNumber = page.PageNumber,
                                SectionTitle = page.SectionTitle,
                                ChunkIndex = globalChunkIndex++,
                                Content = chunkText,
                                WordCount = currentChunkWords.Count,
                                FileLastModified = file.LastModified
                            });
                        }

                        // Retain overlap words from the end of the current chunk
                        int retainCount = Math.Min(overlapWords, currentChunkWords.Count);
                        var overlapList = currentChunkWords.Skip(currentChunkWords.Count - retainCount).ToList();
                        currentChunkWords = new List<string>(overlapList);
                        currentChunkSentences = new List<string> { string.Join(" ", overlapList) };
                    }

                    currentChunkWords.AddRange(words);
                    currentChunkSentences.Add(sentence);
                }

                // Add remaining words as final chunk for this page
                if (currentChunkWords.Count > 0)
                {
                    var remainingText = string.Join(" ", currentChunkSentences).Trim();
                    if (!string.IsNullOrWhiteSpace(remainingText))
                    {
                        chunks.Add(new DocumentChunk
                        {
                            FileId = file.Id,
                            FilePath = file.FilePath,
                            FileName = file.FileName,
                            Extension = file.Extension,
                            PageNumber = page.PageNumber,
                            SectionTitle = page.SectionTitle,
                            ChunkIndex = globalChunkIndex++,
                            Content = remainingText,
                            WordCount = currentChunkWords.Count,
                            FileLastModified = file.LastModified
                        });
                    }
                }
            }

            return chunks;
        }

        private static List<string> SplitIntoSentences(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            // Split on sentence terminators while preserving them
            var pattern = @"(?<=[.?!;:\n])\s+";
            var raw = Regex.Split(text, pattern);

            foreach (var s in raw)
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    result.Add(trimmed);
                }
            }

            return result;
        }
    }
}

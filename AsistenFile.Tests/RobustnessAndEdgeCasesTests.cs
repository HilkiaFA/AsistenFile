using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AsistenFile.Core.AI;
using AsistenFile.Core.Chunking;
using AsistenFile.Core.Config;
using AsistenFile.Core.Extractors;
using AsistenFile.Core.Features;
using AsistenFile.Core.Models;
using AsistenFile.Core.Ocr;
using AsistenFile.Core.Storage;
using Xunit;

namespace AsistenFile.Tests
{
    public class RobustnessAndEdgeCasesTests : IDisposable
    {
        private readonly string _testDir;

        public RobustnessAndEdgeCasesTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "AsistenFileEdge_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [Fact]
        public void SmartTextChunker_HandlesEmptyAndWhitespacePagesGracefully()
        {
            var chunker = new SmartTextChunker();
            var docFile = new DocumentFile
            {
                Id = 1,
                FilePath = "C:\\test\\empty.txt",
                FileName = "empty.txt",
                Extension = ".txt"
            };

            var pages = new List<ExtractedPage>
            {
                new ExtractedPage { PageNumber = 1, SectionTitle = "Halaman 1", Content = "   \n\r\t   " },
                new ExtractedPage { PageNumber = 2, SectionTitle = "Halaman 2", Content = "" }
            };

            var chunks = chunker.CreateChunks(docFile, pages, 300, 50);
            Assert.Empty(chunks);
        }

        [Fact]
        public void SmartTextChunker_SplitsLargeTextAcrossMultipleChunksWithOverlap()
        {
            var chunker = new SmartTextChunker();
            var docFile = new DocumentFile
            {
                Id = 1,
                FilePath = "C:\\test\\long.txt",
                FileName = "long.txt",
                Extension = ".txt"
            };

            // Build ~800 words
            var sb = new StringBuilder();
            for (int i = 1; i <= 80; i++)
            {
                sb.Append($"Ini adalah kalimat nomor {i} yang berisi sepuluh kata umum untuk dokumen. ");
            }

            var pages = new List<ExtractedPage>
            {
                new ExtractedPage { PageNumber = 1, SectionTitle = "Halaman Utama", Content = sb.ToString() }
            };

            var chunks = chunker.CreateChunks(docFile, pages, 300, 50);

            Assert.True(chunks.Count >= 2, $"Expected at least 2 chunks, but got {chunks.Count}");
            Assert.Equal("long.txt", chunks[0].FileName);
            Assert.Equal(1, chunks[0].PageNumber);
            Assert.NotEmpty(chunks[0].Content);
            Assert.NotEmpty(chunks[1].Content);
        }

        [Fact]
        public async Task SettingsService_SavesAndLoadsConfigurationCorrectly()
        {
            var customSettingsPath = Path.Combine(_testDir, "appsettings.test.json");
            var service = new SettingsService(customSettingsPath);

            Assert.NotNull(service.Current);
            Assert.Equal("llama3.2:3b", service.Current.OllamaModel);

            // Modify settings
            service.Current.OllamaModel = "llama3.2:1b";
            service.Current.ChunkWordSize = 450;
            service.Current.ExcludedFolderNames.Add("PrivateConfidential");
            await service.SaveAsync();

            // Reload with a fresh instance
            var reloadedService = new SettingsService(customSettingsPath);
            await reloadedService.LoadAsync();
            Assert.Equal("llama3.2:1b", reloadedService.Current.OllamaModel);
            Assert.Equal(450, reloadedService.Current.ChunkWordSize);
            Assert.Contains("PrivateConfidential", reloadedService.Current.ExcludedFolderNames);
        }

        [Fact]
        public void LocalExtractiveSynthesizer_HandlesEmptySourcesGracefully()
        {
            var synthesizer = new LocalExtractiveSynthesizer();
            var parsed = new SearchQuery { RawQuery = "dokumen rahasia yang tidak pernah ada", CleanedKeywords = "rahasia" };

            var emptySources = new List<SearchResultItem>();
            var response = synthesizer.SynthesizeAnswer("dokumen rahasia yang tidak pernah ada", parsed, emptySources);

            Assert.Contains("Maaf, tidak ditemukan", response);
            Assert.Contains("Tips", response);
        }

        [Fact]
        public void LocalExtractiveSynthesizer_HandlesZeroSimilaritySourcesGracefully()
        {
            var synthesizer = new LocalExtractiveSynthesizer();
            var parsed = new SearchQuery { RawQuery = "pembayaran hosting", CleanedKeywords = "hosting" };

            var lowScoreSources = new List<SearchResultItem>
            {
                new SearchResultItem
                {
                    FileName = "random.txt",
                    RelevanceScore = 0.12f,
                    Snippet = "tidak ada hubungannya sama sekali"
                }
            };

            var response = synthesizer.SynthesizeAnswer("pembayaran hosting", parsed, lowScoreSources);
            Assert.Contains("Maaf, tidak ditemukan", response);
        }

        [Fact]
        public async Task DocumentDiffService_IdenticalDocuments_ReportsNoDifferences()
        {
            var factory = new ExtractorFactory(new WindowsMediaOcrService());
            var dummyOllama = new OllamaClientService("http://127.0.0.1:9999", "dummy");
            var diffService = new DocumentDiffService(factory, dummyOllama);

            var fileA = Path.Combine(_testDir, "docA.txt");
            var fileB = Path.Combine(_testDir, "docB.txt");
            var text = "Perjanjian Kerja Sama antara Pihak Pertama dan Pihak Kedua. Biaya sebesar Rp 50.000.000 selama 12 bulan.";

            await File.WriteAllTextAsync(fileA, text);
            await File.WriteAllTextAsync(fileB, text);

            var diff = await diffService.CompareDocumentsAsync(fileA, fileB);

            Assert.Equal("docA.txt", diff.FileAName);
            Assert.Equal("docB.txt", diff.FileBName);
            Assert.Empty(diff.Changes);
            Assert.Contains("Kedua dokumen identik", diff.ExecutiveSummary);
        }

        [Fact]
        public async Task DocumentDiffService_DetectsFinancialAndDurationChanges()
        {
            var factory = new ExtractorFactory(new WindowsMediaOcrService());
            var dummyOllama = new OllamaClientService("http://127.0.0.1:9999", "dummy");
            var diffService = new DocumentDiffService(factory, dummyOllama);

            var fileA = Path.Combine(_testDir, "kontrak_2024.txt");
            var fileB = Path.Combine(_testDir, "kontrak_2025.txt");

            var textA = "Pasal 1: Biaya sewa ruang kantor disepakati Rp 100.000.000 per tahun. Jangka waktu kontrak berlaku selama 12 bulan.";
            var textB = "Pasal 1: Biaya sewa ruang kantor disepakati Rp 120.000.000 per tahun. Jangka waktu kontrak berlaku selama 24 bulan.";

            await File.WriteAllTextAsync(fileA, textA);
            await File.WriteAllTextAsync(fileB, textB);

            var diff = await diffService.CompareDocumentsAsync(fileA, fileB);

            Assert.NotEmpty(diff.Changes);
            Assert.True(diff.Changes.Exists(c => c.Category == DiffCategory.Financial));
            Assert.True(diff.Changes.Exists(c => c.Category == DiffCategory.Duration));
            Assert.Contains("Perubahan Nilai / Biaya", diff.ExecutiveSummary);
        }

        [Fact]
        public void InvoiceExtractor_CalculateStats_CalculatesTotalsCorrectly()
        {
            var db = new SqliteDatabaseService(Path.Combine(_testDir, "temp_inv.db"));
            var extractor = new InvoiceExtractorService(db);

            var items = new List<ExtractedInvoiceItem>
            {
                new ExtractedInvoiceItem { InvoiceNumber = "INV-001", Amount = 15000000, Status = "Lunas" },
                new ExtractedInvoiceItem { InvoiceNumber = "INV-002", Amount = 25000000, Status = "Belum Lunas" },
                new ExtractedInvoiceItem { InvoiceNumber = "INV-003", Amount = 5000000, Status = "Lunas" }
            };

            var stats = extractor.CalculateStats(items);

            Assert.Equal(3, stats.TotalInvoices);
            Assert.Equal(45000000, stats.TotalAmount);
            Assert.Equal(20000000, stats.PaidAmount);
            Assert.Equal(25000000, stats.UnpaidAmount);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }
    }
}

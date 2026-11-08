using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AsistenFile.Core.AI;
using AsistenFile.Core.Config;
using AsistenFile.Core.Embeddings;
using AsistenFile.Core.Models;
using AsistenFile.Core.Search;
using AsistenFile.Core.Storage;
using Xunit;

namespace AsistenFile.Tests
{
    public class HybridSearchAndRagIntegrationTests : IDisposable
    {
        private readonly string _tempDbDir;
        private readonly string _tempDbPath;
        private readonly SqliteDatabaseService _db;
        private readonly LocalSemanticEmbeddingService _embeddingService;
        private readonly HybridSearchEngine _searchEngine;
        private readonly NaturalLanguageQueryParser _parser;
        private readonly LocalExtractiveSynthesizer _synthesizer;
        private readonly RagOrchestrator _ragOrchestrator;
        private readonly SettingsService _settingsService;

        public HybridSearchAndRagIntegrationTests()
        {
            _tempDbDir = Path.Combine(Path.GetTempPath(), "AsistenFileTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDbDir);
            _tempDbPath = Path.Combine(_tempDbDir, "test.db");

            _db = new SqliteDatabaseService(_tempDbPath);
            _embeddingService = new LocalSemanticEmbeddingService(256);
            _searchEngine = new HybridSearchEngine(_db, _embeddingService);
            _parser = new NaturalLanguageQueryParser();
            _synthesizer = new LocalExtractiveSynthesizer();
            _settingsService = new SettingsService(Path.Combine(_tempDbDir, "settings.json"));

            var dummyOllama = new OllamaClientService("http://127.0.0.1:9999", "dummy");
            _ragOrchestrator = new RagOrchestrator(_parser, _searchEngine, dummyOllama, _synthesizer, _settingsService);
        }

        [Fact]
        public async Task EndToEndPipeline_IndexesAndAnswersNaturalLanguageQueries()
        {
            await _db.InitializeAsync();

            // 1. Setup sample document: Contract
            var contractFile = new DocumentFile
            {
                FilePath = @"C:\Documents\Kontrak_Sewa_Kantor_2025.pdf",
                FileName = "Kontrak_Sewa_Kantor_2025.pdf",
                Extension = ".pdf",
                FileSizeBytes = 10240,
                LastModified = new DateTime(DateTime.Now.Year - 1, 6, 15),
                IndexedAt = DateTime.Now,
                ChunkCount = 1,
                Status = "Indexed",
                FolderId = 1
            };
            long contractFileId = await _db.UpsertFileAsync(contractFile);
            contractFile.Id = contractFileId;

            var contractContent = "SURAT PERJANJIAN SEWA MENYEWA RUANG KANTOR. Pihak Pertama menyewakan ruang kantor kepada Pihak Kedua selama jangka waktu 2 tahun. Nilai kontrak disepakati sebesar Rp 120.000.000 per tahun. Ditandatangani di Jakarta pada tanggal 15 Juni.";
            var contractVec = await _embeddingService.GenerateEmbeddingAsync(contractContent);

            await _db.InsertChunksAsync(new List<DocumentChunk>
            {
                new DocumentChunk
                {
                    FileId = contractFileId,
                    FilePath = contractFile.FilePath,
                    FileName = contractFile.FileName,
                    Extension = contractFile.Extension,
                    PageNumber = 1,
                    SectionTitle = "Pasal 1 - Jangka Waktu & Biaya",
                    ChunkIndex = 0,
                    Content = contractContent,
                    WordCount = 35,
                    Embedding = contractVec,
                    FileLastModified = contractFile.LastModified
                }
            });

            // 2. Setup sample document: Invoices (Excel)
            var invoiceFile = new DocumentFile
            {
                FilePath = @"C:\Documents\Rekap_Invoice_Maret.xlsx",
                FileName = "Rekap_Invoice_Maret.xlsx",
                Extension = ".xlsx",
                FileSizeBytes = 8400,
                LastModified = new DateTime(DateTime.Now.Year, 3, 20),
                IndexedAt = DateTime.Now,
                ChunkCount = 1,
                Status = "Indexed",
                FolderId = 1
            };
            long invoiceFileId = await _db.UpsertFileAsync(invoiceFile);
            invoiceFile.Id = invoiceFileId;

            var invoiceContent = "Baris 1: No=INV-001, Tanggal=10 Maret 2024, Vendor=PT Cloud Hosting, Uraian=Sewa Server, Total=Rp 5.500.000. Baris 2: No=INV-002, Tanggal=22 Maret 2024, Vendor=PT FiberNet, Uraian=Internet Kantor, Total=Rp 2.500.000.";
            var invoiceVec = await _embeddingService.GenerateEmbeddingAsync(invoiceContent);

            await _db.InsertChunksAsync(new List<DocumentChunk>
            {
                new DocumentChunk
                {
                    FileId = invoiceFileId,
                    FilePath = invoiceFile.FilePath,
                    FileName = invoiceFile.FileName,
                    Extension = invoiceFile.Extension,
                    PageNumber = 1,
                    SectionTitle = "Lembar 'Invoice Maret'",
                    ChunkIndex = 0,
                    Content = invoiceContent,
                    WordCount = 40,
                    Embedding = invoiceVec,
                    FileLastModified = invoiceFile.LastModified
                }
            });

            // Test Query 1: "cari kontrak sewa yang saya tanda tangani tahun lalu"
            var respContract = await _ragOrchestrator.AnswerAsync("cari kontrak sewa yang saya tanda tangani tahun lalu");
            Assert.True(respContract.IsGrounded);
            Assert.NotEmpty(respContract.Sources);
            Assert.Equal("Kontrak_Sewa_Kantor_2025.pdf", respContract.Sources[0].FileName);
            Assert.Contains("Kontrak_Sewa_Kantor_2025.pdf", respContract.Answer);

            // Test Query 2: "rangkum semua invoice bulan Maret"
            var respInvoice = await _ragOrchestrator.AnswerAsync("rangkum semua invoice bulan Maret");
            Assert.True(respInvoice.IsGrounded);
            Assert.NotEmpty(respInvoice.Sources);
            Assert.Equal("Rekap_Invoice_Maret.xlsx", respInvoice.Sources[0].FileName);
            Assert.Contains("Total Akumulasi Terdeteksi", respInvoice.Answer);

            // Test Query 3: Non-existent query (Grounded negative response)
            var respUnrelated = await _ragOrchestrator.AnswerAsync("pesawat antariksa misi ke planet pluto");
            Assert.False(respUnrelated.IsGrounded);
            Assert.Contains("tidak ditemukan", respUnrelated.Answer, StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDbDir))
                {
                    Directory.Delete(_tempDbDir, true);
                }
            }
            catch
            {
                // Cleanup best effort
            }
        }
    }
}

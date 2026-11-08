using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AsistenFile.Core.AI;
using AsistenFile.Core.Extractors;
using AsistenFile.Core.Features;
using AsistenFile.Core.Models;
using AsistenFile.Core.Ocr;
using AsistenFile.Core.Storage;
using Xunit;

namespace AsistenFile.Tests
{
    public class AdvancedFeaturesTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly SqliteDatabaseService _db;
        private readonly DeadlineTrackerService _deadlineService;
        private readonly InvoiceExtractorService _invoiceService;
        private readonly DocumentDiffService _diffService;

        public AdvancedFeaturesTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "AdvFeaturesTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _db = new SqliteDatabaseService(Path.Combine(_tempDir, "test.db"));
            _deadlineService = new DeadlineTrackerService(_db);
            _invoiceService = new InvoiceExtractorService(_db);

            var factory = new ExtractorFactory(new WindowsMediaOcrService());
            var dummyOllama = new OllamaClientService("http://127.0.0.1:9999", "dummy");
            _diffService = new DocumentDiffService(factory, dummyOllama);
        }

        [Fact]
        public async Task DeadlineTracker_DetectsIndonesianDatesAndCalculatesUrgency()
        {
            await _db.InitializeAsync();

            var futureDate = DateTime.Today.AddDays(15);
            var overdueDate = DateTime.Today.AddDays(-10);

            long file1Id = await _db.UpsertFileAsync(new DocumentFile
            {
                FilePath = @"C:\Files\Kontrak_Sewa_2026.pdf",
                FileName = "Kontrak_Sewa_2026.pdf",
                Extension = ".pdf",
                LastModified = DateTime.Now,
                IndexedAt = DateTime.Now,
                FolderId = 1
            });

            long file2Id = await _db.UpsertFileAsync(new DocumentFile
            {
                FilePath = @"C:\Files\Invoice_Hosting.pdf",
                FileName = "Invoice_Hosting.pdf",
                Extension = ".pdf",
                LastModified = DateTime.Now,
                IndexedAt = DateTime.Now,
                FolderId = 1
            });

            var chunkFuture = new DocumentChunk
            {
                FileId = file1Id,
                FilePath = @"C:\Files\Kontrak_Sewa_2026.pdf",
                FileName = "Kontrak_Sewa_2026.pdf",
                Extension = ".pdf",
                PageNumber = 1,
                SectionTitle = "Pasal 3",
                Content = $"Perjanjian sewa menyewa ini berlaku sampai dengan tanggal: {futureDate:dd MMMM yyyy}.",
                FileLastModified = DateTime.Now
            };

            var chunkOverdue = new DocumentChunk
            {
                FileId = file2Id,
                FilePath = @"C:\Files\Invoice_Hosting.pdf",
                FileName = "Invoice_Hosting.pdf",
                Extension = ".pdf",
                PageNumber = 1,
                SectionTitle = "Tagihan",
                Content = $"Batas pembayaran jatuh tempo: {overdueDate:dd MMMM yyyy}.",
                FileLastModified = DateTime.Now
            };

            await _db.InsertChunksAsync(new List<DocumentChunk> { chunkFuture, chunkOverdue });

            var deadlines = await _deadlineService.ScanDeadlinesAsync();
            Assert.Equal(2, deadlines.Count);

            var overdueItem = deadlines.Find(d => d.FileName == "Invoice_Hosting.pdf");
            Assert.NotNull(overdueItem);
            Assert.Equal(DeadlineUrgency.Overdue, overdueItem!.Urgency);
            Assert.True(overdueItem.DaysRemaining < 0);

            var futureItem = deadlines.Find(d => d.FileName == "Kontrak_Sewa_2026.pdf");
            Assert.NotNull(futureItem);
            Assert.Equal(DeadlineUrgency.Warning, futureItem!.Urgency);
            Assert.Equal(15, futureItem.DaysRemaining);
        }

        [Fact]
        public async Task InvoiceExtractor_ExtractsStructuredFieldsAndAggregatesStats()
        {
            await _db.InitializeAsync();

            long invFileId = await _db.UpsertFileAsync(new DocumentFile
            {
                FilePath = @"C:\Invoices\Rekap_Invoice.xlsx",
                FileName = "Rekap_Invoice.xlsx",
                Extension = ".xlsx",
                LastModified = DateTime.Now,
                IndexedAt = DateTime.Now,
                FolderId = 1
            });

            var invoiceChunk = new DocumentChunk
            {
                FileId = invFileId,
                FilePath = @"C:\Invoices\Rekap_Invoice.xlsx",
                FileName = "Rekap_Invoice.xlsx",
                Extension = ".xlsx",
                PageNumber = 1,
                SectionTitle = "Sheet 1",
                Content = "Baris 1: No. Invoice: INV-2026-001, Vendor PT Mitra Solusi, Tanggal: 15/03/2026, Uraian: Sewa Cloud Server, Total Tagihan: Rp 15.000.000, Status: Belum Bayar.\n" +
                          "Baris 2: No. Invoice: INV-2026-002, Vendor PT FiberNet, Tanggal: 18/03/2026, Uraian: Internet Kantor, Total Tagihan: Rp 5.000.000, Status: Lunas.",
                FileLastModified = DateTime.Now
            };

            await _db.InsertChunksAsync(new List<DocumentChunk> { invoiceChunk });

            var invoices = await _invoiceService.ExtractAllInvoicesAsync();
            Assert.Equal(2, invoices.Count);

            var inv1 = invoices.Find(i => i.InvoiceNumber.Contains("INV-2026-001"));
            Assert.NotNull(inv1);
            Assert.Equal(15000000m, inv1!.Amount);
            Assert.Equal("PT Mitra Solusi", inv1.VendorOrClient);
            Assert.Equal("Belum Bayar", inv1.Status);

            var stats = _invoiceService.CalculateStats(invoices);
            Assert.Equal(2, stats.TotalInvoices);
            Assert.Equal(20000000m, stats.TotalAmount);
            Assert.Equal(5000000m, stats.PaidAmount);
            Assert.Equal(15000000m, stats.UnpaidAmount);

            // Verify CSV export
            var csvPath = Path.Combine(_tempDir, "export_test.csv");
            await _invoiceService.ExportToCsvAsync(invoices, csvPath);
            Assert.True(File.Exists(csvPath));
            var csvLines = await File.ReadAllLinesAsync(csvPath);
            Assert.True(csvLines.Length >= 3);
        }

        [Fact]
        public async Task DocumentDiff_DetectsFinancialDurationAndClauseChanges()
        {
            var fileA = Path.Combine(_tempDir, "Kontrak_Kerja_2024.txt");
            var fileB = Path.Combine(_tempDir, "Kontrak_Kerja_2025.txt");

            var textA = "SURAT PERJANJIAN KERJA 2024\n\n" +
                        "Pasal 1 Ketentuan Umum\nPerjanjian ini berlaku selama 12 bulan.\n\n" +
                        "Pasal 2 Biaya Sewa\nBiaya sewa disepakati sebesar Rp 100.000.000 per tahun.\n\n" +
                        "Pasal 4 Denda dan Penalti\nKeterlambatan dikenakan denda sebesar 1% per hari.";

            var textB = "SURAT PERJANJIAN KERJA 2025\n\n" +
                        "Pasal 1 Ketentuan Umum\nPerjanjian ini berlaku selama 24 bulan.\n\n" +
                        "Pasal 2 Biaya Sewa\nBiaya sewa disepakati sebesar Rp 120.000.000 per tahun.\n\n" +
                        "Pasal 5 Layanan Pemeliharaan\nPihak Pertama menyediakan layanan pemeliharaan berkala.";

            await File.WriteAllTextAsync(fileA, textA);
            await File.WriteAllTextAsync(fileB, textB);

            var diff = await _diffService.CompareDocumentsAsync(fileA, fileB);
            Assert.NotNull(diff);
            Assert.NotEmpty(diff.Changes);

            // Financial change detected (Rp 100jt -> Rp 120jt)
            var financialChange = diff.Changes.Find(c => c.Category == DiffCategory.Financial);
            Assert.NotNull(financialChange);
            Assert.Contains("100.000.000", financialChange!.OldValue);
            Assert.Contains("120.000.000", financialChange.NewValue);

            // Duration change detected (12 bulan -> 24 bulan)
            var durationChange = diff.Changes.Find(c => c.Category == DiffCategory.Duration);
            Assert.NotNull(durationChange);
            Assert.Contains("12 bulan", durationChange!.OldValue);
            Assert.Contains("24 bulan", durationChange.NewValue);

            // Clause removed (Pasal 4)
            var removedClause = diff.Changes.Find(c => c.Category == DiffCategory.Clause && c.ChangeType == DiffType.Removed);
            Assert.NotNull(removedClause);
            Assert.Contains("Pasal 4", removedClause!.Title);

            // Clause added (Pasal 5)
            var addedClause = diff.Changes.Find(c => c.Category == DiffCategory.Clause && c.ChangeType == DiffType.Added);
            Assert.NotNull(addedClause);
            Assert.Contains("Pasal 5", addedClause!.Title);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }
    }
}

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AsistenFile.Core.Extractors;
using AsistenFile.Core.Ocr;
using Xunit;

namespace AsistenFile.Tests
{
    public class ExtractorsTests : IDisposable
    {
        private readonly string _sampleDir;
        private readonly ExtractorFactory _factory;

        public ExtractorsTests()
        {
            _sampleDir = Path.Combine(Path.GetTempPath(), "AsistenFileSamples_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_sampleDir);
            _factory = new ExtractorFactory(new WindowsMediaOcrService());
        }

        [Fact]
        public async Task PlainTextExtractor_ExtractsMarkdownAndTxt()
        {
            var txtPath = Path.Combine(_sampleDir, "catatan.md");
            File.WriteAllText(txtPath, "# Catatan Meeting Proyek\n\nTanggal: 12 Maret 2026\nPembahasan mengenai peluncuran aplikasi lokal.", Encoding.UTF8);

            var extractor = _factory.GetExtractor(txtPath);
            Assert.NotNull(extractor);

            var pages = await extractor!.ExtractAsync(txtPath);
            Assert.NotEmpty(pages);
            Assert.Contains("peluncuran aplikasi lokal", pages[0].Content);
        }

        [Fact]
        public async Task ExcelExtractor_ExtractsCsvRows()
        {
            var csvPath = Path.Combine(_sampleDir, "invoices.csv");
            var csvContent = "No,Tanggal,Vendor,Keterangan,Total\n" +
                             "1,15/03/2026,PT Sentosa,Sewa Server,Rp 10.000.000\n" +
                             "2,20/03/2026,PT Mega Prima,Lisensi Software,Rp 5.000.000";
            File.WriteAllText(csvPath, csvContent, Encoding.UTF8);

            var extractor = _factory.GetExtractor(csvPath);
            Assert.NotNull(extractor);

            var pages = await extractor!.ExtractAsync(csvPath);
            Assert.NotEmpty(pages);
            Assert.Contains("PT Sentosa", pages[0].Content);
            Assert.Contains("Rp 10.000.000", pages[0].Content);
        }

        [Fact]
        public async Task EmailExtractor_ExtractsEmlFields()
        {
            var emlPath = Path.Combine(_sampleDir, "invoice.eml");
            var emlRaw = "From: finance@perusahaan.com\r\n" +
                         "To: client@bisnis.com\r\n" +
                         "Subject: Tagihan Invoice Pembayaran Maret 2026\r\n" +
                         "Date: Sun, 15 Mar 2026 10:00:00 +0700\r\n" +
                         "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
                         "Halo Pak, berikut terlampir rincian tagihan invoice bulan Maret sebesar Rp 7.500.000. Terima kasih.";
            File.WriteAllText(emlPath, emlRaw, Encoding.UTF8);

            var extractor = _factory.GetExtractor(emlPath);
            Assert.NotNull(extractor);

            var pages = await extractor!.ExtractAsync(emlPath);
            Assert.NotEmpty(pages);
            Assert.Contains("Tagihan Invoice Pembayaran Maret 2026", pages[0].Content);
            Assert.Contains("Rp 7.500.000", pages[0].Content);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_sampleDir))
                {
                    Directory.Delete(_sampleDir, true);
                }
            }
            catch { }
        }
    }
}

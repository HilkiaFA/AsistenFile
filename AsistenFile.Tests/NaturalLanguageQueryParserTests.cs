using System;
using AsistenFile.Core.Search;
using Xunit;

namespace AsistenFile.Tests
{
    public class NaturalLanguageQueryParserTests
    {
        private readonly NaturalLanguageQueryParser _parser = new();

        [Fact]
        public void Parse_ContractLastYear_DetectsYearAndKeywords()
        {
            var query = _parser.Parse("cari kontrak sewa yang saya tanda tangani tahun lalu");

            Assert.NotNull(query.DateFrom);
            Assert.NotNull(query.DateTo);
            Assert.Equal(DateTime.Now.Year - 1, query.DateFrom!.Value.Year);
            Assert.Equal(DateTime.Now.Year - 1, query.DateTo!.Value.Year);
            Assert.Contains("kontrak", query.CleanedKeywords);
            Assert.Contains("sewa", query.CleanedKeywords);
        }

        [Fact]
        public void Parse_InvoiceMarch_DetectsSummarizeIntentAndMonth()
        {
            var query = _parser.Parse("rangkum semua invoice bulan Maret");

            Assert.True(query.IsSummarizeRequest);
            Assert.NotNull(query.DateFrom);
            Assert.Equal(3, query.DateFrom!.Value.Month);
            Assert.Contains("invoice", query.CleanedKeywords);
        }

        [Fact]
        public void Parse_ExplicitFileTypePdf_DetectsExtension()
        {
            var query = _parser.Parse("cari file pdf laporan keuangan tahun 2024");

            Assert.Equal(".pdf", query.FileTypeFilter);
            Assert.NotNull(query.DateFrom);
            Assert.Equal(2024, query.DateFrom!.Value.Year);
            Assert.Contains("laporan", query.CleanedKeywords);
            Assert.Contains("keuangan", query.CleanedKeywords);
        }
    }
}

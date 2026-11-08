using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Models;
using AsistenFile.Core.Storage;

namespace AsistenFile.Core.Features
{
    public interface IInvoiceExtractorService
    {
        Task<List<ExtractedInvoiceItem>> ExtractAllInvoicesAsync(CancellationToken ct = default);
        InvoiceSummaryStats CalculateStats(List<ExtractedInvoiceItem> invoices);
        Task ExportToCsvAsync(List<ExtractedInvoiceItem> invoices, string destinationPath, CancellationToken ct = default);
    }

    public class InvoiceExtractorService : IInvoiceExtractorService
    {
        private readonly IDatabaseService _databaseService;

        private static readonly Regex InvNumberRegex = new Regex(
            @"(?:No\.?\s*Invoice|Invoice\s*No\.?|INV\s*No\.?|No\.?\s*Faktur|Faktur\s*No\.?|No\.|INV-?|Kwitansi\s*No\.?)\s*[:=#]?\s*([A-Za-z0-9\/\-\.]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AmountRegex = new Regex(
            @"(?:Total(?:\s+Tagihan|\s+Bayar|\s+Biaya)?|Jumlah|Nilai|Subtotal|Grand\s*Total|Harga|Rp\.?|IDR)\s*[:=]?\s*(?:Rp\.?|IDR)?\s*([0-9]{1,3}(?:\.[0-9]{3})+(?:,[0-9]{2})?|[0-9]{4,})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex VendorRegex = new Regex(
            @"(?:PT\.?|CV\.?|UD\.?|Toko|Vendor|Dari|Supplier)\s+([A-Za-z0-9\s\.\&]+?)(?=[,\n\r\|;]|\s{2,}|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DateRegex = new Regex(
            @"(?:Tanggal|Tgl|Date)\s*[:=]?\s*([0-9]{1,2}[\s\/\-\.](?:[a-zA-Z]+|[0-9]{1,2})[\s\/\-\.][0-9]{2,4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public InvoiceExtractorService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<ExtractedInvoiceItem>> ExtractAllInvoicesAsync(CancellationToken ct = default)
        {
            var invoices = new List<ExtractedInvoiceItem>();
            var chunks = await _databaseService.GetAllChunksForVectorSearchAsync();

            var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var chunk in chunks)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrWhiteSpace(chunk.Content)) continue;

                var lowerContent = chunk.Content.ToLowerInvariant();
                var lowerFile = chunk.FileName.ToLowerInvariant();

                // Check if this chunk is related to invoices/receipts/bills
                bool isInvoiceCandidate = lowerFile.Contains("invoice") || lowerFile.Contains("tagihan") ||
                                          lowerFile.Contains("kwitansi") || lowerFile.Contains("faktur") ||
                                          lowerFile.Contains("rekap") || lowerContent.Contains("invoice") ||
                                          lowerContent.Contains("tagihan") || lowerContent.Contains("grand total");

                if (!isInvoiceCandidate) continue;

                // Process lines (for spreadsheets, tables, or text lines)
                var lines = chunk.Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    if (line.Length < 10) continue;

                    // Match amount
                    var amountMatch = AmountRegex.Match(line);
                    if (!amountMatch.Success) continue;

                    var amountVal = ParseAmount(amountMatch.Groups[1].Value);
                    if (amountVal <= 0) continue;

                    // Match invoice number
                    var invMatch = InvNumberRegex.Match(line);
                    string invNumber = invMatch.Success ? invMatch.Groups[1].Value.Trim() : GenerateFallbackInvNumber(chunk.FileName, line);

                    // Match date
                    var dateMatch = DateRegex.Match(line);
                    DateTime? parsedDate = null;
                    if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var dt))
                    {
                        parsedDate = dt;
                    }
                    else
                    {
                        parsedDate = chunk.FileLastModified;
                    }

                    // Match Vendor / Client
                    var vendorMatch = VendorRegex.Match(line);
                    string vendor = vendorMatch.Success ? vendorMatch.Value.Trim() : DetectVendorFromFilename(chunk.FileName);
                    vendor = Regex.Replace(vendor, @"^(?:Vendor|Supplier|Dari|Kepada)\s*[:=]?\s*", "", RegexOptions.IgnoreCase).Trim();

                    // Status
                    string status = (line.Contains("lunas", StringComparison.OrdinalIgnoreCase) || line.Contains("paid", StringComparison.OrdinalIgnoreCase))
                        ? "Lunas"
                        : "Belum Bayar";

                    // Description
                    string desc = ExtractDescription(line);

                    var deduplicationKey = $"{chunk.FilePath}_{invNumber}_{amountVal}";
                    if (seenEntries.Contains(deduplicationKey)) continue;
                    seenEntries.Add(deduplicationKey);

                    invoices.Add(new ExtractedInvoiceItem
                    {
                        FileId = chunk.FileId,
                        FilePath = chunk.FilePath,
                        FileName = chunk.FileName,
                        Extension = chunk.Extension,
                        InvoiceNumber = invNumber,
                        InvoiceDate = parsedDate,
                        VendorOrClient = vendor,
                        Description = desc,
                        Amount = amountVal,
                        Status = status,
                        Snippet = line.Trim()
                    });
                }
            }

            return invoices.OrderByDescending(x => x.InvoiceDate ?? DateTime.MinValue).ToList();
        }

        public InvoiceSummaryStats CalculateStats(List<ExtractedInvoiceItem> invoices)
        {
            var stats = new InvoiceSummaryStats
            {
                TotalInvoices = invoices.Count,
                TotalAmount = invoices.Sum(x => x.Amount),
                PaidAmount = invoices.Where(x => x.Status == "Lunas").Sum(x => x.Amount),
                UnpaidAmount = invoices.Where(x => x.Status != "Lunas").Sum(x => x.Amount)
            };
            return stats;
        }

        public async Task ExportToCsvAsync(List<ExtractedInvoiceItem> invoices, string destinationPath, CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            sb.AppendLine("No. Invoice,Tanggal,Vendor/Klien,Uraian Transaksi,Nilai Tagihan (IDR),Status Pembayaran,Nama File Sumber,Lokasi Path");

            foreach (var item in invoices)
            {
                sb.AppendLine(
                    $"\"{EscapeCsv(item.InvoiceNumber)}\"," +
                    $"\"{item.InvoiceDate?.ToString("yyyy-MM-dd") ?? "-"}\"," +
                    $"\"{EscapeCsv(item.VendorOrClient)}\"," +
                    $"\"{EscapeCsv(item.Description)}\"," +
                    $"{item.Amount.ToString("0", CultureInfo.InvariantCulture)}," +
                    $"\"{item.Status}\"," +
                    $"\"{EscapeCsv(item.FileName)}\"," +
                    $"\"{EscapeCsv(item.FilePath)}\"");
            }

            await File.WriteAllTextAsync(destinationPath, sb.ToString(), new UTF8Encoding(true), ct);
        }

        private static decimal ParseAmount(string raw)
        {
            var clean = raw.Replace(".", "").Replace(",", ".");
            if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return 0;
        }

        private static string DetectVendorFromFilename(string fileName)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName).Replace("_", " ").Replace("-", " ");
            var match = VendorRegex.Match(nameWithoutExt);
            if (match.Success) return match.Value.Trim();

            return nameWithoutExt;
        }

        private static string GenerateFallbackInvNumber(string fileName, string line)
        {
            var match = Regex.Match(line, @"\b(?:INV-?\d+|\d{4,})\b", RegexOptions.IgnoreCase);
            if (match.Success) return match.Value;

            return Path.GetFileNameWithoutExtension(fileName);
        }

        private static string ExtractDescription(string line)
        {
            var match = Regex.Match(line, @"(?:Uraian|Deskripsi|Keterangan|Item)\s*[:=]?\s*([^,\|]+)", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value.Trim();

            // Fallback: shorten line
            var cleaned = Regex.Replace(line, @"(?:Baris\s*\d+:|Total.*)", "").Trim();
            return cleaned.Length > 60 ? cleaned.Substring(0, 57) + "..." : cleaned;
        }

        private static string EscapeCsv(string s) => s?.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") ?? "";
    }
}

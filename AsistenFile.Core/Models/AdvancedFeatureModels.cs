using System;
using System.Collections.Generic;

namespace AsistenFile.Core.Models
{
    // === FITUR A: DEADLINE & JATUH TEMPO ===
    public enum DeadlineUrgency
    {
        Overdue,    // Sudah lewat jatuh tempo
        Critical,   // <= 7 hari lagi
        Warning,    // <= 30 hari lagi
        Normal      // > 30 hari lagi
    }

    public class DocumentDeadlineItem
    {
        public long FileId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string DocumentCategory { get; set; } = "Kontrak"; // Kontrak, Invoice, Langganan, Polis
        public DateTime DueDate { get; set; }
        public int DaysRemaining => (int)Math.Ceiling((DueDate.Date - DateTime.Today).TotalDays);
        public DeadlineUrgency Urgency
        {
            get
            {
                if (DaysRemaining < 0) return DeadlineUrgency.Overdue;
                if (DaysRemaining <= 7) return DeadlineUrgency.Critical;
                if (DaysRemaining <= 30) return DeadlineUrgency.Warning;
                return DeadlineUrgency.Normal;
            }
        }
        public string Snippet { get; set; } = string.Empty;
        public string SectionTitle { get; set; } = string.Empty;
    }

    // === FITUR B: REKAP INVOICE TERSTRUKTUR ===
    public class ExtractedInvoiceItem
    {
        public long FileId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = "-";
        public DateTime? InvoiceDate { get; set; }
        public string VendorOrClient { get; set; } = "Tidak Diketahui";
        public string Description { get; set; } = "-";
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Belum Bayar"; // Lunas, Belum Bayar, Jatuh Tempo
        public string Snippet { get; set; } = string.Empty;
    }

    public class InvoiceSummaryStats
    {
        public int TotalInvoices { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal UnpaidAmount { get; set; }
    }

    // === FITUR C: PERBANDINGAN DOKUMEN (DIFF AI) ===
    public enum DiffCategory
    {
        Financial, // Perubahan Nilai / Biaya / Tarif
        Duration,  // Perubahan Tanggal / Jangka Waktu
        Clause,    // Perubahan Klausul / Pasal
        General    // Perubahan Umum
    }

    public enum DiffType
    {
        Modified, // Nilai atau redaksi diubah
        Added,    // Klausul baru ditambahkan
        Removed   // Klausul lama dihapus
    }

    public class DiffChangeItem
    {
        public DiffCategory Category { get; set; }
        public DiffType ChangeType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class DocumentDiffResult
    {
        public string FileAPath { get; set; } = string.Empty;
        public string FileBPath { get; set; } = string.Empty;
        public string FileAName { get; set; } = string.Empty;
        public string FileBName { get; set; } = string.Empty;
        public string ExecutiveSummary { get; set; } = string.Empty;
        public List<DiffChangeItem> Changes { get; set; } = new();
        public string RawComparisonText { get; set; } = string.Empty;
    }
}

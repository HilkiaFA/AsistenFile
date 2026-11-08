using System;

namespace AsistenFile.Core.Models
{
    public class DocumentFile
    {
        public long Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime IndexedAt { get; set; }
        public int ChunkCount { get; set; }
        public string Status { get; set; } = "Indexed"; // Indexed, Error, Skipped
        public string? ErrorMessage { get; set; }
        public long FolderId { get; set; }
    }

    public class DocumentChunk
    {
        public long Id { get; set; }
        public long FileId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public int PageNumber { get; set; } = 1;
        public string SectionTitle { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public byte[]? EmbeddingBlob { get; set; }
        public float[]? Embedding { get; set; }
        public DateTime FileLastModified { get; set; }
    }

    public class IndexedFolder
    {
        public long Id { get; set; }
        public string FolderPath { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public DateTime? LastScannedAt { get; set; }
        public int FileCount { get; set; }
    }

    public class ExtractedPage
    {
        public int PageNumber { get; set; } = 1;
        public string SectionTitle { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class SearchQuery
    {
        public string RawQuery { get; set; } = string.Empty;
        public string CleanedKeywords { get; set; } = string.Empty;
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? FileTypeFilter { get; set; } // e.g. "pdf", "docx", "xlsx", etc.
        public string? FolderFilter { get; set; }
        public int Limit { get; set; } = 6;
        public bool IsSummarizeRequest { get; set; }
    }

    public class SearchResultItem
    {
        public long ChunkId { get; set; }
        public long FileId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public int PageNumber { get; set; } = 1;
        public string SectionTitle { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public float RelevanceScore { get; set; }
        public DateTime FileLastModified { get; set; }
    }

    public class RagResponse
    {
        public string Query { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public List<SearchResultItem> Sources { get; set; } = new();
        public bool IsGrounded { get; set; } = true;
        public long ProcessingTimeMs { get; set; }
        public string ModelUsed { get; set; } = "Local Extractive AI";
        public string? IntentExplanation { get; set; }
    }

    public class AppSettings
    {
        public string OllamaEndpoint { get; set; } = "http://localhost:11434";
        public string OllamaModel { get; set; } = "llama3.2:3b";
        public string OllamaEmbeddingModel { get; set; } = "nomic-embed-text";
        public bool UseOllamaIfAvailable { get; set; } = true;
        public bool HasCompletedOnboarding { get; set; } = false;
        public bool EnableBuiltInOcr { get; set; } = true;
        public int ChunkWordSize { get; set; } = 350;
        public int ChunkWordOverlap { get; set; } = 50;
        public float HybridKeywordWeight { get; set; } = 0.5f;
        public List<string> ExcludedFolderNames { get; set; } = new()
        {
            "node_modules", ".git", ".vs", "bin", "obj", "AppData", "$Recycle.Bin", "Program Files", "Windows"
        };
        public List<string> ExcludedExtensions { get; set; } = new()
        {
            ".exe", ".dll", ".zip", ".tar", ".gz", ".7z", ".iso", ".bin", ".tmp"
        };
        public List<string> SupportedExtensions { get; set; } = new()
        {
            ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".csv", ".txt", ".md", ".eml", ".png", ".jpg", ".jpeg"
        };
    }
}

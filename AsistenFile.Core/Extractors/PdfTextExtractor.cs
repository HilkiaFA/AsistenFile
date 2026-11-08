using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Models;
using AsistenFile.Core.Ocr;
using UglyToad.PdfPig;

namespace AsistenFile.Core.Extractors
{
    public class PdfTextExtractor : ITextExtractor
    {
        private readonly IOcrService? _ocrService;

        public PdfTextExtractor(IOcrService? ocrService = null)
        {
            _ocrService = ocrService;
        }

        public bool CanHandle(string extension)
        {
            return string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<ExtractedPage>> ExtractAsync(string filePath, CancellationToken ct = default)
        {
            var results = new List<ExtractedPage>();

            await Task.Run(async () =>
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var document = PdfDocument.Open(stream);
                foreach (var page in document.GetPages())
                {
                    if (ct.IsCancellationRequested) break;

                    var pageText = page.Text?.Trim() ?? string.Empty;

                    // If page has almost no text and OCR is enabled, try extracting text from images
                    if (pageText.Length < 40 && _ocrService != null && _ocrService.IsAvailable)
                    {
                        var ocrTextBuilder = new StringBuilder();
                        try
                        {
                            foreach (var image in page.GetImages())
                            {
                                if (image.TryGetPng(out var pngBytes) && pngBytes.Length > 0)
                                {
                                    using var memStream = new MemoryStream(pngBytes);
                                    var text = await _ocrService.ExtractTextFromStreamAsync(memStream, ct);
                                    if (!string.IsNullOrWhiteSpace(text))
                                    {
                                        ocrTextBuilder.AppendLine(text);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Ignore OCR image decode errors gracefully
                        }

                        var ocrResult = ocrTextBuilder.ToString().Trim();
                        if (!string.IsNullOrEmpty(ocrResult))
                        {
                            pageText = string.IsNullOrEmpty(pageText) ? ocrResult : $"{pageText}\n{ocrResult}";
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        results.Add(new ExtractedPage
                        {
                            PageNumber = page.Number,
                            SectionTitle = $"Halaman {page.Number}",
                            Content = pageText
                        });
                    }
                }
            }, ct);

            return results;
        }
    }
}

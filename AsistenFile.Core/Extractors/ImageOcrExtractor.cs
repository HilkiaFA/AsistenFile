using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Models;
using AsistenFile.Core.Ocr;

namespace AsistenFile.Core.Extractors
{
    public class ImageOcrExtractor : ITextExtractor
    {
        private readonly IOcrService _ocrService;
        private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif"
        };

        public ImageOcrExtractor(IOcrService ocrService)
        {
            _ocrService = ocrService;
        }

        public bool CanHandle(string extension)
        {
            return Supported.Contains(extension);
        }

        public async Task<List<ExtractedPage>> ExtractAsync(string filePath, CancellationToken ct = default)
        {
            var results = new List<ExtractedPage>();
            if (!_ocrService.IsAvailable) return results;

            var text = await _ocrService.ExtractTextFromImageAsync(filePath, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                results.Add(new ExtractedPage
                {
                    PageNumber = 1,
                    SectionTitle = "Gambar (Hasil OCR)",
                    Content = text.Trim()
                });
            }

            return results;
        }
    }
}

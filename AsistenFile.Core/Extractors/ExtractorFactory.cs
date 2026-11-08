using System;
using System.Collections.Generic;
using System.IO;
using AsistenFile.Core.Ocr;

namespace AsistenFile.Core.Extractors
{
    public interface IExtractorFactory
    {
        ITextExtractor? GetExtractor(string filePath);
        bool IsSupported(string filePath);
    }

    public class ExtractorFactory : IExtractorFactory
    {
        private readonly List<ITextExtractor> _extractors;

        public ExtractorFactory(IOcrService ocrService)
        {
            _extractors = new List<ITextExtractor>
            {
                new PdfTextExtractor(ocrService),
                new DocxTextExtractor(),
                new ExcelTextExtractor(),
                new PlainTextExtractor(),
                new EmailTextExtractor(),
                new ImageOcrExtractor(ocrService)
            };
        }

        public ITextExtractor? GetExtractor(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(ext)) return null;

            foreach (var extractor in _extractors)
            {
                if (extractor.CanHandle(ext))
                {
                    return extractor;
                }
            }

            return null;
        }

        public bool IsSupported(string filePath)
        {
            return GetExtractor(filePath) != null;
        }
    }
}

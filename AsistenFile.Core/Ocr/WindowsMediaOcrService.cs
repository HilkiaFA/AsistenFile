using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace AsistenFile.Core.Ocr
{
    public interface IOcrService
    {
        bool IsAvailable { get; }
        Task<string> ExtractTextFromImageAsync(string imagePath, CancellationToken ct = default);
        Task<string> ExtractTextFromStreamAsync(Stream imageStream, CancellationToken ct = default);
    }

    public class WindowsMediaOcrService : IOcrService
    {
        private OcrEngine? _ocrEngine;
        private bool _isInitialized;
        private readonly object _lock = new();

        public bool IsAvailable
        {
            get
            {
                EnsureInitialized();
                return _ocrEngine != null;
            }
        }

        private void EnsureInitialized()
        {
            if (_isInitialized) return;
            lock (_lock)
            {
                if (_isInitialized) return;
                try
                {
                    // Attempt to create OCR engine based on user profile languages (Indonesian / English)
                    _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (_ocrEngine == null)
                    {
                        var lang = new Windows.Globalization.Language("en-US");
                        if (OcrEngine.IsLanguageSupported(lang))
                        {
                            _ocrEngine = OcrEngine.TryCreateFromLanguage(lang);
                        }
                    }
                }
                catch
                {
                    _ocrEngine = null;
                }
                finally
                {
                    _isInitialized = true;
                }
            }
        }

        public async Task<string> ExtractTextFromImageAsync(string imagePath, CancellationToken ct = default)
        {
            if (!File.Exists(imagePath)) return string.Empty;

            try
            {
                using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return await ExtractTextFromStreamAsync(fs, ct);
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task<string> ExtractTextFromStreamAsync(Stream imageStream, CancellationToken ct = default)
        {
            EnsureInitialized();
            if (_ocrEngine == null) return string.Empty;

            try
            {
                imageStream.Position = 0;
                using var memStream = new MemoryStream();
                await imageStream.CopyToAsync(memStream, ct);
                var bytes = memStream.ToArray();

                using var randomAccessStream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(randomAccessStream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(bytes);
                    await writer.StoreAsync().AsTask(ct);
                }

                var decoder = await BitmapDecoder.CreateAsync(randomAccessStream).AsTask(ct);
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync().AsTask(ct);

                // OcrEngine requires software bitmap in Bgra8 or Nv12 format
                if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    softwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Straight)
                {
                    softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }

                var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap).AsTask(ct);
                if (ocrResult == null || ocrResult.Lines == null) return string.Empty;

                var sb = new StringBuilder();
                foreach (var line in ocrResult.Lines)
                {
                    sb.AppendLine(line.Text);
                }

                return sb.ToString().Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Models;

namespace AsistenFile.Core.Extractors
{
    public interface ITextExtractor
    {
        bool CanHandle(string extension);
        Task<List<ExtractedPage>> ExtractAsync(string filePath, CancellationToken ct = default);
    }
}

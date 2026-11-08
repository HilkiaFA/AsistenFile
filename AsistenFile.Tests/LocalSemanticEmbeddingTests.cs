using System;
using System.Threading.Tasks;
using AsistenFile.Core.Embeddings;
using Xunit;

namespace AsistenFile.Tests
{
    public class LocalSemanticEmbeddingTests
    {
        private readonly LocalSemanticEmbeddingService _service = new(256);

        [Fact]
        public async Task GenerateEmbedding_ReturnsNormalizedVector()
        {
            var text = "Perjanjian sewa menyewa ruang kantor Jakarta Selatan";
            var vec = await _service.GenerateEmbeddingAsync(text);

            Assert.Equal(256, vec.Length);

            // Verify L2 norm is approximately 1.0
            float sumSquares = 0f;
            foreach (var v in vec) sumSquares += v * v;
            Assert.True(Math.Abs(MathF.Sqrt(sumSquares) - 1.0f) < 0.01f);
        }

        [Fact]
        public async Task CosineSimilarity_IdenticalText_Returns1()
        {
            var text = "Invoice tagihan pembayaran jasa desain website bulan Maret";
            var vec1 = await _service.GenerateEmbeddingAsync(text);
            var vec2 = await _service.GenerateEmbeddingAsync(text);

            float sim = _service.ComputeCosineSimilarity(vec1, vec2);
            Assert.True(Math.Abs(sim - 1.0f) < 0.001f);
        }

        [Fact]
        public async Task CosineSimilarity_SemanticallyRelated_HigherThanUnrelated()
        {
            var vecBase = await _service.GenerateEmbeddingAsync("Perjanjian kontrak sewa gedung dan kantor");
            var vecRelated = await _service.GenerateEmbeddingAsync("Surat perjanjian sewa menyewa ruko dan gedung kantor");
            var vecUnrelated = await _service.GenerateEmbeddingAsync("Resep masakan ayam goreng krispi bumbu rempah");

            float simRelated = _service.ComputeCosineSimilarity(vecBase, vecRelated);
            float simUnrelated = _service.ComputeCosineSimilarity(vecBase, vecUnrelated);

            Assert.True(simRelated > simUnrelated);
            Assert.True(simRelated > 0.5f);
        }
    }
}

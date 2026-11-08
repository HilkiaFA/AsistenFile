using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AsistenFile.Core.AI
{
    public interface ILanguageModelService
    {
        string ServiceName { get; }
        Task<bool> IsAvailableAsync(CancellationToken ct = default);
        Task<List<string>> GetInstalledModelsAsync(CancellationToken ct = default);
        Task<string> GenerateAnswerAsync(string prompt, string context, CancellationToken ct = default);
    }

    public class OllamaClientService : ILanguageModelService
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private string _model;

        public string ServiceName => $"Ollama Local ({_model})";

        public OllamaClientService(string endpoint = "http://localhost:11434", string model = "llama3.2:3b")
        {
            _endpoint = endpoint.TrimEnd('/');
            _model = model;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public void SetModel(string model)
        {
            if (!string.IsNullOrWhiteSpace(model))
            {
                _model = model;
            }
        }

        public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            try
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(2));
                var res = await _httpClient.GetAsync($"{_endpoint}/api/tags", cts.Token);
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> GetInstalledModelsAsync(CancellationToken ct = default)
        {
            var models = new List<string>();
            try
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(3));
                var res = await _httpClient.GetAsync($"{_endpoint}/api/tags", cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    var body = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("models", out var modelsProp))
                    {
                        foreach (var m in modelsProp.EnumerateArray())
                        {
                            if (m.TryGetProperty("name", out var nameProp))
                            {
                                models.Add(nameProp.GetString() ?? string.Empty);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Unreachable or offline
            }
            return models;
        }

        public async Task<string> GenerateAnswerAsync(string prompt, string context, CancellationToken ct = default)
        {
            var systemInstruction = @"Anda adalah Asisten Dokumen Pribadi yang cerdas dan terpercaya, berjalan 100% lokal di komputer pengguna.
Prinsip utama Anda:
1. Jawab pertanyaan pengguna HANYA berdasarkan konteks dokumen lokal yang diberikan di bawah.
2. Setiap klaim atau informasi dalam jawaban harus jelas merujuk pada [Nama File] dan [Halaman/Bagian].
3. Jika informasi yang ditanyakan TIDAK terdapat dalam dokumen, katakan dengan jujur: 'Informasi tersebut tidak ditemukan dalam dokumen yang diindeks.' Jangan pernah mengarang data.
4. Gunakan bahasa Indonesia yang ringkas, profesional, dan to the point.";

            var fullPrompt = $"{systemInstruction}\n\n=== KONTEKS DOKUMEN LOKAL ===\n{context}\n\n=== PERTANYAAN PENGGUNA ===\n{prompt}\n\n=== JAWABAN BERDASARKAN DOKUMEN ===";

            var payload = new
            {
                model = _model,
                prompt = fullPrompt,
                stream = false,
                options = new
                {
                    temperature = 0.2,
                    top_p = 0.9,
                    num_ctx = 4096
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var res = await _httpClient.PostAsync($"{_endpoint}/api/generate", content, ct);
            res.EnsureSuccessStatusCode();

            var body = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("response", out var respProp))
            {
                return respProp.GetString()?.Trim() ?? string.Empty;
            }

            return "Gagal mendapatkan respon dari model lokal.";
        }
    }
}

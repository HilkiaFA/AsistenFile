using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AsistenFile.Core.AI
{
    public class ModelDownloadProgress
    {
        public string ModelName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public long CompletedBytes { get; set; }
        public long TotalBytes { get; set; }
        public int Percent { get; set; }
        public bool IsCompleted { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IOllamaInstallerService
    {
        bool IsOllamaInstalled();
        Task<bool> IsOllamaRunningAsync(CancellationToken ct = default);
        Task<bool> HasAnyModelsAsync(CancellationToken ct = default);
        Task StartOllamaProcessAsync();
        Task DownloadOllamaInstallerAsync(IProgress<ModelDownloadProgress> progress, CancellationToken ct = default);
        Task PullModelAsync(string modelName, IProgress<ModelDownloadProgress> progress, CancellationToken ct = default);
        event EventHandler<ModelDownloadProgress>? BackgroundDownloadProgressChanged;
    }

    public class OllamaInstallerService : IOllamaInstallerService
    {
        private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
        {
            UseProxy = false, // Prevents Windows proxy delays for 127.0.0.1
            ConnectTimeout = TimeSpan.FromSeconds(3)
        })
        {
            Timeout = TimeSpan.FromHours(2) // Long timeout for large model downloads
        };

        private const string DefaultEndpoint = "http://127.0.0.1:11434";
        private const string InstallerUrl = "https://ollama.com/download/OllamaSetup.exe";

        public event EventHandler<ModelDownloadProgress>? BackgroundDownloadProgressChanged;

        public bool IsOllamaInstalled()
        {
            // Check PATH
            try
            {
                var psi = new ProcessStartInfo("ollama", "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(1500);
                    return p.ExitCode == 0;
                }
            }
            catch
            {
                // Not in PATH
            }

            // Check default local directory
            var localPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe");
            return File.Exists(localPath);
        }

        public async Task<bool> IsOllamaRunningAsync(CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(2));

                var res = await HttpClient.GetAsync($"{DefaultEndpoint}/api/tags", cts.Token);
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> HasAnyModelsAsync(CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(2));

                var res = await HttpClient.GetAsync($"{DefaultEndpoint}/api/tags", cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("models", out var modelsProp))
                    {
                        return modelsProp.GetArrayLength() > 0;
                    }
                }
            }
            catch
            {
                // Silently return false if offline
            }
            return false;
        }

        public async Task StartOllamaProcessAsync()
        {
            if (await IsOllamaRunningAsync()) return;

            string exePath = "ollama";
            var localPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe");
            if (File.Exists(localPath))
            {
                exePath = localPath;
            }

            try
            {
                var psi = new ProcessStartInfo(exePath, "serve")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);

                // Wait up to 5 seconds for Ollama server to wake up
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(500);
                    if (await IsOllamaRunningAsync()) break;
                }
            }
            catch
            {
                // Ignore if cannot start directly
            }
        }

        public async Task DownloadOllamaInstallerAsync(IProgress<ModelDownloadProgress> progress, CancellationToken ct = default)
        {
            var tempInstaller = Path.Combine(Path.GetTempPath(), "OllamaSetup.exe");

            progress.Report(new ModelDownloadProgress
            {
                ModelName = "Ollama Installer",
                Status = "Mengunduh installer resmi Ollama...",
                Percent = 0
            });

            using (var response = await HttpClient.GetAsync(InstallerUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? 100_000_000;

                using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
                using (var fileStream = new FileStream(tempInstaller, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[16384];
                    long totalRead = 0;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, ct);
                        totalRead += read;
                        int pct = (int)((totalRead / (float)totalBytes) * 100);

                        progress.Report(new ModelDownloadProgress
                        {
                            ModelName = "Ollama Installer",
                            Status = $"Mengunduh installer: {totalRead / 1_048_576} MB / {totalBytes / 1_048_576} MB",
                            CompletedBytes = totalRead,
                            TotalBytes = totalBytes,
                            Percent = Math.Clamp(pct, 0, 99)
                        });
                    }
                }
            }

            progress.Report(new ModelDownloadProgress
            {
                ModelName = "Ollama Installer",
                Status = "Menjalankan instalasi Ollama...",
                Percent = 99
            });

            // Launch installer
            var proc = Process.Start(tempInstaller);
            if (proc != null)
            {
                await proc.WaitForExitAsync(ct);
            }

            // Wait for service to become available
            await StartOllamaProcessAsync();

            progress.Report(new ModelDownloadProgress
            {
                ModelName = "Ollama Installer",
                Status = "Ollama berhasil dipasang!",
                Percent = 100,
                IsCompleted = true
            });
        }

        public async Task PullModelAsync(string modelName, IProgress<ModelDownloadProgress> progress, CancellationToken ct = default)
        {
            // Ensure Ollama is running
            if (!await IsOllamaRunningAsync(ct))
            {
                await StartOllamaProcessAsync();
            }

            // Attempt via HTTP REST streaming API /api/pull
            try
            {
                var payload = new { name = modelName, stream = true };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{DefaultEndpoint}/api/pull")
                {
                    Content = content
                };

                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string? line;
                while ((line = await reader.ReadLineAsync(ct)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;

                        var status = root.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "" : "";
                        long completed = root.TryGetProperty("completed", out var cProp) ? cProp.GetInt64() : 0;
                        long total = root.TryGetProperty("total", out var tProp) ? tProp.GetInt64() : 0;

                        int pct = total > 0 ? (int)((completed / (float)total) * 100) : 0;
                        bool isSuccess = status.Contains("success", StringComparison.OrdinalIgnoreCase);

                        var report = new ModelDownloadProgress
                        {
                            ModelName = modelName,
                            Status = status,
                            CompletedBytes = completed,
                            TotalBytes = total,
                            Percent = isSuccess ? 100 : Math.Clamp(pct, 0, 99),
                            IsCompleted = isSuccess
                        };

                        progress.Report(report);
                        BackgroundDownloadProgressChanged?.Invoke(this, report);

                        if (isSuccess) return;
                    }
                    catch
                    {
                        // Ignore individual line json parse error
                    }
                }

                // Final completion report
                var finalReport = new ModelDownloadProgress
                {
                    ModelName = modelName,
                    Status = "Selesai diunduh & siap digunakan!",
                    Percent = 100,
                    IsCompleted = true
                };
                progress.Report(finalReport);
                BackgroundDownloadProgressChanged?.Invoke(this, finalReport);
            }
            catch
            {
                // Fallback via CLI if HTTP streaming encountered network issue
                await PullModelViaCliAsync(modelName, progress, ct);
            }
        }

        private async Task PullModelViaCliAsync(string modelName, IProgress<ModelDownloadProgress> progress, CancellationToken ct)
        {
            var psi = new ProcessStartInfo("ollama", $"pull {modelName}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) throw new Exception("Gagal menjalankan perintah 'ollama pull'");

            progress.Report(new ModelDownloadProgress
            {
                ModelName = modelName,
                Status = $"Mengunduh {modelName} lewat Ollama CLI...",
                Percent = 10
            });

            var outputTask = Task.Run(async () =>
            {
                while (!proc.StandardOutput.EndOfStream)
                {
                    var line = await proc.StandardOutput.ReadLineAsync(ct);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var report = new ModelDownloadProgress
                        {
                            ModelName = modelName,
                            Status = line,
                            Percent = 50
                        };
                        progress.Report(report);
                        BackgroundDownloadProgressChanged?.Invoke(this, report);
                    }
                }
            }, ct);

            await proc.WaitForExitAsync(ct);
            await outputTask;

            if (proc.ExitCode == 0)
            {
                var finalReport = new ModelDownloadProgress
                {
                    ModelName = modelName,
                    Status = $"Model {modelName} berhasil dipasang!",
                    Percent = 100,
                    IsCompleted = true
                };
                progress.Report(finalReport);
                BackgroundDownloadProgressChanged?.Invoke(this, finalReport);
            }
            else
            {
                var err = await proc.StandardError.ReadToEndAsync(ct);
                throw new Exception($"Gagal mengunduh model: {err}");
            }
        }
    }
}

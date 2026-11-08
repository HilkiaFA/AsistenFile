using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AsistenFile.Core.Models;

namespace AsistenFile.Core.Config
{
    public interface ISettingsService
    {
        AppSettings Current { get; }
        Task LoadAsync();
        Task SaveAsync();
    }

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;
        private AppSettings _current;

        public AppSettings Current => _current;

        public SettingsService(string? customPath = null)
        {
            if (string.IsNullOrEmpty(customPath))
            {
                var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AsistenFile");
                Directory.CreateDirectory(appDir);
                _settingsFilePath = Path.Combine(appDir, "settings.json");
            }
            else
            {
                _settingsFilePath = customPath;
            }

            _current = new AppSettings();
        }

        public async Task LoadAsync()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = await File.ReadAllTextAsync(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        _current = settings;
                    }
                }
                else
                {
                    await SaveAsync();
                }
            }
            catch
            {
                _current = new AppSettings();
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_current, options);
                await File.WriteAllTextAsync(_settingsFilePath, json);
            }
            catch
            {
                // Silently handle if write fails
            }
        }
    }
}

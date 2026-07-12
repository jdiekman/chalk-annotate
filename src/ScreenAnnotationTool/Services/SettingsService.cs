using System;
using System.IO;
using System.Text.Json;

namespace ScreenAnnotationTool.Services;

public class SettingsService
{
    private const long MaximumSettingsFileSize = 64 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    public SettingsService(string filePath) => _filePath = filePath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSettings();
            if (new FileInfo(_filePath).Length > MaximumSettingsFileSize)
                return new AppSettings();
            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string? tempPath = null;
        try
        {
            settings.Normalize();
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            tempPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            // One durability mechanism is plenty for a small preferences file: the
            // temp-write + atomic move below already prevents a torn settings.json.
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, _filePath, overwrite: true);
            tempPath = null;
        }
        finally
        {
            if (tempPath != null)
            {
                try { File.Delete(tempPath); }
                catch { /* preserve the original save exception */ }
            }
        }
    }
}

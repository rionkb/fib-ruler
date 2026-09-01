using System.IO;
using System.Text.Json;
using FibRuler.Models;

namespace FibRuler.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FibRuler",
        "settings.json");

    public AppSettings? Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return null;
            }

            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(_settingsPath), SerializerOptions);
            return settings?.SettingsVersion == 1
                && settings.PointA is not null
                && settings.PointB is not null
                && settings.FibLevels is not null
                ? settings
                : null;
        }
        catch (IOException exception)
        {
            LogLoadFailure(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogLoadFailure(exception);
        }
        catch (JsonException exception)
        {
            LogLoadFailure(exception);
        }

        return null;
    }

    public void Save(AppSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_settingsPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (IOException exception)
        {
            LogSaveFailure(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogSaveFailure(exception);
        }
        catch (JsonException exception)
        {
            LogSaveFailure(exception);
        }
    }

    private void LogLoadFailure(Exception exception) =>
        System.Diagnostics.Debug.WriteLine($"Fib Ruler: 設定の読み込みに失敗しました。デフォルト設定で起動します。{exception}");

    private void LogSaveFailure(Exception exception) =>
        System.Diagnostics.Debug.WriteLine($"Fib Ruler: 設定の保存に失敗しました。{exception}");
}

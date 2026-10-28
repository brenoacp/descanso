using System.Text.Json;
using System.Text.Json.Serialization;

namespace Descanso.Core;

/// <summary>
/// Lê e grava a configuração em <c>%APPDATA%\Descanso\settings.json</c>.
/// A gravação é atômica (arquivo temporário + replace) para não deixar um JSON
/// pela metade se o processo morrer no meio.
/// </summary>
public sealed class SettingsStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _gate = new();
    private string _lastContent = string.Empty;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private bool _disposed;

    public SettingsStore(string? directory = null)
    {
        Directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Descanso");

        FilePath = Path.Combine(Directory, "settings.json");
    }

    public string Directory { get; }

    public string FilePath { get; }

    /// <summary>Correções aplicadas na última leitura (valores fora de faixa).</summary>
    public IReadOnlyList<string> LastFixes { get; private set; } = Array.Empty<string>();

    /// <summary>Disparado quando o arquivo muda fora do aplicativo (edição manual).</summary>
    public event Action<Settings>? ExternalChange;

    public Settings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(FilePath))
            {
                var fresh = new Settings();
                fresh.Normalize();
                _lastContent = Serialize(fresh);
                LastFixes = Array.Empty<string>();
                return fresh;
            }

            try
            {
                var text = File.ReadAllText(FilePath);
                var settings = Deserialize(text);
                _lastContent = text;
                return settings;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                BackupUnreadable();
                var fallback = new Settings();
                fallback.Normalize();
                _lastContent = Serialize(fallback);
                LastFixes = new[] { "O arquivo de configuração estava ilegível; os padrões foram restaurados." };
                return fallback;
            }
        }
    }

    public void Save(Settings settings)
    {
        lock (_gate)
        {
            settings.Normalize();
            var json = Serialize(settings);
            System.IO.Directory.CreateDirectory(Directory);

            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);

            if (File.Exists(FilePath))
            {
                File.Replace(temp, FilePath, null);
            }
            else
            {
                File.Move(temp, FilePath);
            }

            _lastContent = json;
        }
    }

    public void StartWatching()
    {
        if (_watcher is not null)
        {
            return;
        }

        System.IO.Directory.CreateDirectory(Directory);

        _watcher = new FileSystemWatcher(Directory, Path.GetFileName(FilePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };

        _watcher.Changed += OnFileTouched;
        _watcher.Created += OnFileTouched;
        _watcher.Renamed += OnFileTouched;
    }

    private void OnFileTouched(object sender, FileSystemEventArgs e)
    {
        // Editores gravam em várias etapas; espera o arquivo sossegar.
        _debounce?.Dispose();
        _debounce = new Timer(_ => ReloadFromDisk(), null, 500, Timeout.Infinite);
    }

    private void ReloadFromDisk()
    {
        Settings? reloaded = null;

        lock (_gate)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }

                var text = File.ReadAllText(FilePath);
                if (text == _lastContent)
                {
                    return;
                }

                reloaded = Deserialize(text);
                _lastContent = text;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // Gravação em andamento: a próxima notificação do watcher resolve.
                return;
            }
        }

        // Fora do lock: o handler pode chamar Save.
        ExternalChange?.Invoke(reloaded);
    }

    private Settings Deserialize(string text)
    {
        var settings = JsonSerializer.Deserialize<Settings>(text, JsonOptions) ?? new Settings();
        LastFixes = settings.Normalize();
        return settings;
    }

    private static string Serialize(Settings settings) =>
        JsonSerializer.Serialize(settings, JsonOptions);

    private void BackupUnreadable()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Copy(FilePath, $"{FilePath}.corrompido", overwrite: true);
            }
        }
        catch (IOException)
        {
            // Backup é conveniência, não requisito.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debounce?.Dispose();
        _watcher?.Dispose();
    }
}

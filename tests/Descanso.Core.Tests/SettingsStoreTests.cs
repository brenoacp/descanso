using Descanso.Core;

namespace Descanso.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "descanso-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Sem_arquivo_usa_os_padroes()
    {
        using var store = new SettingsStore(_dir);

        var settings = store.Load();

        Assert.Equal(50, settings.IntervalMinutes);
        Assert.False(File.Exists(store.FilePath));
    }

    [Fact]
    public void Salva_e_rele_preservando_os_valores()
    {
        using var store = new SettingsStore(_dir);
        var settings = store.Load();
        settings.IntervalMinutes = 35;
        settings.SoundEnabled = false;
        settings.WorkingHours.Start = new TimeOnly(7, 30);

        store.Save(settings);
        var reloaded = store.Load();

        Assert.Equal(35, reloaded.IntervalMinutes);
        Assert.False(reloaded.SoundEnabled);
        Assert.Equal(new TimeOnly(7, 30), reloaded.WorkingHours.Start);
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void Grava_json_legivel()
    {
        using var store = new SettingsStore(_dir);
        store.Save(store.Load());

        var json = File.ReadAllText(store.FilePath);

        Assert.Contains("\"intervalMinutes\": 50", json);
        Assert.Contains("\"workingHours\"", json);
    }

    [Fact]
    public void Arquivo_corrompido_volta_aos_padroes_e_e_preservado()
    {
        Directory.CreateDirectory(_dir);
        using var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, "{ isto não é json");

        var settings = store.Load();

        Assert.Equal(50, settings.IntervalMinutes);
        Assert.True(File.Exists(store.FilePath + ".corrompido"));
        Assert.NotEmpty(store.LastFixes);
    }

    [Fact]
    public void Arquivo_editado_a_mao_e_normalizado_na_leitura()
    {
        Directory.CreateDirectory(_dir);
        using var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, """{ "intervalMinutes": 5000 }""");

        var settings = store.Load();

        Assert.Equal(480, settings.IntervalMinutes);
        Assert.NotEmpty(store.LastFixes);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Diretório temporário: falhar aqui não invalida o teste.
        }
    }
}

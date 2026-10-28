using Descanso.Core;

namespace Descanso.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void Normalize_corrige_valores_fora_de_faixa()
    {
        var settings = new Settings
        {
            IntervalMinutes = 0,
            BreakSeconds = 1,
            LongBreakEvery = 99,
            Snooze = new SnoozeSettings { Minutes = 0, MaxPerBreak = 42 },
        };

        var fixes = settings.Normalize();

        Assert.NotEmpty(fixes);
        Assert.Equal(1, settings.IntervalMinutes);
        Assert.Equal(10, settings.BreakSeconds);
        Assert.Equal(20, settings.LongBreakEvery);
        Assert.Equal(1, settings.Snooze.Minutes);
        Assert.Equal(10, settings.Snooze.MaxPerBreak);
    }

    [Fact]
    public void Normalize_restaura_dias_quando_horario_esta_ativo_sem_dias()
    {
        var settings = new Settings
        {
            WorkingHours = new WorkingHoursSettings { Enabled = true, Days = new List<int>() },
        };

        settings.Normalize();

        Assert.Equal(new List<int> { 1, 2, 3, 4, 5 }, settings.WorkingHours.Days);
    }

    [Fact]
    public void Normalize_impede_descanso_menor_que_o_limiar()
    {
        var settings = new Settings
        {
            Idle = new IdleSettings { ThresholdSeconds = 300, ResetAfterSeconds = 60 },
        };

        settings.Normalize();

        Assert.Equal(300, settings.Idle.ResetAfterSeconds);
    }

    [Fact]
    public void Clone_nao_compartilha_listas()
    {
        var original = new Settings();
        var copy = original.Clone();

        copy.WorkingHours.Days.Add(6);

        Assert.DoesNotContain(6, original.WorkingHours.Days);
    }
}

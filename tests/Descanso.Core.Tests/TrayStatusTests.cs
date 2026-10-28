using Descanso.Core;

namespace Descanso.Core.Tests;

public class TrayStatusTests
{
    private static readonly DateTimeOffset TuesdayMorning =
        new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static Settings Defaults() => new()
    {
        IntervalMinutes = 50,
        BreakSeconds = 300,
        LongBreakEvery = 4,
        LongBreakSeconds = 900,
        WorkingHours = new WorkingHoursSettings { Enabled = false },
        Idle = new IdleSettings { ThresholdSeconds = 120, ResetAfterSeconds = 300 },
        Snooze = new SnoozeSettings { Minutes = 5, MaxPerBreak = 3 },
    };

    [Fact]
    public void Comeca_cheio_e_contando()
    {
        var scheduler = new BreakScheduler(Defaults());

        var status = TrayStatus.From(scheduler, scheduler.Settings, TuesdayMorning);

        Assert.Equal(TrayState.Counting, status.State);
        Assert.Equal(1.0, status.Remaining, 3);
    }

    [Fact]
    public void Esvazia_na_proporcao_do_tempo()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(25));

        var status = TrayStatus.From(scheduler, scheduler.Settings, now);

        Assert.Equal(TrayState.Counting, status.State);
        Assert.Equal(0.5, status.Remaining, 3);
    }

    [Fact]
    public void Adiamento_deixa_um_resto_pequeno()
    {
        var scheduler = new BreakScheduler(Defaults());

        scheduler.ForceBreak();
        Assert.True(scheduler.Snooze());

        var status = TrayStatus.From(scheduler, scheduler.Settings, TuesdayMorning);

        // 5 min de adiamento em um ciclo de 50 min.
        Assert.Equal(0.1, status.Remaining, 3);
    }

    [Fact]
    public void Pausa_manual_congela_e_fica_cinza()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(20));
        scheduler.Pause(TimeSpan.FromMinutes(30), now);

        var paused = TrayStatus.From(scheduler, scheduler.Settings, now);
        Assert.Equal(TrayState.Paused, paused.State);
        Assert.Equal(0.6, paused.Remaining, 3);

        Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(20));

        var later = TrayStatus.From(scheduler, scheduler.Settings, now);
        Assert.Equal(TrayState.Paused, later.State);
        Assert.Equal(0.6, later.Remaining, 3);
    }

    [Fact]
    public void Fora_do_horario_fica_cinza_sem_consumir_o_ciclo()
    {
        var settings = Defaults();
        settings.WorkingHours = new WorkingHoursSettings
        {
            Enabled = true,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(18, 0),
        };

        var scheduler = new BreakScheduler(settings);
        var now = new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.Zero);

        Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(30));

        var status = TrayStatus.From(scheduler, settings, now);

        Assert.Equal(TrayState.OffHours, status.State);
        Assert.Equal(1.0, status.Remaining, 3);
    }

    [Fact]
    public void Fracao_fica_sempre_entre_zero_e_um()
    {
        var scheduler = new BreakScheduler(Defaults());

        var status = TrayStatus.From(scheduler, scheduler.Settings, TuesdayMorning);

        Assert.InRange(status.Remaining, 0.0, 1.0);
    }
}

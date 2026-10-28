using Descanso.Core;

namespace Descanso.Core.Tests;

public class BreakSchedulerTests
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
    public void Dispara_uma_vez_ao_completar_o_intervalo()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        var fires = Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(50));

        var fire = Assert.Single(fires);
        Assert.Equal(BreakKind.Short, fire.Kind);
        Assert.Equal(TimeSpan.FromMinutes(5), fire.BreakDuration);
        Assert.Equal(3, fire.SnoozesRemaining);
    }

    [Fact]
    public void Nao_dispara_antes_do_intervalo()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        var fires = Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(49));

        Assert.Empty(fires);
        Assert.Equal(TimeSpan.FromMinutes(1), scheduler.UntilNextBreak);
    }

    [Fact]
    public void Suspensao_longa_nao_dispara_lembrete_falso()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        // Um único tick de duas horas: o processo ficou suspenso.
        var afterResume = scheduler.Tick(new SchedulerContext(now, TimeSpan.FromHours(2), TimeSpan.Zero));
        Assert.False(afterResume.BreakDue);
        Assert.Equal(TimeSpan.Zero, scheduler.Worked);

        now += TimeSpan.FromHours(2);
        var fires = Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(50));
        Assert.Single(fires);
    }

    [Fact]
    public void Ausencia_longa_conta_como_descanso()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        Assert.Empty(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(40)));

        // Seis minutos longe da máquina: já descansou.
        Assert.Empty(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(6), TimeSpan.FromMinutes(6)));
        Assert.Equal(TimeSpan.Zero, scheduler.Worked);

        Assert.Empty(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(40)));
        Assert.Single(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void Ausencia_curta_nao_acumula_nem_zera()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(40));

        // Três minutos ausente: acima do limiar, abaixo do descanso.
        Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(3));
        Assert.Equal(TimeSpan.FromMinutes(40), scheduler.Worked);

        // Faltam 10 minutos, mas os 3 de ausência não contam.
        Assert.Empty(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(9)));
        Assert.Single(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Fora_do_horario_nao_acumula()
    {
        var settings = Defaults();
        settings.WorkingHours = new WorkingHoursSettings { Enabled = true, Start = new TimeOnly(8, 0), End = new TimeOnly(18, 0) };
        var scheduler = new BreakScheduler(settings);
        var now = new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.Zero);

        var fires = Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(90));

        Assert.Empty(fires);
        Assert.Equal(TimeSpan.Zero, scheduler.Worked);
    }

    [Fact]
    public void Fim_de_semana_nao_dispara()
    {
        var settings = Defaults();
        settings.WorkingHours = new WorkingHoursSettings { Enabled = true, Start = new TimeOnly(8, 0), End = new TimeOnly(18, 0) };
        var scheduler = new BreakScheduler(settings);
        var saturday = new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);
        var now = saturday;

        Assert.Empty(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(90)));
    }

    [Fact]
    public void Quarta_pausa_e_longa()
    {
        var settings = Defaults();
        settings.IntervalMinutes = 10;
        settings.LongBreakEvery = 4;
        var scheduler = new BreakScheduler(settings);
        var now = TuesdayMorning;

        var fires = Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(40));

        Assert.Equal(4, fires.Count);
        Assert.All(fires.Take(3), f => Assert.Equal(BreakKind.Short, f.Kind));
        Assert.Equal(BreakKind.Long, fires[3].Kind);
        Assert.Equal(TimeSpan.FromMinutes(15), fires[3].BreakDuration);
    }

    [Fact]
    public void Adiar_reagenda_para_o_tempo_configurado()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        Assert.Single(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(50)));
        Assert.True(scheduler.Snooze());

        Assert.Empty(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(4)));
        Assert.Single(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Adiar_respeita_o_limite_por_pausa()
    {
        var scheduler = new BreakScheduler(Defaults());

        Assert.True(scheduler.Snooze());
        Assert.True(scheduler.Snooze());
        Assert.True(scheduler.Snooze());
        Assert.False(scheduler.Snooze());
        Assert.Equal(0, scheduler.SnoozesRemaining);
    }

    [Fact]
    public void Pausa_manual_congela_o_ciclo()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        scheduler.Pause(TimeSpan.FromMinutes(30), now);
        var fires = Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(20));

        Assert.Empty(fires);
        Assert.Equal(TimeSpan.Zero, scheduler.Worked);
        Assert.True(scheduler.IsPaused);
    }

    [Fact]
    public void Fim_da_pausa_manual_recomeca_o_ciclo()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        scheduler.Pause(TimeSpan.FromMinutes(10), now);

        // O tick que cai exatamente no fim da pausa apenas retoma o ciclo.
        now = now.AddMinutes(10);
        var resume = scheduler.Tick(new SchedulerContext(now, TimeSpan.FromSeconds(5), TimeSpan.Zero));

        Assert.False(resume.BreakDue);
        Assert.False(scheduler.IsPaused);
        Assert.Equal(TimeSpan.Zero, scheduler.Worked);

        // A partir da retomada, é necessário um ciclo inteiro.
        Assert.Single(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(50)));
    }

    [Fact]
    public void Fiz_a_pausa_zera_o_ciclo()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(50));
        scheduler.CompleteBreak();

        Assert.Equal(TimeSpan.Zero, scheduler.Worked);
        Assert.Equal(0, scheduler.SnoozesUsed);
    }

    [Fact]
    public void Pular_preserva_o_excedente_do_ciclo()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        // 50 min mais 10 s: o excedente vira crédito para o próximo ciclo.
        Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(50) + TimeSpan.FromSeconds(10));
        var carry = scheduler.Worked;

        scheduler.SkipBreak();

        Assert.Equal(carry, scheduler.Worked);
        Assert.Equal(TimeSpan.FromSeconds(10), carry);
    }

    [Fact]
    public void Intervalo_reduzido_em_execucao_antecipa_a_pausa()
    {
        var settings = Defaults();
        var scheduler = new BreakScheduler(settings);
        var now = TuesdayMorning;

        Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(40));

        settings.IntervalMinutes = 10;
        scheduler.ApplySettings();

        Assert.Single(Sim.Advance(scheduler, ref now, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Pausa_manual_do_usuario_nao_espera_o_intervalo()
    {
        var scheduler = new BreakScheduler(Defaults());

        var result = scheduler.ForceBreak();

        Assert.True(result.BreakDue);
        Assert.Equal(BreakKind.Short, result.Kind);
        Assert.Equal(1, scheduler.BreaksTaken);
    }

    [Fact]
    public void Ausencia_longa_durante_a_pausa_manual_nao_dispara()
    {
        var scheduler = new BreakScheduler(Defaults());
        var now = TuesdayMorning;

        scheduler.PauseUntil(now.AddHours(1));
        Assert.Empty(Sim.Advance(scheduler, ref now, TimeSpan.FromMinutes(75), TimeSpan.FromMinutes(75)));

        Assert.False(scheduler.IsPaused);
        Assert.Equal(TimeSpan.Zero, scheduler.Worked);
    }
}

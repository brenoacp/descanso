namespace Descanso.Core;

/// <summary>Como o ícone da bandeja deve se apresentar.</summary>
public enum TrayState
{
    /// <summary>Contando para a próxima pausa.</summary>
    Counting,

    /// <summary>Pausa manual ativa: a contagem está congelada.</summary>
    Paused,

    /// <summary>Fora do horário de trabalho: sem contagem.</summary>
    OffHours,
}

/// <summary>
/// Fração restante do ciclo e se ela está correndo. Separado do desenho para poder
/// ser testado com relógio falso: o ícone é só uma projeção disto.
/// </summary>
public readonly record struct TrayStatus(TrayState State, double Remaining)
{
    /// <summary>Ciclo recém-iniciado: ícone cheio.</summary>
    public static TrayStatus CycleStart { get; } = new(TrayState.Counting, 1);

    public static TrayStatus From(BreakScheduler scheduler, Settings settings, DateTimeOffset now)
    {
        var interval = scheduler.Interval.TotalSeconds;
        var remaining = interval <= 0 ? 0 : scheduler.UntilNextBreak.TotalSeconds / interval;

        // Pausa manual tem precedência: se o expediente está fechado e a pessoa
        // pausou de propósito, o que importa é a pausa.
        var state = scheduler.IsPaused
            ? TrayState.Paused
            : settings.WorkingHours.IsOpen(now)
                ? TrayState.Counting
                : TrayState.OffHours;

        return new TrayStatus(state, Math.Clamp(remaining, 0, 1));
    }
}

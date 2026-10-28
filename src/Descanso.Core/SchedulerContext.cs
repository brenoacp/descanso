namespace Descanso.Core;

public enum BreakKind
{
    Short,
    Long,
}

/// <summary>Uma leitura do ambiente pelo chamador.</summary>
/// <param name="Now">Hora local de parede.</param>
/// <param name="Elapsed">Tempo desde o tick anterior, medido por relógio monotônico.</param>
/// <param name="IdleDuration">Há quanto tempo o usuário não usa teclado ou mouse.</param>
public readonly record struct SchedulerContext(DateTimeOffset Now, TimeSpan Elapsed, TimeSpan IdleDuration);

public readonly record struct TickResult(
    bool BreakDue,
    BreakKind Kind,
    TimeSpan BreakDuration,
    int SnoozesRemaining)
{
    public static TickResult None { get; } = new(false, BreakKind.Short, TimeSpan.Zero, 0);
}

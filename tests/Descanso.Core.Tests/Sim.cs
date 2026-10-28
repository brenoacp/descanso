namespace Descanso.Core.Tests;

internal static class Sim
{
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(5);

    /// <summary>Avança o relógio em ticks de 5 s e devolve os disparos ocorridos.</summary>
    public static List<TickResult> Advance(
        BreakScheduler scheduler,
        ref DateTimeOffset now,
        TimeSpan duration,
        TimeSpan idle = default)
    {
        var fires = new List<TickResult>();
        var left = duration;

        while (left > TimeSpan.Zero)
        {
            var delta = left < Step ? left : Step;
            var result = scheduler.Tick(new SchedulerContext(now, delta, idle));
            if (result.BreakDue)
            {
                fires.Add(result);
            }

            now += delta;
            left -= delta;
        }

        return fires;
    }
}

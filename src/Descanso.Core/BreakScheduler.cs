namespace Descanso.Core;

/// <summary>
/// Decide quando avisar que é hora de descansar. Não conhece WPF, notificações
/// nem o registro do Windows: recebe o estado do ambiente e devolve eventos.
/// </summary>
public sealed class BreakScheduler
{
    /// <summary>
    /// Intervalos maiores que isso são descartados como tempo de trabalho. Protege
    /// contra suspensão, hibernação e travadas: voltar do sleep não deve disparar
    /// um lembrete falso por causa do relógio.
    /// </summary>
    public const int MaxTickSeconds = 30;

    private Settings _settings;

    private TimeSpan _worked;
    private int _breaksTaken;
    private int _snoozesUsed;
    private DateTimeOffset? _pausedUntil;
    private bool _idleRestApplied;

    public BreakScheduler(Settings settings)
    {
        _settings = settings;
    }

    public Settings Settings => _settings;

    /// <summary>Adota uma configuração nova sem perder o progresso do ciclo.</summary>
    public void UpdateSettings(Settings settings)
    {
        _settings = settings;
        ApplySettings();
    }

    public TimeSpan Interval => TimeSpan.FromMinutes(_settings.IntervalMinutes);

    /// <summary>Tempo de trabalho acumulado no ciclo atual.</summary>
    public TimeSpan Worked => _worked;

    public TimeSpan UntilNextBreak
    {
        get
        {
            var remaining = Interval - _worked;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public bool IsPaused => _pausedUntil is not null;

    public DateTimeOffset? PausedUntil => _pausedUntil;

    public int BreaksTaken => _breaksTaken;

    public int SnoozesUsed => _snoozesUsed;

    public TickResult Tick(SchedulerContext context)
    {
        // Suspensão, hibernação ou processo travado: o tempo decorrido não é trabalho.
        var elapsed = context.Elapsed;
        if (elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(MaxTickSeconds))
        {
            elapsed = TimeSpan.Zero;
        }

        if (_pausedUntil is { } until)
        {
            if (context.Now < until)
            {
                return TickResult.None;
            }

            // Voltou da pausa manual: recomeça o ciclo do zero.
            _pausedUntil = null;
            _worked = TimeSpan.Zero;
            _snoozesUsed = 0;
            _idleRestApplied = false;
            return TickResult.None;
        }

        var idle = context.IdleDuration;
        if (idle >= TimeSpan.FromSeconds(_settings.Idle.ResetAfterSeconds))
        {
            // Ausência longa = a pessoa já descansou.
            if (!_idleRestApplied)
            {
                _worked = TimeSpan.Zero;
                _snoozesUsed = 0;
                _idleRestApplied = true;
            }

            return TickResult.None;
        }

        if (idle >= TimeSpan.FromSeconds(_settings.Idle.ThresholdSeconds))
        {
            // Ausente, mas ainda não o suficiente para contar como descanso.
            return TickResult.None;
        }

        _idleRestApplied = false;

        if (!_settings.WorkingHours.IsOpen(context.Now))
        {
            return TickResult.None;
        }

        _worked += elapsed;
        if (_worked < Interval)
        {
            return TickResult.None;
        }

        _worked -= Interval;
        if (_worked < TimeSpan.Zero)
        {
            _worked = TimeSpan.Zero;
        }

        return BuildResult();
    }

    /// <summary>Dispara uma pausa pedida pelo usuário, sem esperar o intervalo.</summary>
    public TickResult ForceBreak() => BuildResult();

    private TickResult BuildResult()
    {
        _breaksTaken++;

        var every = _settings.LongBreakEvery;
        var isLong = every > 0 && _breaksTaken % every == 0;
        var duration = TimeSpan.FromSeconds(isLong ? _settings.LongBreakSeconds : _settings.BreakSeconds);

        return new TickResult(true, isLong ? BreakKind.Long : BreakKind.Short, duration, SnoozesRemaining);
    }

    public int SnoozesRemaining => Math.Max(0, _settings.Snooze.MaxPerBreak - _snoozesUsed);

    /// <summary>Adia a próxima pausa. Retorna false quando o limite já foi usado.</summary>
    public bool Snooze()
    {
        if (_snoozesUsed >= _settings.Snooze.MaxPerBreak)
        {
            return false;
        }

        _snoozesUsed++;

        var target = Interval - TimeSpan.FromMinutes(_settings.Snooze.Minutes);
        _worked = target > TimeSpan.Zero ? target : TimeSpan.Zero;

        return true;
    }

    /// <summary>A pessoa fez a pausa: recomeça o ciclo do zero.</summary>
    public void CompleteBreak()
    {
        _worked = TimeSpan.Zero;
        _snoozesUsed = 0;
    }

    /// <summary>A pessoa ignorou a pausa: mantém o excedente do ciclo.</summary>
    public void SkipBreak()
    {
        _snoozesUsed = 0;
    }

    public void Pause(TimeSpan duration, DateTimeOffset now) => PauseUntil(now + duration);

    public void PauseUntil(DateTimeOffset until) => _pausedUntil = until;

    public void Resume()
    {
        _pausedUntil = null;
        _worked = TimeSpan.Zero;
        _snoozesUsed = 0;
    }

    public void ResetCycle()
    {
        _worked = TimeSpan.Zero;
        _snoozesUsed = 0;
        _idleRestApplied = false;
    }

    /// <summary>
    /// Reaplica a configuração depois de uma edição, preservando o progresso quando
    /// o novo intervalo ainda faz sentido e limitando quando ele já foi ultrapassado.
    /// </summary>
    public void ApplySettings()
    {
        if (_worked > Interval)
        {
            _worked = Interval;
        }
    }
}

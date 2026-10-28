namespace Descanso.Core;

/// <summary>
/// Configuração persistida do aplicativo. Os valores padrão existem para que o app
/// funcione na primeira execução, sem arquivo de configuração.
/// </summary>
public sealed class Settings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Tempo de trabalho até a próxima pausa.</summary>
    public int IntervalMinutes { get; set; } = 50;

    /// <summary>Duração da pausa curta, apenas informativa na notificação.</summary>
    public int BreakSeconds { get; set; } = 300;

    /// <summary>A cada N pausas, uma pausa longa. Zero desativa.</summary>
    public int LongBreakEvery { get; set; } = 4;

    public int LongBreakSeconds { get; set; } = 900;

    public bool SoundEnabled { get; set; } = true;

    public IdleSettings Idle { get; set; } = new();

    public WorkingHoursSettings WorkingHours { get; set; } = new();

    public SnoozeSettings Snooze { get; set; } = new();

    public bool StartWithWindows { get; set; }

    public Settings Clone() => new()
    {
        Version = Version,
        IntervalMinutes = IntervalMinutes,
        BreakSeconds = BreakSeconds,
        LongBreakEvery = LongBreakEvery,
        LongBreakSeconds = LongBreakSeconds,
        SoundEnabled = SoundEnabled,
        Idle = Idle.Clone(),
        WorkingHours = WorkingHours.Clone(),
        Snooze = Snooze.Clone(),
        StartWithWindows = StartWithWindows,
    };

    /// <summary>
    /// Corrige valores fora de faixa (arquivo editado à mão, versão antiga).
    /// Retorna a descrição de cada correção aplicada.
    /// </summary>
    public List<string> Normalize()
    {
        var fixes = new List<string>();

        Idle ??= new IdleSettings();
        WorkingHours ??= new WorkingHoursSettings();
        Snooze ??= new SnoozeSettings();
        WorkingHours.Days ??= new List<int>();

        Version = CurrentVersion;

        IntervalMinutes = Clamp(IntervalMinutes, 1, 480, "Intervalo", fixes);
        BreakSeconds = Clamp(BreakSeconds, 10, 7200, "Duração da pausa", fixes);
        LongBreakEvery = Clamp(LongBreakEvery, 0, 20, "Pausa longa a cada", fixes);
        LongBreakSeconds = Clamp(LongBreakSeconds, 10, 7200, "Duração da pausa longa", fixes);

        Idle.ThresholdSeconds = Clamp(Idle.ThresholdSeconds, 30, 3600, "Limiar de inatividade", fixes);
        Idle.ResetAfterSeconds = Clamp(Idle.ResetAfterSeconds, 60, 7200, "Descanso por inatividade", fixes);

        if (Idle.ResetAfterSeconds < Idle.ThresholdSeconds)
        {
            Idle.ResetAfterSeconds = Idle.ThresholdSeconds;
            fixes.Add("Descanso por inatividade ajustado para o limiar de inatividade.");
        }

        WorkingHours.Days = WorkingHours.Days.Where(d => d is >= 1 and <= 7).Distinct().Order().ToList();
        if (WorkingHours.Enabled && WorkingHours.Days.Count == 0)
        {
            WorkingHours.Days = new List<int> { 1, 2, 3, 4, 5 };
            fixes.Add("Nenhum dia de trabalho marcado; seg–sex restaurado.");
        }

        Snooze.Minutes = Clamp(Snooze.Minutes, 1, 60, "Adiamento", fixes);
        Snooze.MaxPerBreak = Clamp(Snooze.MaxPerBreak, 0, 10, "Adiamentos por pausa", fixes);

        return fixes;
    }

    private static int Clamp(int value, int min, int max, string label, List<string> fixes)
    {
        if (value >= min && value <= max)
        {
            return value;
        }

        var clamped = Math.Clamp(value, min, max);
        fixes.Add($"{label} ajustado de {value} para {clamped} (faixa {min}–{max}).");
        return clamped;
    }
}

public sealed class IdleSettings
{
    /// <summary>A partir deste tempo sem teclado/mouse o cronômetro congela.</summary>
    public int ThresholdSeconds { get; set; } = 120;

    /// <summary>A partir deste tempo ausente, considera-se que a pessoa já descansou.</summary>
    public int ResetAfterSeconds { get; set; } = 300;

    public IdleSettings Clone() => new()
    {
        ThresholdSeconds = ThresholdSeconds,
        ResetAfterSeconds = ResetAfterSeconds,
    };
}

public sealed class WorkingHoursSettings
{
    public bool Enabled { get; set; } = true;

    public TimeOnly Start { get; set; } = new(8, 0);

    public TimeOnly End { get; set; } = new(18, 0);

    /// <summary>Dias ISO: 1 = segunda … 7 = domingo.</summary>
    public List<int> Days { get; set; } = new() { 1, 2, 3, 4, 5 };

    public WorkingHoursSettings Clone() => new()
    {
        Enabled = Enabled,
        Start = Start,
        End = End,
        Days = new List<int>(Days),
    };

    /// <summary>
    /// Indica se <paramref name="now"/> (hora local de parede) está dentro da janela.
    /// Janelas que atravessam a meia-noite usam o dia em que o turno começou.
    /// </summary>
    public bool IsOpen(DateTimeOffset now)
    {
        if (!Enabled)
        {
            return true;
        }

        var time = TimeOnly.FromTimeSpan(now.TimeOfDay);
        var today = IsoDay(now.DayOfWeek);

        if (Start <= End)
        {
            return Days.Contains(today) && time >= Start && time < End;
        }

        // Ex.: 22:00 → 06:00
        if (time >= Start)
        {
            return Days.Contains(today);
        }

        if (time < End)
        {
            return Days.Contains(IsoDay(now.AddDays(-1).DayOfWeek));
        }

        return false;
    }

    public static int IsoDay(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;
}

public sealed class SnoozeSettings
{
    public int Minutes { get; set; } = 5;

    public int MaxPerBreak { get; set; } = 3;

    public SnoozeSettings Clone() => new()
    {
        Minutes = Minutes,
        MaxPerBreak = MaxPerBreak,
    };
}

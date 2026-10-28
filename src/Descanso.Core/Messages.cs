namespace Descanso.Core;

/// <summary>Textos em português do Brasil. Um lugar só, para revisar depois.</summary>
public static class Messages
{
    public const string DoneButton = "Fiz a pausa";

    public const string SkipButton = "Pular";

    public static string SnoozeButton(int minutes) => $"Adiar {minutes} min";

    public static string Title(BreakKind kind) =>
        kind == BreakKind.Long ? "Pausa longa" : "Hora de descansar";

    public static string Body(BreakKind kind, TimeSpan duration, int intervalMinutes, int breaksTaken) =>
        kind == BreakKind.Long
            ? $"Você completou {breaksTaken} ciclos. Faça uma pausa de {Format(duration)} longe da tela."
            : $"Você está há {intervalMinutes} min na mesma tarefa. Levante, alongue-se e olhe para longe por {Format(duration)}.";

    public static string Format(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            var minutes = (int)Math.Round(duration.TotalMinutes);
            return minutes == 1 ? "1 minuto" : $"{minutes} minutos";
        }

        return $"{(int)Math.Round(duration.TotalSeconds)} segundos";
    }

    public static string NextBreak(TimeSpan remaining) =>
        remaining.TotalSeconds < 1
            ? "Próxima pausa a qualquer momento"
            : $"Próxima pausa em {remaining:mm\\:ss}";

    public static string PausedStatus(DateTimeOffset until) =>
        until.Date == DateTimeOffset.Now.Date
            ? $"Pausado até {until:HH:mm}"
            : $"Pausado até {until:dd/MM HH:mm}";

    public static string PauseConfirmation(DateTimeOffset until) =>
        until.Date == DateTimeOffset.Now.Date
            ? $"Volto a lembrar às {until:HH:mm}."
            : $"Volto a lembrar em {until:dd/MM} às {until:HH:mm}.";
}

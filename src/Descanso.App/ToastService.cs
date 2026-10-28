using CommunityToolkit.WinUI.Notifications;
using Descanso.Core;

namespace Descanso.App;

internal readonly record struct BreakNotification(
    BreakKind Kind,
    TimeSpan Duration,
    int IntervalMinutes,
    int BreaksTaken,
    int SnoozesRemaining,
    int SnoozeMinutes,
    bool Sound);

/// <summary>
/// Toasts do Windows. O cenário <c>Reminder</c> deixa a notificação na tela até
/// ser dispensada, com som em loop — o mais próximo de um overlay sem overlay.
/// </summary>
internal sealed class ToastService
{
    private const string ReminderSound = "ms-winsoundevent:Notification.Reminder";
    private const string DefaultSound = "ms-winsoundevent:Notification.Default";

    public event Action<string>? ActionInvoked;

    public ToastService()
    {
        ToastNotificationManagerCompat.OnActivated += OnActivated;
    }

    public void ShowBreak(BreakNotification notice)
    {
        var builder = new ToastContentBuilder()
            .AddArgument("action", "open")
            .AddText(Messages.Title(notice.Kind))
            .AddText(Messages.Body(notice.Kind, notice.Duration, notice.IntervalMinutes, notice.BreaksTaken))
            .AddButton(new ToastButton().SetContent(Messages.DoneButton).AddArgument("action", "done"))
            .AddButton(new ToastButton().SetContent(Messages.SkipButton).AddArgument("action", "skip"))
            .SetToastScenario(ToastScenario.Reminder);

        if (notice.SnoozesRemaining > 0)
        {
            builder.AddButton(
                new ToastButton()
                    .SetContent(Messages.SnoozeButton(notice.SnoozeMinutes))
                    .AddArgument("action", "snooze"));
        }

        if (notice.Sound)
        {
            builder.AddAudio(new Uri(ReminderSound), loop: true, silent: false);
        }
        else
        {
            builder.AddAudio(new Uri(DefaultSound), loop: false, silent: true);
        }

        builder.Show();
    }

    public void ShowInfo(string title, string body, bool sound = true)
    {
        var builder = new ToastContentBuilder()
            .AddArgument("action", "open")
            .AddText(title)
            .AddText(body);

        if (!sound)
        {
            builder.AddAudio(new Uri(DefaultSound), loop: false, silent: true);
        }

        builder.Show();
    }

    public void ShowTest(bool sound) =>
        ShowInfo("Descanso", "As notificações estão funcionando. Os lembretes vão chegar assim.", sound);

    /// <summary>
    /// Remove o registro do AUMID. O próximo toast registra de novo com o caminho
    /// atual do executável — necessário se o app foi movido de pasta.
    /// </summary>
    public void ReRegister()
    {
        try
        {
            ToastNotificationManagerCompat.Uninstall();
        }
        catch (Exception ex)
        {
            Log.Error("toast-uninstall", ex);
        }
    }

    private void OnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var action = ToastAction.Parse(e.Argument);
        ActionInvoked?.Invoke(action);
    }
}

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Descanso.App.Views;
using Descanso.Core;

namespace Descanso.App;

/// <summary>
/// Raiz de composição: liga o agendador puro à casca do Windows (bandeja, toast,
/// inatividade, autostart) e mantém o ciclo de vida do aplicativo.
/// </summary>
internal sealed class AppHost : IDisposable
{
    private readonly SingleInstance _instance;
    private readonly SettingsStore _store = new();
    private readonly IdleMonitor _idle = new();
    private readonly ToastService _toasts = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };

    private Settings _settings = new();
    private BreakScheduler _scheduler = null!;
    private TrayIcon? _tray;
    private SettingsWindow? _window;
    private TimeSpan _lastReading;

    public AppHost(SingleInstance instance) => _instance = instance;

    public void Start(string[] args)
    {
        var firstRun = !File.Exists(_store.FilePath);

        _settings = _store.Load();
        _scheduler = new BreakScheduler(_settings);

        Autostart.Sync(_settings.StartWithWindows);

        _store.StartWatching();
        _store.ExternalChange += OnExternalSettings;

        _toasts.ActionInvoked += OnToastAction;

        _tray = new TrayIcon(_settings.StartWithWindows);
        _tray.BreakNowRequested += BreakNow;
        _tray.PauseRequested += Pause;
        _tray.ResumeRequested += Resume;
        _tray.SettingsRequested += OpenSettings;
        _tray.StartWithWindowsChanged += OnStartWithWindowsChanged;
        _tray.ExitRequested += () => Application.Current.Shutdown();

        _instance.Listen(OnSecondInstance);

        _lastReading = _clock.Elapsed;
        _timer.Tick += OnTick;
        _timer.Start();

        UpdateStatus();

        if (firstRun)
        {
            // Grava os padrões para não reabrir esta janela a cada início.
            try
            {
                _store.Save(_settings);
            }
            catch (Exception ex)
            {
                Log.Error("config-primeira-execucao", ex);
            }

            _toasts.ShowInfo(
                "Descanso está ativo",
                $"Vou lembrar você de descansar a cada {_settings.IntervalMinutes} min. O ícone fica na bandeja.");
            OpenSettings();
        }
        else if (!HasArgument(args, "--minimized"))
        {
            OpenSettings();
        }

        if (HasArgument(args, "--break-now"))
        {
            BreakNow();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var elapsed = _clock.Elapsed - _lastReading;
        _lastReading = _clock.Elapsed;
        _idle.Refresh();

        var result = _scheduler.Tick(new SchedulerContext(DateTimeOffset.Now, elapsed, _idle.IdleTime));

        UpdateStatus();

        if (result.BreakDue)
        {
            Fire(result);
        }
    }

    private void Fire(TickResult result)
    {
        try
        {
            _toasts.ShowBreak(new BreakNotification(
                result.Kind,
                result.BreakDuration,
                _settings.IntervalMinutes,
                _scheduler.BreaksTaken,
                result.SnoozesRemaining,
                _settings.Snooze.Minutes,
                _settings.SoundEnabled));
        }
        catch (Exception ex)
        {
            Log.Error("toast-pausa", ex);
        }
    }

    private void BreakNow()
    {
        Fire(_scheduler.ForceBreak());
        UpdateStatus();
    }

    private void Pause(TimeSpan? duration)
    {
        var now = DateTimeOffset.Now;
        var until = duration is { } span ? now + span : NextWorkdayStart(now);

        _scheduler.PauseUntil(until);
        _toasts.ShowInfo("Descanso pausado", Messages.PauseConfirmation(until));
        UpdateStatus();
    }

    private void Resume()
    {
        _scheduler.Resume();
        UpdateStatus();
    }

    private void OnToastAction(string action)
    {
        // O evento do toolkit chega em thread de pool.
        Application.Current.Dispatcher.BeginInvoke(() => HandleAction(action));
    }

    private void HandleAction(string action)
    {
        switch (action)
        {
            case ToastAction.Done:
                _scheduler.CompleteBreak();
                break;

            case ToastAction.Skip:
                _scheduler.SkipBreak();
                break;

            case ToastAction.Snooze:
                if (!_scheduler.Snooze())
                {
                    _toasts.ShowInfo("Sem adiamentos", "O limite de adiamentos desta pausa já foi usado.");
                }

                break;

            default:
                OpenSettings();
                return;
        }

        UpdateStatus();
    }

    private void OnSecondInstance(string[] args)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (HasArgument(args, "--break-now"))
            {
                BreakNow();
                return;
            }

            var action = ToastAction.Find(args);
            if (action is not null)
            {
                HandleAction(action);
                return;
            }

            if (!HasArgument(args, "--minimized"))
            {
                OpenSettings();
            }
        });
    }

    private void OpenSettings()
    {
        if (_window is { IsVisible: true })
        {
            _window.Activate();
            return;
        }

        var window = new SettingsWindow(
            _settings,
            NotificationHealth.IsDisabled(),
            TestNotification,
            _toasts.ReRegister);

        _window = window;
        var accepted = window.ShowDialog();
        _window = null;

        if (accepted == true && window.Result is { } updated)
        {
            ApplySettings(updated);
        }
    }

    private void TestNotification(Settings preview)
    {
        try
        {
            _toasts.ShowTest(preview.SoundEnabled);
        }
        catch (Exception ex)
        {
            Log.Error("toast-teste", ex);
            MessageBox.Show(
                "Não foi possível mostrar a notificação.\n\nVerifique Configurações → Notificações do Windows e o Assistente de Foco.",
                "Descanso",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ApplySettings(Settings updated)
    {
        try
        {
            _store.Save(updated);
        }
        catch (Exception ex)
        {
            Log.Error("config-save", ex);
            MessageBox.Show(
                $"Não foi possível salvar em {_store.FilePath}.\n\n{ex.Message}",
                "Descanso",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _settings = updated;
        _scheduler.UpdateSettings(updated);
        Autostart.Sync(updated.StartWithWindows);
        UpdateStatus();
    }

    private void OnExternalSettings(Settings reloaded)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _settings = reloaded;
            _scheduler.UpdateSettings(reloaded);
            Autostart.Sync(reloaded.StartWithWindows);
            UpdateStatus();
        });
    }

    private void OnStartWithWindowsChanged(bool enabled)
    {
        var updated = _settings.Clone();
        updated.StartWithWindows = enabled;

        try
        {
            _store.Save(updated);
            Autostart.Sync(enabled);
            _settings = updated;
            _scheduler.UpdateSettings(updated);
        }
        catch (Exception ex)
        {
            Log.Error("autostart-toggle", ex);
            _toasts.ShowInfo("Não foi possível alterar", "Falha ao gravar a configuração de inicialização.");
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_tray is null)
        {
            return;
        }

        var now = DateTimeOffset.Now;

        string status;
        if (_scheduler.PausedUntil is { } until)
        {
            status = Messages.PausedStatus(until);
        }
        else if (!_settings.WorkingHours.IsOpen(now))
        {
            status = "Fora do horário de trabalho";
        }
        else
        {
            status = $"Descanso — {Messages.NextBreak(_scheduler.UntilNextBreak)}";
        }

        _tray.Update(
            status,
            TrayStatus.From(_scheduler, _settings, now),
            _scheduler.IsPaused,
            _settings.StartWithWindows);
    }

    private DateTimeOffset NextWorkdayStart(DateTimeOffset now)
    {
        var tomorrow = now.Date.AddDays(1);
        var start = _settings.WorkingHours.Enabled
            ? _settings.WorkingHours.Start.ToTimeSpan()
            : TimeSpan.Zero;

        return new DateTimeOffset(tomorrow + start, now.Offset);
    }

    private static bool HasArgument(string[] args, string value) =>
        args.Any(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;

        _store.ExternalChange -= OnExternalSettings;
        _store.Dispose();

        _tray?.Dispose();
        _instance.Dispose();
    }
}

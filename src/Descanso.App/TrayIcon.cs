using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Descanso.Core;
using Hardcodet.Wpf.TaskbarNotification;

namespace Descanso.App;

/// <summary>Ícone da bandeja, menu de contexto e tooltip com a contagem.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly MenuItem _statusItem;
    private readonly MenuItem _resumeItem;
    private readonly MenuItem _startupItem;
    private string _status = string.Empty;

    private Icon? _currentIcon;
    private int _lastStep = -1;
    private TrayState _lastState = (TrayState)(-1);
    private int _lastSize = -1;

    public event Action? BreakNowRequested;

    /// <summary>null significa "até amanhã".</summary>
    public event Action<TimeSpan?>? PauseRequested;

    public event Action? ResumeRequested;

    public event Action? SettingsRequested;

    public event Action<bool>? StartWithWindowsChanged;

    public event Action? ExitRequested;

    public TrayIcon(bool startWithWindows)
    {
        _statusItem = new MenuItem
        {
            IsEnabled = false,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(6, 2, 6, 2),
        };

        _resumeItem = new MenuItem { Header = "Retomar descanso", Visibility = Visibility.Collapsed };
        _resumeItem.Click += (_, _) => ResumeRequested?.Invoke();

        _startupItem = new MenuItem
        {
            Header = "Iniciar com o Windows",
            IsCheckable = true,
            IsChecked = startWithWindows,
        };
        _startupItem.Click += (_, _) => StartWithWindowsChanged?.Invoke(_startupItem.IsChecked);

        var breakNow = new MenuItem { Header = "Fazer uma pausa agora" };
        breakNow.Click += (_, _) => BreakNowRequested?.Invoke();

        var pause = new MenuItem { Header = "Pausar" };
        pause.Items.Add(CreatePauseItem("Por 30 minutos", TimeSpan.FromMinutes(30)));
        pause.Items.Add(CreatePauseItem("Por 1 hora", TimeSpan.FromHours(1)));
        pause.Items.Add(CreatePauseItem("Até amanhã", null));

        var settings = new MenuItem { Header = "Configurar..." };
        settings.Click += (_, _) => SettingsRequested?.Invoke();

        var exit = new MenuItem { Header = "Sair" };
        exit.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new ContextMenu();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(breakNow);
        menu.Items.Add(pause);
        menu.Items.Add(_resumeItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(settings);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);

        var size = TrayMetrics.IconSize;
        _currentIcon = IconRenderer.Render(TrayStatus.CycleStart, size);
        _lastSize = size;
        _lastState = TrayState.Counting;
        _lastStep = Steps(size);

        _icon = new TaskbarIcon
        {
            Icon = _currentIcon,
            ToolTipText = "Descanso",
            ContextMenu = menu,
        };

        _icon.TrayMouseDoubleClick += (_, _) => SettingsRequested?.Invoke();
    }

    public void Update(string status, TrayStatus visual, bool paused, bool startWithWindows)
    {
        if (status != _status)
        {
            _status = status;
            _statusItem.Header = status;
            _icon.ToolTipText = status.Length > 120 ? status[..120] : status;
        }

        _resumeItem.Visibility = paused ? Visibility.Visible : Visibility.Collapsed;
        _startupItem.IsChecked = startWithWindows;

        ApplyIcon(visual);
    }

    /// <summary>
    /// Redesenha só quando o degrau, o estado ou o tamanho da bandeja mudam: o
    /// relógio bate a cada 5 s, mas o desenho tem 16–48 degraus por ciclo.
    /// </summary>
    private void ApplyIcon(TrayStatus visual)
    {
        var size = TrayMetrics.IconSize;
        var steps = Steps(size);
        var remaining = Math.Clamp(visual.Remaining, 0, 1);
        var step = (int)Math.Round(remaining * steps);

        if (_currentIcon is not null
            && step == _lastStep
            && visual.State == _lastState
            && size == _lastSize)
        {
            return;
        }

        var icon = IconRenderer.Render(visual, size);
        var previous = _currentIcon;

        _icon.Icon = icon;
        _currentIcon = icon;
        _lastStep = step;
        _lastState = visual.State;
        _lastSize = size;

        // Depois da troca: o shell lê o HICON durante o NIM_MODIFY.
        previous?.Dispose();
    }

    private static int Steps(int size) => Math.Clamp(size * 2, 16, 48);

    private MenuItem CreatePauseItem(string header, TimeSpan? duration)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => PauseRequested?.Invoke(duration);
        return item;
    }

    public void Dispose()
    {
        _icon.Dispose();
        _currentIcon?.Dispose();
        _currentIcon = null;
    }
}

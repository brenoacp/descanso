using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Descanso.Core;

namespace Descanso.App.Views;

public partial class SettingsWindow : Window
{
    private static readonly string[] TimeFormats = { "H:mm", "HH:mm", "H", "HH", "H:mm:ss" };

    private readonly Settings _current;
    private readonly Action<Settings> _test;
    private readonly Action _reRegister;
    private readonly TextBox[] _numberBoxes;
    private readonly ToggleButton[] _dayToggles;

    public SettingsWindow(Settings current, bool notificationsDisabled, Action<Settings> test, Action reRegister)
    {
        InitializeComponent();

        _current = current;
        _test = test;
        _reRegister = reRegister;

        _numberBoxes = new[]
        {
            IntervalBox, BreakBox, LongBreakEveryBox, LongBreakMinutesBox,
            IdleThresholdBox, IdleResetBox, SnoozeMinutesBox, SnoozeMaxBox,
        };

        _dayToggles = new[] { Day1, Day2, Day3, Day4, Day5, Day6, Day7 };

        WarningBar.Visibility = notificationsDisabled ? Visibility.Visible : Visibility.Collapsed;

        foreach (var box in _numberBoxes)
        {
            box.PreviewTextInput += OnNumberPreviewInput;
            box.GotKeyboardFocus += (sender, _) => ((TextBox)sender).SelectAll();
        }

        LoadFrom(current);
        UpdateEnabledStates();
        FitToScreen();

        Loaded += OnLoaded;
    }

    /// <summary>Configuração aprovada pelo usuário. Null enquanto não salvar.</summary>
    public Settings? Result { get; private set; }

    /// <summary>
    /// Ajusta o tamanho antes de exibir. Definir MaxHeight depois de a janela
    /// existir não reduz o HWND já criado, e o rodapé fica fora da tela.
    /// </summary>
    private void FitToScreen()
    {
        var work = SystemParameters.WorkArea;

        MaxWidth = Math.Max(MinWidth, work.Width - 60);
        MaxHeight = Math.Max(MinHeight, work.Height - 40);

        Width = Math.Min(Width, MaxWidth);
        Height = Math.Min(Height, MaxHeight);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        IntervalBox.Focus();
        IntervalBox.SelectAll();
    }

    private static void OnNumberPreviewInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsDigit);

    private void OnLongBreakToggled(object sender, RoutedEventArgs e) => UpdateEnabledStates();

    private void OnHoursToggled(object sender, RoutedEventArgs e) => UpdateEnabledStates();

    private void UpdateEnabledStates()
    {
        var longBreak = LongBreakCheck.IsChecked == true;
        LongBreakEveryBox.IsEnabled = longBreak;
        LongBreakMinutesBox.IsEnabled = longBreak;

        var hours = HoursCheck.IsChecked == true;
        StartBox.IsEnabled = hours;
        EndBox.IsEnabled = hours;

        foreach (var day in _dayToggles)
        {
            day.IsEnabled = hours;
        }
    }

    private void LoadFrom(Settings settings)
    {
        IntervalBox.Text = Number(settings.IntervalMinutes);
        BreakBox.Text = Number(Minutes(settings.BreakSeconds));

        LongBreakCheck.IsChecked = settings.LongBreakEvery > 0;
        LongBreakEveryBox.Text = Number(Math.Max(1, settings.LongBreakEvery));
        LongBreakMinutesBox.Text = Number(Minutes(settings.LongBreakSeconds));

        IdleThresholdBox.Text = Number(Minutes(settings.Idle.ThresholdSeconds));
        IdleResetBox.Text = Number(Minutes(settings.Idle.ResetAfterSeconds));

        HoursCheck.IsChecked = settings.WorkingHours.Enabled;
        StartBox.Text = settings.WorkingHours.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
        EndBox.Text = settings.WorkingHours.End.ToString("HH:mm", CultureInfo.InvariantCulture);

        for (var index = 0; index < _dayToggles.Length; index++)
        {
            _dayToggles[index].IsChecked = settings.WorkingHours.Days.Contains(index + 1);
        }

        SoundCheck.IsChecked = settings.SoundEnabled;
        SnoozeMinutesBox.Text = Number(settings.Snooze.Minutes);
        SnoozeMaxBox.Text = Number(settings.Snooze.MaxPerBreak);
        StartupCheck.IsChecked = settings.StartWithWindows;
    }

    private void OnTest(object sender, RoutedEventArgs e)
    {
        var preview = _current.Clone();
        preview.SoundEnabled = SoundCheck.IsChecked == true;
        _test(preview);
    }

    private void OnReRegister(object sender, RoutedEventArgs e)
    {
        _reRegister();

        var preview = _current.Clone();
        preview.SoundEnabled = SoundCheck.IsChecked == true;
        _test(preview);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var problems = new List<string>();

        if (!TryBuild(problems, out var settings))
        {
            ValidationText.Text = string.Join("  ", problems);
            return;
        }

        Result = settings;
        DialogResult = true;
    }

    private bool TryBuild(List<string> problems, out Settings settings)
    {
        settings = _current.Clone();
        var valid = true;

        valid &= TryNumber(IntervalBox, "Intervalo", 1, 480, problems, out var interval);
        valid &= TryNumber(BreakBox, "Duração da pausa", 1, 120, problems, out var breakMinutes);
        valid &= TryNumber(LongBreakEveryBox, "Pausas até a longa", 1, 20, problems, out var longEvery);
        valid &= TryNumber(LongBreakMinutesBox, "Duração da pausa longa", 1, 120, problems, out var longMinutes);
        valid &= TryNumber(IdleThresholdBox, "Tempo para considerar ausente", 1, 60, problems, out var idleThreshold);
        valid &= TryNumber(IdleResetBox, "Tempo para considerar descanso", 1, 120, problems, out var idleReset);
        valid &= TryNumber(SnoozeMinutesBox, "Tempo de adiamento", 1, 60, problems, out var snoozeMinutes);
        valid &= TryNumber(SnoozeMaxBox, "Adiamentos por pausa", 0, 10, problems, out var snoozeMax);

        valid &= TryTime(StartBox, "Horário inicial", problems, out var start);
        valid &= TryTime(EndBox, "Horário final", problems, out var end);

        var hoursEnabled = HoursCheck.IsChecked == true;
        var days = _dayToggles
            .Where(toggle => toggle.IsChecked == true)
            .Select(toggle => int.Parse((string)toggle.Tag, CultureInfo.InvariantCulture))
            .ToList();

        if (hoursEnabled && days.Count == 0)
        {
            problems.Add("Marque ao menos um dia.");
            valid = false;
        }

        if (valid && hoursEnabled && start == end)
        {
            problems.Add("Horário inicial e final iguais.");
            valid = false;
        }

        if (valid && idleReset < idleThreshold)
        {
            problems.Add("O descanso por ausência deve ser maior que o tempo para considerar ausente.");
            valid = false;
        }

        if (!valid)
        {
            return false;
        }

        settings.IntervalMinutes = interval;
        settings.BreakSeconds = breakMinutes * 60;
        settings.LongBreakEvery = LongBreakCheck.IsChecked == true ? longEvery : 0;
        settings.LongBreakSeconds = longMinutes * 60;
        settings.Idle.ThresholdSeconds = idleThreshold * 60;
        settings.Idle.ResetAfterSeconds = idleReset * 60;
        settings.WorkingHours.Enabled = hoursEnabled;
        settings.WorkingHours.Start = start;
        settings.WorkingHours.End = end;
        settings.WorkingHours.Days = days;
        settings.SoundEnabled = SoundCheck.IsChecked == true;
        settings.Snooze.Minutes = snoozeMinutes;
        settings.Snooze.MaxPerBreak = snoozeMax;
        settings.StartWithWindows = StartupCheck.IsChecked == true;

        settings.Normalize();
        return true;
    }

    private static bool TryNumber(TextBox box, string label, int min, int max, List<string> problems, out int value)
    {
        if (!int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            || value < min
            || value > max)
        {
            problems.Add($"{label}: use de {min} a {max}.");
            return false;
        }

        return true;
    }

    private static bool TryTime(TextBox box, string label, List<string> problems, out TimeOnly value)
    {
        if (TimeOnly.TryParseExact(
                box.Text.Trim(),
                TimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out value))
        {
            return true;
        }

        problems.Add($"{label}: use o formato HH:mm.");
        return false;
    }

    private static int Minutes(int seconds) => Math.Max(1, seconds / 60);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}

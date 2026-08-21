using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using SonoSonnette.App.Dialogs;
using SonoSonnette.Core.Models;
using SonoSonnette.Core.Services;

namespace SonoSonnette.App;

public partial class MainWindow : Window
{
    private static readonly IBrush OnAirIdleBrush = new SolidColorBrush(Color.Parse("#C62828"));
    private static readonly IBrush OnAirActiveBrush = new SolidColorBrush(Color.Parse("#FF1744"));

    private readonly SettingsService _settingsService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly IMicPassthroughService _micPassthroughService;
    private readonly AppSettings _settings;
    private readonly SchedulerService _scheduler;
    private readonly ObservableCollection<ScheduleListItem> _items = new();

    private bool _unlocked;

    public MainWindow() : this(new SettingsService(), new AudioPlaybackService(), new MicPassthroughService())
    {
    }

    public MainWindow(SettingsService settingsService, IAudioPlaybackService playbackService, IMicPassthroughService micPassthroughService)
    {
        _settingsService = settingsService;
        _playbackService = playbackService;
        _micPassthroughService = micPassthroughService;
        _settings = _settingsService.Load();

        InitializeComponent();

        var list = this.FindControl<ListBox>("ScheduleList")!;
        list.ItemsSource = _items;
        RefreshItems();

        _scheduler = new SchedulerService(
            _playbackService,
            () => _settings.Schedules,
            () => (_settings.OutputDeviceId, (float)_settings.Volume));

        _scheduler.ScheduleTriggered += OnScheduleTriggered;
        _scheduler.ScheduleFailed += OnScheduleFailed;

        // Garantit que la clé de registre reflète le paramètre enregistré (et le chemin actuel de l'exécutable),
        // y compris si l'application a été déplacée depuis le dernier démarrage.
        OnAutoStartChanged(_settings.StartWithWindows);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void RefreshItems()
    {
        _items.Clear();
        foreach (var schedule in _settings.Schedules.OrderBy(s => s.TimeOfDay))
        {
            _items.Add(new ScheduleListItem(schedule));
        }
    }

    private void SaveSettings() => _settingsService.Save(_settings);

    private async Task<bool> RequireUnlockAsync()
    {
        if (!PasswordService.HasPassword(_settings))
        {
            var setPassword = new SetPasswordWindow();
            var created = await setPassword.ShowDialog<bool>(this);
            if (created && setPassword.NewPassword is not null)
            {
                PasswordService.SetPassword(_settings, setPassword.NewPassword);
                SaveSettings();
                _unlocked = true;
                return true;
            }

            return false;
        }

        if (_unlocked)
        {
            return true;
        }

        var verify = new VerifyPasswordWindow(password => PasswordService.Verify(_settings, password));
        var ok = await verify.ShowDialog<bool>(this);
        _unlocked = ok;
        return ok;
    }

    private async void OnAddClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await RequireUnlockAsync())
        {
            return;
        }

        var dialog = new ScheduleEditWindow(null);
        var saved = await dialog.ShowDialog<bool>(this);
        if (saved && dialog.Result is { } entry)
        {
            _settings.Schedules.Add(entry);
            SaveSettings();
            RefreshItems();
        }
    }

    private async void OnEditClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var list = this.FindControl<ListBox>("ScheduleList")!;
        if (list.SelectedItem is not ScheduleListItem selected)
        {
            SetStatus("Sélectionnez d'abord un planning à modifier.");
            return;
        }

        if (!await RequireUnlockAsync())
        {
            return;
        }

        var dialog = new ScheduleEditWindow(selected.Entry);
        var saved = await dialog.ShowDialog<bool>(this);
        if (saved && dialog.Result is { } entry)
        {
            var index = _settings.Schedules.FindIndex(s => s.Id == entry.Id);
            if (index >= 0)
            {
                _settings.Schedules[index] = entry;
                SaveSettings();
                RefreshItems();
            }
        }
    }

    private async void OnDeleteClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var list = this.FindControl<ListBox>("ScheduleList")!;
        if (list.SelectedItem is not ScheduleListItem selected)
        {
            SetStatus("Sélectionnez d'abord un planning à supprimer.");
            return;
        }

        if (!await RequireUnlockAsync())
        {
            return;
        }

        var confirm = new ConfirmWindow($"Supprimer le planning « {selected.Entry.Name} » ?");
        if (await confirm.ShowDialog<bool>(this))
        {
            _settings.Schedules.RemoveAll(s => s.Id == selected.Entry.Id);
            SaveSettings();
            RefreshItems();
        }
    }

    private async void OnSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await RequireUnlockAsync())
        {
            return;
        }

        var settingsWindow = new SettingsWindow(_settings, _playbackService, SaveSettings, OnAutoStartChanged);
        await settingsWindow.ShowDialog(this);
    }

    private void OnAutoStartChanged(bool enabled)
    {
        var executablePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(executablePath))
        {
            AutoStartService.SetEnabled(enabled, executablePath);
        }
    }

    private void OnAirPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_micPassthroughService.IsActive)
        {
            return;
        }

        try
        {
            _micPassthroughService.Start(_settings.OutputDeviceId, (float)_settings.Volume);
            var button = this.FindControl<Button>("OnAirButton")!;
            button.Background = OnAirActiveBrush;
            SetStatus("ON AIR — le micro est diffusé en direct.");
        }
        catch (Exception ex)
        {
            SetStatus($"Impossible de démarrer la diffusion micro : {ex.Message}");
        }
    }

    private void OnAirReleased(object? sender, PointerReleasedEventArgs e) => StopOnAir();

    private void OnAirCaptureLost(object? sender, PointerCaptureLostEventArgs e) => StopOnAir();

    private void StopOnAir()
    {
        if (!_micPassthroughService.IsActive)
        {
            return;
        }

        _micPassthroughService.Stop();
        var button = this.FindControl<Button>("OnAirButton")!;
        button.Background = OnAirIdleBrush;
        SetStatus("Diffusion micro arrêtée.");
    }

    /// <summary>Libère les ressources audio avant l'arrêt complet de l'application (menu Quitter).</summary>
    public void PrepareForExit()
    {
        _micPassthroughService.Stop();
        _scheduler.Dispose();
    }

    private void OnScheduleTriggered(object? sender, ScheduleTriggeredEventArgs e) =>
        Dispatcher.UIThread.Post(() => SetStatus($"Déclenché : {e.Schedule.Name} ({DateTime.Now:HH:mm})"));

    private void OnScheduleFailed(object? sender, ScheduleFailedEventArgs e) =>
        Dispatcher.UIThread.Post(() => SetStatus($"Échec de lecture pour « {e.Schedule.Name} » : {e.Exception.Message}"));

    private void SetStatus(string message) => this.FindControl<TextBlock>("StatusBar")!.Text = message;

    private sealed class ScheduleListItem
    {
        private static readonly DayOfWeek[] WeekOrder =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
        };

        private static readonly Dictionary<DayOfWeek, string> DayLabels = new()
        {
            [DayOfWeek.Monday] = "Lun",
            [DayOfWeek.Tuesday] = "Mar",
            [DayOfWeek.Wednesday] = "Mer",
            [DayOfWeek.Thursday] = "Jeu",
            [DayOfWeek.Friday] = "Ven",
            [DayOfWeek.Saturday] = "Sam",
            [DayOfWeek.Sunday] = "Dim",
        };

        public ScheduleListItem(ScheduleEntry entry) => Entry = entry;

        public ScheduleEntry Entry { get; }

        public string Name => Entry.Name;

        public string TimeLabel => Entry.TimeOfDay.ToString("HH:mm");

        public string StatusLabel => Entry.Enabled ? "Activé" : "Désactivé";

        public string DaysSummary => Entry.ActiveDays.Count == 7
            ? "Tous les jours"
            : string.Join(", ", WeekOrder.Where(Entry.ActiveDays.Contains).Select(d => DayLabels[d]));
    }
}

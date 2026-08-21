using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SonoSonnette.Core.Models;

namespace SonoSonnette.App.Dialogs;

public partial class ScheduleEditWindow : Window
{
    private readonly Guid _id;
    private string _audioFilePath = string.Empty;

    public ScheduleEntry? Result { get; private set; }

    public ScheduleEditWindow() : this(null)
    {
    }

    public ScheduleEditWindow(ScheduleEntry? existing)
    {
        InitializeComponent();

        _id = existing?.Id ?? Guid.NewGuid();

        var nameBox = this.FindControl<TextBox>("NameBox")!;
        var filePathBox = this.FindControl<TextBox>("FilePathBox")!;
        var timeBox = this.FindControl<TimePicker>("TimeBox")!;
        var enabledBox = this.FindControl<CheckBox>("EnabledBox")!;

        nameBox.Text = existing?.Name ?? string.Empty;
        _audioFilePath = existing?.AudioFilePath ?? string.Empty;
        filePathBox.Text = string.IsNullOrEmpty(_audioFilePath) ? string.Empty : Path.GetFileName(_audioFilePath);
        timeBox.SelectedTime = (existing?.TimeOfDay ?? new TimeOnly(8, 0)).ToTimeSpan();
        enabledBox.IsChecked = existing?.Enabled ?? true;

        var activeDays = existing?.ActiveDays ?? new HashSet<DayOfWeek>(Enum.GetValues<DayOfWeek>());
        GetDayBox(DayOfWeek.Monday).IsChecked = activeDays.Contains(DayOfWeek.Monday);
        GetDayBox(DayOfWeek.Tuesday).IsChecked = activeDays.Contains(DayOfWeek.Tuesday);
        GetDayBox(DayOfWeek.Wednesday).IsChecked = activeDays.Contains(DayOfWeek.Wednesday);
        GetDayBox(DayOfWeek.Thursday).IsChecked = activeDays.Contains(DayOfWeek.Thursday);
        GetDayBox(DayOfWeek.Friday).IsChecked = activeDays.Contains(DayOfWeek.Friday);
        GetDayBox(DayOfWeek.Saturday).IsChecked = activeDays.Contains(DayOfWeek.Saturday);
        GetDayBox(DayOfWeek.Sunday).IsChecked = activeDays.Contains(DayOfWeek.Sunday);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private CheckBox GetDayBox(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => this.FindControl<CheckBox>("MondayBox")!,
        DayOfWeek.Tuesday => this.FindControl<CheckBox>("TuesdayBox")!,
        DayOfWeek.Wednesday => this.FindControl<CheckBox>("WednesdayBox")!,
        DayOfWeek.Thursday => this.FindControl<CheckBox>("ThursdayBox")!,
        DayOfWeek.Friday => this.FindControl<CheckBox>("FridayBox")!,
        DayOfWeek.Saturday => this.FindControl<CheckBox>("SaturdayBox")!,
        DayOfWeek.Sunday => this.FindControl<CheckBox>("SundayBox")!,
        _ => throw new ArgumentOutOfRangeException(nameof(day)),
    };

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choisir un fichier audio",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Fichiers audio")
                {
                    Patterns = new[] { "*.mp3", "*.wav", "*.wma", "*.m4a", "*.flac", "*.ogg" },
                },
            },
        });

        var file = files.Count > 0 ? files[0] : null;
        if (file?.TryGetLocalPath() is { } localPath)
        {
            _audioFilePath = localPath;
            this.FindControl<TextBox>("FilePathBox")!.Text = Path.GetFileName(localPath);
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var nameBox = this.FindControl<TextBox>("NameBox")!;
        var timeBox = this.FindControl<TimePicker>("TimeBox")!;
        var enabledBox = this.FindControl<CheckBox>("EnabledBox")!;
        var errorText = this.FindControl<TextBlock>("ErrorText")!;

        var name = nameBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(name))
        {
            ShowError(errorText, "Veuillez saisir un nom.");
            return;
        }

        if (string.IsNullOrEmpty(_audioFilePath))
        {
            ShowError(errorText, "Veuillez sélectionner un fichier audio.");
            return;
        }

        if (timeBox.SelectedTime is not { } selectedTime)
        {
            ShowError(errorText, "Veuillez choisir une heure.");
            return;
        }

        var activeDays = new HashSet<DayOfWeek>();
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            if (GetDayBox(day).IsChecked == true)
            {
                activeDays.Add(day);
            }
        }

        if (activeDays.Count == 0)
        {
            ShowError(errorText, "Sélectionnez au moins un jour actif.");
            return;
        }

        Result = new ScheduleEntry
        {
            Id = _id,
            Name = name,
            AudioFilePath = _audioFilePath,
            TimeOfDay = TimeOnly.FromTimeSpan(selectedTime),
            ActiveDays = activeDays,
            Enabled = enabledBox.IsChecked ?? true,
        };

        Close(true);
    }

    private static void ShowError(TextBlock errorText, string message)
    {
        errorText.Text = message;
        errorText.IsVisible = true;
    }
}

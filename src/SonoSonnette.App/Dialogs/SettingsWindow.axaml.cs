using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SonoSonnette.Core.Models;
using SonoSonnette.Core.Services;

namespace SonoSonnette.App.Dialogs;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly IAudioPlaybackService _playbackService;
    private readonly Action _onSave;
    private readonly Action<bool> _onAutoStartChanged;
    private List<AudioOutputDevice> _devices = new();

    public SettingsWindow()
    {
        _settings = new AppSettings();
        _playbackService = new AudioPlaybackService();
        _onSave = () => { };
        _onAutoStartChanged = _ => { };
        InitializeComponent();
    }

    public SettingsWindow(AppSettings settings, IAudioPlaybackService playbackService, Action onSave, Action<bool> onAutoStartChanged)
    {
        _settings = settings;
        _playbackService = playbackService;
        _onSave = onSave;
        _onAutoStartChanged = onAutoStartChanged;

        InitializeComponent();
        LoadDevices();

        var volumeSlider = this.FindControl<Slider>("VolumeSlider")!;
        volumeSlider.Value = _settings.Volume;
        volumeSlider.ValueChanged += (_, _) =>
        {
            _settings.Volume = volumeSlider.Value;
            _onSave();
        };

        var autoStartBox = this.FindControl<CheckBox>("AutoStartBox")!;
        autoStartBox.IsChecked = _settings.StartWithWindows;
        autoStartBox.IsCheckedChanged += (_, _) =>
        {
            _settings.StartWithWindows = autoStartBox.IsChecked ?? false;
            _onAutoStartChanged(_settings.StartWithWindows);
            _onSave();
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void LoadDevices()
    {
        var combo = this.FindControl<ComboBox>("OutputDeviceCombo")!;
        _devices = _playbackService.GetOutputDevices().ToList();

        var items = new List<string> { "Périphérique par défaut du système" };
        items.AddRange(_devices.Select(d => d.IsDefault ? $"{d.FriendlyName} (par défaut)" : d.FriendlyName));
        combo.ItemsSource = items;

        var selectedIndex = 0;
        if (!string.IsNullOrEmpty(_settings.OutputDeviceId))
        {
            var idx = _devices.FindIndex(d => d.Id == _settings.OutputDeviceId);
            if (idx >= 0)
            {
                selectedIndex = idx + 1;
            }
        }

        combo.SelectedIndex = selectedIndex;

        combo.SelectionChanged += (_, _) =>
        {
            var index = combo.SelectedIndex;
            _settings.OutputDeviceId = index <= 0 ? null : _devices[index - 1].Id;
            _onSave();
        };
    }

    private async void OnTestSoundClicked(object? sender, RoutedEventArgs e)
    {
        var statusText = this.FindControl<TextBlock>("StatusText")!;
        try
        {
            await _playbackService.PlayTestToneAsync(_settings.OutputDeviceId, (float)_settings.Volume);
        }
        catch (Exception ex)
        {
            statusText.Foreground = Avalonia.Media.Brushes.Red;
            statusText.Text = $"Impossible de jouer le son de test : {ex.Message}";
            statusText.IsVisible = true;
        }
    }

    private async void OnChangePasswordClicked(object? sender, RoutedEventArgs e)
    {
        var statusText = this.FindControl<TextBlock>("StatusText")!;

        if (PasswordService.HasPassword(_settings))
        {
            var verify = new VerifyPasswordWindow(password => PasswordService.Verify(_settings, password));
            var verified = await verify.ShowDialog<bool>(this);
            if (!verified)
            {
                return;
            }
        }

        var setPassword = new SetPasswordWindow();
        var saved = await setPassword.ShowDialog<bool>(this);
        if (saved && setPassword.NewPassword is not null)
        {
            PasswordService.SetPassword(_settings, setPassword.NewPassword);
            _onSave();

            statusText.Foreground = Avalonia.Media.Brushes.Green;
            statusText.Text = "Mot de passe mis à jour.";
            statusText.IsVisible = true;
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}

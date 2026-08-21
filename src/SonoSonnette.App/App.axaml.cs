using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SonoSonnette.Core.Services;

namespace SonoSonnette.App;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private bool _isExiting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService();
            var playbackService = new AudioPlaybackService();
            var micPassthroughService = new MicPassthroughService();

            _mainWindow = new MainWindow(settingsService, playbackService, micPassthroughService);
            _mainWindow.Closing += OnMainWindowClosing;

            desktop.MainWindow = _mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var startMinimized = desktop.Args?.Contains("--minimized") == true;
            if (!startMinimized)
            {
                _mainWindow.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        // Fermer la fenêtre ne quitte pas l'application : elle continue en arrière-plan (icône dans la zone de notification).
        e.Cancel = true;
        _mainWindow?.Hide();
    }

    private void OnTrayIconClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnOpenClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnExitClicked(object? sender, EventArgs e)
    {
        _isExiting = true;
        _mainWindow?.PrepareForExit();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace SonoSonnette.App.Dialogs;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow() : this(string.Empty)
    {
    }

    public ConfirmWindow(string message)
    {
        InitializeComponent();
        this.FindControl<TextBlock>("MessageText")!.Text = message;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnYesClicked(object? sender, RoutedEventArgs e) => Close(true);

    private void OnNoClicked(object? sender, RoutedEventArgs e) => Close(false);
}

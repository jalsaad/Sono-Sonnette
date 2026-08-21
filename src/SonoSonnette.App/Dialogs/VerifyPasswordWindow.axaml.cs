using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace SonoSonnette.App.Dialogs;

public partial class VerifyPasswordWindow : Window
{
    private readonly Func<string, bool> _verify;

    public VerifyPasswordWindow() : this(_ => false)
    {
        // Constructeur sans paramètre requis par le chargeur XAML au design-time.
    }

    public VerifyPasswordWindow(Func<string, bool> verify)
    {
        _verify = verify;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void OnValidateClicked(object? sender, RoutedEventArgs e)
    {
        var passwordBox = this.FindControl<TextBox>("PasswordBox")!;
        var errorText = this.FindControl<TextBlock>("ErrorText")!;

        if (_verify(passwordBox.Text ?? string.Empty))
        {
            Close(true);
        }
        else
        {
            errorText.Text = "Mot de passe incorrect.";
            errorText.IsVisible = true;
            passwordBox.Text = string.Empty;
        }
    }
}

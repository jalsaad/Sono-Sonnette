using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace SonoSonnette.App.Dialogs;

public partial class SetPasswordWindow : Window
{
    public string? NewPassword { get; private set; }

    public SetPasswordWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var newPasswordBox = this.FindControl<TextBox>("NewPasswordBox")!;
        var confirmPasswordBox = this.FindControl<TextBox>("ConfirmPasswordBox")!;
        var errorText = this.FindControl<TextBlock>("ErrorText")!;

        var newPassword = newPasswordBox.Text ?? string.Empty;
        var confirmPassword = confirmPasswordBox.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
        {
            errorText.Text = "Le mot de passe doit contenir au moins 4 caractères.";
            errorText.IsVisible = true;
            return;
        }

        if (newPassword != confirmPassword)
        {
            errorText.Text = "Les deux mots de passe ne correspondent pas.";
            errorText.IsVisible = true;
            return;
        }

        NewPassword = newPassword;
        Close(true);
    }
}

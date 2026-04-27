using System.Windows;

namespace WindowPilot.Views;

public partial class TextInputDialog : Window
{
    public TextInputDialog()
    {
        InitializeComponent();
    }

    public string Value => ValueText.Text.Trim();

    public static string? Prompt(Window owner, string title, string prompt, string defaultValue = "")
    {
        var dialog = new TextInputDialog
        {
            Owner = owner,
            Title = title
        };
        dialog.PromptText.Text = prompt;
        dialog.ValueText.Text = defaultValue;
        dialog.ValueText.SelectAll();
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}

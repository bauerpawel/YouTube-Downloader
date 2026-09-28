using System.Threading.Tasks;
using Avalonia.Controls;

namespace YouTubeDownloader;

public partial class MessageDialog : Window
{
    private bool confirmed;

    public MessageDialog()
    {
        InitializeComponent();
    }

    private MessageDialog(string message, string title, bool isConfirmation) : this()
    {
        // A dialog nobody can click blocks a headless CI smoke test forever; log it so
        // the CI output shows which error stopped the app.
        Console.WriteLine($"[dialog] {title}: {message}");
        Title = title;
        MessageText.Text = message;

        if (isConfirmation)
        {
            OkButton.Content = "Tak";
            CancelButton.IsVisible = true;
        }

        OkButton.Click += (s, e) => { confirmed = true; Close(); };
        CancelButton.Click += (s, e) => { confirmed = false; Close(); };
    }

    public static Task ShowAsync(Window owner, string message, string title)
    {
        var dialog = new MessageDialog(message, title, isConfirmation: false);
        return dialog.ShowDialog(owner);
    }

    public static async Task<bool> ShowConfirmAsync(Window owner, string message, string title)
    {
        var dialog = new MessageDialog(message, title, isConfirmation: true);
        await dialog.ShowDialog(owner);
        return dialog.confirmed;
    }
}

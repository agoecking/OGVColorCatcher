using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace OGVColorCatcher;

public partial class Popup : Window
{
    public Popup()
    {
        InitializeComponent();
    }

    public Popup(string message) : this()
    {
        MessageText.Text = message;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
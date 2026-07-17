using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OGVColorCatcher;

public partial class Settings : Window
{
    public Settings()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
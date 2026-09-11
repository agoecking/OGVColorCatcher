using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OGVColorCatcher.ViewModels;
using System;

namespace OGVColorCatcher;

public partial class ActivationWindow : Window
{
    public ActivationWindow()
    {
        InitializeComponent();
        System.Diagnostics.Debug.WriteLine(
    $"DataContext: {DataContext?.GetType().FullName ?? "NULL"}"
);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    public void CloseActivationWindow()
    {
        Close();
    }
}
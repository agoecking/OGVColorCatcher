using Avalonia.Controls;
using Avalonia.Interactivity;
using OGVColorCatcher.Models;
using OGVColorCatcher.ViewModels;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace OGVColorCatcher.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += Window_Closing;
            _viewModel = new MainWindowViewModel(new I1SharpModel());
            DataContext = _viewModel;

            this.Opened += async (_, _) =>
            {
                //await _viewModel.InitializeAsync();
                LoadRememberedSettings();
            };
        }
        public MainWindow(I1SharpModel model) : this()
        {
            DataContext = new MainWindowViewModel(model);
        }
        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.RememberSettings)
                {
                    var settings = new RememberSettings
                    {
                        IsDensity = vm.IsDensity,
                        IsLab = vm.IsLab,
                        IsSpectrum = vm.IsSpectrum,
                        StatusDensityaux = vm.StatusDensityaux,
                        FirstMode = vm.FirstMode,
                        SecondMode = vm.SecondMode,
                        ThirdMode = vm.ThirdMode,
                        ShowIlluminantMode = vm.ShowIlluminantMode,
                        DecimalSeparatoraux = vm.DecimalSeparatoraux,
                        IsExtra = vm.IsExtra,
                        SameLineIlluminantModeData = vm.SameLineIlluminantModeData,
                        SameLineAllData = vm.SameLineAllData,
                        ShowStatusaux = vm.ShowStatusaux,
                        RememberMe = vm.RememberSettings,
                    };
                    RememberSettingsManager.Save(settings);
                }
                else
                {
                    RememberSettingsManager.Clear();
                }

                if (vm.CurrentDevice != null)
                {
                    vm.CurrentDevice.Close();
                }
            }

        }

        public class RememberSettings
        {
            public bool IsDensity { get; set; }
            public bool IsLab { get; set; }
            public bool IsSpectrum { get; set; }
            public string StatusDensityaux { get; set; }
            public string FirstMode { get; set; }
            public string SecondMode { get; set; }
            public string ThirdMode { get; set; }
            public bool ShowIlluminantMode { get; set; }
            public char DecimalSeparatoraux { get; set; }
            public bool IsExtra { get; set; }
            public bool SameLineIlluminantModeData { get; set; }
            public bool SameLineAllData { get; set; }
            public bool ShowStatusaux { get; set; }
            public bool RememberMe { get; set; }

        }
        private async void OpenSettings(object? sender, RoutedEventArgs e)
        {
            var dialog = new Settings();
            dialog.DataContext = this.DataContext;

            //if (dialog.DataContext is MainWindowViewModel vm)
            //{
            //    vm.CloseSettingsAction = dialog.Close;
            //}

            await dialog.ShowDialog(this);
        }

        private async void Popup()
        {
            var dialog = new Popup("This is a popup message.");
            await dialog.ShowDialog(this);
        }

        public static class RememberSettingsManager
        {
            private static readonly string filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OGVColorCatcher",
                "remember-settings.json"
            );

            public static void Save(RememberSettings settings)
            {
                var dir = Path.GetDirectoryName(filePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }

            public static RememberSettings Load()
            {
                if (!File.Exists(filePath))
                    return new RememberSettings();

                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<RememberSettings>(json) ?? new RememberSettings();
            }

            public static void Clear()
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        private void LoadRememberedSettings()
        {
            var settings = RememberSettingsManager.Load();
            if (DataContext is MainWindowViewModel vm)
            {
                vm.IsDensity = settings.IsDensity;
                vm.IsLab = settings.IsLab;
                vm.IsSpectrum = settings.IsSpectrum;
                vm.StatusDensityaux = settings.StatusDensityaux;
                vm.FirstMode = settings.FirstMode;
                vm.SecondMode = settings.SecondMode;
                vm.ThirdMode = settings.ThirdMode;
                vm.ShowIlluminantMode = settings.ShowIlluminantMode;
                vm.DecimalSeparatoraux = settings.DecimalSeparatoraux;
                vm.IsExtra = settings.IsExtra;
                vm.SameLineIlluminantModeData = settings.SameLineIlluminantModeData;
                vm.SameLineAllData = settings.SameLineAllData;
                vm.ShowStatusaux = settings.ShowStatusaux;
                vm.RememberSettings = settings.RememberMe;
            }
        }
    }
}
using Avalonia.Controls;
using Avalonia.Interactivity;
using OGVColorCatcher.Models;
using OGVColorCatcher.ViewModels;
using System.ComponentModel;

namespace OGVColorCatcher.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel(new I1SharpModel());
            DataContext = _viewModel;
        }
        public MainWindow(I1SharpModel model) : this()
        {
            DataContext = new MainWindowViewModel(model);
        }
        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                //var settings = new RememberSettings
                //{
                //    Substrate = vm.PaperSelected,
                //    Status = vm.StatusSelected,
                //    Decimais = vm.Decimais,
                //    DecimaisPorcentagem = vm.DecimaisPorcentagem
                //};
                //RememberSettingsManager.Save(settings);

                if (vm.CurrentDevice != null)
                {
                    vm.CurrentDevice.Close();
                }
            }

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
    }

}
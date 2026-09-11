using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Newtonsoft.Json;
using OGVColorCatcher.Models;
using OGVColorCatcher.Services;
using OGVColorCatcher.ViewModels;
using OGVColorCatcher.Views;
using System;
using System.Diagnostics;
using System.Linq;

namespace OGVColorCatcher
{
    public partial class App : Application
    {
        private const string BaseUrl = "https://licensing.ogvcolor.cloud/";
        private const int OfflineGraceDays = 3;
        private PeriodicLicenseValidationService _periodicLicenseValidationService;
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            try
            {
                var licenseJson = LicenseStore.LoadOrNull();
                if (string.IsNullOrWhiteSpace(licenseJson))
                {
                    Debug.WriteLine("[App.Startup] ⚠️  Licença não encontrada localmente. Abrindo janela de ativação.");
                    OpenActivationWindow();
                    return;
                }
                string localReason;
                bool validLocal = LicenseValidator.IsValidLicense(licenseJson, out localReason);
                if (!validLocal)
                {
                    Debug.WriteLine($"[App.Startup] ❌ Validação local falhou: {localReason}");
                    OpenActivationWindow();
                    return;
                }
                DateTime? lastGood;
                if (LicenseClockGuard.IsClockRolledBack(DateTime.UtcNow, TimeSpan.FromMinutes(10), out lastGood))
                {
                    Debug.WriteLine($"[App.Startup] ⚠️  CLOCK ROLLBACK DETECTADO! Last good: {lastGood}");
                    //MessageBox.Show(
                    //    "⚠️  Possível alteração do relógio do sistema detectada!\n\n" +
                    //    "Conecte à internet para validar sua licença.",
                    //    "Segurança",
                    //    MessageBoxButton.OK,
                    //    MessageBoxImage.Warning
                    //);
                    OpenActivationWindow();
                    return;
                }
                LicenseClockGuard.UpdateLastGoodUtc(DateTime.UtcNow);
                LicenseEnvelope env;
                LicensePayload payload;
                try
                {
                    env = JsonConvert.DeserializeObject<LicenseEnvelope>(licenseJson);
                    if (env == null)
                        throw new Exception("Envelope nulo");

                    payload = JsonConvert.DeserializeObject<LicensePayload>(env.payload);
                    if (payload == null)
                        throw new Exception("Payload nulo");
                }
                catch (Exception ex)
                {
                    OpenActivationWindow();
                    return;
                }
                string onlineReason;
                DateTime? serverUtc;
                bool validOnline = LicensingApi.ValidateOnlineWithServer(
                    BaseUrl,
                    payload.licenseKeyId,
                    FingerPrintService.ComputeFingerprint(),
                    out onlineReason,
                    out serverUtc
                );
                if (validOnline)
                {
                    if (serverUtc.HasValue)
                    {
                        LicenseClockGuard.UpdateFromServerUtc(serverUtc.Value);
                    }

                    OpenMainWindow();

                    _periodicLicenseValidationService = new PeriodicLicenseValidationService(BaseUrl);
                    _periodicLicenseValidationService.Start();

                    return;
                }
                if (onlineReason == "revoked" || onlineReason == "expired" ||
                    onlineReason == "fingerprint_mismatch" || onlineReason == "not_found" ||
                    onlineReason == "invalid_license_id" || onlineReason == "missing_data")
                {
                    Debug.WriteLine($"[App.Startup] ❌ BLOQUEIO: Licença {onlineReason}");
                    //MessageBox.Show(
                    //    $"❌ Sua licença não é válida: {onlineReason}\n\n" +
                    //    "Contacte o suporte para resolver este problema.",
                    //    "Licença Inválida",
                    //    MessageBoxButton.OK,
                    //    MessageBoxImage.Error
                    //);
                    OpenActivationWindow();
                    return;
                }
                if (LicenseClockGuard.IsWithinOfflineGrace(DateTime.UtcNow, OfflineGraceDays))
                {
                    //MessageBox.Show(
                    //    "⚠️  Sem conexão com o servidor de licenças.\n\n" +
                    //    $"Você pode continuar usando a aplicação por mais {OfflineGraceDays} dias.",
                    //    "Modo Offline",
                    //    MessageBoxButton.OK,
                    //    MessageBoxImage.Information
                    //);
                    OpenMainWindow();

                    _periodicLicenseValidationService = new PeriodicLicenseValidationService(BaseUrl);
                    _periodicLicenseValidationService.Start();

                    return;
                }
                OpenActivationWindow();
            }
            catch (Exception ex)
            {
                //MessageBox.Show(
                //    $"❌ Erro durante validação de licença:\n\n{ex.Message}",
                //    "Erro Crítico",
                //    MessageBoxButton.OK,
                //    MessageBoxImage.Error
                //);
                OpenActivationWindow();
            }
            //if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            //{
            //    I1SharpModel model = new I1SharpModel();
            //    model.SearchDevices();
            //    // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            //    // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            //    //DisableAvaloniaDataAnnotationValidation();
            //    desktop.MainWindow = new MainWindow
            //    {
            //        DataContext = new MainWindowViewModel(model),
            //    };
            //}

            base.OnFrameworkInitializationCompleted();
        }
        private void OpenMainWindow()
        {
            var model = new I1SharpModel();
            model.SearchDevices();
            var main = new MainWindow(model);
            main.Show();
        }
        private void OpenActivationWindow()
        {
            var model = new I1SharpModel();
            var viewModel = new MainWindowViewModel(model);

            var activation = new ActivationWindow
            {
                DataContext = viewModel
            };

            viewModel.ActivationWindow = activation;

            activation.Show();
        }
        //private void DisableAvaloniaDataAnnotationValidation()
        //{
        //    // Get an array of plugins to remove
        //    var dataValidationPluginsToRemove =
        //        BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        //    // remove each entry found
        //    foreach (var plugin in dataValidationPluginsToRemove)
        //    {
        //        BindingPlugins.DataValidators.Remove(plugin);
        //    }
        //}
    }
}
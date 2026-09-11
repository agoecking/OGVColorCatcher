using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Newtonsoft.Json;
using OGVColorCatcher.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OGVColorCatcher.Services
{
    internal class PeriodicLicenseValidationService
    {
        private readonly DispatcherTimer _timer;
        private readonly string _baseUrl;
        private bool _isChecking;
        public PeriodicLicenseValidationService(string baseUrl)
        {
            _baseUrl = baseUrl;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromHours(1)
            };
            _timer.Tick += Timer_Tick;
        }
        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            CheckNow();
        }
        public void CheckNow()
        {
            if (_isChecking)
                return;

            _isChecking = true;

            try
            {
                var licenseJson = LicenseStore.LoadOrNull();
                if (string.IsNullOrWhiteSpace(licenseJson))
                {
                    InvalidateAndRequestActivation("Licença não encontrada.");
                    return;
                }

                string localReason;
                if (!LicenseValidator.IsValidLicense(licenseJson, out localReason))
                {
                    InvalidateAndRequestActivation("Licença inválida: " + localReason);
                    return;
                }

                DateTime? lastGood;
                if (LicenseClockGuard.IsClockRolledBack(DateTime.UtcNow, TimeSpan.FromMinutes(10), out lastGood))
                {
                    InvalidateAndRequestActivation("Alteração de relógio detectada.");
                    return;
                }

                LicenseEnvelope env = JsonConvert.DeserializeObject<LicenseEnvelope>(licenseJson);
                if (env == null || string.IsNullOrWhiteSpace(env.payload))
                {
                    InvalidateAndRequestActivation("Envelope de licença inválido.");
                    return;
                }

                LicensePayload payload = JsonConvert.DeserializeObject<LicensePayload>(env.payload);
                if (payload == null)
                {
                    InvalidateAndRequestActivation("Payload de licença inválido.");
                    return;
                }

                string onlineReason;
                DateTime? serverUtc;
                bool validOnline = LicensingApi.ValidateOnlineWithServer(
                    _baseUrl,
                    payload.licenseKeyId,
                    FingerPrintService.ComputeFingerprint(),
                    out onlineReason,
                    out serverUtc
                );

                if (!validOnline)
                {
                    InvalidateAndRequestActivation("Licença não validou no servidor: " + onlineReason);
                    return;
                }

                if (serverUtc.HasValue)
                    LicenseClockGuard.UpdateFromServerUtc(serverUtc.Value);

                LicenseClockGuard.UpdateLastGoodUtc(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                //MessageBox.Show(
                //    "Erro ao verificar licença:\n" + ex.Message,
                //    "Licença",
                //    MessageBoxButton.OK,
                //    MessageBoxImage.Warning
               // );
            }
            finally
            {
                _isChecking = false;
            }
        }
        private void InvalidateAndRequestActivation(string message)
        {
            Stop();

            //MessageBox.Show(
            //    message + "\n\nSerá necessário reativar o sistema.",
            //    "Licença inválida",
            //    MessageBoxButton.OK,
            //    MessageBoxImage.Warning
            //);

            var activation = new OGVColorCatcher.ActivationWindow();
            activation.Show();

            if (Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow?.Close();
            }
        }
    }
}

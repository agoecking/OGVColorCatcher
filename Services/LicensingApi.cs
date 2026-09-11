using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OGVColorCatcher.Services
{
    public class ValidateResponse
    {
        public bool valid { get; set; }
        public string reason { get; set; }
        public string serverUtc { get; set; }
    }
    internal class LicensingApi
    {
        private static readonly HttpClient _http = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // ✅ ADICIONE ESTE MÉTODO
        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return "https://licensing.ogvcolor.cloud";

            // Remove trailing slash se existir
            return baseUrl.TrimEnd('/');
        }

        public static string Activate(string baseUrl, string licenseKey, string fingerprint)
        {
            var bodyObj = new
            {
                licenseKey = licenseKey,
                fingerprint = fingerprint
            };

            var json = JsonConvert.SerializeObject(bodyObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            System.Diagnostics.Debug.WriteLine($"[LicensingApi] POST {baseUrl}api/activate/");
            System.Diagnostics.Debug.WriteLine($"[LicensingApi] Request: {json}");

            var response = _http.PostAsync(baseUrl.TrimEnd('/') + "/api/activate/", content).Result;
            var responseText = response.Content.ReadAsStringAsync().Result;

            System.Diagnostics.Debug.WriteLine($"[LicensingApi] Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"[LicensingApi] Response: {responseText}");

            if (!response.IsSuccessStatusCode)
                throw new Exception("Falha na ativação: " + responseText);

            return responseText;
        }

        public static bool ValidateOnline(string baseUrl, string licenseKeyId, string fingerprint, out string reason)
        {
            reason = "";

            try
            {
                var bodyObj = new
                {
                    licenseKeyId = licenseKeyId,
                    fingerprint = fingerprint
                };

                var json = JsonConvert.SerializeObject(bodyObj);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                System.Diagnostics.Debug.WriteLine($"[LicensingApi] POST {baseUrl}api/validate-license/");

                var response = _http.PostAsync(baseUrl.TrimEnd('/') + "/api/validate-license/", content).Result;
                var responseText = response.Content.ReadAsStringAsync().Result;

                dynamic result = JsonConvert.DeserializeObject(responseText);

                if (result != null && result.valid == true)
                    return true;

                reason = result != null && result.reason != null
                    ? (string)result.reason
                    : "invalid";

                return false;
            }
            catch (Exception ex)
            {
                reason = "server_error: " + ex.Message;
                return false;
            }
        }

        public static bool Deactivate(string baseUrl, string licenseKeyId, string fingerprint, out string reason)
        {
            reason = "";

            try
            {
                var bodyObj = new
                {
                    licenseKeyId = licenseKeyId,
                    fingerprint = fingerprint
                };

                var json = JsonConvert.SerializeObject(bodyObj);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string normalizedUrl = NormalizeBaseUrl(baseUrl);
                string fullUrl = $"{normalizedUrl}/api/deactivate/";

                System.Diagnostics.Debug.WriteLine($"[LicensingApi] POST {fullUrl}");

                var response = _http.PostAsync(fullUrl, content).Result;
                var responseText = response.Content.ReadAsStringAsync().Result;

                System.Diagnostics.Debug.WriteLine($"[LicensingApi] Deactivate Response: {responseText}");

                if (!response.IsSuccessStatusCode)
                {
                    reason = responseText;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = "server_error: " + ex.Message;
                return false;
            }
        }

        public static bool ValidateOnlineWithServer(
            string baseUrl,
            string licenseKeyId,
            string fingerprint,
            out string reason,
            out DateTime? serverUtc
        )
        {
            reason = "";
            serverUtc = null;

            try
            {
                var bodyObj = new
                {
                    licenseKeyId = licenseKeyId,
                    fingerprint = fingerprint
                };

                var json = JsonConvert.SerializeObject(bodyObj);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                System.Diagnostics.Debug.WriteLine($"[LicensingApi] POST {baseUrl}api/validate-license/");

                var response = _http.PostAsync(baseUrl.TrimEnd('/') + "/api/validate-license/", content).Result;
                var responseText = response.Content.ReadAsStringAsync().Result;

                System.Diagnostics.Debug.WriteLine($"[LicensingApi] Validate Response: {responseText}");

                var result = JsonConvert.DeserializeObject<ValidateResponse>(responseText);
                if (result == null)
                {
                    reason = "invalid";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(result.serverUtc))
                    serverUtc = DateTime.Parse(result.serverUtc).ToUniversalTime();

                if (result.valid)
                    return true;

                reason = !string.IsNullOrWhiteSpace(result.reason) ? result.reason : "invalid";
                return false;
            }
            catch (Exception ex)
            {
                reason = "server_error: " + ex.Message;
                return false;
            }
        }
    }
}

using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OGVColorCatcher.Services
{
    public class LicenseEnvelope
    {
        public string payload { get; set; }
        public string signature { get; set; }
    }

    public class LicensePayload
    {
        public string app { get; set; }
        public string customerId { get; set; }
        public string licenseKeyId { get; set; }
        public string fingerprint { get; set; }
        public string expiresAt { get; set; }
        public string[] features { get; set; }
        public string issuedAt { get; set; }
    }
    public static class LicenseValidator
    {
        private const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
            MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAtSZt+aok7FmRiKpkbQOD
            TUWLVJ9qbaMftt8C2uI9nCX0RyolQAp3CNfRPvzdYV/aTmXFQ6xNbBnsWeoGO7LS
            lxMaZwpn+VPdSZrheEznwoMjDIQY+HSisfJMYvIeN8pVvFQRaLw9u/p+GRyKsfeb
            ulv9vjUzj4kuh+QKY9DvB9F+uFMctl89ZLF1VmHigNMNGZTiXHzQe1pw9SS7AvhT
            HiVqGf3xVu7TV1tbnTq2/8HKmGNpcwtOiwZ8UbnIYXGSvT/zn+9O51lXxWLpC/V0
            +qEz5u1VPx6TdAHgQhA3mGtES1jXftgE7UqYxfJKTfDu+lCgYAcBo6K1azggsyBj
            6wIDAQAB
            -----END PUBLIC KEY-----";

        public static bool IsValidLicense(string envelopeJson, out string reason)
        {
            reason = "";

            LicenseEnvelope env;
            try
            {
                env = JsonConvert.DeserializeObject<LicenseEnvelope>(envelopeJson);
            }
            catch
            {
                reason = "Arquivo de licença inválido";
                return false;
            }

            if (env == null || string.IsNullOrWhiteSpace(env.payload) || string.IsNullOrWhiteSpace(env.signature))
            {
                reason = "Licença incompleta";
                return false;
            }

            if (!VerifySignature(env.payload, env.signature))
            {
                reason = "Assinatura inválida";
                return false;
            }

            LicensePayload payload;
            try
            {
                payload = JsonConvert.DeserializeObject<LicensePayload>(env.payload);
            }
            catch
            {
                reason = "Payload inválido";
                return false;
            }

            if (!string.Equals(payload.app, "OGVColorCatcher", StringComparison.Ordinal))
            {
                reason = "Licença de outro aplicativo";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(payload.expiresAt))
            {
                var exp = DateTime.Parse(payload.expiresAt).ToUniversalTime();
                if (exp < DateTime.UtcNow)
                {
                    reason = "Licença expirada";
                    return false;
                }
            }

            var localFingerprint = FingerPrintService.ComputeFingerprint();
            if (!string.Equals(payload.fingerprint, localFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Licença não pertence a este computador";
                return false;
            }

            return true;
        }

        private static bool VerifySignature(string payloadJson, string signatureB64)
        {
            byte[] data = Encoding.UTF8.GetBytes(payloadJson);
            byte[] signature = Convert.FromBase64String(signatureB64);

            AsymmetricKeyParameter pubKey;
            using (var sr = new StringReader(PublicKeyPem))
            {
                var pr = new PemReader(sr);
                pubKey = (AsymmetricKeyParameter)pr.ReadObject();
            }

            var rsaParams = DotNetUtilities.ToRSAParameters((RsaKeyParameters)pubKey);

            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.ImportParameters(rsaParams);
                return rsa.VerifyData(data, CryptoConfig.MapNameToOID("SHA256"), signature);
            }
        }
    }
}

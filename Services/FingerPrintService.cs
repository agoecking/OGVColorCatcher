using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OGVColorCatcher.Services
{
    internal class FingerPrintService
    {
        static string GetMachineGuid()
        {
            using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
            {
                var v = key != null ? key.GetValue("MachineGuid") : null;
                return v != null ? v.ToString() : "unknown";
            }
        }
        public static string ComputeFingerprint()
        {
            var raw = "MachineGuid=" + GetMachineGuid();
            var bytes = Encoding.UTF8.GetBytes(raw);

            byte[] hashBytes;
            using (var sha = SHA256.Create())
            {
                hashBytes = sha.ComputeHash(bytes);
            }

            return BytesToHex(hashBytes);
        }
        static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2")); // hex minúsculo
            return sb.ToString();
        }
    }
}

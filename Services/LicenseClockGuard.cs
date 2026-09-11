using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OGVColorCatcher.Services
{
    internal class LicenseClockGuard
    {
        private class State
        {            
            public string LastGoodUtc { get; set; }
            public string LastServerUtc { get; set; }
        }
        private static string GetPath()
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "OGVColor", "ColorCatcher", "clock_state.bin");
        }
        private static State LoadStateOrNull()
        {
            try
            {
                var path = GetPath();
                if (!File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"[LicenseClockGuard] Arquivo não encontrado: {path}");
                    return null;
                }

                var protectedBytes = File.ReadAllBytes(path);
                var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.LocalMachine);
                var json = Encoding.UTF8.GetString(plain);

                System.Diagnostics.Debug.WriteLine($"[LicenseClockGuard] ✓ Estado carregado");
                return JsonConvert.DeserializeObject<State>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseClockGuard] Erro ao carregar: {ex.Message}");
                return null;
            }
        }
        private static void SaveState(State state)
        {
            try
            {
                var path = GetPath();
                var dir = Path.GetDirectoryName(path);

                if (!Directory.Exists(dir))
                {
                    System.Diagnostics.Debug.WriteLine($"[LicenseClockGuard] Criando diretório: {dir}");
                    Directory.CreateDirectory(dir);
                }

                var json = JsonConvert.SerializeObject(state);
                var plain = Encoding.UTF8.GetBytes(json);
                var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.LocalMachine);

                File.WriteAllBytes(path, protectedBytes);
                System.Diagnostics.Debug.WriteLine($"[LicenseClockGuard] ✓ Estado salvo com sucesso");
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseClockGuard] ❌ ERRO: Sem permissão - Programa precisa rodar como ADMINISTRADOR");
                System.Diagnostics.Debug.WriteLine($"[LicenseClockGuard] Detalhes: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseClockGuard] ❌ Erro ao salvar: {ex.Message}");
                throw;
            }
        }
        public static bool IsClockRolledBack(DateTime utcNow, TimeSpan allowedSkew, out DateTime? lastGoodUtc)
        {
            lastGoodUtc = null;

            try
            {
                var st = LoadStateOrNull();
                if (st == null || string.IsNullOrWhiteSpace(st.LastGoodUtc)) return false;

                var last = DateTime.Parse(st.LastGoodUtc).ToUniversalTime();
                lastGoodUtc = last;

                return utcNow < last.Subtract(allowedSkew);
            }
            catch
            {
                return true;
            }
        }
        public static void UpdateLastGoodUtc(DateTime utcNow)
        {
            try
            {
                var st = LoadStateOrNull() ?? new State();
                st.LastGoodUtc = utcNow.ToString("o");
                SaveState(st);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseClockGuard] Erro ao atualizar LastGoodUtc: {ex.Message}");
            }
        }
        public static void UpdateFromServerUtc(DateTime serverUtc)
        {
            try
            {
                var st = LoadStateOrNull() ?? new State();
                st.LastServerUtc = serverUtc.ToString("o");
                st.LastGoodUtc = serverUtc.ToString("o");
                SaveState(st);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseClockGuard] Erro ao atualizar ServerUtc: {ex.Message}");
            }
        }
        public static bool IsWithinOfflineGrace(DateTime utcNow, int graceDays)
        {
            try
            {
                var st = LoadStateOrNull();
                if (st == null || string.IsNullOrWhiteSpace(st.LastServerUtc)) return false;

                var lastServer = DateTime.Parse(st.LastServerUtc).ToUniversalTime();
                return utcNow <= lastServer.AddDays(graceDays);
            }
            catch
            {
                return false;
            }
        }
    }
}

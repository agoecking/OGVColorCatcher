using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OGVColorCatcher.Services
{
    internal class LicenseStore
    {
        static string DirPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "OGVColor", "ColorCatcher");
        static string FilePath = Path.Combine(DirPath, "license.json");
        public static string DebugGetPath() => FilePath;
        public static void Save(string licenseJson)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseStore] Salvando em: {FilePath}");

                if (!Directory.Exists(DirPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[LicenseStore] Criando diretório: {DirPath}");
                    Directory.CreateDirectory(DirPath);
                }

                File.WriteAllText(FilePath, licenseJson);
                System.Diagnostics.Debug.WriteLine($"[LicenseStore] ✓ Licença salva com sucesso!");
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseStore] ❌ ERRO: Sem permissão - Programa precisa rodar como ADMINISTRADOR");
                System.Diagnostics.Debug.WriteLine($"[LicenseStore] Caminho: {FilePath}");
                System.Diagnostics.Debug.WriteLine($"[LicenseStore] Detalhes: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseStore] ❌ ERRO: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }
        public static string LoadOrNull()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseStore] Carregando de: {FilePath}");

                if (!File.Exists(FilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[LicenseStore] Arquivo não encontrado");
                    return null;
                }

                var content = File.ReadAllText(FilePath);
                System.Diagnostics.Debug.WriteLine($"[LicenseStore] ✓ Licença carregada com sucesso");
                return content;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseStore] ❌ Erro ao carregar: {ex.Message}");
                return null;
            }
        }
    }
}

using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OGVColorCatcher.ViewModels.MainWindowViewModel;

namespace OGVColorCatcher.Services
{
    internal class ColorService
    {
        public string ClassificarCanalLabTrapping(
            double L, double a, double b,
            double cromaNeutroBase = 8.0,   // era 6; suba p/ 8–12 conforme ruído do seu medidor
            double limitePreto = 25.0,      // 25–30 costuma funcionar melhor que 60
            double toleranciaAB = 3.0       // janela extra de neutralidade em |a*| e |b*|
            )
        {
            // 1) Muito escuro = preto (antes de olhar croma/matiz)
            if (L <= limitePreto)
                return "K";

            // 2) Cálculos básicos
            double C = Math.Sqrt(a * a + b * b);
            double h = Math.Atan2(b, a) * 180.0 / Math.PI;
            if (h < 0) h += 360.0;

            // 3) Tornar o limiar de neutralidade mais frouxo em L* baixos
            //    Ex.: abaixo de L*=60, aumente o limiar de C* gradualmente
            double boost = 0.0;
            if (L < 60.0)
                boost = (60.0 - L) * 0.25; // ajuste fino: 0.2–0.4 conforme seus dados

            double cromaNeutro = cromaNeutroBase + boost;

            // 4) Neutro/substrato (dois critérios: C* baixo OU |a*| e |b*| pequenos)
            //bool quaseNeutro = C <= cromaNeutro || (Math.Abs(a) <= toleranciaAB && Math.Abs(b) <= toleranciaAB);
            //if (quaseNeutro)
            //    return "Neutro/Substrato";

            // 5) Classificação por setores de matiz
            if (h >= 200 && h < 280) return "C";
            if (h >= 340 || h < 20) return "M";
            if (h >= 70 && h < 110) return "Y";

            // Misturas (secundárias)
            //Colocar condicional para trapping
            if (h >= 120 && h < 180) return "C+Y";
            if (h >= 20 && h < 70) return "M+Y";
            if (h >= 280 && h < 340) return "C+M";

            return "Indefinido/Misto";
        }

        public string ClassificarCanalLab(
            double L, double a, double b,
            double limitePreto = 20.0,
            double cromaNeutro = 8.0
        )
        {
            // 1) Preto bem escuro
            if (L <= limitePreto)
                return "K";

            // 2) Cálculos básicos
            double C = Math.Sqrt(a * a + b * b);
            double h = Math.Atan2(b, a) * 180.0 / Math.PI;
            if (h < 0) h += 360.0;

            // 3) Muito pouco croma = neutro/substrato
            if (C <= cromaNeutro)
                return "Neutro/Substrato";

            // 4) Hues de referência das primárias
            const double hC = 230.0;
            const double hM = 350.0; // -10° → 350°
            const double hY = 90.0;

            double dC = HueDelta(h, hC);
            double dM = HueDelta(h, hM);
            double dY = HueDelta(h, hY);

            // 5) Regiões de mistura com viés

            // Faixa M+Y (20–70°): favorecer M
            if (h >= 20 && h < 70)
            {
                double wM = dM * 0.8; // viés pró-M (20% mais "barato")
                double wY = dY;

                return wM <= wY ? "M" : "Y";
            }

            // Faixa Y+C (110–200°): favorecer C
            if (h >= 110 && h < 200)
            {
                double wC = dC * 0.8; // viés pró-C
                double wY = dY;

                return wC <= wY ? "C" : "Y";
            }

            // Faixa C+M (azul, 280–340°): decide entre C e M
            if (h >= 280 && h < 340)
            {
                double wC = dC;
                double wM = dM;
                return wC <= wM ? "C" : "M";
            }

            // 6) Fora das misturas → primária mais próxima pura
            if (dC <= dM && dC <= dY) return "C";
            if (dM <= dC && dM <= dY) return "M";
            return "Y";
        }

        private static double HueDelta(double h1, double h2)
        {
            double d = Math.Abs(h1 - h2);
            return d > 180.0 ? 360.0 - d : d;
        }

        public string ClassificarCanalPorDensidade(double C, double M, double Y, double K)
        {
            const double EPS = 0.05;
            const double TOL_K = 0.04; // tolerância de 0.04 = 4% relativa do hue/densidade

            double max = Math.Max(Math.Max(C, M), Math.Max(Y, K));

            // Se K for alto e estiver muito próximo do max → forçar K
            if (K > EPS && Math.Abs(max - K) <= TOL_K)
                return "K";

            // Caso contrário, se um primário dominar claramente:
            if (C == max && C > EPS) return "C";
            if (M == max && M > EPS) return "M";
            if (Y == max && Y > EPS) return "Y";
            if (K == max && K > EPS) return "K";

            return "Indefinido/Misto";
        }

        public (double L, double A, double B) RgbToLab(float r, float g, float b)
        {
            // --- Normaliza RGB para 0–1 ---
            double R = r / 255.0;
            double G = g / 255.0;
            double B = b / 255.0;

            // --- Corrige gamma (sRGB) ---
            R = (R > 0.04045) ? Math.Pow((R + 0.055) / 1.055, 2.4) : (R / 12.92);
            G = (G > 0.04045) ? Math.Pow((G + 0.055) / 1.055, 2.4) : (G / 12.92);
            B = (B > 0.04045) ? Math.Pow((B + 0.055) / 1.055, 2.4) : (B / 12.92);

            // --- Converte para XYZ (referência D65) ---
            R *= 100.0;
            G *= 100.0;
            B *= 100.0;

            double X = R * 0.4124564 + G * 0.3575761 + B * 0.1804375;
            double Y = R * 0.2126729 + G * 0.7151522 + B * 0.0721750;
            double Z = R * 0.0193339 + G * 0.1191920 + B * 0.9503041;

            // --- Normaliza pelo branco de referência D65 ---
            X /= 95.047;
            Y /= 100.000;
            Z /= 108.883;

            // --- Função auxiliar f(t) ---
            X = PivotXYZ(X);
            Y = PivotXYZ(Y);
            Z = PivotXYZ(Z);

            // --- Converte para Lab ---
            double L = (116 * Y) - 16;
            double A = 500 * (X - Y);
            double Bc = 200 * (Y - Z);

            return (L, A, Bc);
        }

        private static double PivotXYZ(double n)
        {
            return (n > 0.008856) ? Math.Pow(n, 1.0 / 3.0) : ((7.787 * n) + (16.0 / 116.0));
        }
    }

    public class AllDensityRow
    {
        public ObservableCollection<SolidColorBrush> Colors { get; } = new();
        public List<float> Densities { get; set; } = new(); // [C,M,Y,K]
    }
}

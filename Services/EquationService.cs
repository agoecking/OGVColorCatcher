using Avalonia.Utilities;
using DynamicData.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OGVColorCatcher.Services
{
    internal class EquationService
    {
        public enum Ink { C = 0, M = 1, Y = 2, K = 3 }

        public List<float[]> DiffDensity(List<List<float[]>> measurements)
        {

            List<float[]> diffDensity = new List<float[]>();
            float[] test = new float[measurements[0][0].Length];
            if (measurements.Count < 2)
            {
                throw new ArgumentException("At least two sets of measurements are required for difference density calculation.");
            }

            for (int i = 0; i < measurements[0].Count; i++)
            {
                for (int j = 0; j < measurements[0][i].Length; j++)
                {
                    if (measurements[1][i][j] != measurements[0][i][j])
                    {
                        if (measurements[0][i].Length <= 3)
                        {
                            Math.Abs(test[j] = measurements[1][i][j] - measurements[0][i][j]);
                            test[j] = 1;
                        }
                        else
                        {
                            test[j] = measurements[1][i][j] - measurements[0][i][j];

                        }

                    }
                    else
                    {
                        test[j] = measurements[0][i][j];

                    }


                }
                diffDensity.Add(test);
            }
            Debug.WriteLine(diffDensity);
            return diffDensity;
        }

        public List<float[]> DiffRgb(List<List<float[]>> rgb)
        {

            List<float[]> diffDensity = new List<float[]>();
            float[] test = new float[rgb[0][0].Length];
            if (rgb.Count < 2)
            {
                throw new ArgumentException("At least two sets of measurements are required for difference density calculation.");
            }


            for (int j = 0; j < rgb[0][0].Length; j++)
            {
                test[j] = rgb[1][0][j] - rgb[0][0][j];
                test[j] = Math.Max(0, test[j]);
            }
            diffDensity.Add(test);

            Debug.WriteLine(diffDensity);

            return diffDensity;
        }

        public List<float[]> DotGain(List<float[]> density100, List<float[]> densityDot, float targetTone)
        {
            List<float[]> dotGainResults = new List<float[]>();

            if (density100.Count != densityDot.Count)
            {
                throw new ArgumentException("As listas density100 e densityDot devem ter o mesmo tamanho.");
            }
            for (int i = 0; i < density100.Count; i++)
            {
                // Verifica se os arrays têm o mesmo tamanho
                if (density100[i].Length != densityDot[i].Length)
                {
                    throw new ArgumentException("Os arrays de densidade devem ter o mesmo tamanho.");
                }

                // Array para armazenar os resultados do dot gain para o conjunto atual
                float[] dotGainValues = new float[density100[i].Length];

                //dotGainValues[0] = targetTone;
                int valuecount = 1;
                // Calcula o dot gain para cada valor no array
                for (int j = 0; j < density100[i].Length; j++)
                {
                    float tvi = CalculateTVI(density100[i][j], densityDot[i][j], targetTone);
                    Console.WriteLine(tvi);
                    dotGainValues[j] = tvi;
                }

                // Adiciona o array de resultados à lista
                dotGainResults.Add(dotGainValues);
                Console.WriteLine("");
            }

            return dotGainResults;
        }
        public List<float> Trapping(float[] First, float[] Second, float[] Overlap)
        {
            const float EPS = 1e-3f;
            var results = new List<float>();

            // M+Y  (canal Y)
            if (Second[2] > EPS)
                results.Add(((Overlap[2] - First[2]) / Second[2]) * 100f);

            // C+M  (canal M)
            if (Second[1] > EPS)
                results.Add(((Overlap[1] - First[1]) / Second[1]) * 100f);

            // C+Y  (canal Y)
            if (Second[2] > EPS)
                results.Add(((Overlap[2] - First[2]) / Second[2]) * 100f);

            return results;
        }




        static float CalculateTVI(float solidDensity, float halftoneDensity, float targetToneValue)
        {
            float measuredToneValue = (float)DensityToToneValue(halftoneDensity, solidDensity);

            float tvi = measuredToneValue - targetToneValue;

            return tvi;
        }
        static double DensityToToneValue(float halftoneDensity, float solidDensity)
        {
            return (1 - Math.Pow(10, -halftoneDensity)) / (1 - Math.Pow(10, -solidDensity)) * 100;
        }

    }
}

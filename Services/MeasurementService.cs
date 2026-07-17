using OGVColorCatcher.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OGVColorCatcher.Services
{
    internal class MeasurementService
    {
        private I1Pro64.MeasurementModeType selectedMeasurementMode;
        public List<float[]> readData;
        public List<float[]> densityData;
        public List<float[]> labData;
        public List<float[]> rgbValues;
        public string illuminationKey;
        public string serialNumber;
        public string deviceName;
        public string observerKey;
        public int densityDecimals = 2;
        public int porcentagemDecimals = 0;
        public int labDecimals = 0;
        public int rgbDecimals = 0;

        public void SetDecimals(int decimals)
        {
            densityDecimals = decimals;
            labDecimals = decimals;
        }
        public void SetDecimalsPorcentagem(int pdecimals)
        {
            porcentagemDecimals = pdecimals;
            
        }


        public void CalibrateDevice(I1SharpModel model)
        {
            try
            {
                model.CurrentDevice.Calibrate();
            }
            catch (Exception ex)
            {
                // Log the exception or handle it accordingly
                Console.WriteLine($"Error during calibration: {ex.Message}");
            }
        }

        public void ReflectanceSpot(I1SharpModel model)
        {
            try
            {
                model.CurrentDevice.TriggerMeasurementSpot(model.CurrentDevice.MeasurementMode);
                DataCatcher(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during ReflectanceSpot: {ex.Message}");
            }
        }

        public void DualReflectanceSpot(I1SharpModel model)
        {
            try
            {
                model.CurrentDevice.TriggerMeasurementSpot(model.CurrentDevice.MeasurementMode);
                DataCatcher(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during ReflectanceSpot: {ex.Message}");
            }
        }

        public void DataCatcher(I1SharpModel model)
        {
            readData = RoundList(model.CurrentDevice.listOfMeasurement, densityDecimals);
            densityData = RoundList(model.CurrentDevice.listOfDensity, densityDecimals);
            labData = RoundList(model.CurrentDevice.tristimulusReqLab, labDecimals);
            rgbValues = model.CurrentDevice.tristimulusReq;

            illuminationKey = model.CurrentDevice.illuminationKey;
            serialNumber = model.CurrentDevice.SerialNumber;
            deviceName = model.CurrentDevice.Name;
            observerKey = model.CurrentDevice.observerKey;
        }

        private List<float[]> RoundList(List<float[]> source, int decimals)
        {
            if (source == null)
                return new List<float[]>();

            return source
                .Select(arr => arr == null
                    ? Array.Empty<float>()
                    : arr.Select(v => (float)Math.Round(v, decimals, MidpointRounding.AwayFromZero)).ToArray())
                .ToList();
        }

        public List<float[]> RoundPercentageList(List<float[]> source)
        {
            return RoundList(source, porcentagemDecimals);
        }

        public string FormatDensity(float value)
        {
            return value.ToString($"F{densityDecimals}");
        }

        public string FormatLab(float value)
        {
            return value.ToString($"F{labDecimals}");
        }

        public string FormatRgb(float value)
        {
            return value.ToString($"F{rgbDecimals}");
        }
    }
}

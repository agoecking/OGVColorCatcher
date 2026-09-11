using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.TextFormatting.Unicode;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using Newtonsoft.Json;
using OGVColorCatcher.Models;
using OGVColorCatcher.Services;
using OGVColorCatcher.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reactive;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WindowsInput;
using WindowsInput.Native;

namespace OGVColorCatcher.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        //Variáveis

        //------------------------------------
        //DISPOSITIVO
        private I1SharpModel model;
        public I1Pro64 _currentDevice;
        public ObservableCollection<I1Pro64> Devices => model.Devices;
        public ReactiveCommand<Unit, Unit> CalibrateCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseDeviceCommand { get; }
        public ReactiveCommand<TopLevel, Unit> SelectFolderCommand { get; }
        public I1Pro64 CurrentDevice
        {
            get => _currentDevice;
            set
            {
                this.RaiseAndSetIfChanged(ref _currentDevice, value);
                model.CurrentDevice = value; // sincroniza com o seu model
            }
        }

        //------------------------------------
        //Configurações de leitura
        private bool _isDensity;
        private bool _isLab;
        private bool _isSpectrum;
        private string _statusDensityaux;
        public string _firstMode;
        public string _secondMode;
        public string _thirdMode;
        private int[] printOptions = new int[3];
        public List<string> StatusOption
        {
            get => new List<string> { "ANSIA", "ANSIE", "ANSII", "ANSIT" };
        }
        public List<string> IlluminantModeOption
        {
            get => new List<string> { " ", "M0", "M1", "M2" };
        }
        public bool IsDensity
        {
            get => _isDensity;
            set => this.RaiseAndSetIfChanged(ref _isDensity, value);
        }
        public bool IsLab
        {
            get => _isLab;
            set => this.RaiseAndSetIfChanged(ref _isLab, value);
        }
        public bool IsSpectrum
        {
            get => _isSpectrum;
            set => this.RaiseAndSetIfChanged(ref _isSpectrum, value);
        }
        public string StatusDensityaux
        {
            get => _statusDensityaux;
            set => this.RaiseAndSetIfChanged(ref _statusDensityaux, value);
        }
        public string FirstMode
        {
            get => _firstMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _firstMode, value);
                printOptions[0] = value switch
                {
                    "M0" => 0,
                    "M1" => 1,
                    "M2" => 2,
                    _ => 9
                };
            }
        }
        public string SecondMode
        {
            get => _secondMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _secondMode, value);
                printOptions[1] = value switch
                {
                    "M0" => 0,
                    "M1" => 1,
                    "M2" => 2,
                    _ => 9
                };
            }
        }
        public string ThirdMode
        {
            get => _thirdMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _thirdMode, value);
                printOptions[2] = value switch
                {
                    "M0" => 0,
                    "M1" => 1,
                    "M2" => 2,
                    _ => 9
                };
            }
        }

        //------------------------------------
        //Configurações de escrita
        private bool _isExtra;
        private char _decimalSeparatoraux;
        private bool _showIlluminantMode = true;
        private bool _sameLineIlluminantModeData = false;
        private bool _SameLineAllData = false;
        private bool _showStatusaux = true;
        public List<char> DecimalSeparatorOption
        {
            get => new List<char> { '.', ',' };
        }
        public bool ShowIlluminantMode
        {
            get => _showIlluminantMode;
            set => this.RaiseAndSetIfChanged(ref _showIlluminantMode, value);
        }
        public char DecimalSeparatoraux
        {
            get => _decimalSeparatoraux;
            set => this.RaiseAndSetIfChanged(ref _decimalSeparatoraux, value);
        }
        public bool IsExtra
        {
            get => _isExtra;
            set => this.RaiseAndSetIfChanged(ref _isExtra, value);
        }
        public bool SameLineIlluminantModeData
        {
            get => _sameLineIlluminantModeData;
            set 
            { 
                this.RaiseAndSetIfChanged(ref _sameLineIlluminantModeData, value);
                if (value)
                    SameLineAllData = false;
            }
        }
        public bool SameLineAllData
        {
            get => _SameLineAllData;
            set 
            { 
                this.RaiseAndSetIfChanged(ref _SameLineAllData, value);
                if (value)
                    SameLineIlluminantModeData = false;
            }
        }
        public bool ShowStatusaux
        {
            get => _showStatusaux;
            set => this.RaiseAndSetIfChanged(ref _showStatusaux, value);
        }

        //------------------------------------
        //Configurações de Media

        bool isMedia = false;
        int numberOfReadings;
        List<List<List<float[]>>> mediaList = new List<List<List<float[]>>>();

        //------------------------------------
        //Configurações de Arquivo
        private bool _saveToFile = false;
        private string measurementFolderPath = string.Empty;  // Pasta selecionada pelo usuário
        public bool _isHeaderOnFile = false;
        private bool isShowMeasurement = true;

        //------------------------------------
        //Configurações gerais
        private int passwordSum;
        bool shouldTriggerMeasurements = false;
        public ActivationWindow? ActivationWindow { get; set; }
        public ICommand SubmitPasswordCommand { get; }
        public ICommand SubmitActivationCommand { get; }
        public ICommand SaveDeviceSerialCommand { get; }
        private bool _isCalibrateEnabled = false;
        public string _showError;
        private bool _rememberSettings = false;

        public bool RememberSettings
        {
            get => _rememberSettings;
            set => this.RaiseAndSetIfChanged(ref _rememberSettings, value);
        }
        public bool SaveToFile
        {
            get => _saveToFile;
            set => this.RaiseAndSetIfChanged(ref _saveToFile, value);
        }

        public bool IsHeaderOnFile
        {
            get => _isHeaderOnFile;
            set => this.RaiseAndSetIfChanged(ref _isHeaderOnFile, value);
        }
        public bool IsCalibrateEnabled
        {
            get => _isCalibrateEnabled;
            set => this.RaiseAndSetIfChanged(ref _isCalibrateEnabled, value);
        }
        public string ShowError
        {
            get => _showError;
            set => this.RaiseAndSetIfChanged(ref _showError, value);
        }
        //------------------------------------
        //Construtor
        public MainWindowViewModel(I1SharpModel model)
        {
            this.model = model;
            this.model.DeviceChanged += model_DeviceChanged;
            ShowError = string.Empty;
            var scheduler = AvaloniaScheduler.Instance;

            CalibrateCommand = ReactiveCommand.Create(CalibrateDevice, outputScheduler: scheduler);
            CloseDeviceCommand = ReactiveCommand.Create(CloseDevice, outputScheduler: scheduler);
            SelectFolderCommand = ReactiveCommand.CreateFromTask<TopLevel>(
                SelectFolder,
                outputScheduler: scheduler);
            SubmitPasswordCommand = new RelayCommand<object?>(SubmitPassword);
            SubmitActivationCommand = new RelayCommand<object?>(SubmitActivation);
            SaveDeviceSerialCommand = new RelayCommand<object?>(SaveDeviceSerial);

            DateTime currentDate = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"));
            int daySum = currentDate.Day.ToString().Sum(c => int.Parse(c.ToString()));
            int monthSum = currentDate.Month.ToString().Sum(c => int.Parse(c.ToString()));
            int yearSum = currentDate.Year.ToString().Sum(c => int.Parse(c.ToString()));
            string password = daySum.ToString() + monthSum.ToString() + yearSum.ToString();
            passwordSum = int.Parse(password);

            //InitializeTimer();
        }


        async void model_DeviceChanged(I1Pro64 device)
        {
            if (device != null)
            {

                bool encontrado = DecryptFileContent();
                if (!encontrado)
                {
                    var box = MessageBoxManager.GetMessageBoxStandard("Aviso", "Aparelho não cadastrado");
                    await box.ShowAsync();
                    Devices.Clear();
                }
                else
                {
                    device.ButtonPressed += device_ButtonPressed;
                }
            }
        }

        private void CalibrateDevice()
        {
            try
            {
                if (model.CurrentDevice == null)
                    throw new Exception("Dispositivo não conectado");

                model.CurrentDevice.Calibrate();

            }
            catch (I1Exception ex)
            {

                //HandleError(ex);
            }
        }
        private void CloseDevice()
        {
        }

        private void SubmitPassword(object? parameter)
        {
            var text = parameter?.ToString();

            if (text == passwordSum.ToString())
            {
                Debug.WriteLine("Senha correta!");
                settingsEnabled = true;
            }
            else
            {
                settingsEnabled = false;
            }
        }

        private bool _settingsEnabled = false;
        public bool settingsEnabled
        {
            get => _settingsEnabled;
            set => this.RaiseAndSetIfChanged(ref _settingsEnabled, value);
        }
        private void device_ButtonPressed(I1Pro64 device)
        {
            try
            {
                bool isDensityCheck = this.IsDensity;
                int cont = 0;

                var listOfMeasurements = model.CurrentDevice.TriggerMeasurementNew(
                    this.IsDensity,
                    this.IsLab,
                    this.IsSpectrum,
                    StatusDensityaux
                );

                Dispatcher.UIThread.InvokeAsync(() => 
                {
                    if (SaveToFile)
                    {
                        SaveMeasurementsToFile(listOfMeasurements);
                        return;
                    }

                    var simulator = new InputSimulator();

                    var cultureInfo = DecimalSeparatoraux == ','
                        ? new CultureInfo("pt-BR")
                        : CultureInfo.InvariantCulture;

                    if (!isShowMeasurement && isMedia)
                    {
                        HandleMedia(listOfMeasurements);
                        //SystemSounds.Beep.Play();
                        return;
                    }

                    if (isDensityCheck)
                    {
                        cont = HandleDensity(listOfMeasurements, simulator, cultureInfo);
                        //PressReturn(simulator);
                        if (!SameLineAllData || !SameLineIlluminantModeData)
                        {
                            PressReturn(simulator);

                            if (this.IsExtra)
                                PressReturnWithDelay(simulator, 1000);
                        }
                    }

                    for (int i = cont; i < listOfMeasurements.Count; i++)
                    {
                        var measurementAux = listOfMeasurements[i];
                        int numberOfArrays = printOptions.Length - 1;

                        bool isLastI = (i == listOfMeasurements.Count - 1);

                        for (int j = 0; j < measurementAux.Count; j++)
                        {
                            if (this.printOptions[j] == 9)
                            {
                                numberOfArrays--;
                                continue;
                            }

                            bool isLastValidJ = true;
                            for (int jj = j + 1; jj < measurementAux.Count; jj++)
                            {
                                if (this.printOptions[jj] != 9)
                                {
                                    isLastValidJ = false;
                                    break;
                                }
                            }

                            bool isLastReading = isLastI && isLastValidJ;

                            var measurement = measurementAux[this.printOptions[j]];
                            int numberOfValues = measurement.Length - 1;
                            WriteIlluminantLabelIfNeeded(simulator, j);

                            for (int k = 0; k < measurement.Length; k++)
                            {
                                Debug.WriteLine(numberOfArrays + "" + numberOfValues);

                                string text = measurement[k].ToString(null, cultureInfo);

                                foreach (char c in text)
                                {
                                    SimChar(simulator, c);
                                }

                                SimKey(simulator, VirtualKeyCode.TAB);
                                numberOfValues--;
                            }

                            if (j == 2 || !SameLineIlluminantModeData)
                            {
                                if (!SameLineAllData)
                                {
                                    PressReturn(simulator);
                                    if (IsExtra)
                                        PressReturnWithDelay(simulator, 1000);

                                }
                                else
                                {
                                    PressReturn(simulator);
                                }
                            }
                            if (isLastReading && !IsExtra)
                            {
                                // simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                            }

                            numberOfArrays--;
                        }
                    }
                });

            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message, "Error during measurement", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int HandleDensity(
            List<List<float[]>> listOfMeasurements,
            InputSimulator simulator,
            CultureInfo cultureInfo
        )
        {
            var densityList = listOfMeasurements[0];
            float[] densityArray = densityList[0];

            if (this.ShowStatusaux)
            {
                string statusDensitySafe = this.StatusDensityaux ?? string.Empty;

                SimText(simulator, statusDensitySafe);
                SimKey(simulator, VirtualKeyCode.TAB);
            }

            for (int i = 0; i < densityArray.Length; i++)
            {
                SimText(simulator, densityArray[i].ToString(null, cultureInfo));
                SimKey(simulator, VirtualKeyCode.TAB);
            }

            return 1;
        }

        private void SimText(InputSimulator simulator, string text)
        {
            simulator.Keyboard.TextEntry(text);
            System.Threading.Thread.Sleep(10);
        }

        private void SimChar(InputSimulator simulator, char c)
        {
            simulator.Keyboard.TextEntry(c);
            System.Threading.Thread.Sleep(10);
        }

        private void SimKey(InputSimulator simulator, VirtualKeyCode key)
        {
            simulator.Keyboard.KeyPress(key);
            System.Threading.Thread.Sleep(40);
        }

        private void SaveMeasurementsToFile(List<List<float[]>> listOfMeasurements)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(measurementFolderPath))
                    return;

                if (!Directory.Exists(measurementFolderPath))
                    return;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string deviceSerial = model.CurrentDevice?.SerialNumber ?? "Unknown";
                string filename = $"Measurements_{deviceSerial}_{timestamp}.txt";
                string filePath = Path.Combine(measurementFolderPath, filename);

                var cultureInfo = DecimalSeparatoraux == ','
                    ? new CultureInfo("pt-BR")
                    : CultureInfo.InvariantCulture;

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    if (IsHeaderOnFile)
                    {
                        writer.WriteLine("=== OGV COLOR CATCHER - MEASUREMENT DATA ===");
                        writer.WriteLine($"Device: {model.CurrentDevice?.Name ?? "Unknown"}");
                        writer.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine($"Decimal Separator: {(DecimalSeparatoraux == ',' ? "Comma (,)" : "Dot (.)")}");
                        writer.WriteLine($"Density Standard: {StatusDensityaux ?? "ANSIA"}");
                        writer.WriteLine();
                    }

                    for (int i = 0; i < listOfMeasurements.Count; i++)
                    {
                        var measurementAux = listOfMeasurements[i];

                        for (int j = 0; j < measurementAux.Count; j++)
                        {
                            if (j >= printOptions.Length)
                                break;

                            int measurementIndex = printOptions[j];

                            if (measurementIndex == 9)
                                continue;

                            if (measurementIndex < 0 || measurementIndex >= measurementAux.Count)
                                continue;

                            var measurement = measurementAux[measurementIndex];

                            if (IsHeaderOnFile)
                            {
                                if (i == 0 && IsDensity)
                                {
                                    writer.WriteLine("--- DENSITY ---");
                                }
                                else if (i == 1 && IsLab)
                                {
                                    writer.WriteLine("--- LAB DATA ---");
                                    writer.WriteLine("L\ta\tb\tIlluminant");
                                }
                                else if (i == 2 && IsSpectrum)
                                {
                                    writer.WriteLine("--- SPECTRUM DATA ---");
                                }
                            }

                            int illuminant = 0;

                            if (i == 1 && IsLab)
                            {
                                illuminant = j % 3;
                            }

                            for (int k = 0; k < measurement.Length; k++)
                            {
                                string text = measurement[k].ToString(null, cultureInfo);

                                writer.Write(text);

                                if (k < measurement.Length - 1)
                                    writer.Write("\t");
                            }

                            if (i == 1 && IsLab)
                            {
                                writer.Write($"\t{illuminant}");
                            }

                            writer.WriteLine();
                        }

                        writer.WriteLine();
                    }
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[SaveMeasurementsToFile] Arquivo salvo: {filePath}"
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SaveMeasurementsToFile] Erro: {ex}"
                );
            }
        }

        private void HandleMedia(List<List<float[]>> listOfMeasurements)
        {
            if (!isMedia)
                return;

            if (numberOfReadings >= 1)
            {
                this.mediaList.Add(listOfMeasurements);
                numberOfReadings--;
                UpdateHowManyReadings(numberOfReadings);
            }

            if (numberOfReadings < 1)
            {
                CalcularMedia(mediaList);
            }
        }

        private void UpdateHowManyReadings(int numberOfReadings)
        {
            //howManyReadings.Text = numberOfReadings.ToString();
            //if (numberOfReadings == 1)
            //{
            //    howManyReadings.Text = "";
            //}
        }

        public void CalcularMedia(List<List<List<float[]>>> mediaList)
        {
            List<List<float[]>> mediaFeita = new List<List<float[]>>();

            if (mediaList == null || mediaList.Count == 0)
            {
                Console.WriteLine("A lista está vazia.");
                return;
            }

            int numMedia = mediaList.Count;
            int numType = 0;
            int numIllu = 0;
            int numFloat = 0;

            for (int i = 0; i < mediaList[0].Count; i++)
            {
                mediaFeita.Add(new List<float[]>());
                for (int j = 0; j < mediaList[0][i].Count; j++)
                {
                    mediaFeita[i].Add(new float[mediaList[0][i][j].Length]);
                }
            }
            Console.WriteLine(mediaFeita);

            for (int i = 0; i < numMedia; i++)
            {
                numType = mediaList[i].Count;

                for (int j = 0; j < numType; j++)
                {
                    numIllu = mediaList[i][j].Count;

                    for (int k = 0; k < numIllu; k++)
                    {
                        float[] aux = mediaList[i][j][k];
                        numFloat = aux.Length;

                        for (int l = 0; l < numFloat; l++)
                        {
                            mediaFeita[j][k][l] += aux[l];
                        }
                    }
                }
            }
            Console.WriteLine(mediaFeita);
            for (int j = 0; j < mediaFeita.Count; j++)
            {
                for (int k = 0; k < mediaFeita[j].Count; k++)
                {
                    for (int l = 0; l < mediaFeita[j][k].Length; l++)
                    {
                        mediaFeita[j][k][l] /= numMedia;
                    }
                }
            }

            ImprimirMedia(mediaFeita);

            mediaList.Clear();
            mediaFeita.Clear();

            //mediaBox.Text = string.Empty;

            //numberOfReadings = Int32.Parse(mediaTeste.Text);
            UpdateHowManyReadings(numberOfReadings);
            //isMedia = false;
        }

        public void ImprimirMedia(List<List<float[]>> mediaFeita)
        {
            var simulator = new InputSimulator();

            var cultureInfo = DecimalSeparatoraux == ','
                ? new CultureInfo("pt-BR")
                : CultureInfo.InvariantCulture;

            int cont = 0;

            if (mediaFeita == null || mediaFeita.Count == 0)
            {
                Console.WriteLine("A lista de médias está vazia.");
                return;
            }

            bool isDensityCheck = this.IsDensity;

            if (isDensityCheck)
            {
                cont = HandleDensityMedia(mediaFeita, simulator, cultureInfo);

                if (!SameLineIlluminantModeData && !SameLineAllData)
                {
                    PressReturn(simulator);

                    if (this.IsExtra)
                        PressReturnWithDelay(simulator, 1000);
                }
            }

            for (int i = cont; i < mediaFeita.Count; i++)
            {
                var measurementAux = mediaFeita[i];
                int numberOfArrays = printOptions.Length - 1;

                bool isLastI = (i == mediaFeita.Count - 1);

                for (int j = 0; j < measurementAux.Count; j++)
                {
                    if (this.printOptions[j] == 9)
                    {
                        numberOfArrays--;
                        continue;
                    }

                    bool isLastValidJ = true;
                    for (int jj = j + 1; jj < measurementAux.Count; jj++)
                    {
                        if (this.printOptions[jj] != 9)
                        {
                            isLastValidJ = false;
                            break;
                        }
                    }

                    bool isLastReading = isLastI && isLastValidJ;

                    var measurement = measurementAux[this.printOptions[j]];

                    WriteIlluminantLabelIfNeeded(simulator, j);

                    for (int k = 0; k < measurement.Length; k++)
                    {
                        simulator.Keyboard.TextEntry(measurement[k].ToString(null, cultureInfo));
                        simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                    }

                    if (!SameLineIlluminantModeData && !SameLineAllData)
                    {
                        PressReturn(simulator);

                        if (this.IsExtra)
                            PressReturnWithDelay(simulator, 1000);
                    }
                    else if (SameLineIlluminantModeData && !SameLineAllData)
                    {
                        PressReturn(simulator);
                    }

                    if (isLastReading && !IsExtra)
                    {
                        // simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                    }

                    numberOfArrays--;
                }
            }

            if (SameLineIlluminantModeData)
            {
                System.Threading.Thread.Sleep(1000);
                PressReturn(simulator);

                if (IsExtra)
                    PressReturn(simulator);
            }
        }

        private int HandleDensityMedia(
            List<List<float[]>> mediaFeita,
            InputSimulator simulator,
            CultureInfo cultureInfo
        )
        {
            var densityList = mediaFeita[0];
            float[] densityArray = densityList[0];

            if (this.ShowStatusaux)
            {
                string statusDensitySafe = this.StatusDensityaux ?? string.Empty;
                simulator.Keyboard.TextEntry(statusDensitySafe);
                simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
            }

            for (int i = 0; i < densityArray.Length; i++)
            {
                simulator.Keyboard.TextEntry(densityArray[i].ToString(null, cultureInfo));
                simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
            }

            return 1;
        }
        private void PressReturn(InputSimulator simulator)
        {
            simulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
        }
        private void PressReturnWithDelay(InputSimulator simulator, int delayMs)
        {
            System.Threading.Thread.Sleep(delayMs);
            simulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
        }
        private void WriteIlluminantLabelIfNeeded(InputSimulator simulator, int j)
        {
            if ((this.IsLab || this.IsSpectrum) && this.ShowIlluminantMode)
            {
                simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.SHIFT, VirtualKeyCode.VK_M);
                System.Threading.Thread.Sleep(60);

                int digit = printOptions[j];
                if (digit >= 0 && digit <= 9)
                {
                    var key = (VirtualKeyCode)((int)VirtualKeyCode.VK_0 + digit);
                    simulator.Keyboard.KeyPress(key);
                    System.Threading.Thread.Sleep(60);
                }
                else
                {
                    SimText(simulator, digit.ToString().ToUpperInvariant());
                    System.Threading.Thread.Sleep(60);
                }

                System.Threading.Thread.Sleep(100);
                simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
            }
        }

        private async Task SelectFolder(TopLevel topLevel)
        {
            try
            {
                if (topLevel?.StorageProvider == null)
                    return;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions
                    {
                        Title = "Selecione a pasta para salvar as medições",
                        AllowMultiple = false
                    });

                if (folders.Count > 0)
                {
                    measurementFolderPath = folders[0].Path.LocalPath;

                    Debug.WriteLine(
                        $"[SelectFolder] Pasta selecionada: {measurementFolderPath}"
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"[SelectFolder] Erro: {ex}"
                );
            }
        }

        private void SaveDeviceSerial(object? parameter)
        {
            var input = parameter?.ToString();

            this.model.SearchDevices();

            if (!string.IsNullOrWhiteSpace(input))
            {

                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "aparelhos.txt");

                if (!File.Exists(filePath))
                {
                    using (FileStream fs = File.Create(filePath))
                    {
                        // Apenas cria o arquivo vazio
                    }
                }

                string encryptedInput = Encrypt(input);

                File.AppendAllText(filePath, encryptedInput + Environment.NewLine);

                //advancedOutput.Text = $"Aparelho cadastrado";
               // advancedOutput.Visibility = Visibility.Visible;

                //advancedInput.Clear();
            }
            else
            {
                //advancedOutput.Text = "Por favor, insira um valor.";
                //advancedOutput.Visibility = Visibility.Visible;
            }
        }

        private string Encrypt(string plainText)
        {
            byte[] key = Encoding.UTF8.GetBytes("12345678901234567890123456789012");
            byte[] iv = Encoding.UTF8.GetBytes("1234567890123456");

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                    }

                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        private bool DecryptFileContent()
        {

            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "aparelhos.txt");

            if (File.Exists(filePath))
            {
                string[] encryptedLines = File.ReadAllLines(filePath);

                //advancedOutput.Text = "";

                bool deviceFound = false;

                foreach (string encryptedLine in encryptedLines)
                {
                    if (!string.IsNullOrWhiteSpace(encryptedLine))
                    {
                        try
                        {
                            string decryptedLine = Decrypt(encryptedLine);

                            //advancedOutput.Text += decryptedLine + Environment.NewLine;
                            Console.WriteLine(decryptedLine);

                            if (decryptedLine == model.CurrentDevice.SerialNumber)
                            {
                                Console.WriteLine("SUCESSO");
                                IsCalibrateEnabled = true;
                                deviceFound = true;
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            //advancedOutput.Text += $"Erro ao decriptar: {ex.Message}" + Environment.NewLine;
                        }

                        
                    }
                }

                if (!deviceFound)
                {
                    Console.WriteLine("FALHA");
                    //MessageBox.Show("Aparelho não cadastrado", "Cadastre o aparelho", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                //advancedOutput.Visibility = Visibility.Visible;
            }
            else
            {
                //MessageBox.Show("Aparelho não cadastrado", "Cadastre o aparelho", MessageBoxButton.OK, MessageBoxImage.Error);
                //advancedOutput.Text = "Arquivo não encontrado.";
                //advancedOutput.Visibility = Visibility.Visible;
            }

            return false;
        }

        private string Decrypt(string cipherText)
        {
            byte[] key = Encoding.UTF8.GetBytes("12345678901234567890123456789012"); 
            byte[] iv = Encoding.UTF8.GetBytes("1234567890123456"); 

            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(cipherBytes))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }
        private const string BaseUrl = "https://licensing.ogvcolor.cloud/";

        private void SubmitActivation(object? parameter)
        {
            try
            {
                string licenseKey = parameter?.ToString()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    return;
                }

                //System.Windows.Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                string fingerprint = FingerPrintService.ComputeFingerprint();
                string responseJson;
                try
                {
                    responseJson = LicensingApi.Activate(BaseUrl, licenseKey, fingerprint);
                }
                catch (Exception ex)
                {
                    SetShowError(ex.Message);
                    return;
                }

                LicenseEnvelope envelope;
                LicensePayload payload;
                try
                {
                    envelope = JsonConvert.DeserializeObject<LicenseEnvelope>(responseJson);
                    if (envelope == null || string.IsNullOrWhiteSpace(envelope.payload) || string.IsNullOrWhiteSpace(envelope.signature))
                    {
                        ShowError = "Resposta incompleta do servidor";
                        throw new Exception("Resposta incompleta do servidor");
                    }

                    payload = JsonConvert.DeserializeObject<LicensePayload>(envelope.payload);
                    if (payload == null)
                    {
                        ShowError = "Payload inválido";
                        throw new Exception("Payload inválido");
                    }
                }
                catch (Exception ex)
                {
                    SetShowError(ex.Message);
                    return;
                }

                string localReason;
                bool validLocal = LicenseValidator.IsValidLicense(responseJson, out localReason);
                if (!validLocal)
                {
                    //ShowError = localReason;
                    return;
                }

                string onlineReason;
                DateTime? serverUtc;
                bool validOnline = LicensingApi.ValidateOnlineWithServer(
                    BaseUrl,
                    payload.licenseKeyId,
                    fingerprint,
                    out onlineReason,
                    out serverUtc
                );

                if (!validOnline)
                {
                    return;
                }

                if (serverUtc.HasValue)
                {
                    LicenseClockGuard.UpdateFromServerUtc(serverUtc.Value);
                }

                try
                {
                    LicenseStore.Save(responseJson);
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    SetShowError(uaEx.Message);
                    return;
                }
                catch (Exception ex)
                {
                    SetShowError(ex.Message);
                    return;
                }

                //System.Windows.Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                System.Threading.Thread.Sleep(1500);

                var main = new MainWindow(new I1SharpModel());
                main.Show();

                ActivationWindow?.Close();
            }
            catch (Exception ex)
            {
                SetShowError(ex.Message);
            }
        }

        private void SetShowError(string message)
        {
            try
            {
                int jsonStart = message.IndexOf('{');

                if (jsonStart >= 0)
                {
                    string json = message.Substring(jsonStart);

                    var error = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                    if (error != null &&
                        error.TryGetValue("detail", out string? detail) &&
                        !string.IsNullOrWhiteSpace(detail))
                    {
                        ShowError = detail;
                        return;
                    }
                }

                ShowError = message;
            }
            catch
            {
                ShowError = message;
            }
        }
    }
}
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using OGVColorCatcher.Models;
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
using System.Text;
using System.Windows.Input;
using WindowsInput;
using WindowsInput.Native;

namespace OGVColorCatcher.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        //Variáveis

        //Configurações de leitura
        private bool _isDensity;
        private bool _isLab;
        private bool _isSpectrum;
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

        //Configurações de escrita
        private char decimalSeparatoraux = '.';
        private bool _showIlluminant = true;
        private bool _showSpectrumIlluminant = false;
        public bool ShowIlluminant
        {
            get => _showIlluminant;
            set
            {
                if (this.RaiseAndSetIfChanged(ref _showIlluminant, value))
                {
                    this.RaiseAndSetIfChanged(ref _showSpectrumIlluminant, !value);
                }
            }
        }
        //public bool ShowSpectrumIlluminant
        //{
        //    get => _showSpectrumIlluminant;
        //    set
        //    {
        //        if (this.RaiseAndSetIfChanged(ref _showSpectrumIlluminant, value))
        //        {
        //            this.RaiseAndSetIfChanged(ref _showLabIlluminant, !value);
        //        }
        //    }
        //}
        private int passwordSum;
        private I1SharpModel model;
        bool shouldTriggerMeasurements = false;
        private bool _isExtra;
        private bool saveToFile = false;
        private string measurementFolderPath = string.Empty;  // Pasta selecionada pelo usuário

        public bool isHeaderOnFile = false;
        private bool isShowMeasurement = true;
        bool isMedia = false;
        int numberOfReadings;
        List<List<List<float[]>>> mediaList = new List<List<List<float[]>>>();
        private bool shouldTriggerMeasurementsaux = false;
        private bool isForceTab = false;
        private int[] printOptions = new int[3];
        private bool showStatusaux = true;


        public I1Pro64 _currentDevice;

        public I1Pro64 CurrentDevice
        {
            get => _currentDevice;
            set
            {
                this.RaiseAndSetIfChanged(ref _currentDevice, value);
                model.CurrentDevice = value; // sincroniza com o seu model
            }
        }

        public bool IsExtra
        {
            get => _isExtra;
            set => this.RaiseAndSetIfChanged(ref _isExtra, value);
        }



        public ObservableCollection<I1Pro64> Devices => model.Devices;

        public ReactiveCommand<Unit, Unit> CalibrateCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseDeviceCommand { get; }
        public ICommand SubmitPasswordCommand { get; }

        //Construtor
        public MainWindowViewModel(I1SharpModel model)
        {
            this.model = model;
            this.model.DeviceChanged += model_DeviceChanged;

            var scheduler = AvaloniaScheduler.Instance;

            CalibrateCommand = ReactiveCommand.Create(CalibrateDevice, outputScheduler: scheduler);
            CloseDeviceCommand = ReactiveCommand.Create(CloseDevice, outputScheduler: scheduler);
            SubmitPasswordCommand = new RelayCommand<object?>(SubmitPassword);

            DateTime currentDate = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"));
            int daySum = currentDate.Day.ToString().Sum(c => int.Parse(c.ToString()));
            int monthSum = currentDate.Month.ToString().Sum(c => int.Parse(c.ToString()));
            int yearSum = currentDate.Year.ToString().Sum(c => int.Parse(c.ToString()));
            string password = daySum.ToString() + monthSum.ToString() + yearSum.ToString();
            passwordSum = int.Parse(password);

            //InitializeTimer();
        }

        
        void model_DeviceChanged(I1Pro64 device)
        {
            if (device != null)
            {
                device.ButtonPressed += device_ButtonPressed;
                //DecryptFileContent();
                //I1Pro64_LogEvent(I1Pro64.LogType.eNormal, "Device changed - Active device: " + device.Name);
            }
            else
            {
                //I1Pro64_LogEvent(I1Pro64.LogType.eNormal, "Device changed - No active device");
            }
            //updateDeviceFeatureList();
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

        private string statusDensityaux;

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
                    statusDensityaux
                );

                Dispatcher.UIThread.InvokeAsync(() => 
                {
                    if (saveToFile)
                    {
                        SaveMeasurementsToFile(listOfMeasurements);
                        return;
                    }

                    var simulator = new InputSimulator();

                    var cultureInfo = decimalSeparatoraux == ','
                        ? new CultureInfo("pt-BR")
                        : CultureInfo.InvariantCulture;

                    if (!isShowMeasurement && isMedia)
                    {
                        HandleMedia(listOfMeasurements);
                        //SystemSounds.Beep.Play();
                        return;
                    }

                });

                //Debug.WriteLine(shouldTriggerMeasurements);

                //bool isDensityCheck = this.isDensity;
                //int cont = 0;

                //var listOfMeasurements = model.CurrentDevice.TriggerMeasurementNew(
                //    this.isDensity,
                //    this.isLab,
                //    this.isSpectrum,
                //    statusDensityaux
                //);

                //this.Dispatcher.BeginInvoke((Action)(() =>
                //{
                //    // ✅ NOVO: Verificar se está em modo "salvar em arquivo"
                //    if (saveToFile)
                //    {
                //        // Salvar em arquivo TXT em vez de usar teclado
                //        SaveMeasurementsToFile(listOfMeasurements);
                //        SystemSounds.Beep.Play();
                //        return;
                //    }

                //    // ✅ CÓDIGO ORIGINAL: Usar teclado
                //    var simulator = new InputSimulator();

                //    var cultureInfo = decimalSeparatoraux == ','
                //        ? new CultureInfo("pt-BR")
                //        : CultureInfo.InvariantCulture;

                //    if (!isShowMeasurement && isMedia)
                //    {
                //        HandleMedia(listOfMeasurements);
                //        SystemSounds.Beep.Play();
                //        return;
                //    }

                //    if (isDensityCheck)
                //    {
                //        cont = HandleDensity(listOfMeasurements, simulator, cultureInfo);

                //        if (!shouldTriggerMeasurementsaux && !isForceTab)
                //        {
                //            PressReturn(simulator);

                //            if (this.isExtra)
                //                PressReturnWithDelay(simulator, 1000);
                //        }
                //    }

                //    for (int i = cont; i < listOfMeasurements.Count; i++)
                //    {
                //        var measurementAux = listOfMeasurements[i];
                //        int numberOfArrays = printOptions.Length - 1;

                //        bool isLastI = (i == listOfMeasurements.Count - 1);

                //        for (int j = 0; j < measurementAux.Count; j++)
                //        {
                //            if (this.printOptions[j] == 9)
                //            {
                //                numberOfArrays--;
                //                continue;
                //            }

                //            bool isLastValidJ = true;
                //            for (int jj = j + 1; jj < measurementAux.Count; jj++)
                //            {
                //                if (this.printOptions[jj] != 9)
                //                {
                //                    isLastValidJ = false;
                //                    break;
                //                }
                //            }

                //            bool isLastReading = isLastI && isLastValidJ;

                //            var measurement = measurementAux[this.printOptions[j]];
                //            int numberOfValues = measurement.Length - 1;

                //            WriteIlluminantLabelIfNeeded(simulator, j);

                //            for (int k = 0; k < measurement.Length; k++)
                //            {
                //                Debug.WriteLine(numberOfArrays + "" + numberOfValues);

                //                simulator.Keyboard.TextEntry(measurement[k].ToString(null, cultureInfo));

                //                simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                //                numberOfValues--;
                //            }

                //            if (!shouldTriggerMeasurementsaux && !isForceTab)
                //            {
                //                PressReturn(simulator);

                //                if (this.isExtra)
                //                    PressReturnWithDelay(simulator, 1000);
                //            }
                //            else if (shouldTriggerMeasurementsaux && !isForceTab)
                //            {
                //                PressReturn(simulator);
                //            }

                //            if (isLastReading && !isExtra)
                //            {
                //                // simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                //            }

                //            numberOfArrays--;
                //        }
                //    }

                //    if (shouldTriggerMeasurementsaux)
                //    {
                //        System.Threading.Thread.Sleep(1000);
                //        PressReturn(simulator);

                //        if (isExtra)
                //            PressReturn(simulator);
                //    }

                //    HandleMedia(listOfMeasurements);
                //    SystemSounds.Beep.Play();
                //}));
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message, "Error during measurement", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveMeasurementsToFile(List<List<float[]>> listOfMeasurements)
        {
            try
            {
                // Validações iniciais...
                if (string.IsNullOrWhiteSpace(measurementFolderPath))
                {
                    //MessageBox.Show(
                    //    "Por favor, selecione uma pasta antes de medir!",
                    //    "Aviso",
                    //    MessageBoxButton.OK,
                    //    MessageBoxImage.Warning
                    //);
                    return;
                }

                if (!Directory.Exists(measurementFolderPath))
                {
                    //MessageBox.Show(
                    //    $"A pasta selecionada não existe:\n{measurementFolderPath}",
                    //    "Erro",
                    //    MessageBoxButton.OK,
                    //    MessageBoxImage.Error
                    //);
                    return;
                }

                // ✅ NOVO: Calcular índices dinamicamente
                int densityIndex = -1;
                int labIndex = -1;
                int spectrumIndex = -1;
                int currentIndex = 0;

                if (IsDensity && currentIndex < listOfMeasurements.Count)
                {
                    densityIndex = currentIndex++;
                }

                if (IsLab && currentIndex < listOfMeasurements.Count)
                {
                    labIndex = currentIndex++;
                }

                if (IsSpectrum && currentIndex < listOfMeasurements.Count)
                {
                    spectrumIndex = currentIndex++;
                }

                // Criar nome do arquivo com timestamp
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string deviceSerial = model.CurrentDevice?.SerialNumber ?? "Unknown";
                string filename = $"Measurements_{deviceSerial}_{timestamp}.txt";
                string filePath = Path.Combine(measurementFolderPath, filename);

                var cultureInfo = decimalSeparatoraux == ','
                    ? new CultureInfo("pt-BR")
                    : CultureInfo.InvariantCulture;

                // Escrever dados no arquivo
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    if (isHeaderOnFile)
                    {
                        // Cabeçalho
                        writer.WriteLine("=== OGV COLOR CATCHER - MEASUREMENT DATA ===");
                        writer.WriteLine($"Device: {model.CurrentDevice?.Name ?? "Unknown"}");
                        writer.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine($"Decimal Separator: {(decimalSeparatoraux == ',' ? "Comma (,)" : "Dot (.)")}");
                        writer.WriteLine($"Density Standard: {statusDensityaux ?? "ANSIA"}");
                        writer.WriteLine();
                    }

                    // ✅ Densidade (usando índice dinâmico)
                    if (IsDensity && densityIndex >= 0 && densityIndex < listOfMeasurements.Count)
                    {
                        if (isHeaderOnFile)
                        {
                            writer.WriteLine("--- DENSITY ---");
                        }

                        var densityList = listOfMeasurements[densityIndex];
                        foreach (var density in densityList)
                        {
                            for (int i = 0; i < density.Length; i++)
                            {
                                writer.Write(density[i].ToString(null, cultureInfo));
                                if (i < density.Length - 1) writer.Write("\t");
                            }
                            writer.WriteLine();
                        }
                        writer.WriteLine();
                    }

                    // ✅ LAB (usando índice dinâmico)
                    if (IsLab && labIndex >= 0 && labIndex < listOfMeasurements.Count)
                    {
                        if (isHeaderOnFile)
                        {
                            writer.WriteLine("--- LAB DATA ---");
                            writer.WriteLine("L\ta\tb\tIlluminant");
                        }

                        var labList = listOfMeasurements[labIndex];
                        int illuminant = 0;
                        foreach (var labData in labList)
                        {
                            for (int i = 0; i < labData.Length; i++)
                            {
                                writer.Write(labData[i].ToString(null, cultureInfo));
                                if (i < labData.Length - 1) writer.Write("\t");
                            }
                            writer.Write($"\t{illuminant}");
                            writer.WriteLine();

                            if ((illuminant + 1) % 3 == 0) illuminant = 0;
                            else illuminant++;
                        }
                        writer.WriteLine();
                    }

                    // ✅ Spectrum (usando índice dinâmico)
                    if (IsSpectrum && spectrumIndex >= 0 && spectrumIndex < listOfMeasurements.Count)
                    {
                        if (isHeaderOnFile)
                        {
                            writer.WriteLine("--- SPECTRUM DATA ---");
                            writer.WriteLine();

                            // Cabeçalho com wavelengths
                            writer.Write("Wavelength (nm):\t");
                        }

                        for (int i = 0; i < 36; i++)
                        {
                            writer.Write((380 + i * 10).ToString("D4"));
                            if (i < 35) writer.Write("\t");
                        }
                        writer.WriteLine();
                        writer.WriteLine();

                        // Dados
                        var spectrumList = listOfMeasurements[spectrumIndex];
                        int dataIndex = 1;
                        foreach (var spectrum in spectrumList)
                        {
                            writer.Write($"Sample {dataIndex}:\t");
                            for (int i = 0; i < spectrum.Length; i++)
                            {
                                writer.Write(spectrum[i].ToString("F6", cultureInfo));
                                if (i < spectrum.Length - 1) writer.Write("\t");
                            }
                            writer.WriteLine();
                            dataIndex++;
                        }
                    }
                }

                // Mostrar confirmação
                //saveFileStatus.Text = $"✓ Salvo em:\n{filePath}";
                //saveFileStatus.Foreground = Brushes.Green;

                System.Diagnostics.Debug.WriteLine($"[SaveMeasurementsToFile] Arquivo salvo: {filePath}");

                //MessageBox.Show(
                //    $"Medição salva com sucesso!\n\nArquivo:\n{Path.GetFileName(filePath)}\n\nPasta:\n{measurementFolderPath}",
                //    "Sucesso",
                //    MessageBoxButton.OK,
                //    MessageBoxImage.Information
                //);
            }
            catch (Exception ex)
            {
                //saveFileStatus.Text = $"✗ Erro ao salvar";
                //saveFileStatus.Foreground = Brushes.Red;

                //MessageBox.Show(
                //    $"Erro ao salvar medição:\n{ex.Message}",
                //    "Erro",
                //    MessageBoxButton.OK,
                //    MessageBoxImage.Error
                //);

                System.Diagnostics.Debug.WriteLine($"[SaveMeasurementsToFile] Erro: {ex.StackTrace}");
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

            // Inicializa a estrutura de mediaFeita para armazenar os resultados
            for (int i = 0; i < mediaList[0].Count; i++)
            {
                mediaFeita.Add(new List<float[]>());
                for (int j = 0; j < mediaList[0][i].Count; j++)
                {
                    // Inicializa o array com o tamanho correto
                    mediaFeita[i].Add(new float[mediaList[0][i][j].Length]);
                }
            }
            Console.WriteLine(mediaFeita);

            // Primeiro FOR: Leitura das médias
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
                            // Soma os valores para calcular a média posteriormente
                            mediaFeita[j][k][l] += aux[l];
                        }
                    }
                }
            }
            Console.WriteLine(mediaFeita);
            // Calcula a média dividindo os valores somados pelo número de medições
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

            // Imprime a média
            ImprimirMedia(mediaFeita);

            // Limpa a lista de medições
            mediaList.Clear();
            mediaFeita.Clear();

            // Limpa a caixa de entrada de média
            //mediaBox.Text = string.Empty;

            // Reseta o estado de leituras e da flag de média
            //numberOfReadings = Int32.Parse(mediaTeste.Text);
            UpdateHowManyReadings(numberOfReadings);
            //isMedia = false;
        }

        public void ImprimirMedia(List<List<float[]>> mediaFeita)
        {
            var simulator = new InputSimulator();

            var cultureInfo = decimalSeparatoraux == ','
                ? new CultureInfo("pt-BR")
                : CultureInfo.InvariantCulture;

            int cont = 0;

            if (mediaFeita == null || mediaFeita.Count == 0)
            {
                Console.WriteLine("A lista de médias está vazia.");
                return;
            }

            bool isDensityCheck = this.IsDensity;

            // ✅ Densidade primeiro, igual ao fluxo do device_ButtonPressed
            if (isDensityCheck)
            {
                cont = HandleDensityMedia(mediaFeita, simulator, cultureInfo);

                // mesma regra: só confirma linha se NÃO for trigger e NÃO for forceTab
                if (!shouldTriggerMeasurementsaux && !isForceTab)
                {
                    PressReturn(simulator);

                    if (this.IsExtra)
                        PressReturnWithDelay(simulator, 1000);
                }
            }

            // ✅ Agora imprime o resto (Lab/Spectrum etc) seguindo a mesma lógica do device_ButtonPressed
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

                    // ✅ Descobre se este "j" é o último válido (printOptions[j] != 9)
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

                    // ✅ Regra de ENTER igual ao device_ButtonPressed
                    if (!shouldTriggerMeasurementsaux && !isForceTab)
                    {
                        PressReturn(simulator);

                        if (this.IsExtra)
                            PressReturnWithDelay(simulator, 1000);
                    }
                    else if (shouldTriggerMeasurementsaux && !isForceTab)
                    {
                        // no modo trigger, confirma a linha de cada leitura (Sheets precisa disso)
                        PressReturn(simulator);
                    }

                    // ✅ TAB EXTRA somente após a última leitura do for (se quiser reativar)
                    if (isLastReading && !IsExtra)
                    {
                        // simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                    }

                    numberOfArrays--;
                }
            }

            // ✅ Finalização no modo trigger (igual ao device_ButtonPressed)
            if (shouldTriggerMeasurementsaux)
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

            if (this.showStatusaux)
            {
                string statusDensitySafe = this.statusDensityaux ?? string.Empty;
                simulator.Keyboard.TextEntry(statusDensitySafe);
                simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
            }

            for (int i = 0; i < densityArray.Length; i++)
            {
                simulator.Keyboard.TextEntry(densityArray[i].ToString(null, cultureInfo));
                simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
            }

            // densidade ocupa o "primeiro bloco", então o resto começa em 1
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
            if (this.IsLab && this.ShowIlluminant)
            {
                simulator.Keyboard.TextEntry("M" + printOptions[j]);
                simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                return;
            }

            if (this.IsSpectrum && this.ShowIlluminant)
            {
                simulator.Keyboard.TextEntry("M" + printOptions[j]);
                simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
            }
        }

        //    //-----------teste-------
        //    //public ObservableCollection<ISeries> DotAreaSeries { get; } = new();

        //    //    private static ISeries BuildLine(SKColor color, IEnumerable<ObservablePoint> pts) // ⬅
        //    //=> new LineSeries<ObservablePoint>
        //    //{
        //    //    Values = pts.ToList(),
        //    //    Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
        //    //    GeometrySize = 6,
        //    //    GeometryFill = new SolidColorPaint(color),
        //    //    AnimationsSpeed = TimeSpan.Zero // ⬅ evita repinturas caras no reset
        //    //};

        //    //private void UpdateDotGainChart()
        //    //{
        //    //    PostUiWorkCoalesced(() =>
        //    //    {
        //    //        if (ChannelNum == null || ChannelNum.Count == 0)
        //    //        {
        //    //            ReplaceCollection(ref _dotAreaSeries, null, nameof(DotAreaSeries));
        //    //            return;
        //    //        }

        //    //        var xs = new double[] { 0, 25, 50, 75, 100 };
        //    //        var pts = new List<ObservablePoint>();

        //    //        // Para cada leitura acumulada em ChannelNum
        //    //        for (int i = 0; i < ChannelNum.Count && i < xs.Length; i++)
        //    //        {
        //    //            double x = xs[i];
        //    //            float val = ChannelNum[i];

        //    //            // Zera nos extremos como antes
        //    //            double y = (x == 0 || x == 100) ? 0d : val;

        //    //            pts.Add(new ObservablePoint(x, y));
        //    //        }

        //    //        // Garante o primeiro e último ponto
        //    //        if (pts.Count == 0 || pts[0].X != 0)
        //    //            pts.Insert(0, new ObservablePoint(0, 0));

        //    //        if (pts[^1].X != 100)
        //    //            pts.Add(new ObservablePoint(100, 0));

        //    //        var newSeries = new List<ISeries>
        //    //{
        //    //    BuildLine(new SKColor(0, 0, 0), pts)
        //    //};

        //    //        ReplaceCollection(ref _dotAreaSeries, newSeries, nameof(DotAreaSeries));
        //    //    });
        //    //}



        //    //-----------------------
        //    //private I1SharpModel model;
        //    //private readonly MeasurementService _mss;
        //    //private readonly EquationService _eqs;
        //    //private readonly ColorService _cls;
        //    //private readonly SetupService _sts;
        //    //private int _selectedTab;
        //    ////private List<float> _channelNum;
        //    //List<float[]> measurements;
        //    //List<float[]> densities;
        //    //List<float[]> _densitiesValue;
        //    //List<float[]> _densitiesValueSample;
        //    //List<float> _densitiesTSample;
        //    //List<float[]> _densitiesValueDiff;
        //    //private ObservableCollection<List<float[]>> _dotGainValues;
        //    //List<float[]> rgbValues;
        //    //List<float[]> labValues;
        //    //List<List<float[]>> rgbColors;
        //    //List<float[]> dot100;
        //    //List<float[]> dotCn;
        //    //public bool _readSubstrate = false;
        //    //List<float> _trappingValue;
        //    //private const int MaxAllDensityReads = 5;
        //    //public string _showMessage;
        //    //private bool _enableSubstrateButton = false;
        //    //private readonly RibbonColorService _rcs;
        //    //private bool isSubstrateRead = false;
        //    //private string _paperSelected;
        //    //private string _statusSelected;
        //    //private int _decimais;
        //    //private int _decimaisPorcentagem;

        //    private I1SharpModel model;

        //    private int passwordSum;

        //    bool shouldTriggerMeasurements = false;
        //    bool isDensity;
        //    bool isLab;
        //    bool isSpectrum;
        //    bool isExtra;
        //    public I1Pro64 _currentDevice;

        //    public MainWindowViewModel(I1SharpModel model)
        //    {
        //        this.model = model;

        //        //_eqs = new EquationService();
        //        //_mss = new MeasurementService();
        //        //_cls = new ColorService();
        //        //_rcs = new RibbonColorService();
        //        //_sts = new SetupService(model);

        //        //rgbColors = new List<List<float[]>>();
        //        //rgbValues = new List<float[]>
        //        //{
        //        //    new float[] { 200, 200, 200 }, // White
        //        //};
        //        //Dispatcher.UIThread.Invoke(() => ReplaceRibbonRow(rgbValues));
        //        //Dispatcher.UIThread.Invoke(() => ReplaceRibbonRow2(rgbValues));
        //        //Dispatcher.UIThread.Invoke(() => ReplaceRibbonRow3(rgbValues));
        //        //Dispatcher.UIThread.Invoke(() => ReplaceRibbonTrapping(rgbValues));

        //        //ShowMessage = "";

        //        //DotGainValues = new ObservableCollection<List<float[]>>();
        //        //DotGainValues.CollectionChanged += DotGainValues_CollectionChanged;
        //        ////ChannelNum = new List<float>();
        //        //ChannelNum = new ObservableCollection<float>();

        //        var scheduler = AvaloniaScheduler.Instance;

        //        CalibrateCommand = ReactiveCommand.Create(CalibrateDevice, outputScheduler: scheduler);
        //        CloseDeviceCommand = ReactiveCommand.Create(CloseDevice, outputScheduler: scheduler);

        //        this.model.DeviceChanged += model_DeviceChanged;

        //        SubmitPasswordCommand = new RelayCommand<object?>(SubmitPassword);

        //        //modeSelected = modeSelectedType.allDensity; // Default mode

        //        //if (PaperSelected == null)
        //        //{
        //        //    PaperSelected = "Absolute";
        //        //}
        //        //if (StatusSelected == null)
        //        //{
        //        //    StatusSelected = "ANSIT";
        //        //}

        //        //ChannelColors = new ObservableCollection<string>
        //        //{
        //        //   "Gray"
        //        //};

        //        DateTime currentDate = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"));

        //        int daySum = currentDate.Day.ToString().Sum(c => int.Parse(c.ToString()));
        //        int monthSum = currentDate.Month.ToString().Sum(c => int.Parse(c.ToString()));
        //        int yearSum = currentDate.Year.ToString().Sum(c => int.Parse(c.ToString()));
        //        string password = daySum.ToString() + monthSum.ToString() + yearSum.ToString();
        //        passwordSum = int.Parse(password);
        //        Debug.WriteLine(passwordSum);

        //        //var scheduler = DispatcherScheduler.Current;

        //        InitializeTimer();

        //    }


        //    //-----------------------------------------------------
        //    public ObservableCollection<string> PaperSetup { get; } =
        //        new ObservableCollection<string>
        //        {
        //            "Absolute",
        //            "Paper White"
        //        };

        //    public ObservableCollection<string> StatusSetup { get; } =
        //        new ObservableCollection<string>
        //        {
        //            "ANSIT",
        //            "ANSIA",
        //            "DIN",
        //            "SIP",
        //            "DINNB" 
        //            //"ANSIE",
        //            //"ANSII",
        //        };

        //    string PaperSetupStr = "";
        //    string StatusSetupStr = "";
        //    //public string? PaperSelected
        //    //{
        //    //    get => _paperSelected;
        //    //    set
        //    //    {
        //    //        this.RaiseAndSetIfChanged(ref _paperSelected, value);

        //    //        PaperSetupStr = value ?? "";

        //    //        var isAbsolute = string.Equals(value, "Absolute", StringComparison.Ordinal);

        //    //        ReadSubstrate = !isAbsolute;
        //    //        EnableSubstrateButton = !isAbsolute;
        //    //        ShowMessage = isAbsolute ? string.Empty : "Leia o substrato";
        //    //    }
        //    //}

        //    //public string? StatusSelected
        //    //{
        //    //    get => _statusSelected;
        //    //    set
        //    //    {
        //    //        this.RaiseAndSetIfChanged(ref _statusSelected, value);

        //    //        StatusSetupStr = value ?? "";

        //    //    }
        //    //}

        //    //public int Decimais
        //    //{
        //    //    get => _decimais;
        //    //    set
        //    //    {
        //    //        this.RaiseAndSetIfChanged(ref _decimais, value);
        //    //        _mss.SetDecimals(value);
        //    //    }
        //    //}
        //    //public int DecimaisPorcentagem
        //    //{
        //    //    get => _decimaisPorcentagem;
        //    //    set
        //    //    {
        //    //        this.RaiseAndSetIfChanged(ref _decimaisPorcentagem, value);
        //    //        _mss.SetDecimalsPorcentagem(value);
        //    //    }
        //    //}

        //    //public bool EnableSubstrateButton
        //    //{
        //    //    get => _enableSubstrateButton;
        //    //    set => this.RaiseAndSetIfChanged(ref _enableSubstrateButton, value);
        //    //}
        //    //public string ShowMessage
        //    //{
        //    //    get => _showMessage;
        //    //    set => this.RaiseAndSetIfChanged(ref _showMessage, value);
        //    //}

        //    private ObservableCollection<string> _channelColors;
        //    public ObservableCollection<string> ChannelColors
        //    {
        //        get => _channelColors;
        //        set => this.RaiseAndSetIfChanged(ref _channelColors, value);
        //    }

        //    public ReactiveCommand<Unit, Unit> CalibrateCommand { get; }
        //    public ObservableCollection<I1Pro64> Devices => model.Devices;

        //    private ObservableCollection<float> _channelNum = new();
        //    public ObservableCollection<float> ChannelNum
        //    {
        //        get => _channelNum;
        //        set => this.RaiseAndSetIfChanged(ref _channelNum, value);
        //    }

        //    //public modeSelectedType _modeSelected;
        //    //public modeSelectedType modeSelected
        //    //{
        //    //    get => _modeSelected;
        //    //    set => this.RaiseAndSetIfChanged(ref _modeSelected, value);
        //    //}

        //    // =======================
        //    // BACKINGS ORIGINAIS
        //    // =======================
        //    //public List<float[]> DensitiesValue
        //    //{
        //    //    get => _densitiesValue;
        //    //    set
        //    //    {
        //    //        this.RaiseAndSetIfChanged(ref _densitiesValue, value);
        //    //        // garante refresh das "Views" no XAML
        //    //        this.RaisePropertyChanged(nameof(DensitiesValueView));
        //    //    }
        //    //}

        //    //public List<float> TrappingValue
        //    //{
        //    //    get => _trappingValue;
        //    //    set
        //    //    {
        //    //        this.RaiseAndSetIfChanged(ref _trappingValue, value);
        //    //        // garante refresh das "Views" no XAML
        //    //        this.RaisePropertyChanged(nameof(TrappingValueView));
        //    //    }
        //    //}

        //    //public List<float[]> DensitiesValueSample
        //    //{
        //    //    get => _densitiesValueSample;
        //    //    set
        //    //    {
        //    //        this.RaiseAndSetIfChanged(ref _densitiesValueSample, value);
        //    //        this.RaisePropertyChanged(nameof(DensitiesValueSampleView));
        //    //    }
        //    //}

        //    //public List<float> DensitiesTSample
        //    //{
        //    //    get => _densitiesTSample;
        //    //    set
        //    //    {
        //    //        this.RaiseAndSetIfChanged(ref _densitiesTSample, value);
        //    //        this.RaisePropertyChanged(nameof(DensitiesTSampleView));
        //    //    }
        //    //}

        //    //public List<float[]> DensitiesValueDiff
        //    //{
        //    //    get => _densitiesValueDiff;
        //    //    set
        //    //    {
        //    //        this.RaiseAndSetIfChanged(ref _densitiesValueDiff, value);
        //    //        this.RaisePropertyChanged(nameof(DensitiesValueDiffView));
        //    //    }
        //    //}
        //    //public bool ReadSubstrate
        //    //{
        //    //    get => _readSubstrate;
        //    //    set => this.RaiseAndSetIfChanged(ref _readSubstrate, value);
        //    //}
        //    //public ObservableCollection<List<float[]>> DotGainValues
        //    //{
        //    //    get => _dotGainValues;
        //    //    set
        //    //    {
        //    //        // desinscreve antigo
        //    //        if (_dotGainValues != null)
        //    //            _dotGainValues.CollectionChanged -= DotGainValues_CollectionChanged;

        //    //        this.RaiseAndSetIfChanged(ref _dotGainValues, value);

        //    //        // inscreve novo e força refresh da View
        //    //        if (_dotGainValues != null)
        //    //            _dotGainValues.CollectionChanged += DotGainValues_CollectionChanged;

        //    //        this.RaisePropertyChanged(nameof(DotGainValuesView));
        //    //    }
        //    //}


        //    // =======================
        //    // PROPRIEDADES *VIEW* (somente List) — MINIMALISTAS
        //    // =======================
        //    // Observação: são computadas "on the fly"; não alteram sua lógica.
        //    //public List<List<float>> DensitiesValueView =>
        //    //    DensitiesValue?.Select(a => a?.ToList() ?? new List<float>()).ToList() ?? new();

        //    //public List<float> TrappingValueView =>
        //    //    TrappingValue?.ToList() ?? new List<float>();


        //    //public List<List<float>> DensitiesValueSampleView =>
        //    //    DensitiesValueSample?.Select(a => a?.ToList() ?? new List<float>()).ToList() ?? new();

        //    //public List<float> DensitiesTSampleView =>
        //    //    DensitiesTSample?.ToList() ?? new();

        //    //public List<List<float>> DensitiesValueDiffView =>
        //    //    DensitiesValueDiff?.Select(a => a?.ToList() ?? new List<float>()).ToList() ?? new();

        //    // Converte ObservableCollection<List<float[]>> -> ObservableCollection<List<List<float>>>
        //    // para o XAML poder indexar sem erro.
        //    private ObservableCollection<List<List<float>>> _dotGainValuesView = new();
        //    public ObservableCollection<List<List<float>>> DotGainValuesView => _dotGainValuesView;



        //    //private void DotGainValues_CollectionChanged(object? s, NotifyCollectionChangedEventArgs e)
        //    //{
        //    //    if (IsSuspended) return;

        //    //    switch (e.Action)
        //    //    {
        //    //        case NotifyCollectionChangedAction.Reset:
        //    //            RebuildDotGainValuesView();
        //    //            break;
        //    //        case NotifyCollectionChangedAction.Add:
        //    //            foreach (List<float[]>? added in e.NewItems!)
        //    //                _dotGainValuesView.Add(added?.Select(a => a?.ToList() ?? new()).ToList());
        //    //            break;
        //    //        case NotifyCollectionChangedAction.Remove:
        //    //            // Espelho 1:1 por índice
        //    //            if (e.OldStartingIndex >= 0 && e.OldStartingIndex < _dotGainValuesView.Count)
        //    //                _dotGainValuesView.RemoveAt(e.OldStartingIndex);
        //    //            break;
        //    //        default:
        //    //            RebuildDotGainValuesView();
        //    //            break;
        //    //    }

        //    //    this.RaisePropertyChanged(nameof(DotGainValuesView));
        //    //}

        //    //private void ReplaceCollection<T>(ref ObservableCollection<T> field, IEnumerable<T>? items = null, string? propName = null)
        //    //{
        //    //    var newCol = items != null ? new ObservableCollection<T>(items) : new ObservableCollection<T>();
        //    //    field = newCol;
        //    //    this.RaisePropertyChanged(propName ?? string.Empty);
        //    //}


        //    // =======================

        //    //public enum modeSelectedType
        //    //{
        //    //    allDensity,
        //    //    diffDensity,
        //    //    dotGain,
        //    //    dotArea,
        //    //    empty
        //    //}

        //    public ObservableCollection<string> Tabs { get; } = new()
        //    {
        //        "Método A",
        //        "Método B",
        //        "Método C"
        //    };

        //    //public int SelectedTab
        //    //{
        //    //    get => _selectedTab;
        //    //    set
        //    //    {
        //    //        if (value == _selectedTab) return;
        //    //        var old = _selectedTab;
        //    //        this.RaiseAndSetIfChanged(ref _selectedTab, value);
        //    //        OnTabChanged(old, value);
        //    //    }
        //    //}

        //    //private void OnTabChanged(int oldTab, int newTab)
        //    //{
        //    //    using (SuspendUpdates()) // evita avalanche de notificações
        //    //    {
        //    //        dot100 = null;
        //    //        aux = 100;
        //    //        aux2 = 25;
        //    //        isFinished = false;
        //    //        //ReadSubstrate = true;

        //    //        densityList?.Clear();
        //    //        densities = null;
        //    //        measurements = null;
        //    //        rgbValues = null;
        //    //        auxTrapping = 0;

        //    //        if (newTab != 0 && newTab != 1 && newTab != 6 && !isSubstrateRead)
        //    //        {
        //    //            ShowMessage = "Leia o substrato";
        //    //            ReadSubstrate = true;
        //    //            EnableSubstrateButton = true;
        //    //        }
        //    //        else if ((newTab == 0 || newTab == 1 || newTab == 6) && PaperSetupStr == "Absolute")
        //    //        {
        //    //            ShowMessage = string.Empty;
        //    //            ReadSubstrate = false;
        //    //            EnableSubstrateButton = false;
        //    //        }
        //    //        // 🔽 TUDO que afeta UI:
        //    //        PostUiWorkCoalesced(() =>
        //    //        {
        //    //            DensitiesValue?.Clear();
        //    //            DensitiesValueSample?.Clear();
        //    //            DensitiesValueDiff?.Clear();
        //    //            DensitiesTSample?.Clear();  // já limpava T sample

        //    //            TrappingValue?.Clear();     // ⬅️ LIMPA O RESULTADO DE TRAPPING

        //    //            ChannelBrush = Brushes.Gray;
        //    //            ChannelNum?.Clear();
        //    //            rgbColors?.Clear();
        //    //            ChannelColors.Clear();
        //    //            ChannelColors.Add("Gray");

        //    //            DotGainValues = new ObservableCollection<List<float[]>>();
        //    //            RebuildDotGainValuesView();

        //    //            ReplaceCollection(ref _dotAreaSeries, null, nameof(DotAreaSeries));
        //    //            ReplaceCollection(ref _dotGainValuesView, null, nameof(DotGainValuesView));

        //    //            var whiteRgb = new List<float[]> { new float[] { 200, 200, 200 } };
        //    //            ReplaceRibbonRow(whiteRgb);
        //    //            ReplaceRibbonRow2(whiteRgb);
        //    //            ReplaceRibbonRow3(whiteRgb);
        //    //            ReplaceRibbonTrapping(whiteRgb);

        //    //            _lastRibbon = whiteRgb;

        //    //            NotifyViewsOnce();
        //    //        });

        //    //    }
        //    //}

        //    private ObservableCollection<ISeries> _dotAreaSeries = new();
        //    public ObservableCollection<ISeries> DotAreaSeries
        //    {
        //        get => _dotAreaSeries;
        //        set
        //        {
        //            if (_dotAreaSeries != value)
        //            {
        //                _dotAreaSeries = value;
        //                this.RaisePropertyChanged(nameof(DotAreaSeries));
        //            }
        //        }
        //    }

        //    public float? ChannelNumFirst => ChannelNum != null && ChannelNum.Count > 0
        //? ChannelNum[0]
        //: (float?)null;

        //    void model_DeviceChanged(I1Pro64 device)
        //    {
        //        if (device != null)
        //        {
        //            device.ButtonPressed += device_ButtonPressed;
        //        }
        //    }

        //    List<List<float[]>> densityList = new List<List<float[]>>();

        //    private static void OnUI(Action a) => Dispatcher.UIThread.Post(a);

        //    public class ChannelReading
        //    {
        //        public float Value { get; set; }       // valor da densidade
        //        public IBrush Brush { get; set; }      // cor do quadrado (C, M, Y, K)
        //    }
        //    private const int MaxChannelReads = 7;

        //    // Lista de leituras (cada uma com valor + cor)
        //    public ObservableCollection<ChannelReading> ChannelReadings { get; } = new();

        //    //private void Tab0_Density(Action deviceRead)
        //    //{
        //    //    Debug.WriteLine(ReadSubstrate);
        //    //    deviceRead();

        //    //    if (densities == null)
        //    //        return;

        //    //    dotCn = densities;
        //    //    var lab = _cls.RgbToLab(rgbValues[0][0], rgbValues[0][1], rgbValues[0][2]);
        //    //    string chann = _cls.ClassificarCanalLab(labValues[0][0], labValues[0][1], labValues[0][2]);

        //    //    if (chann == "C") cn = 0;
        //    //    else if (chann == "M") cn = 1;
        //    //    else if (chann == "Y") cn = 2;
        //    //    else if (chann == "K") cn = 3;
        //    //    else
        //    //    {
        //    //        // fallback, se quiser manter
        //    //        chann = _cls.ClassificarCanalLab(labValues[0][0], labValues[0][1], labValues[0][2]);
        //    //    }

        //    //    float valorAux = dotCn[0][cn];

        //    //    OnUI(() =>
        //    //    {
        //    //        // Define o brush da leitura atual
        //    //        var brush = chann switch
        //    //        {
        //    //            "C" => Brushes.Cyan,
        //    //            "M" => Brushes.Magenta,
        //    //            "Y" => Brushes.Yellow,
        //    //            "K" => Brushes.Black,
        //    //            _ => Brushes.Black
        //    //        };

        //    //        // Atualiza o brush atual (se usado em outro lugar)
        //    //        ChannelBrush = brush;

        //    //        // Se já deu 5 leituras, reseta tudo
        //    //        if (ChannelReadings.Count >= MaxChannelReads)
        //    //        {
        //    //            ChannelReadings.Clear();
        //    //            ChannelNum.Clear();
        //    //        }

        //    //        // Guarda o valor cru se ainda precisar
        //    //        ChannelNum.Add(valorAux);

        //    //        // Adiciona uma leitura com valor + cor
        //    //        ChannelReadings.Add(new ChannelReading
        //    //        {
        //    //            Value = valorAux,
        //    //            Brush = brush
        //    //        });

        //    //        this.RaisePropertyChanged(nameof(ChannelBrush));
        //    //        this.RaisePropertyChanged(nameof(ChannelReadings));
        //    //        this.RaisePropertyChanged(nameof(ChannelNum));
        //    //        this.RaisePropertyChanged(nameof(ChannelNumFirst));
        //    //    });
        //    //}

        //    //private void Tab1_AllDensity(Action deviceRead)
        //    //{
        //    //    deviceRead();

        //    //    OnUI(() =>
        //    //    {
        //    //        if (densities == null || densities.Count == 0)
        //    //            return;

        //    //        if (DensitiesValue == null)
        //    //            DensitiesValue = new List<float[]>();

        //    //        if (DensitiesValue.Count >= MaxAllDensityReads)
        //    //            DensitiesValue.Clear();

        //    //        var row = densities[0];
        //    //        DensitiesValue.Add(row);

        //    //        ReplaceRibbonRow(rgbValues);

        //    //        this.RaisePropertyChanged(nameof(DensitiesValueView));
        //    //    });
        //    //}

        //    //private void Tab2_DotGain(Action deviceRead)
        //    //{
        //    //    deviceRead();

        //    //    if (densities == null)
        //    //        return;

        //    //    if (dot100 == null)
        //    //    {
        //    //        dot100 = densities;

        //    //        var lab100 = _cls.RgbToLab(rgbValues[0][0], rgbValues[0][1], rgbValues[0][2]);
        //    //        string chann = _cls.ClassificarCanalPorDensidade(dot100[0][0], dot100[0][1], dot100[0][2], dot100[0][3]);

        //    //        if (chann == "C") cn = 0;
        //    //        else if (chann == "M") cn = 1;
        //    //        else if (chann == "Y") cn = 2;
        //    //        else if (chann == "K") cn = 3;
        //    //        else
        //    //        {
        //    //            chann = _cls.ClassificarCanalLab(labValues[0][0], labValues[0][1], labValues[0][2]);
        //    //        }

        //    //        float valorAux = dot100[0][cn];

        //    //        OnUI(() =>
        //    //        {
        //    //            ChannelNum.Add(valorAux);

        //    //            ChannelBrush = chann switch
        //    //            {
        //    //                "C" => Brushes.Cyan,
        //    //                "M" => Brushes.Magenta,
        //    //                "Y" => Brushes.Yellow,
        //    //                "K" => Brushes.Black,
        //    //                _ => Brushes.Black
        //    //            };

        //    //            this.RaisePropertyChanged(nameof(ChannelBrush));
        //    //            this.RaisePropertyChanged(nameof(ChannelNumFirst));
        //    //        });
        //    //    }
        //    //    else if (aux >= 25)
        //    //    {
        //    //        aux -= 25;

        //    //        var lab100 = _cls.RgbToLab(rgbValues[0][0], rgbValues[0][1], rgbValues[0][2]);
        //    //        string chann = _cls.ClassificarCanalLab(labValues[0][0], labValues[0][1], labValues[0][2]);

        //    //        if (chann == "C") cn = 0;
        //    //        else if (chann == "M") cn = 1;
        //    //        else if (chann == "Y") cn = 2;
        //    //        else if (chann == "K") cn = 3;

        //    //        var dotDensitiesCurve = _eqs.DotGain(dot100, densities, aux);
        //    //        dotDensitiesCurve = _mss.RoundPercentageList(dotDensitiesCurve);
        //    //        float aux2Local = dotDensitiesCurve[0][cn];

        //    //        OnUI(() =>
        //    //        {
        //    //            ChannelNum.Add(aux2Local);
        //    //            this.RaisePropertyChanged(nameof(ChannelNumFirst));
        //    //        });
        //    //    }

        //    //    if (aux <= 25)
        //    //    {
        //    //        aux = 100;
        //    //        dot100 = null;
        //    //        isFinished = true;
        //    //    }
        //    //}

        //    //private void Tab3_DotAreaCurve(Action deviceRead)
        //    //{
        //    //    deviceRead();

        //    //    if (densities == null)
        //    //        return;

        //    //    if (dot100 == null)
        //    //    {
        //    //        dot100 = densities;

        //    //        var lab100 = _cls.RgbToLab(rgbValues[0][0], rgbValues[0][1], rgbValues[0][2]);
        //    //        string chann = _cls.ClassificarCanalLab(labValues[0][0], labValues[0][1], labValues[0][2]);

        //    //        if (chann == "C") cn = 0;
        //    //        else if (chann == "M") cn = 1;
        //    //        else if (chann == "Y") cn = 2;
        //    //        else if (chann == "K") cn = 3;

        //    //        float chico = dot100[0][cn];

        //    //        OnUI(() =>
        //    //        {
        //    //            ChannelNum.Add(chico);

        //    //            ChannelBrush = chann switch
        //    //            {
        //    //                "C" => Brushes.Cyan,
        //    //                "M" => Brushes.Magenta,
        //    //                "Y" => Brushes.Yellow,
        //    //                "K" => Brushes.Black,
        //    //                _ => Brushes.Black
        //    //            };

        //    //            this.RaisePropertyChanged(nameof(ChannelBrush));
        //    //            this.RaisePropertyChanged(nameof(ChannelNumFirst));
        //    //        });
        //    //    }
        //    //    else if (aux2 <= 100)
        //    //    {
        //    //        var lab100 = _cls.RgbToLab(rgbValues[0][0], rgbValues[0][1], rgbValues[0][2]);
        //    //        string chann = _cls.ClassificarCanalLab(labValues[0][0], labValues[0][1], labValues[0][2]);

        //    //        if (chann == "C") cn = 0;
        //    //        else if (chann == "M") cn = 1;
        //    //        else if (chann == "Y") cn = 2;
        //    //        else if (chann == "K") cn = 3;

        //    //        var dotDensitiesCurve = _eqs.DotGain(dot100, densities, aux2);
        //    //        dotDensitiesCurve = _mss.RoundPercentageList(dotDensitiesCurve);

        //    //        aux2 += 25;

        //    //        float auxLocal = dotDensitiesCurve[0][cn];

        //    //        OnUI(() =>
        //    //        {
        //    //            ChannelNum.Add(auxLocal);
        //    //            this.RaisePropertyChanged(nameof(ChannelNumFirst));
        //    //        });
        //    //    }

        //    //    if (aux2 >= 100)
        //    //    {
        //    //        aux2 = 25;
        //    //        dot100 = null;
        //    //        isFinished = true;
        //    //        UpdateDotGainChart();
        //    //    }
        //    //}

        //    //private void Tab4_DotArea(Action deviceRead)
        //    //{
        //    //    deviceRead();

        //    //    if (dot100 == null)
        //    //    {
        //    //        dot100 = densities;

        //    //        var lab100 = _cls.RgbToLab(rgbValues[0][0], rgbValues[0][1], rgbValues[0][2]);
        //    //        string chann = _cls.ClassificarCanalLab(labValues[0][0], labValues[0][1], labValues[0][2]);

        //    //        if (chann == "C") cn = 0;
        //    //        else if (chann == "M") cn = 1;
        //    //        else if (chann == "Y") cn = 2;
        //    //        else if (chann == "K") cn = 3;

        //    //        float chico = dot100[0][cn];

        //    //        OnUI(() =>
        //    //        {
        //    //            ChannelNum.Add(chico);

        //    //            ChannelBrush = chann switch
        //    //            {
        //    //                "C" => Brushes.Cyan,
        //    //                "M" => Brushes.Magenta,
        //    //                "Y" => Brushes.Yellow,
        //    //                "K" => Brushes.Black,
        //    //                _ => Brushes.Black
        //    //            };

        //    //            this.RaisePropertyChanged(nameof(ChannelBrush));
        //    //            this.RaisePropertyChanged(nameof(ChannelNumFirst));
        //    //        });
        //    //    }
        //    //    else if (aux >= 25)
        //    //    {
        //    //        aux -= 25;

        //    //        var lab100 = _cls.RgbToLab(rgbValues[0][0], rgbValues[0][1], rgbValues[0][2]);
        //    //        string chann = _cls.ClassificarCanalLab(labValues[0][0], labValues[0][1], labValues[0][2]);

        //    //        if (chann == "C") cn = 0;
        //    //        else if (chann == "M") cn = 1;
        //    //        else if (chann == "Y") cn = 2;
        //    //        else if (chann == "K") cn = 3;

        //    //        var dotDensitiesCurve = _eqs.DotGain(dot100, densities, 0);
        //    //        dotDensitiesCurve = _mss.RoundPercentageList(dotDensitiesCurve);

        //    //        float aux2Local = dotDensitiesCurve[0][cn];

        //    //        OnUI(() =>
        //    //        {
        //    //            ChannelNum.Add(aux2Local);
        //    //            this.RaisePropertyChanged(nameof(ChannelNumFirst));
        //    //        });
        //    //    }

        //    //    if (aux <= 25)
        //    //    {
        //    //        aux = 100;
        //    //        dot100 = null;
        //    //        isFinished = true;
        //    //    }
        //    //}

        //    //private void Tab5_Trapping(Action deviceRead)
        //    //{
        //    //    deviceRead();

        //    //    if (densities != null && ReadSubstrate == false)
        //    //    {
        //    //        densityList.Add(densities);
        //    //        rgbColors.Add(rgbValues);
        //    //    }

        //    //    if (DensitiesTSample == null)
        //    //        DensitiesTSample = new List<float>();

        //    //    OnUI(() =>
        //    //    {
        //    //        if (densityList.Count == 1)
        //    //        {
        //    //            ReplaceRibbonRow(rgbColors[0]);

        //    //            var (L, A, B) = _cls.RgbToLab(
        //    //                rgbColors[0][0][0],
        //    //                rgbColors[0][0][1],
        //    //                rgbColors[0][0][2]
        //    //            );

        //    //            var chann = _cls.ClassificarCanalLab(labValues[0][0], labValues[0][1], labValues[0][2]);

        //    //            if (chann == "C") cn = 0;
        //    //            else if (chann == "M") cn = 1;
        //    //            else if (chann == "Y") cn = 2;
        //    //            else if (chann == "K") cn = 3;

        //    //            DensitiesTSample.Add(densityList[0][0][cn]);
        //    //            this.RaisePropertyChanged(nameof(DensitiesTSampleView));
        //    //            DensitiesValueSample = densityList[0];
        //    //            auxTrapping++;
        //    //        }
        //    //        else if (densityList.Count == 2)
        //    //        {
        //    //            ReplaceRibbonRow2(rgbColors[1]);

        //    //            var (L, A, B) = _cls.RgbToLab(
        //    //                rgbColors[0][0][0],
        //    //                rgbColors[0][0][1],
        //    //                rgbColors[0][0][2]
        //    //            );

        //    //            var chann = _cls.ClassificarCanalLab(labValues[0][0], labValues[0][1], labValues[0][2]);

        //    //            if (chann == "C") cn = 0;
        //    //            else if (chann == "M") cn = 1;
        //    //            else if (chann == "Y") cn = 2;
        //    //            else if (chann == "K") cn = 3;

        //    //            DensitiesTSample.Add(densityList[1][0][cn]);
        //    //            this.RaisePropertyChanged(nameof(DensitiesTSampleView));
        //    //            DensitiesValue = densityList[1];
        //    //            auxTrapping++;
        //    //        }
        //    //        else if (densityList.Count == 3)
        //    //        {
        //    //            ReplaceRibbonRow3(rgbColors[2]);

        //    //            var (L, A, B) = _cls.RgbToLab(
        //    //                rgbColors[0][0][0],
        //    //                rgbColors[0][0][1],
        //    //                rgbColors[0][0][2]
        //    //            );

        //    //            var chann = _cls.ClassificarCanalLabTrapping(labValues[0][0], labValues[0][1], labValues[0][2]);

        //    //            ChannelColors.Clear();

        //    //            switch (chann)
        //    //            {
        //    //                case "C+M":
        //    //                    ChannelColors.Add("DarkBlue");
        //    //                    cn = 1;
        //    //                    break;
        //    //                case "M+Y":
        //    //                    ChannelColors.Add("Red");
        //    //                    cn = 0;
        //    //                    break;
        //    //                case "C+Y":
        //    //                    ChannelColors.Add("Green");
        //    //                    cn = 2;
        //    //                    break;
        //    //            }

        //    //            TrappingValue = new List<float>();
        //    //            var auxiliar = _eqs.Trapping(DensitiesValueSample[0], DensitiesValue[0], densityList[2][0]);
        //    //            TrappingValue.Add(auxiliar[cn]);
        //    //            this.RaisePropertyChanged(nameof(TrappingValueView));

        //    //            isFinished = true;
        //    //            auxTrapping = 0;
        //    //        }

        //    //        // if (densityList.Count >= 3) { ... } – está comentado no código original,
        //    //        // então não mexi.
        //    //    });
        //    //}

        //    //private void Tab6_DiffDensity(Action deviceRead)
        //    //{
        //    //    deviceRead();

        //    //    densityList.Add(densities);
        //    //    rgbColors.Add(rgbValues);

        //    //    OnUI(() =>
        //    //    {
        //    //        if (densityList.Count >= 1)
        //    //            DensitiesValueSample = densityList[0];

        //    //        if (densityList.Count == 1)
        //    //            ReplaceRibbonRow(rgbColors[0]);
        //    //        else
        //    //            ReplaceRibbonRow2(rgbColors[1]);

        //    //        if (densityList.Count >= 2)
        //    //        {
        //    //            DensitiesValue = densityList[1];
        //    //            DensitiesValueDiff = _eqs.DiffDensity(densityList);
        //    //            isFinished = true;
        //    //        }
        //    //    });
        //    //}

        //    private void device_ButtonPressed(I1Pro64 device)
        //    {
        //        //if (CurrentDevice.isCalibrated == false)
        //        //    throw new Exception ("Device not calibrated.");

        //        //try
        //        //{
        //        //    model.CurrentDevice.SetStatus(StatusSetupStr);
        //        //    void deviceRead()
        //        //    {
        //        //        if (ReadSubstrate)
        //        //        {
        //        //            List<float[]> substrateData = new List<float[]>();
        //        //            _mss.ReflectanceSpot(model);
        //        //            substrateData = _mss.readData;
        //        //            rgbValues = _mss.rgbValues;
        //        //            model.CurrentDevice.SubstrateConfiguration(substrateData[0]);
        //        //            ReadSubstrate = false;
        //        //            ShowMessage = string.Empty;
        //        //            isSubstrateRead = true;
        //        //            OnUI(() =>
        //        //            {
        //        //                DensitiesValue?.Clear();
        //        //                ReplaceRibbonSubstrate(rgbValues);
        //        //            });

        //        //            return;
        //        //        }

        //        //        if (isFinished)
        //        //        {
        //        //            ResetDotGain();
        //        //            ResetDiffDensity();
        //        //            isFinished = false;
        //        //        }

        //        //        if (this.model.CurrentDevice.MeasurementMode == I1Pro64.MeasurementModeType.eReflectanceSpot)
        //        //        {
        //        //            _mss.ReflectanceSpot(model);
        //        //        }
        //        //        else if (this.model.CurrentDevice.MeasurementMode == I1Pro64.MeasurementModeType.eDualReflectanceSpot)
        //        //        {
        //        //            _mss.DualReflectanceSpot(model);
        //        //        }

        //        //        densities = _mss.densityData;
        //        //        measurements = _mss.readData;
        //        //        rgbValues = _mss.rgbValues;
        //        //        labValues = _mss.labData;
        //        //    }

        //        //    switch (SelectedTab)
        //        //    {
        //        //        case 0:
        //        //            Tab0_Density(deviceRead);
        //        //            break;

        //        //        case 1:
        //        //            Tab1_AllDensity(deviceRead);
        //        //            break;

        //        //        case 2:
        //        //            Tab2_DotGain(deviceRead);
        //        //            break;

        //        //        case 3:
        //        //            //Tab3_DotAreaCurve(deviceRead);
        //        //            break;

        //        //        case 4:
        //        //            Tab4_DotArea(deviceRead);
        //        //            break;

        //        //        case 5:
        //        //            Tab5_Trapping(deviceRead);
        //        //            break;

        //        //        case 6:
        //        //            Tab6_DiffDensity(deviceRead);
        //        //            break;
        //        //    }
        //        //}
        //        //catch (Exception)
        //        //{
        //        //    // _ = WarningPopupAsync(ex);
        //        //}
        //    }

        //    int cn = 0;
        //    int aux = 100;
        //    int aux2 = 25;
        //    int auxTrapping = 0;

        //    public void AtualizarCorDoCanal(string chann)
        //    {
        //        ChannelColors.Clear();

        //        switch (chann)
        //        {
        //            case "C":
        //                ChannelColors.Add("DarkBlue"); // ou "Cyan"
        //                break;
        //            case "M":
        //                ChannelColors.Add("Red");
        //                break;
        //            case "Y":
        //                ChannelColors.Add("Green"); // ou "Yellow" se quiser
        //                break;
        //            case "K":
        //                ChannelColors.Add("Black");
        //                break;
        //        }
        //    }

        //    //public void Substrate()
        //    //{
        //    //    ReadSubstrate = true;
        //    //    isSubstrateRead = false;
        //    //    ResetTrapping();
        //    //}
        //    private bool _isButtonEnable = false;

        //    public bool IsButtonEnable
        //    {
        //        get => _isButtonEnable;
        //        set => this.RaiseAndSetIfChanged(ref _isButtonEnable, value);
        //    }

        //    private IBrush _channelBrush = Brushes.Black;
        //    public IBrush ChannelBrush
        //    {
        //        get => _channelBrush;
        //        set => this.RaiseAndSetIfChanged(ref _channelBrush, value);
        //    }

        //    private void CalibrateDevice()
        //    {
        //        try
        //        {
        //            //_mss.CalibrateDevice(model);
        //            Dispatcher.UIThread.Invoke(() =>
        //            {
        //                CalibrationElapsedTime = TimeSpan.Zero;
        //                CalibrationTimer.Start();
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.WriteLine($"Calibration error: {ex.Message}");
        //        }
        //    }
        //    private TimeSpan _calibrationElapsedTime;

        //    public TimeSpan CalibrationElapsedTime
        //    {
        //        get => _calibrationElapsedTime;
        //        set => this.RaiseAndSetIfChanged(ref _calibrationElapsedTime, value);
        //    }
        //    private DispatcherTimer _calibrationTimer;

        //    public DispatcherTimer CalibrationTimer
        //    {
        //        get => _calibrationTimer;
        //        set => this.RaiseAndSetIfChanged(ref _calibrationTimer, value);
        //    }

        //    private bool _settingsEnabled = false;
        //    public bool settingsEnabled
        //    {
        //        get => _settingsEnabled;
        //        set => this.RaiseAndSetIfChanged(ref _settingsEnabled, value);
        //    }

        //    public ICommand SubmitPasswordCommand { get; }

        //    private void SubmitPassword(object? parameter)
        //    {
        //        var text = parameter?.ToString();

        //        if (text == "0")
        //        {
        //            // executa seu código aqui
        //            settingsEnabled = true;
        //        }
        //        else
        //        {
        //            // senha inválida (opcional)
        //            settingsEnabled = false;
        //        }
        //    }

        //    private void InitializeTimer()
        //    {
        //        CalibrationTimer = new DispatcherTimer
        //        {
        //            Interval = TimeSpan.FromSeconds(1) // Atualiza a cada segundo
        //        };
        //        CalibrationTimer.Tick += CalibrationTimer_Tick;
        //        CalibrationElapsedTime = TimeSpan.Zero;
        //    }

        //    private void CalibrationTimer_Tick(object sender, EventArgs e)
        //    {
        //        CalibrationElapsedTime = CalibrationElapsedTime.Add(TimeSpan.FromSeconds(1));
        //        //timerCount.Text = $"{CalibrationElapsedTime}";
        //    }

        //    private bool isFinished = false;
        //    public I1Pro64 CurrentDevice
        //    {
        //        get => _currentDevice;
        //        set
        //        {
        //            this.RaiseAndSetIfChanged(ref _currentDevice, value);
        //            model.CurrentDevice = value; // sincroniza com o seu model
        //        }
        //    }

        //    //public void setAllDensityMode() => modeSelected = modeSelectedType.allDensity;
        //    //public void setDiffDensityMode() => modeSelected = modeSelectedType.diffDensity;
        //    //public void setDotGainMode() => modeSelected = modeSelectedType.dotGain;
        //    //public void setDotAreaMode() => modeSelected = modeSelectedType.dotArea;

        //    public class RibbonRowModel
        //    {
        //        public ObservableCollection<SolidColorBrush> Colors { get; } = new();
        //    }
        //    public ObservableCollection<RibbonRowModel> RibbonRowsFullT { get; } = new();
        //    public ObservableCollection<RibbonRowModel> RibbonRowsFull { get; } = new();
        //    public ObservableCollection<RibbonRowModel> RibbonRowsFull2 { get; } = new();
        //    public ObservableCollection<RibbonRowModel> RibbonRowsFull3 { get; } = new();

        //    public ObservableCollection<RibbonRowModel> RibbonRowsSubstrate { get; } = new();



        //    public ReactiveCommand<Unit, Unit> CloseDeviceCommand { get; }


        //    private int _suspendViewUpdates; // >0 => suspenso
        //    private bool IsSuspended => _suspendViewUpdates > 0;



        //    private int _uiWorkScheduled; // ⬅

        //    //-------------------------------------------------------------
        //    //-------------------------------------------------------------
        //    //-------------------RIBBON COLOR FUNCTIONS--------------------
        //    //-------------------------------------------------------------
        //    //-------------------------------------------------------------

        //    //public void ReplaceRibbonTrapping(List<float[]> newRgbSamples)
        //    //=> _rcs.ReplaceRibbonRow(RibbonRowsFullT, newRgbSamples);

        //    //public void ReplaceRibbonSubstrate(List<float[]> newRgbSamples)
        //    //    => _rcs.ReplaceRibbonRow(RibbonRowsSubstrate, newRgbSamples);

        //    //public void ReplaceRibbonRow(List<float[]> newRgbSamples)
        //    //    => _rcs.ReplaceRibbonRow(RibbonRowsFull, newRgbSamples);

        //    //public void ReplaceRibbonRow2(List<float[]> newRgbSamples)
        //    //    => _rcs.ReplaceRibbonRow(RibbonRowsFull2, newRgbSamples);

        //    //public void ReplaceRibbonRow3(List<float[]> newRgbSamples)
        //    //    => _rcs.ReplaceRibbonRow(RibbonRowsFull3, newRgbSamples);


        //    //-------------------------------------------------------------
        //    //-------------------------------------------------------------
        //    //-----------------------RESET FUNCTIONS-----------------------
        //    //-------------------------------------------------------------
        //    //-------------------------------------------------------------
        //    //private void RebuildDotGainValuesView()
        //    //{
        //    //    _dotGainValuesView.Clear();
        //    //    if (DotGainValues == null) return;

        //    //    foreach (var l in DotGainValues)
        //    //        _dotGainValuesView.Add(l?.Select(a => a?.ToList() ?? new()).ToList());
        //    //}


        //    private IDisposable SuspendUpdates()
        //    {
        //        Interlocked.Increment(ref _suspendViewUpdates);
        //        return Disposable.Create(() => Interlocked.Decrement(ref _suspendViewUpdates));
        //    }
        //    private void NotifyViewsOnce()
        //    {
        //        if (IsSuspended) return;
        //        //this.RaisePropertyChanged(nameof(DensitiesValueView));
        //        //this.RaisePropertyChanged(nameof(DensitiesValueSampleView));
        //        //this.RaisePropertyChanged(nameof(DensitiesValueDiffView));
        //        //this.RaisePropertyChanged(nameof(DensitiesTSampleView));
        //        //this.RaisePropertyChanged(nameof(TrappingValueView));
        //        this.RaisePropertyChanged(nameof(DotGainValuesView));
        //        this.RaisePropertyChanged(nameof(DotAreaSeries));
        //    }




        //    private void PostUiWorkCoalesced(Action a) // ⬅
        //    {
        //        if (Interlocked.Exchange(ref _uiWorkScheduled, 1) == 1) return;
        //        Dispatcher.UIThread.InvokeAsync(() =>
        //        {
        //            try { a(); }
        //            finally { Interlocked.Exchange(ref _uiWorkScheduled, 0); }
        //        }, DispatcherPriority.Background);
        //    }

        //    private void ResetCommon()
        //    {
        //        densities = null;
        //        measurements = null;
        //        //rgbValues = null;
        //        rgbValues = new List<float[]>
        //        {
        //            new float[] { 200, 200, 200 }, // White
        //        };
        //        Dispatcher.UIThread.Invoke(() => ReplaceRibbonRow(rgbValues));
        //        Dispatcher.UIThread.Invoke(() => ReplaceRibbonRow2(rgbValues));
        //        Dispatcher.UIThread.Invoke(() => ReplaceRibbonRow3(rgbValues));
        //        Dispatcher.UIThread.Invoke(() => ReplaceRibbonTrapping(rgbValues));
        //    }

        //    private void ResetDiffDensity()
        //    {
        //        using (SuspendUpdates())
        //        {
        //            densityList?.Clear();
        //            rgbColors?.Clear();
        //            ResetCommon();

        //            PostUiWorkCoalesced(() =>
        //            {
        //                DensitiesValue?.Clear();
        //                DensitiesValueSample?.Clear();
        //                DensitiesValueDiff?.Clear();
        //                DensitiesTSample?.Clear(); // ⬅ idem aqui

        //                NotifyViewsOnce();
        //            });
        //        }
        //    }

        //    private void ResetTrapping()
        //    {
        //        using (SuspendUpdates())
        //        {
        //            densityList?.Clear();
        //            rgbColors?.Clear();
        //            ResetCommon();
        //            PostUiWorkCoalesced(() =>
        //            {
        //                DensitiesValue?.Clear();
        //                DensitiesTSample?.Clear();
        //                TrappingValue?.Clear(); // ⬅ idem aqui
        //                NotifyViewsOnce();
        //            });
        //        }
        //    }

        //    private void ResetDotGain()
        //    {
        //        using (SuspendUpdates()) // ⬅
        //        {
        //            dot100 = null;
        //            aux = 100;
        //            aux2 = 25;
        //            isFinished = false;

        //            PostUiWorkCoalesced(() =>
        //            {
        //                DotGainValues?.Clear();
        //                ChannelBrush = Brushes.Gray;
        //                ChannelNum.Clear();
        //                RebuildDotGainValuesView(); // ⬅ manter espelho

        //                ReplaceCollection(ref _dotAreaSeries, null, nameof(DotAreaSeries)); // ⬅ gráfico

        //                NotifyViewsOnce(); // ⬅
        //            });

        //            ResetCommon();
        //        }
        //    }
        //    //-------------------------------------------------------------
        //    //-------------------------------------------------------------
        //    //-------------------CLOSE DEVICE FUNCTION---------------------
        //    //-------------------------------------------------------------
        //    //-------------------------------------------------------------
        //    public async void CloseDevice()
        //    {
        //        try
        //        {
        //            var dev = CurrentDevice;
        //            if (dev != null)
        //            {
        //                try { dev.ButtonPressed -= device_ButtonPressed; } catch { }

        //                // Zera seleção do ComboBox (ver resposta anterior)
        //                CurrentDevice = null;

        //                // Aborta estados de leitura
        //                try { if (dev.ScanState != MeasurementScanState.Idle) dev.isMeasuring = false; } catch { }

        //                // Fecha off-UI
        //                await Task.Run(() => { try { dev.Close(); } catch { } });
        //            }
        //            else
        //            {
        //            }

        //            await Dispatcher.UIThread.InvokeAsync(() =>
        //            {
        //                CurrentDevice = null;
        //            });

        //            // Em vez de model.SearchDevices():
        //            //await ReinitModelAsync();
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.WriteLine(ex);
        //        }
        //    }
        //    private List<float[]> _lastRibbon = new() { new float[] { 200, 200, 200 } }; // ⬅


        //    //public void SetAbsolutePaper()
        //    //{
        //    //    _sts.DensitySetup();
        //    //}

        //    //public void SetPaper()
        //    //{

        //    //}

    }

}
//public class AllDensityRow
//{
//    public ObservableCollection<SolidColorBrush> Colors { get; } = new();
//    public List<float> Densities { get; set; } = new(); // [C,M,Y,K]
//}

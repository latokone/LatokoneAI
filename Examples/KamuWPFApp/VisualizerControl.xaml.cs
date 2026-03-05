using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WPFExample
{
    /// <summary>
    /// Interaction logic for VisualizerControl.xaml
    /// </summary>
    public partial class VisualizerControl : UserControl, INotifyPropertyChanged
    {
        public double VUMeterLevelL { get; set; }
        public double VUMeterLevelR { get; set; }
        public double VUMeterRMSLevelL { get; set; }
        public double VUMeterRMSLevelR { get; set; }

        private readonly int FFT_SIZE = 2048;

        const double VUMeterRange = 80.0;

        float maxSampleL;
        float maxSampleR;

        const double MinAmp = 66;

        public SolidColorBrush AudioLBrush { get; set; }
        public Brush AudioLBGBrush { get; set; }

        private float[] AudioBuffer { get; set; }
        private int audioBufferFillPosition = 0;
        private readonly int AUDIO_BUFFER_SIZE = 20 * 2048;

        public double WindowSize { get; set; }
        public double WindowPosition { get; set; }

        double waveZoom;
        public double WaveZoom { get => waveZoom; set { waveZoom = value; PropertyChanged?.Raise(this, "WaveZoom"); } }

        DispatcherTimer timer;

        public event PropertyChangedEventHandler? PropertyChanged;

        readonly string[] fftWindows = new string[] { "Hanning", "Hamming", "Blackman", "BlackmanExact", "BlackmanHarris", "FlatTop", "Bartlett", "Cosine" };
        public string[] FFTWindows { get => fftWindows; }

        string selectedFFTWindow;


        public string SelectedFFTWindow { get => selectedFFTWindow; set { selectedFFTWindow = value; PropertyChanged.Raise(this, "SelectedFFTWindow"); } }

        public SolidColorBrush AudioRBrush { get; }
        public VisualizerControl()
        {
            InitializeComponent();
            DataContext = this;

            AudioLBrush = TryFindResource("AudioLBrush") as SolidColorBrush;
            if (AudioLBrush == null)
                AudioLBrush = Brushes.Red;

            AudioRBrush = TryFindResource("AudioRBrush") as SolidColorBrush;
            if (AudioRBrush == null)
                AudioRBrush = Brushes.SpringGreen;

            AudioLBGBrush = TryFindResource("VUMeterBackgroundGradientBrush") as LinearGradientBrush;

            AudioBuffer = new float[AUDIO_BUFFER_SIZE];
            SelectedFFTWindow = "Hanning";

            SetTimer();

            WindowSize = 1;
            WindowPosition = 1;
            WaveZoom = 0;

            this.Loaded += (sender, e) =>
            {
                DrawDbText();
                timer?.Start();
            };

            this.SizeChanged += (sender, e) =>
            {
                DrawDbText();
            };
        }

        internal void SetBlueTheme()
        {
            cWaveL.Background = TryFindResource("VUMeterBackgroundGradientBrushBlue") as LinearGradientBrush;
            cSpecL.Background = TryFindResource("VUMeterBackgroundGradientBrushBlue") as LinearGradientBrush;
            AudioLBrush = TryFindResource("AudioLBrushBlue") as SolidColorBrush;
        }

        void SetTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000 / 30);
            timer.Tick += (sender, e) =>
            {
                if (this.ActualHeight == 0 || this.ActualWidth == 0)
                    return;

                if (maxSampleL >= 0)
                {
                    var db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleL), -VUMeterRange), 0.0);
                    VUMeterLevelL = (db + VUMeterRange) / VUMeterRange;
                    VUMeterRMSLevelL = VUMeterLevelL * 0.70710;
                    PropertyChanged.Raise(this, "VUMeterLevelL");
                    PropertyChanged.Raise(this, "VUMeterRMSLevelL");
                    maxSampleL = -1;
                }
                if (maxSampleR >= 0)
                {
                    var db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleR), -VUMeterRange), 0.0);
                    VUMeterLevelR = (db + VUMeterRange) / VUMeterRange;
                    VUMeterLevelR = !double.IsNormal(VUMeterLevelR) ? 0 : VUMeterLevelR;
                    VUMeterRMSLevelR = VUMeterLevelR * 0.70710;
                    PropertyChanged.Raise(this, "VUMeterLevelR");
                    PropertyChanged.Raise(this, "VUMeterRMSLevelR");
                    maxSampleR = -1;
                }

                double[] audioBufL = GetBuffer(FFT_SIZE * 2, 0);
                double[] audioBufR = GetBuffer(FFT_SIZE * 2, 1);

                double[] window = null;

                switch (SelectedFFTWindow)
                {
                    case "Hanning":
                        window = FftSharp.Window.Hanning(audioBufL.Length);
                        break;
                    case "Hamming":
                        window = FftSharp.Window.Hamming(audioBufL.Length);
                        break;
                    case "Blackman":
                        window = FftSharp.Window.Blackman(audioBufL.Length);
                        break;
                    case "BlackmanExact":
                        window = FftSharp.Window.BlackmanExact(audioBufL.Length);
                        break;
                    case "BlackmanHarris":
                        window = FftSharp.Window.BlackmanHarris(audioBufL.Length);
                        break;
                    case "FlatTop":
                        window = FftSharp.Window.FlatTop(audioBufL.Length);
                        break;
                    case "Bartlett":
                        window = FftSharp.Window.Bartlett(audioBufL.Length);
                        break;
                    case "Cosine":
                        window = FftSharp.Window.Cosine(audioBufL.Length);
                        break;
                }

                FftSharp.Window.ApplyInPlace(window, audioBufL);
                FftSharp.Window.ApplyInPlace(window, audioBufR);

                double[] fftPowerL = FftSharp.Transform.FFTpower(audioBufL);
                double[] fftPowerR = FftSharp.Transform.FFTpower(audioBufR);

                cSpecL.Children.Clear();
                cSpecR.Children.Clear();
                Polyline polylineL = new Polyline() { Height = cSpecL.ActualHeight, Width = cSpecL.ActualWidth };
                polylineL.Stroke = AudioLBrush;

                double canvasStepX = (WindowSize + 9) / 10.0;

                double fftStep = fftPowerL.Length / cSpecL.ActualWidth / canvasStepX;

                double fftIndexStart = (WindowPosition / 100) * fftPowerL.Length;
                double fftIndex = fftIndexStart;
                double clamp = 80;
                double move = 10;

                int prevInd = -1;

                for (double x = 0; x < cSpecL.ActualWidth; x++)
                {
                    int ind = Math.Min((int)fftIndex, fftPowerL.Length - 1);
                    if (ind != prevInd)
                    {
                        double y = cSpecL.ActualHeight - (cSpecL.ActualHeight * fftPowerL[ind] / clamp) - move;
                        polylineL.Points.Add(new Point(x, y));
                        prevInd = ind;
                    }
                    fftIndex += fftStep;
                }
                cSpecL.Children.Add(polylineL);

                Polyline polylineR = new Polyline() { Height = cSpecR.ActualHeight, Width = cSpecR.ActualWidth };
                polylineR.Stroke = AudioRBrush;

                fftIndex = fftIndexStart;
                prevInd = -1;
                for (double x = 0; x < cSpecR.ActualWidth; x++)
                {
                    int ind = Math.Min((int)fftIndex, fftPowerR.Length - 1);
                    if (ind != prevInd)
                    {
                        double y = cSpecR.ActualHeight - (cSpecR.ActualHeight * fftPowerR[ind] / clamp) - move;
                        polylineR.Points.Add(new Point(x, y));
                        prevInd = ind;
                    }
                    fftIndex += fftStep;
                }
                cSpecR.Children.Add(polylineR);

                // Wave
                double zoom = 1 - WaveZoom + 1;
                int bufSize = (int)(cWaveL.ActualWidth * zoom);
                audioBufL = GetBuffer(bufSize, 0);

                double audiomul = (1.0f / 32768.0f) * cWaveL.ActualHeight;

                polylineL = new Polyline() { Height = cWaveL.ActualHeight, Width = cWaveL.ActualWidth };
                polylineL.Stroke = AudioLBrush;
                double waveIndex = 0;

                for (int x = 0; x < cWaveL.ActualWidth; x++)
                {
                    int index = (int)waveIndex % audioBufL.Length;
                    double y = cWaveL.ActualHeight / 2 - audioBufL[index] * audiomul;
                    y = double.IsRealNumber(y) ? y : 0;
                    polylineL.Points.Add((new Point(x, y)));
                    waveIndex += zoom;
                }

                cWaveL.Children.Clear();
                cWaveL.Children.Add(polylineL);

                bufSize = (int)(cWaveR.ActualWidth * zoom);
                audioBufR = GetBuffer(bufSize, 1);

                polylineR = new Polyline() { Height = cSpecR.ActualHeight, Width = cSpecR.ActualWidth };
                polylineR.Stroke = AudioRBrush;
                waveIndex = 0;

                for (int x = 0; x < cWaveR.ActualWidth; x++)
                {
                    int index = (int)waveIndex % audioBufR.Length;
                    double y = cWaveR.ActualHeight / 2 - audioBufR[index] * audiomul;
                    y = double.IsRealNumber(y) ? y : 0;
                    polylineR.Points.Add((new Point(x, y)));
                    waveIndex += zoom;
                }
                cWaveR.Children.Clear();
                cWaveR.Children.Add(polylineR);
            };
        }

        private void DrawDbText()
        {
            volTextCanvas.Children.Clear();

            double y = 0;
            while (y < volTextCanvas.ActualHeight)
            {
                Brush b = TryFindResource("GridLinesBrush") as SolidColorBrush;
                Line l = new Line() { X1 = volTextCanvas.ActualWidth - 10, X2 = volTextCanvas.ActualWidth, Y1 = y, Y2 = y, SnapsToDevicePixels = true, Stroke = b, StrokeThickness = 1, ClipToBounds = false };
                volTextCanvas.Children.Add(l);

                if (y + 2 >= volTextCanvas.ActualHeight)
                    break;

                TextBlock tb = new TextBlock();
                Canvas.SetRight(tb, 3);
                Canvas.SetTop(tb, y);

                double v = (volTextCanvas.ActualHeight - y) / volTextCanvas.ActualHeight;

                int newamp = v == 0 ? 0 : (int)Math.Round(Decibel.ToAmplitude(v * (MinAmp + Decibel.FromAmplitude((double)0xfffe / 0x4000)) - MinAmp) * 0x4000);

                string dbTxt = newamp > 0 ? string.Format("{0:F0}dB", Decibel.FromAmplitude(newamp * (1.0 / 0x4000))) : "-inf.dB";

                if (y + 18 + 2 >= volTextCanvas.ActualHeight)
                    dbTxt = "-inf.dB";

                tb.Text = dbTxt;
                tb.FontSize = 11;
                tb.Foreground = TryFindResource("TextForeground") as SolidColorBrush;
                volTextCanvas.Children.Add((tb));

                y += 18;
            }
        }

        internal void FillAudioBuffer(float[] samples, int length)
        {

            lock (audioLock)
            {
                float[] intSamples = new float[length];
                for (int i = 0; i < length; i++)
                {
                    intSamples[i] = samples[i] * 32768.0f;
                }
                FillAudioBufferInt(intSamples);
            }
        }

        Lock audioLock = new();
        private void FillAudioBufferInt(float[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                AudioBuffer[audioBufferFillPosition] = samples[i];
                audioBufferFillPosition++;
                audioBufferFillPosition %= AUDIO_BUFFER_SIZE;
            }

            bool stereo = false;
            // VU bars
            if (!stereo) // Mono
            {
                maxSampleL = Math.Max(maxSampleL, AbsMax(samples) * (1.0f / 32768.0f));
                maxSampleR = maxSampleL;
            }
            else
            {
                float[] L = new float[samples.Length / 2];
                float[] R = new float[samples.Length / 2];
                for (int i = 0; i < samples.Length / 2; i++)
                {
                    L[i] = samples[i * 2];
                    R[i] = samples[i * 2 + 1];
                }

                maxSampleL = Math.Max(maxSampleL, AbsMax(L) * (1.0f / 32768.0f));
                maxSampleR = Math.Max(maxSampleR, AbsMax(R) * (1.0f / 32768.0f));
            }
        }

        public static float AbsMax(float[] samples)
        {
            float ms = 0.0f;

            for (int i = 0; i < samples.Length; i++)
            {
                float x = Math.Abs(samples[i]);
                if (x > ms) ms = x;
            }

            return ms;
        }

        private double[] GetBuffer(int buf_size, int channel)
        {
            lock (audioLock)
            {
                //int buf_size = FFT_SIZE;
                double[] ret = new double[buf_size];

                int pos = (((audioBufferFillPosition - buf_size * 2) % AUDIO_BUFFER_SIZE + AUDIO_BUFFER_SIZE)) % AUDIO_BUFFER_SIZE;

                for (int i = 0; i < buf_size; i++)
                {
                    if (channel < 2)
                    {
                        ret[i] = AudioBuffer[(pos + channel) % AUDIO_BUFFER_SIZE];
                    }
                    else
                    {
                        ret[i] = (AudioBuffer[pos % AUDIO_BUFFER_SIZE] + AudioBuffer[(pos + 1) % AUDIO_BUFFER_SIZE]) / 2.0;
                    }

                    pos += 2;
                }

                return ret;
            }
        }
    }

    public static class Decibel
    {
        public static double FromAmplitude(double a) { return Math.Log10(a) * 20.0; }
        public static double ToAmplitude(double db) { return Math.Pow(10, db * 0.05); }
    }

    public static class PropertyChangedRaiser
    {
        public static void Raise(this PropertyChangedEventHandler p, INotifyPropertyChanged x, string propertyname)
        {
            if (p != null)
                p(x, new PropertyChangedEventArgs(propertyname));
        }

        public static void RaiseAll(this PropertyChangedEventHandler p, INotifyPropertyChanged x)
        {
            if (p == null) return;

            foreach (var pi in x.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                p(x, new PropertyChangedEventArgs(pi.Name));
        }

    }
}

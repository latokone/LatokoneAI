using GitHub.secile.Video;
using LatokoneAI.Common.Interfaces;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static LatokoneAI.Common.AcceleratorTypes;

namespace WPFExample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        LatokoneAI.Engine.Engine latokoneAI;
        private ISpeechToText sttPlugin;
        private ILlmPlugin llmPlugin;
        private ITextToSpeech ttsPlugin;
        private IObjectDetection objectDetectionPlugin;

        internal LatokoneAI.Engine.Engine Kamu { get => latokoneAI; set => latokoneAI = value; }

        UsbCamera camera;
        UsbCamera Camera { get => camera; }

        BitmapSource objectDetectionImage;
        public BitmapSource ObjectDetectionImage
        {
            get => objectDetectionImage;
            set
            {
                objectDetectionImage = value;
                PropertyChanged?.Raise(this, "ObjectDetectionImage");
            }
        }
        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();

            visualizerOut.SetBlueTheme();

            latokoneAI = new LatokoneAI.Engine.Engine();
            latokoneAI.AudioEngine.CreateWasapiOut();

            sttPlugin = latokoneAI.CreateSpeechToTextPlugin(@"..\..\Plugins\WhisperProcessPlugin\WhisperProcessPlugin.exe", "WhisperPlugin", 0, latokoneAI.AudioEngine.SampleRateIn);
            sttPlugin.WithSetting([Accelerator.Vulcan, Accelerator.Cpu]);
            sttPlugin.InitializeAndRun();

            llmPlugin = latokoneAI.CreateLLMPlugin(@"..\..\Plugins\LlamaChatProcessPlugin\LlamaChatProcessPlugin.exe", "LlamaPlugin");
            llmPlugin.WithSetting([Accelerator.Cpu, Accelerator.Vulcan]).
                WithSetting(CommonPluginSetting.ModelPath, @"D:\Downloads\Models\Distill-Qwen-7B-Uncensored.i1-Q4_K_M.gguf");
            llmPlugin.InitializeAndRun();

            ttsPlugin = latokoneAI.CreateTextToSpeechPlugin(@"..\..\Plugins\KokoroProcessPlugin\KokoroProcessPlugin.exe", "KokoroPlugin", 0, latokoneAI.AudioEngine.SampleRate);
            ttsPlugin.InitializeAndRun();

            objectDetectionPlugin = latokoneAI.CreateVisualPlugin(@"..\..\Plugins\YoloProcessPlugin\YoloProcessPlugin.exe", "YoloPlugin");
            objectDetectionPlugin.InitializeAndRun();

            latokoneAI.AudioEngine.Play();

            Loaded += (s, e) =>
            {
                sttPlugin.TextRecognized += Whisper_WhisperTextAdded;
                llmPlugin.ResponseReceived += ChatLlm_ResponseReceived;
                tbUserInput.KeyDown += TbUserInput_KeyDown;

                StartCamera();
            };

            Unloaded += (s, e) =>
            {
                sttPlugin.TextRecognized -= Whisper_WhisperTextAdded;
                llmPlugin.ResponseReceived -= ChatLlm_ResponseReceived;
                tbUserInput.KeyDown -= TbUserInput_KeyDown;
            };

            this.Closed += (s, e) =>
            {
                latokoneAI.Dispose();
                camera?.Release();
            };

            latokoneAI.AudioReceived += Kamu_AudioReceived;
            latokoneAI.AudioOutputted += Kamu_AudioOutputted;
        }

        bool cameraCaptureProcessed = true;
        SKBitmap bitmap;
        SKBitmap result;
        void StartCamera()
        {
            // check USB camera is available.
            string[] devices = UsbCamera.FindDevices();
            if (devices.Length == 0) return; // no camera.

            // get video format.
            var cameraIndex = 0;
            var formats = UsbCamera.GetVideoFormat(cameraIndex);

            // select the format you want.
            foreach (var item in formats) Console.WriteLine(item);
            // for example, video format is like as follows.
            // 0:[Video], [MJPG], {Width=1280, Height=720}, 333333, 30fps, [VideoInfo], ...
            // 1:[Video], [MJPG], {Width=320, Height=180}, 333333, 30fps, [VideoInfo], ...
            // 2:[Video], [MJPG], {Width=320, Height=240}, 333333, 30fps, [VideoInfo], ...
            // ...
            var format = formats[1];

            // create instance.
            camera = new UsbCamera(cameraIndex, format);

            camera.PreviewCaptured += (bmpImg) =>
            {
                if (!cameraCaptureProcessed)
                    return;

                cameraCaptureProcessed = false;

                Task.Run(() =>
                {
                    bitmap = CreateImage((byte[])bmpImg, format.Size.Width, format.Size.Height);
                    result = objectDetectionPlugin.DoDetect(bitmap);

                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        //using var skBitmap = WPFExtensions.ToSKBitmap(bmpImg);


                        ImageSource imageSource = WPFExtensions.ToWriteableBitmap(result);


                        if (result != null)
                        {
                            ObjectDetectionImage = imageSource as BitmapSource;
                            bitmap?.Dispose();
                            result?.Dispose();
                            bitmap = null;
                            result = null;
                        }

                        cameraCaptureProcessed = true;
                    });
                });

            };

            camera.Start();
        }

        private SKBitmap CreateImage(byte[] imageBuffer, int width, int height)
        {
            // Create SKBitmap from encoded barcode buffer
            SKBitmap bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

            // Lock the bitmap's pixels
            IntPtr pixels = bitmap.GetPixels();
            int buffSize = bitmap.Height * bitmap.RowBytes;
            byte[] pixelBuffer = new byte[buffSize];

            int i = width * height * 3 - 1;//0;
            int x = 0;
            int padding = bitmap.RowBytes - (4 * width);

            // Copy native pixel buffer into managed buffer, one scan line at a time
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    pixelBuffer[x++] = imageBuffer[i--];
                    pixelBuffer[x++] = imageBuffer[i--];
                    pixelBuffer[x++] = imageBuffer[i--];
                    pixelBuffer[x++] = 255;
                }
                x += padding;
            }

            // Copy the managed buffer to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(pixelBuffer, 0, pixels, buffSize);
            return bitmap;
        }

        private void Kamu_AudioOutputted(float[] buffer, int length)
        {
            visualizerOut.FillAudioBuffer(buffer, length);
        }

        private void Kamu_AudioReceived(float[] buffer, int length)
        {
            visualizerIn.FillAudioBuffer(buffer, length);
        }

        private void TbUserInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var txt = tbUserInput.Text.Trim();
                var command = txt.ToLower().Trim();
                if (command == "seis")
                {
                    llmPlugin.StopTalking();
                    ttsPlugin.StopTalking();
                }
                else if (command == "uusi")
                {
                    llmPlugin.StopTalking();
                    llmPlugin.ClearHistory();
                }
                else if (command == "cls")
                {
                    tbChat.Text = "";
                    tbUserInput.Text = "";
                    tbWhistper.Text = "";
                    e.Handled = true;
                    return;
                }
                else
                {
                    llmPlugin.UserInput(command);
                    tbChat.Text += command + "\n\n";
                }
                tbUserInput.Text = "";
                e.Handled = true;
            }
        }

        private void ChatLlm_ResponseReceived(string txt)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                txt = txt.Replace("[/INST]", "");
                tbChat.Text += txt;
                tbChat.ScrollToEnd();
                ttsPlugin.AddPartOfASentence(txt);
            });
        }

        string[][] controlWords =
        {
            new string[] {"stop", "new", "hey felix"},
            new string[] {"seis", "uusi", "hey fexix" }
        };

        int language = 0;
        bool listeningQuestion = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Whisper_WhisperTextAdded(string txt)
        {
            // Remove substring (*)
            txt = Regex.Replace(txt, @"\[[^\]]*\]", "").Trim();
            // Remove substring [*]
            txt = Regex.Replace(txt, @"\s*\(.*?\)", "").Trim();
            if (txt.Length == 0)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                tbWhistper.Text += txt + "\n";
                tbWhistper.ScrollToEnd();

                var command = txt.ToLower().Replace(".", "").Replace("!", "").Replace("?", "").Trim();

                if (command == controlWords[language][0])
                {
                    llmPlugin.StopTalking();
                    ttsPlugin.StopTalking();
                }
                else if (command == controlWords[language][1])
                {
                    llmPlugin.ResetState();
                    ttsPlugin.StopTalking();
                }
                else if (command == controlWords[language][2])
                {
                    listeningQuestion = true;
                    // kamu.piperEngine.AddWork("Yes?");
                }
                else
                {
                    if (listeningQuestion)
                    {
                        llmPlugin.UserInput(txt);
                        listeningQuestion = false;
                    }
                }
            });
        }
    }
}
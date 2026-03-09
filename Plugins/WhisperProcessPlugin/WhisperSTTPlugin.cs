using LatokoneAI.Common;
using LatokoneAI.Common.Audio;
using LatokoneAI.Common.Interfaces;
using LatokoneAI.Common.WindowsRegistry;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;
using static LatokoneAI.Common.AcceleratorTypes;

namespace WhisperProcessPlugin
{
    public class WhisperSTTPlugin
    {
        tiesky.com.ISharm sm = null;

        static WhisperSTTPlugin whisperSTTPlugin;

        RealTimeResampler realTimeResampler = new();
        WhisperFactory? whisperFactory;
        WhisperProcessor? processor;

        internal Lock bufferLock = new();

        internal List<WhisperModel> Models { get; set; }
        Task workTask;
        bool stopWork = false;

        private int sampleRate;
        private bool skip;
        private bool modelReady;
        readonly ManualResetEvent workAvailable = new(false);

        public bool Process { get; set; }

        public event Action ModelLoadingCompeleted;
        public event Action BufferMaxed;

        int selectedModel = 2;
        internal int SelectedModel { get => selectedModel; }

        static void Main(string[] args)
        {
            int sampleRate = 44100;
            int modelIndex = 0;
            string ipcID = "WhisperPlugin";

            IConfiguration config = new ConfigurationBuilder()
                        .AddCommandLine(args)
                        .Build();

            sampleRate = config["SampleRate"] != null ? int.Parse(config["SampleRate"]) : sampleRate;
            modelIndex = config["ModelIndex"] != null ? int.Parse(config["ModelIndex"]) : modelIndex;
            ipcID = config["IpcID"] ?? ipcID;

            var accPriority = config["AcceleratiorPriority"];
            if (accPriority != null)
            {
                List<RuntimeLibrary> libs = new List<RuntimeLibrary>();

                foreach (var item in accPriority.Split(','))
                {
                    if (Enum.TryParse(item.Trim(), out RuntimeLibrary lib))
                    {
                        libs.Add(lib);
                    }
                }

                RuntimeOptions.RuntimeLibraryOrder = libs;
            }

            whisperSTTPlugin = new WhisperSTTPlugin(ipcID, modelIndex, sampleRate);
        }

        public WhisperSTTPlugin(string ipcID, int modelIndex, int sampleRate)
        {

            this.sampleRate = sampleRate;
            selectedModel = modelIndex;

            workTask = Task.Factory.StartNew(AudioWork, TaskCreationOptions.LongRunning);

            realTimeResampler.Reset(16000, sampleRate);

            if (sm == null)
            {
                sm = new tiesky.com.SharmNpc(ipcID, tiesky.com.SharmNpcInternals.PipeRole.Client, this.AsyncRemoteCallHandler, externalProcessing: false);
            }

            while (true)
            {
                Thread.Sleep(100);
            }
        }

        private Tuple<bool, byte[]> AsyncRemoteCallHandler(byte[] data)
        {
            SttPluginIPCMessageType messageType = (SttPluginIPCMessageType)BitConverter.ToInt32(data, 0);

            switch (messageType)
            {
                case SttPluginIPCMessageType.TextRecognized:
                    // This case is not used because we are using event to send text to main process, but you can implement it if you want to use IPC for sending text
                    break;
                case SttPluginIPCMessageType.Release:
                    Dispose();
                    break;
                case SttPluginIPCMessageType.ProcessAudioBuffer:
                    int count = BitConverter.ToInt32(data, 4);
                    float[] buffer = new float[count];

                    Buffer.BlockCopy(
                        data,                         // source array
                        8,                              // source offset in bytes
                        buffer,                        // destination array
                        0,                              // destination offset in bytes
                        count * 4                       // number of bytes to copy
                    );

                    AudioReceived(buffer, count);
                    break;
                case SttPluginIPCMessageType.Initialize:
                    InitWhisper();
                    InitCapture();
                    break;
                case SttPluginIPCMessageType.Setting:
                    CommonPluginSetting setting = (CommonPluginSetting)BitConverter.ToInt32(data, 4);
                    string accs = Encoding.UTF8.GetString(data, 8, data.Length - 8);
                    HandleSetting(setting, accs);
                    break;
            }

            return Tuple.Create(false, new byte[0]);
        }

        void HandleSetting(CommonPluginSetting setting, string accs)
        {
            switch (setting)
            {
                case CommonPluginSetting.AcceleratiorPriority:
                    List<Accelerator> apList = new List<Accelerator>();
                    foreach (string ac in accs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Enum.TryParse(ac, out Accelerator aEnum))
                        {
                            apList.Add(aEnum);
                        }
                    }
                    acceleratorPriority = apList.ToArray();

                    break;
                case CommonPluginSetting.ModelPath:
                    // whisperModel = new WhisperModel() { Ggml = GgmlType.Base, Name = "Base", Filename = Path.Combine(dir, "ggml-base.bin") };
                    break;
            }
        }

        public async void InitWhisper()
        {
            skip = true;

            string dir = ModelPath;

            Models = new List<WhisperModel>()
            {
                new WhisperModel() { Ggml = GgmlType.Base, Name = "Base", Filename = Path.Combine(dir, "ggml-base.bin") },
                new WhisperModel() { Ggml = GgmlType.Base, Name = "Base", Filename = Path.Combine(dir, "ggml-base.bin") },
                new WhisperModel() { Ggml = GgmlType.BaseEn, Name = "Base.En", Filename = Path.Combine(dir, "ggml-base.en.bin") },
                //new WhisperModel() { Ggml = GgmlType.Small, Name = "Small", Quantization = QuantizationType.Q8_0, Filename = Path.Combine(dir, "Q8_0", "ggml-small.bin") },
				new WhisperModel() { Ggml = GgmlType.Small, Name = "Small", Filename = Path.Combine(dir, "ggml-small.bin") },
                new WhisperModel() { Ggml = GgmlType.SmallEn, Name = "Small.En", Filename = Path.Combine(dir, "ggml-small.en.bin") },
                new WhisperModel() { Ggml = GgmlType.LargeV3Turbo, Name = "LargeV3Turbo", Filename = Path.Combine(dir, "ggml-largev3-turbo.bin") },
                new WhisperModel() { Ggml = GgmlType.Tiny, Name = "Tiny Fin", Filename = Path.Combine(dir, "ggml-model-fi-tiny.bin") },
                new WhisperModel() { Ggml = GgmlType.Medium, Name = "Medium Fin", Filename = Path.Combine(dir, "ggml-model-fi-medium.bin") }
            };

            lock (bufferLock)
            {
                ReleaseWhisper();
                // We declare three variables which we will use later, ggmlType, modelFileName and wavFileName
                var model = Models[SelectedModel];

                // Optional set the order of the runtimes:
                // RuntimeOptions.Instance.SetRuntimeLibraryOrder([RuntimeLibrary.OpenVino, RuntimeLibrary.Cpu]);

                // This section detects whether the "ggml-base.bin" file exists in our project disk. If it doesn't, it downloads it from the internet
                if (!File.Exists(model.Filename))
                {
                    return;
                }

                modelReady = true;

                // This section creates the whisperFactory object which is used to create the processor object.
                whisperFactory = WhisperFactory.FromPath(model.Filename);

                // This section creates the processor object which is used to process the audio file, it uses language `auto` to detect the language of the audio file.
                if (Translate)
                {
                    processor = whisperFactory.CreateBuilder()
                        .WithLanguage("auto").WithSegmentEventHandler(SegmentEventHandler).WithTranslate().WithProbabilities()
                        .Build();
                }
                else
                {
                    processor = whisperFactory.CreateBuilder()
                        .WithLanguage("auto").WithSegmentEventHandler(SegmentEventHandler).WithProbabilities()
                        .Build();
                }
            }
            skip = false;

            Process = true;
        }

        internal void InitCapture()
        {
            lock (bufferLock)
            {
                ReleaseCapture();
            }
        }

        internal void ReleaseCapture()
        {
        }

        public event Action<string> TextRecognized;
        void SegmentEventHandler(SegmentData segment)
        {
            //kamu.DCWriteLine($"{segment.Start}->{segment.End}: {segment.Text}");

            //Application.Current.Dispatcher.BeginInvoke(() =>
            //{
            string s = segment.Text.Trim();
            sm.RemoteRequest(IPCMessage.CreateMessage((int)SttPluginIPCMessageType.TextRecognized, s));
            //kamu.DCWriteLine(s);
            //});
        }

        float[] bufferForWhisper = new float[16000 * 10];  // At least 10 secs, because there is no continuous stream option?
        int outputBufferFillIndex = 0;

        ConcurrentQueue<float[]> audioWorkList = new();
        float silenceInSeconds = 0;
        bool audioInputAvailable = false;
        void AudioWork()
        {
            while (!stopWork)
            {
                workAvailable.WaitOne();

                while (audioWorkList.Count > 0 && !skip)
                {
                    // Clear to-do list if system is too slow
                    if (audioWorkList.Count > 1000)
                    {
                        BufferMaxed?.Invoke();

                        audioWorkList.Clear();
                        break;
                    }

                    lock (bufferLock)
                    {
                        // Get stereo samples
                        if (processor != null && audioWorkList.TryDequeue(out float[] inputBuffer))
                        {
                            float div = 16000f / sampleRate;
                            int n = (int)Math.Ceiling(inputBuffer.Length * div) & ~1;

                            // Resampler class works in stereo. Convert to 16000 Hz to be used by whisper
                            realTimeResampler.FillBuffer(inputBuffer, inputBuffer.Length / 2);
                            float[] outputBuffer = new float[n];
                            bool ok = realTimeResampler.GetSamples(outputBuffer, 0, outputBuffer.Length / 2);

                            // Wait until silence ends and then start to copy to buffer? Save atleast 5 seconds and then wait until there is silence?
                            if (ok)
                            {
                                if (IsSilent(outputBuffer))
                                {
                                    silenceInSeconds += n / 16000f;
                                    if (silenceInSeconds >= 1)
                                    {
                                        if (audioInputAvailable)
                                            sendAudio = true;
                                        audioInputAvailable = false;
                                    }
                                }
                                else
                                {
                                    silenceInSeconds = 0;
                                    audioInputAvailable = true;
                                }

                                if (sendAudio)
                                {
                                    float[] cleanedBuffer = new float[outputBufferFillIndex];
                                    for (int i = 0; i < outputBufferFillIndex; i++)
                                    {
                                        cleanedBuffer[i] = bufferForWhisper[i];
                                    }
                                    processor.Process(cleanedBuffer);
                                    outputBufferFillIndex = 0;
                                    sendAudio = false;
                                }

                                if (audioInputAvailable)
                                {
                                    for (int i = 0; i < n;)
                                    {
                                        // Conver to mono
                                        bufferForWhisper[outputBufferFillIndex] = (outputBuffer[i] + outputBuffer[i + 1]) / 2f;

                                        outputBufferFillIndex++;
                                        if (outputBufferFillIndex >= bufferForWhisper.Length)
                                        {
                                            outputBufferFillIndex = bufferForWhisper.Length;
                                            sendAudio = true;
                                            break;
                                        }
                                        i += 2;
                                    }
                                }
                            }
                        }
                    }
                }

                workAvailable.Reset();
            }
        }

        private void AudioReceived(float[] buffer, int n)
        {
            if (Process == false || skip)
                return;
            // Split into smaller buffers
            int offset = 0;
            do
            {
                var size = Math.Min(n, 512);
                float[] inputBuffer = new float[size];
                for (int i = 0; i < size; i++)
                {
                    inputBuffer[i] = buffer[i + offset];
                }

                audioWorkList.Enqueue(inputBuffer);
                workAvailable.Set();
                n -= size;
                offset += size;
            } while (n > 0);
        }

        int silenceThreshold = -40;
        private bool sendAudio;


        public int SilenceThreshold
        {
            get => silenceThreshold;
            set
            {
                silenceThreshold = value;
            }
        }
        public bool IsSilent(float[] buffer)
        {
            const int maxAmplitude = 32767;
            int silenceThresholdDb = this.silenceThreshold;
            int sum = 0;
            int numSamples = 0;

            for (int i = 0; i < buffer.Length; ++i)
            {
                var sample = (short)(buffer[i] * maxAmplitude);
                sum += sample * sample;
                numSamples++;
            }

            double rms = Math.Sqrt((double)sum / numSamples);
            double db = 20.0 * Math.Log10(rms / 32767.0);
            return db < silenceThresholdDb;
        }

        public void Dispose()
        {
            stopWork = true;
            workAvailable.Set();

            lock (bufferLock)
            {
                ReleaseCapture();
                realTimeResampler.Dispose();
                ReleaseWhisper();
            }
        }

        void ReleaseWhisper()
        {
            skip = true;

            lock (bufferLock)
            {
                modelReady = false;

                if (processor != null)
                {
                    processor.DisposeAsync();
                    processor = null;
                }

                if (whisperFactory != null)
                {
                    whisperFactory.Dispose();
                    whisperFactory = null;
                }
            }
        }

        public string ModelPath
        {
            get
            {
                var defaultDir = Path.Combine(AppContext.BaseDirectory, "Models", "Whisper");
                return RegistryEx.Read("ModelPathWhisper", defaultDir, "Models");
            }
            internal set
            {
                RegistryEx.Write("ModelPath", value, "Whisper");
            }
        }

        private bool translate = false;
        private Accelerator[] acceleratorPriority;
        private WhisperModel whisperModel;

        public bool Translate { get => translate; }
    }

    public class WhisperModel
    {
        public string Name { get; set; }
        public string Filename { get; set; }

        public GgmlType Ggml { get; set; }

        public QuantizationType Quantization { get; set; }

        public override string ToString() { return Name; }
    }
}

using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Utilities;
using LatokoneAI.Common.Audio;
using LatokoneAI.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using static LatokoneAI.Common.AcceleratorTypes;

namespace KokoroProcessPlugin
{
    public class KokoroTTSProcessPlugin
    {
        tiesky.com.ISharm sm = null;

        static KokoroTTSProcessPlugin kokoroTTSProcessPlugin;

        KokoroTTS tts;
        RealTimeResampler realTimeResampler = new();
        float ratio = 1;

        int modelIndex = 0;

        KokoroWavSynthesizer kokoroWavSynthesizer;

        Lock audioLock = new();

        ConcurrentQueue<string> workItems = new();
        ConcurrentQueue<Task<byte[]>> piperTasks = new ConcurrentQueue<Task<byte[]>>();
        private int playOffset;
        byte[]? result = null;

        KokoroVoice heartVoice;

        static void Main(string[] args)
        {
            int sampleRate = 44100;
            int modelIndex = 0;
            string ipcID = "KokoroProcessPlugin";

            IConfiguration config = new ConfigurationBuilder()
                        .AddCommandLine(args)
                        .Build();

            sampleRate = config["SampleRate"] != null ? int.Parse(config["SampleRate"]) : sampleRate;
            modelIndex = config["ModelIndex"] != null ? int.Parse(config["ModelIndex"]) : modelIndex;
            ipcID = config["IpcID"] ?? ipcID;

            kokoroTTSProcessPlugin = new KokoroTTSProcessPlugin(ipcID, modelIndex, sampleRate);
        }

        public KokoroTTSProcessPlugin(string ipcID, int modelIndex, int sampleRate)
        {
            this.ipcID = ipcID;

            this.modelIndex = modelIndex;

            ratio = KokoroPlayback.waveFormat.SampleRate / (float)sampleRate;
            realTimeResampler.Reset(sampleRate, KokoroPlayback.waveFormat.SampleRate);

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
            TtsPluginIPCMessageType messageType = (TtsPluginIPCMessageType)BitConverter.ToInt32(data, 0);
            switch (messageType)
            {
                case TtsPluginIPCMessageType.FillBuffer:
                    // Handle audio data request
                    int count = BitConverter.ToInt32(data, 4);
                    byte[] message = new byte[4 + 4 + count * 4];
                    float[] buffer = new float[count];
                    this.FillBuffer(buffer, 0, count);

                    BitConverter.GetBytes((int)TtsPluginIPCMessageType.FillBuffer).CopyTo(message, 0);
                    BitConverter.GetBytes(count).CopyTo(message, 4);

                    Buffer.BlockCopy(
                        buffer,                         // source array
                        0,                              // source offset in bytes
                        message,                        // destination array
                        8,                              // destination offset in bytes
                        count * 4                       // number of bytes to copy
                    );

                    return Tuple.Create(true, message);
                    break;
                case TtsPluginIPCMessageType.AddPartOfASentence:
                    string txt = System.Text.Encoding.UTF8.GetString(data, 4, data.Length - 4);
                    this.AddPartOfASentence(txt);
                    break;
                case TtsPluginIPCMessageType.StopTalking:
                    this.StopTalking();
                    break;
                case TtsPluginIPCMessageType.Init:
                    this.Init();
                    break;
                case TtsPluginIPCMessageType.Start:
                    this.Start();
                    break;
                case TtsPluginIPCMessageType.Release:
                    this.Dispose();
                    break;
                case TtsPluginIPCMessageType.Setting:
                    CommonPluginSetting setting = (CommonPluginSetting)BitConverter.ToInt32(data, 4);
                    string accs = Encoding.UTF8.GetString(data, 8, data.Length - 8);
                    WithSetting(setting, accs);
                    break;
            }

            return Tuple.Create(false, new byte[0]);
        }

        public ITextToSpeech WithSetting(CommonPluginSetting setting, string accs)
        {
            switch (setting)
            {
                case CommonPluginSetting.ModelBasePath:
                    modelBasePath = accs;
                    break;
            }

            return null;
        }

        internal void AddWork(string txt, bool continieTalkingIfStopped = true)
        {
            workItems.Enqueue(txt);

            if (continieTalkingIfStopped && playOffset == -1)
            {
                StartNextWorkItem();
            }
        }

        internal async void StartNextWorkItem()
        {
            lock (audioLock)
            {
                result = null;
                playOffset = -1;
            }

            bool success = workItems.TryDequeue(out string? txt);

            if (kokoroWavSynthesizer != null && success)
            {
                lock (audioLock)
                {
                    playOffset = 0;
                }

                // Generate audio, currently supported formats are Mp3, Wav, Raw
                piperTasks.Enqueue(kokoroWavSynthesizer.SynthesizeAsync(txt, heartVoice));
            }
        }

        string sentenceString = "";

        public void AddPartOfASentence(string txt)
        {
            //lock (audioLock)
            {
                sentenceString += txt;
                sentenceString = sentenceString.Replace("User:", "").Replace("Järjestelmä:", "").Replace("Assistant:", "").Replace("System:", "").Replace("Avustaja:", "");
                if (sentenceString.EndsWith(".") || sentenceString.EndsWith("!") ||
                    sentenceString.EndsWith("?") || sentenceString.Contains("\n"))
                {
                    var nextWork = sentenceString.Replace("*", "").Replace("#", "").Trim();
                    if (nextWork.Length > 0)
                    {
                        AddWork(nextWork);
                    }
                    else
                    {
                        // No work to add.
                    }
                    sentenceString = "";
                }
            }
        }

        float audioOutMul = 1 / 32768.0f;
        private string ipcID;
        private string modelBasePath;

        public void FillBuffer(float[] buffer, int offset, int count)
        {

            lock (audioLock)
            {
                bool txtDone = false;
                int sampleCount = count / 2;
                if (result != null && playOffset >= 0)
                {
                    int j = 0;
                    while (realTimeResampler.AvailableSamples() < sampleCount)
                    {
                        // How many samples to read from result
                        int fromSamplesCount = (int)(sampleCount * ratio);
                        // 16 bit mono
                        if (playOffset + fromSamplesCount < result.Length / 2)
                        {
                            // Stereo buffer
                            float[] fBuf = new float[fromSamplesCount * 2];

                            j = 0;
                            for (int i = 0; i < fromSamplesCount; i++)
                            {
                                short sample = BitConverter.ToInt16(result, 2 * playOffset);
                                fBuf[j++] = sample;
                                fBuf[j++] = sample;
                                playOffset++;
                            }
                            realTimeResampler.FillBuffer(fBuf, fromSamplesCount);
                        }
                        else
                        {
                            var fillZeroSamples = playOffset + fromSamplesCount - result.Length / 2;
                            // Stereo buffer
                            float[] fBuf = new float[fillZeroSamples * 2];
                            realTimeResampler.FillBuffer(fBuf, fillZeroSamples);
                            txtDone = true;
                        }
                    }

                    float[] rStereoBuf = new float[count];
                    realTimeResampler.GetSamples(rStereoBuf, 0, sampleCount);

                    // Copy to stereo buffer
                    j = 0;
                    for (int i = 0; i < count; i++)
                    {
                        buffer[j++] = rStereoBuf[i] * audioOutMul;
                    }

                    //sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)TtsPluginIPCMessageType.AudioOutputAvailable, buffer, count));
                    //kamu.AudioOutputAvailable(buffer, count);

                    if (txtDone)
                    {
                        playOffset = -1;
                        result = null;
                        StartNextWorkItem();
                    }
                }
                else if (realTimeResampler.AvailableSamples() > 0)
                {
                    float[] rStereoBuf = new float[count];
                    int readSize = Math.Min(realTimeResampler.AvailableSamples(), sampleCount);
                    realTimeResampler.GetSamples(rStereoBuf, 0, readSize);
                    // Copy to stereo buffer
                    int j = 0;
                    for (int i = 0; i < readSize * 2; i++)
                    {
                        buffer[j++] = rStereoBuf[i] * audioOutMul;
                    }
                    for (int i = readSize * 2; i < count; i++)
                    {
                        buffer[j++] = 0;
                    }
                    //sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)TtsPluginIPCMessageType.AudioOutputAvailable, buffer, count));
                }
                else
                {
                    var t = piperTasks.FirstOrDefault();
                    if (t != null && t.IsCompletedSuccessfully)
                    {
                        result = t.Result;
                        piperTasks.TryDequeue(out _);
                        playOffset = 0;
                    }
                }
            }
        }

        public void Init()
        {
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var defaultDir = modelBasePath != null ? Path.Combine(modelBasePath, "Models", "kokoro.onnx") : Path.Combine(assemblyPath, "Models", "kokoro.onnx");
            tts = KokoroTTS.LoadModel(defaultDir);

            kokoroWavSynthesizer = new KokoroWavSynthesizer(defaultDir);
            var defaultDirVoices = modelBasePath != null ? Path.Combine(modelBasePath, "Voices") : Path.Combine(assemblyPath, "Voices");
            KokoroVoiceManager.LoadVoicesFromPath(defaultDirVoices);
            heartVoice = KokoroVoiceManager.GetVoice("af_heart");
        }

        public void Dispose()
        {
            realTimeResampler?.Dispose();
        }

        public void Start()
        {
            StartNextWorkItem();
        }

        public void StopTalking()
        {
            lock (audioLock)
            {
                result = null;
                playOffset = -1;
                workItems.Clear();
                sentenceString = "";
                foreach (var t in piperTasks)
                {
                    t.Dispose();
                }
                piperTasks.Clear();

                sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)TtsPluginIPCMessageType.AudioOutputAvailable, new float[40000], 40000));
            }
        }

        public void InitializeAndRun()
        {
            Init();
            Start();
        }
    }
}

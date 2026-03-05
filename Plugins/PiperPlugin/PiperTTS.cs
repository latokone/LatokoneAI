using PiperSharp;
using PiperSharp.Models;
using SharpCompress;
using System.Collections.Concurrent;
using System.Reflection;
using KamuCommon.Audio;
using KamuCommon.Interfaces;
using KamuCommon.WindowsRegistry;

namespace PiperPlugin
{
    public class PiperTTS : ITextToSpeech
    {
        private IKamuAI kamu;
        VoiceModel? model = null;
        PiperProvider? piperModel = null;

        RealTimeResampler realTimeResampler = new();
        private int playOffset;
        byte[]? result = null;
        float ratio = 1;
        int modelIndex = 0;

        ConcurrentQueue<string> workItems = new();

        Lock audioLock = new();
        public PiperTTS(IKamuAI kamu, int modelIndex, int sampleRate)
        {
            this.kamu = kamu;
            this.modelIndex = modelIndex;
            ratio = 22050f / sampleRate;
            realTimeResampler.Reset(sampleRate, 22050);
        }

        public void Init()
        {
            InitModelAsync();
        }

        public void Start()
        {
            StartNextWorkItem();
        }

        internal async Task InitModelAsync()
        {
            string[] piperModels = {
                Path.Combine(ModelPath, "en_US-lessac-high"),
                Path.Combine(ModelPath, "fi_FI-harri-medium"),
            };
            int piperModelNumber = modelIndex;
            string useUserDefinedLocalPath = piperModels[piperModelNumber];
            model = await VoiceModel.LoadModel(useUserDefinedLocalPath);

            if (model == null)
                return;

            //var cwd = Directory.GetCurrentDirectory();
            var cwd = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var piperExe = Path.Combine(cwd, "piper", "piper.exe");
            if (!File.Exists(piperExe))
            {
                kamu.DCWriteLine("Piper needs piper.exe to run.\n\n1. Go to: https://github.com/rhasspy/piper/releases/latest\n2. Download and unzip everything to ReBuzz folder.\n3. Ensure your have piper/piper.exe in ReBuzz folder.");
                return;
            }

            piperModel = new PiperProvider(new PiperConfiguration()
            {
                ExecutableLocation = Path.Join(cwd, "piper", "piper.exe"), // Path to piper executable
                WorkingDirectory = Path.Join(cwd, "piper"), // Path to piper working directory
                Model = model, // Loaded/downloaded VoiceModel
                SpeakingRate = 1,
            });

            return;
        }

        ConcurrentQueue<Task<byte[]>> piperTasks = new ConcurrentQueue<Task<byte[]>>();

        internal async void StartNextWorkItem()
        {
            lock (audioLock)
            {
                result = null;
                playOffset = -1;
            }

            bool success = workItems.TryDequeue(out string? txt);

            if (piperModel != null && success)
            {
                lock (audioLock)
                {
                    playOffset = 0;
                }

                // Generate audio, currently supported formats are Mp3, Wav, Raw
                piperTasks.Enqueue(piperModel.InferAsync(txt, AudioOutputType.Raw));
            }
        }

        public void StopTalking()
        {
            lock (audioLock)
            {
                result = null;
                playOffset = -1;
                workItems.Clear();
                sentenceString = "";
                piperTasks.ForEach(t => t.Dispose());
                piperTasks.Clear();

                kamu.AudioOutputAvailable(new float[40000], 40000);
            }
        }

        internal void AddWork(string txt, bool continieTalkingIfStopped = true)
        {
            workItems.Enqueue(txt);

            if (continieTalkingIfStopped && playOffset == -1)
            {
                StartNextWorkItem();
            }
        }

        float audioOutMul = 1 / 32768.0f;
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

                    kamu.AudioOutputAvailable(buffer, count);

                    if (txtDone)
                    {
                        playOffset = -1;
                        result = null;
                        StartNextWorkItem();
                    }
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

        string sentenceString = "";
        public void AddPartOfASentence(string txt)
        {
            lock (audioLock)
            {
                sentenceString += txt;
                sentenceString = sentenceString.Replace("User:", "").Replace("Järjestelmä:", "").Replace("Assistant:", "").Replace("System:", "").Replace("Avustaja:", "");
                if (sentenceString.EndsWith(".") || sentenceString.EndsWith("!") ||
                    sentenceString.EndsWith("?") || sentenceString.Contains("\n"))
                {
                    AddWork(sentenceString.Replace("*", "").Replace("#", "").Trim());
                    sentenceString = "";
                }
            }
        }

        public void Release()
        {
            realTimeResampler?.Dispose();
        }

        public string ModelPath
        {
            get
            {   
                //var defaultDir = Path.Combine(AppContext.BaseDirectory, "Models", "Piper");
                var defaultDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Models", "Piper");
                return RegistryEx.Read("ModelPath", defaultDir, "Piper");
            }
            internal set
            {
                RegistryEx.Write("ModelPath", value, "Piper");
            }
        }
    }
}

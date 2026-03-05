
using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using LatokoneAI.Engine.Audio;
using LatokoneAI.Engine.PluginHosts.AudioPlugin;
using LatokoneAI.Engine.PluginHosts.LLMPlugins;
using LatokoneAI.Engine.PluginHosts.VisualPlugin;
using System.Reflection;

namespace LatokoneAI.Engine
{
    public class Engine : IKamuAI, IDisposable
    {
        private AudioEngine audioEngine;

        public AudioEngine AudioEngine { get => audioEngine; }

        LLMPluginHost? ollamaPluginHost;

        List<ISpeechToText> speechToTextPlugins = new();
        internal List<ITextToSpeech> textToSpeechPlugins = new();
        List<ILlmPlugin> llmPlugins = new();
        List<IObjectDetection> visualPlugins = new();

        internal ILlmPlugin? chatOllama;

        LlmConfig config = new LlmConfig()
        {
            Models = new List<LlmModel>()
            {
                new() { Url = @"https://huggingface.co/bartowski/Phi-3.5-mini-instruct_Uncensored-GGUF/resolve/main/Phi-3.5-mini-instruct_Uncensored-IQ2_M.gguf", Name = "Ahma-7B-Instruct.Q6_K", Filename = Path.Combine(LlmConfig.ModelPath, "Ahma-7B-Instruct.Q6_K.gguf") },
                new() { Url = @"https://huggingface.co/bartowski/Phi-3.5-mini-instruct_Uncensored-GGUF/resolve/main/Phi-3.5-mini-instruct_Uncensored-Q6_K_L.gguf", Name = "Ahma-7B.Q4_K_S", Filename = Path.Combine(LlmConfig.ModelPath, "Ahma-7B.Q4_K_S.gguf") },
                new() { Url = @"https://huggingface.co/bartowski/Phi-3.5-mini-instruct_Uncensored-GGUF/resolve/main/Phi-3.5-mini-instruct_Uncensored-Q6_K_L.gguf", Name = "Ahma-3B.Q6_K", Filename = Path.Combine(LlmConfig.ModelPath, "Ahma-3B.Q6_K.gguf") },
                new() { Url = @"https://huggingface.co/bartowski/Phi-3.5-mini-instruct_Uncensored-GGUF/resolve/main/Phi-3.5-mini-instruct_Uncensored-Q6_K_L.gguf", Name = "Phi-3.5-mini-instruct_Uncensored-Q6_K_L", Filename = "D:\\Downloads\\Models\\Phi-3.5-mini-instruct_Uncensored-Q6_K_L.gguf" },

            },
            Accelerators = new List<LlmAccelerator>()
            {
                new() { Name = "AVX2", Library = "runtimes\\llama\\win-x64\\native\\avx2\\llama.dll" },
                new() { Name = "Vulcan", Library = "runtimes\\llama\\win-x64\\native\\vulcan\\llama.dll" }
            },
            SelectedModel = 3,
            SelectedAccelerator = 0,

            SelectedLanguage = 0, // English

            SystemRoles = new string[]
            {
                "Transcript of a dialog, where the User interacts with an Assistant named ReBuzz. ReBuzz is helpful, kind, honest, good at writing, and never fails to answer the User's requests immediately and with precision.",
                "Olet tekoälyavustaja. Vastaat aina mahdollisimman avuliaasti mutta lyhyesti. Vastauksesi eivät saa sisältää mitään haitallista, epäeettistä, rasistista, seksististä, vaarallista tai laitonta sisältöä. Jos kysymyksessä ei ole mitään järkeä tai se ei ole asiasisällöltään johdonmukainen, selitä miksi sen sijaan, että vastaisit jotain väärin. Jos et tiedä vastausta kysymykseen, älä kerro väärää tietoa.",
            },

            ChatMessages = new string[][]
            {
                new string[] {
                "Hi Assistant.",
                "Hi. How can I assist you today?",
                },
                new string[] {
                "Hei avustaja.",
                "Hei. Miten voin auttaa sinua tänään?",
                },
            },

            AntiPromptLists = new List<string[]>()
            {
            new string[] { "User" },
            new string[] { "</s>", "[/Inst]", "User:", "Käyttäjä:" }
            },
        };


        AppDomain currentDomain;
        public Engine()
        {
            currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += MyResolveEventHandler;

            audioEngine = new AudioEngine(this);

            Init();
        }

        public ISpeechToText CreateSpeechToTextPlugin(string pluginPath, string ipcID, int deviceIndex,  int sampleRate, IEnumerable<AcceleratorTypes.Accelerator> accelerators)
        {
            var host = new AudioInPluginHost();
            var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID, deviceIndex, sampleRate, accelerators);
            speechToTextPlugins.Add(pluginInstance);
            return pluginInstance;
        }

        public ILlmPlugin CreateLLMPlugin(string pluginPath, string ipcID, IEnumerable<AcceleratorTypes.Accelerator> accelerators)
        {
            var host = new LLMPluginHost();
            var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID, accelerators);
            llmPlugins.Add(pluginInstance);
            return pluginInstance;
        }

        public ITextToSpeech CreateTextToSpeechPlugin(string pluginPath, string ipcID, int modelIndex, int sampleRate, IEnumerable<AcceleratorTypes.Accelerator> accelerators)
        {
            var host = new AudioOutPluginHost();
            var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID, modelIndex, sampleRate, accelerators);
            textToSpeechPlugins.Add(pluginInstance);
            return pluginInstance;
        }

        public IObjectDetection CreateVisualPlugin(string pluginPath, string ipcID, IEnumerable<AcceleratorTypes.Accelerator> accelerators)
        {
            var host = new VisualPluginHost();
            var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID, accelerators);
            visualPlugins.Add(pluginInstance);
            return pluginInstance;
        }

        internal void Init()
        {
        }

        public event Action<float[], int> AudioReceived;
        internal void AudioInputAvalable(float[] samples, int n)
        {
            AudioReceived?.Invoke(samples, n);
        }

        public void DCWriteLine(string v)
        {
            System.Console.WriteLine(v);
        }

        public event Action<float[], int> AudioOutputted;
        public void AudioOutputAvailable(float[] buffer, int count)
        {
            AudioOutputted?.Invoke(buffer, count);
        }

        private Assembly MyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName == args.Name).FirstOrDefault();
            if (loadedAssembly != null)
            {
                return loadedAssembly;
            }

            string strTempAssmbPath = "";

            strTempAssmbPath = args.Name.Substring(0, args.Name.IndexOf(","));

            string folderPath = "";
            string rawAssemblyFile = new AssemblyName(args.Name).Name;
            string rawAssemblyPath = Path.Combine(folderPath, rawAssemblyFile);

            string assemblyPath = rawAssemblyPath + ".dll";
            Assembly assembly = null;

            if (File.Exists(assemblyPath))
            {
                assembly = Assembly.LoadFile(assemblyPath);
            }

            return assembly;
        }

        public void Dispose()
        {
            currentDomain.AssemblyResolve -= MyResolveEventHandler;

            speechToTextPlugins.ForEach(p => p.Dispose());
            speechToTextPlugins.Clear();

            llmPlugins.ForEach(p => p.Dispose());
            llmPlugins.Clear();

            textToSpeechPlugins.ForEach(p => p.Dispose());
            textToSpeechPlugins.Clear();

            visualPlugins.ForEach(p => p.Dispose());
            visualPlugins.Clear();
        }
    }
}

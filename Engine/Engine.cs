
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

        AppDomain currentDomain;
        public Engine()
        {
            currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += MyResolveEventHandler;

            audioEngine = new AudioEngine(this);

            Init();
        }

        public ISpeechToText CreateSpeechToTextPlugin(string pluginPath, string ipcID, int deviceIndex,  int sampleRate)
        {
            var host = new AudioInPluginHost();
            var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID, deviceIndex, sampleRate);
            speechToTextPlugins.Add(pluginInstance);
            return pluginInstance;
        }

        public ILlmPlugin CreateLLMPlugin(string pluginPath, string ipcID)
        {
            var host = new LLMPluginHost();
            var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID);
            llmPlugins.Add(pluginInstance);
            return pluginInstance;
        }

        public ITextToSpeech CreateTextToSpeechPlugin(string pluginPath, string ipcID, int modelIndex, int sampleRate)
        {
            var host = new AudioOutPluginHost();
            var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID, modelIndex, sampleRate);
            textToSpeechPlugins.Add(pluginInstance);
            return pluginInstance;
        }

        public IObjectDetection CreateVisualPlugin(string pluginPath, string ipcID)
        {
            var host = new VisualPluginHost();
            var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID);
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


using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using LatokoneAI.Engine.Audio;
using LatokoneAI.Engine.Common;
using System.Reflection;
using static LatokoneAI.Common.PluginType;

namespace LatokoneAI.Engine
{
    public class Engine : ILatokoneAI, IDisposable
    {
        private AudioEngine audioEngine;

        public AudioEngine AudioEngine { get => audioEngine; }

        ILatokonePlugin? ollamaPluginHost;

        internal List<ILatokonePlugin> speechToTextPlugins = new();
        internal List<ILatokonePlugin> textToSpeechPlugins = new();
        internal List<ILatokonePlugin> llmPlugins = new();
        internal List<ILatokonePlugin> visualPlugins = new();

        internal ILatokonePlugin? chatOllama;

        List<IPluginConnection> connections = new();
        public IEnumerable<IPluginConnection> Connections { get => connections; }

        AppDomain currentDomain;
        public Engine()
        {
            currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += MyResolveEventHandler;

            audioEngine = new AudioEngine(this);

            Init();
        }

        public IPluginConnection ConnectPlugins(ILatokonePlugin from, ILatokonePlugin to)
        {
            var c = new PluginConnection(from, to);
            connections.Add(c);

            return c;
        }

        public void DisconnectPlugins(IPluginConnection connection)
        {
            connections.Remove(connection);
            connection.Release();

        }

        public ILatokonePlugin CreatePlugin(LatokonePluginType type, ILatokonePluginHost host, string pluginPath, string ipcID)
        {
            ILatokonePlugin? plugin = null;

            switch (type)
            {
                case LatokonePluginType.STT:
                    {
                        var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID);
                        speechToTextPlugins.Add(pluginInstance);
                        return pluginInstance;
                    }
                case LatokonePluginType.LLM:
                    {
                        var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID);
                        llmPlugins.Add(pluginInstance);
                        return pluginInstance;
                    }
                case LatokonePluginType.TTS:
                    {
                        var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID);
                        textToSpeechPlugins.Add(pluginInstance);
                        return pluginInstance;
                    }
                case LatokonePluginType.ObjectDetection:
                    {
                        var pluginInstance = host.LoadPlugin(pluginPath, this, ipcID);
                        visualPlugins.Add(pluginInstance);
                        return pluginInstance;
                    }
            }

            return plugin;
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
            foreach (var connection in connections)
            {
                connection.Release();
            }
            connections.Clear();

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

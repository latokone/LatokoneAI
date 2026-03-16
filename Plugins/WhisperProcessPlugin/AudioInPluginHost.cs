using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using LatokoneAI.Common.Messaging;
using System.Diagnostics;
using System.Text;
using static LatokoneAI.Common.PluginType;

namespace WhisperProcessPlugin
{
    public class AudioInPluginHost : ILatokonePluginHost, IDisposable
    {
        internal Process? childProcess;
        public event Action<bool> Connected;
        public event Action<bool> Disconnected;

        AudioInPluginProcess ttsPluginProcess;
        public ILatokonePlugin LoadPlugin(string path, ILatokoneAI engine, string ipcID)
        {
            try
            {
                int modelIndex = 0;
                int sampleRate = 44100;
                ProcessStartInfo processInfo = new ProcessStartInfo(path);
                processInfo.CreateNoWindow = true;
                processInfo.Arguments = $"--IpcID {ipcID} --modelIndex {modelIndex} --SampleRate {sampleRate}";

                childProcess = Process.Start(processInfo);
                childProcess.EnableRaisingEvents = true;

                childProcess.Exited += (sender, e) =>
                {
                    Disconnected.Invoke(true);
                };

                // Track child processes and close them is main app crashes/closes
                ChildProcessTracker.AddProcess(childProcess);

                ttsPluginProcess = new AudioInPluginProcess(engine, ipcID);

                return ttsPluginProcess;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load plugin from {path}: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            try
            {
                if (childProcess != null && !childProcess.HasExited)
                {
                    childProcess.Kill();
                    childProcess.Dispose();
                    childProcess = null;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to end plugin process: {ex.Message}");
            }
        }
    }

    public class AudioInPluginProcess : ILatokonePlugin
    {
        ILatokoneAI host;

        private ILatokoneAI kamuAI;
        tiesky.com.ISharm? sm = null;

        public LatokonePluginType Type {  get; set; }
        public string Name { get; set; }

        public AudioInPluginProcess(ILatokoneAI host, string ipcID)
        {
            this.host = host;
            Name = ipcID;

            Type = LatokonePluginType.STT;

            if (sm != null)
            {
                sm.Dispose();
                sm = null;
            }

            bool connected = false;
            // Server listener
            sm = new tiesky.com.SharmNpc(ipcID, tiesky.com.SharmNpcInternals.PipeRole.Server, this.RemoteCall,
                50000, 100000000,
                (descr, excep) =>
                {

                })
            {
                Verbose = false,
                PeerDisconnected = () =>
                {
                    throw new Exception("Plugin process disconnected");
                },
                PeerConnected = () =>
                {
                    connected = true;
                }
            };

            while (!connected)
                Thread.Sleep(100);
        }

        private void Host_AudioReceived(float[] buffer, int count)
        {
            // Send audio data to plugin process
            var result = sm.RemoteRequest(IPCMessage.CreateMessage((int)SttPluginIPCMessageType.ProcessAudioBuffer, buffer, count));
        }

        private Tuple<bool, byte[]> RemoteCall(byte[] data)
        {
            SttPluginIPCMessageType messageType = (SttPluginIPCMessageType)IPCMessage.GetMessageType(data);

            switch (messageType)
            {
                case SttPluginIPCMessageType.TextRecognized:
                    var text = Encoding.UTF8.GetString(data, 4, data.Length - 4);
                    DataReceived?.Invoke(text);
                    break;
                default:
                    break;
            }

            return Tuple.Create(false, new byte[0]);
        }

        public event Action<object> DataReceived;

        public void Dispose()
        {
            host.AudioReceived -= Host_AudioReceived;
        }

        public ILatokonePlugin WithSetting(AcceleratorTypes.Accelerator[] accelerators)
        {
            var accs = string.Join(",", accelerators);
            WithSetting(CommonPluginSetting.AcceleratiorPriority, accs);
            return this;
        }

        public ILatokonePlugin WithSetting(CommonPluginSetting setting, string value)
        {
            sm.RemoteRequest(IPCMessage.CreateMessage((int)SttPluginIPCMessageType.Setting, (int)setting, value));
            return this;
        }

        public void InitializeAndRun()
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)SttPluginIPCMessageType.Initialize));

            host.AudioReceived += Host_AudioReceived;
        }

        public ILatokonePlugin WithConfig(LlmConfig config)
        {
            return this;
        }

        public void Stop()
        {  
        }
    }
}


using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using System.Diagnostics;
using System.Text;

namespace LatokoneAI.Engine.PluginHosts.AudioPlugin
{
    internal class AudioInPluginHost : IDisposable
    {
        internal Process? childProcess;
        public event Action<bool> Connected;
        public event Action<bool> Disconnected;

        AudioInPluginProcess ttsPluginProcess;
        public ISpeechToText LoadPlugin(string path, Engine kamu, string ipcID, int modelIndex, int sampleRate)
        {
            try
            {   
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

                ttsPluginProcess = new AudioInPluginProcess(kamu, ipcID);

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

    public class AudioInPluginProcess : ISpeechToText
    {
        IKamuAI host;

        private IKamuAI kamuAI;
        tiesky.com.ISharm? sm = null;

        public AudioInPluginProcess(IKamuAI host, string ipcID)
        {
            this.host = host;

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

            host.AudioReceived += Host_AudioReceived;
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
                    TextRecognized?.Invoke(text);
                    break;
                default:
                    break;
            }

            return Tuple.Create(false, new byte[0]);
        }

        public event Action<string> TextRecognized;
        public void Dispose()
        {
            host.AudioReceived -= Host_AudioReceived;
        }

        public ISpeechToText WithSetting(AcceleratorTypes.Accelerator[] accelerators)
        {
            var accs = string.Join(",", accelerators);
            sm.RemoteRequest(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.Setting, (int)CommonPluginSetting.AcceleratiorPriority, accs));
            return this;
        }

        public ISpeechToText WithSetting(CommonPluginSetting setting, string value)
        {
            switch (setting)
            {
                case CommonPluginSetting.ModelPath:
                    sm.RemoteRequest(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.Setting, (int)CommonPluginSetting.ModelPath, value));
                    break;
            }
            return this;
        }

        public void InitializeAndRun()
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)SttPluginIPCMessageType.Initialize));
        }
    }
}


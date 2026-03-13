using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using System.Diagnostics;

namespace LatokoneAI.Engine.PluginHosts.AudioPlugin
{
    internal class AudioOutPluginHost
    {
        internal Process? childProcess;
        public event Action<bool> Connected;
        public event Action<bool> Disconnected;

        AudioOutPluginProcess ttsPluginProcess;
        public ITextToSpeech LoadPlugin(string path, Engine kamu, string ipcID, int modelIndex, int sampleRate)
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

                ttsPluginProcess = new AudioOutPluginProcess(kamu, ipcID);

                return ttsPluginProcess;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load plugin from {path}: {ex.Message}");
                return null;
            }
        }

        public void Release()
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

    public class AudioOutPluginProcess : ITextToSpeech
    {
        ILatokoneAI host;

        private ILatokoneAI kamuAI;
        tiesky.com.ISharm? sm = null;

        public string Name { get; set; }

        public AudioOutPluginProcess(ILatokoneAI host, string ipcID)
        {
            this.host = host;
            Name = ipcID;

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

        private Tuple<bool, byte[]> RemoteCall(byte[] data)
        {
            TtsPluginIPCMessageType messageType = (TtsPluginIPCMessageType)IPCMessage.GetMessageType(data);

            switch (messageType)
            {
                case TtsPluginIPCMessageType.AudioOutputAvailable:
                    // Handle audio data request
                    // For example, you can return a byte array of audio data here
                    float[] buffer = IPCMessage.GetAudioBuffer(data);

                    host.AudioOutputAvailable(buffer, buffer.Length);
                    return Tuple.Create(true, new byte[0]);
                default:
                    break;
            }

            return Tuple.Create(false, new byte[0]);
        }

        public void AddPartOfASentence(string txt)
        {
            sm.RemoteRequest(IPCMessage.CreateMessage((int)TtsPluginIPCMessageType.AddPartOfASentence, txt));
        }
        public void FillBuffer(float[] buffer, int offset, int count)
        {
            var result = sm.RemoteRequest(IPCMessage.CreateMessage((int)TtsPluginIPCMessageType.FillBuffer, count));

            if (result.Item2 != null)
            {
                for (int i = 0; i < count; i++)
                {
                    buffer[offset + i] = BitConverter.ToSingle(result.Item2, 8 + i * 4);
                }
            }

            host.AudioOutputAvailable(buffer, count);
        }

        public void Init()
        {
            var res = sm.RemoteRequest(IPCMessage.CreateMessage((int)TtsPluginIPCMessageType.Init, ""));
        }
        public void Dispose()
        {
            sm.RemoteRequest(IPCMessage.CreateMessage((int)TtsPluginIPCMessageType.Release, ""));
        }
        public void Start()
        {
            sm.RemoteRequest(IPCMessage.CreateMessage((int)TtsPluginIPCMessageType.Start, ""));
        }
        public void StopTalking()
        {
            sm.RemoteRequest(IPCMessage.CreateMessage((int)TtsPluginIPCMessageType.StopTalking, ""));
        }

        public void InitializeAndRun()
        {
            Init();
            Start();
        }

        public ITextToSpeech WithSetting(CommonPluginSetting setting, string value)
        {
            sm.RemoteRequest(IPCMessage.CreateMessage((int)TtsPluginIPCMessageType.Setting, (int)setting, value));
            return this;
        }
    }
}

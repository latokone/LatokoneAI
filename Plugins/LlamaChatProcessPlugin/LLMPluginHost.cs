using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using LatokoneAI.Common.Messaging;
using System.Diagnostics;
using System.Text;
using System.Xml.Serialization;

namespace LatokoneAI.Plugins.LLmaChatProcessPlugin
{
    public class LLMPluginHost : ILatokonePluginHost
    {
        internal Process? childProcess;
        public event Action<bool> Connected;
        public event Action<bool> Disconnected;

        LlmPluginProcess llmPluginProcess;
        public ILatokonePlugin LoadPlugin(string path, ILatokoneAI kamu, string ipcID)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo(path);
                processInfo.CreateNoWindow = true;
                processInfo.Arguments = $"--IpcID {ipcID}";

                childProcess = Process.Start(processInfo);
                childProcess.EnableRaisingEvents = true;

                childProcess.Exited += (sender, e) =>
                {
                    Disconnected.Invoke(true);
                };

                // Track child processes and close them is main app crashes/closes
                ChildProcessTracker.AddProcess(childProcess);

                llmPluginProcess = new LlmPluginProcess(kamu, ipcID);

                return llmPluginProcess;
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

    public class LlmPluginProcess : ILatokonePlugin, IDisposable
    {
        private ILatokoneAI latokoneAI;
        tiesky.com.ISharm? sm = null;

        public event Action<object> DataReceived;

        public string Name { get; set; }
        public PluginType.LatokonePluginType Type { get; set; }

        public LlmPluginProcess(ILatokoneAI host, string ipcID)
        {
            this.latokoneAI = host;
            Name = ipcID;

            Type = PluginType.LatokonePluginType.LLM;

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
            LlmPluginIPCMessageType messageType = (LlmPluginIPCMessageType)IPCMessage.GetMessageType(data);

            switch (messageType)
            {
                case LlmPluginIPCMessageType.ResponseReceived:
                    string llmResponse = Encoding.UTF8.GetString(data, 4, data.Length - 4);
                    DataReceived?.Invoke(llmResponse);
                    break;
            }
            // Response
            return Tuple.Create(false, new byte[0]);
        }

        public void Reset()
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.ClearHistory));
        }

        public void ResetState()
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.ResetState));
        }

        public void Stop()
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.StopTalking));
        }

        public void Input(object input)
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.UserInput, (string)input));
        }

        public ILatokonePlugin WithConfig(LlmConfig config)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(config.GetType());

            using (StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, config);
                sm.RemoteRequest(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.Config, textWriter.ToString()));
            }

            return this;
        }

        public void InitializeAndRun()
        {
            sm.RemoteRequest(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.Initialize));
        }

        public void Dispose()
        {
        }

        public ILatokonePlugin WithSetting(AcceleratorTypes.Accelerator[] accelerators)
        {
            var accs = string.Join(",", accelerators);
            WithSetting(CommonPluginSetting.AcceleratiorPriority, accs);
            return this;
        }

        public ILatokonePlugin WithSetting(CommonPluginSetting setting, string value)
        {
            sm.RemoteRequest(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.Setting, (int)setting, value));
            return this;
        }

    }
}

using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using System.Diagnostics;
using System.Text;

namespace LatokoneAI.Engine.PluginHosts.LLMPlugins
{
    internal class LLMPluginHost
    {

        internal Process? childProcess;
        public event Action<bool> Connected;
        public event Action<bool> Disconnected;

        LlmPluginProcess llmPluginProcess;
        public ILlmPlugin LoadPlugin(string path, Engine kamu, string ipcID, IEnumerable<AcceleratorTypes.Accelerator> accelerators)
        {
            try
            {
                string acceleratorPriority = "";
                acceleratorPriority = string.Join(",", accelerators.Select(acc => acc.ToString()));

                ProcessStartInfo processInfo = new ProcessStartInfo(path);
                processInfo.CreateNoWindow = true;
                processInfo.Arguments = $"--IpcID {ipcID} --AcceleratiorPriority {acceleratorPriority}";

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

    public class LlmPluginProcess : ILlmPlugin, IDisposable
    {
        private IKamuAI kamuAI;
        tiesky.com.ISharm? sm = null;

        public event Action<string> ResponseReceived;

        public LlmPluginProcess(IKamuAI host, string ipcID)
        {
            this.kamuAI = host;

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
                    ResponseReceived?.Invoke(llmResponse);
                    break;
            }
            // Response
            return Tuple.Create(false, new byte[0]);
        }

        public void ClearHistory()
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.ClearHistory));
        }

        public void ResetState()
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.ResetState));
        }

        public void StopTalking()
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.StopTalking));
        }

        public void UserInput(string input)
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.UserInput, input));
        }

        public void Dispose()
        {

        }

        public void Initialize()
        {
            throw new NotImplementedException();
        }
    }
}

using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using LatokoneAI.Common.Messaging;
using SkiaSharp;
using System.Diagnostics;

namespace ImageGenerationPlugin
{
    public class ImageGenerationHost : ILatokonePluginHost
    {
        internal Process? childProcess;
        public event Action<bool> Connected;
        public event Action<bool> Disconnected;

        ILatokonePlugin odPluginProcess;
        public ILatokonePlugin LoadPlugin(string path, ILatokoneAI engine, string ipcID)
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

                odPluginProcess = new ImageDetectionProcess(engine, ipcID);

                return odPluginProcess;
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

    public class ImageDetectionProcess : ILatokonePlugin
    {
        ILatokoneAI host;

        private ILatokoneAI kamuAI;
        tiesky.com.ISharm? sm = null;

        public string Name { get; set; }

        public PluginType.LatokonePluginType Type => throw new NotImplementedException();

        public ImageDetectionProcess(ILatokoneAI host, string ipcID)
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

        public event Action<SKBitmap> ImageReady;
        public event Action<object> DataReceived;

        private Tuple<bool, byte[]> RemoteCall(byte[] data)
        {
            ImgGenPluginIPCMessageType messageType = (ImgGenPluginIPCMessageType)IPCMessage.GetMessageType(data);

            switch (messageType)
            {
                case ImgGenPluginIPCMessageType.ImageReady:
                    using (SKBitmap image = SKBitmap.Decode(data.Skip(4).ToArray()))
                    {
                        ImageReady?.Invoke(image);
                    }
                    break;
                default:
                    return Tuple.Create(false, new byte[0]);
            }

            return Tuple.Create(false, new byte[0]);
        }

        public void Dispose()
        {

        }

        public void Input(object input)
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)ImgGenPluginIPCMessageType.UserInput, (string)input));
        }

        public void InitializeAndRun()
        {
            throw new NotImplementedException();
        }

        public ILatokonePlugin WithConfig(LlmConfig config)
        {
            throw new NotImplementedException();
        }

        public ILatokonePlugin WithSetting(AcceleratorTypes.Accelerator[] accelerators)
        {
            throw new NotImplementedException();
        }

        public ILatokonePlugin WithSetting(CommonPluginSetting setting, string value)
        {
            throw new NotImplementedException();
        }
    }
}

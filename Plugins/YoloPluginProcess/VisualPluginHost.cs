using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using LatokoneAI.Common.Messaging;
using SkiaSharp;
using System.Diagnostics;

namespace ObjectDetection
{
    public class VisualPluginHost : ILatokonePluginHost
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

                odPluginProcess = new ObjectDetectionPluginProcess(engine, ipcID);

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

    public class ObjectDetectionPluginProcess : ILatokonePlugin
    {
        ILatokoneAI host;

        private ILatokoneAI kamuAI;
        tiesky.com.ISharm? sm = null;

        public event Action<object> DataReceived;

        public PluginType.LatokonePluginType Type { get; set; }

        public string Name { get; }
        public ObjectDetectionPluginProcess(ILatokoneAI host, string ipcID)
        {
            this.host = host;
            Name = ipcID;

            Type = PluginType.LatokonePluginType.ObjectDetection;

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
            ObjectDetectionPluginIPCMessageType messageType = (ObjectDetectionPluginIPCMessageType)IPCMessage.GetMessageType(data);

            switch (messageType)
            {
                case ObjectDetectionPluginIPCMessageType.Run:
                    return Tuple.Create(true, new byte[0]);
                case ObjectDetectionPluginIPCMessageType.DoDetect:
                    break;
                default:
                    return Tuple.Create(false, new byte[0]);
            }

            return Tuple.Create(false, new byte[0]);
        }

        public void InitializeAndRun()
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)ObjectDetectionPluginIPCMessageType.Run));
        }

        public SKBitmap Input(SKBitmap sourceImage)
        {
            // To improve performance, implement the plugin as part of main app to avoid expensive IPC memory copies, encoding/decoding, and serialization.
            // The plugin can send back detection results (e.g. bounding boxes, labels) instead of the whole image.
            var result = sm.RemoteRequest(IPCMessage.CreateMessage((int)ObjectDetectionPluginIPCMessageType.DoDetect, sourceImage.Encode(SKEncodedImageFormat.Png, 90).ToArray()));

            var bitmap = SKBitmap.Decode(result.Item2);
            DataReceived?.Invoke(bitmap);

            return bitmap;
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
            sm.RemoteRequest(IPCMessage.CreateMessage((int)ObjectDetectionPluginIPCMessageType.Setting, (int)setting, value));
            return this;
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

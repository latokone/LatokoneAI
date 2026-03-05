using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using SkiaSharp;
using System.Diagnostics;

namespace LatokoneAI.Engine.PluginHosts.ImageGeneration
{
    internal class ImageGenerationHost
    {
        internal Process? childProcess;
        public event Action<bool> Connected;
        public event Action<bool> Disconnected;

        ImageDetectionProcess odPluginProcess;
        public ImageDetectionProcess LoadPlugin(string path, Engine kamu, IEnumerable<AcceleratorTypes.Accelerator> accelerators)
        {
            try
            {
                string ipcID = "ObjectDetectionPluginYolo";

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

                odPluginProcess = new ImageDetectionProcess(kamu, ipcID);

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

    public class ImageDetectionProcess : IImgGenPlugin
    {
        IKamuAI host;

        private IKamuAI kamuAI;
        tiesky.com.ISharm? sm = null;

        public ImageDetectionProcess(IKamuAI host, string ipcID)
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
        }

        public event Action<SKBitmap> ImageReady;

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

        public void UserInput(string input)
        {
            sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)ImgGenPluginIPCMessageType.UserInput, input));
        }
    }
}

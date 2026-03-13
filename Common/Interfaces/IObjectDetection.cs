using SkiaSharp;

namespace LatokoneAI.Common.Interfaces
{
    public interface IObjectDetection : ILatokonePlugin, IDisposable
    {
        SKBitmap DoDetect(SKBitmap sourceImage);
        void InitializeAndRun();
        IObjectDetection WithSetting(CommonPluginSetting modelBasePath, string v);

        public event Action<object> ImageProcessed;
    }

    public enum ObjectDetectionPluginIPCMessageType
    {
        Run,
        DoDetect,
        Setting
    }
}

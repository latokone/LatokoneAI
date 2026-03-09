using SkiaSharp;

namespace LatokoneAI.Common.Interfaces
{
    public interface IObjectDetection : IDisposable
    {
        SKBitmap DoDetect(SKBitmap sourceImage);
        void InitializeAndRun();
    }

    public enum ObjectDetectionPluginIPCMessageType
    {
        Run,
        DoDetect
    }
}

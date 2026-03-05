using SkiaSharp;

namespace LatokoneAI.Common.Interfaces
{
    public interface IObjectDetection : IDisposable
    {
        void Run();

        SKBitmap DoDetect(SKBitmap sourceImage);
    }

    public enum ObjectDetectionPluginIPCMessageType
    {
        Run,
        DoDetect
    }
}

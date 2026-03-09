using LatokoneAI.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.ExecutionProvider.OpenVino;
using YoloDotNet.Extensions;
using YoloDotNet.Models;

public class ObjectDetection
{
    tiesky.com.ISharm sm = null;

    Yolo? yolo;
    private static ObjectDetection objectDetection;

    static void Main(string[] args)
    {
        string ipcID = "ObjectDetectionPlugin";

        IConfiguration config = new ConfigurationBuilder()
                    .AddCommandLine(args)
                    .Build();

        ipcID = config["IpcID"] ?? ipcID;

        objectDetection = new ObjectDetection(ipcID);
    }

    public ObjectDetection(string ipcID)
    {
        if (sm == null)
        {
            sm = new tiesky.com.SharmNpc(ipcID, tiesky.com.SharmNpcInternals.PipeRole.Client, this.AsyncRemoteCallHandler, externalProcessing: false);
        }
        while (true)
        {
            Thread.Sleep(100);
        }
    }

    private Tuple<bool, byte[]> AsyncRemoteCallHandler(byte[] data)
    {
        ObjectDetectionPluginIPCMessageType messageType = (ObjectDetectionPluginIPCMessageType)IPCMessage.GetMessageType(data);

        switch (messageType)
        {
            case ObjectDetectionPluginIPCMessageType.Run:
                Run();
                break;
            case ObjectDetectionPluginIPCMessageType.DoDetect:
                {
                    var count = data.Length - 4;
                    byte[] imageData = new byte[count];
                    Buffer.BlockCopy(
                        data,                         // source array
                        4,                              // source offset in bytes
                        imageData,                        // destination array
                        0,                              // destination offset in bytes
                        count                          // number of bytes to copy
                    );
                    using var skBitmap = SKBitmap.Decode(imageData);
                    DoDetect(skBitmap);
                    return Tuple.Create(true, skBitmap.Encode(SKEncodedImageFormat.Png, 90).ToArray());
                }
        }

        return Tuple.Create(false, new byte[0]);
    }

    public void Run()
    {
        var assemblyPath = AppContext.BaseDirectory;
        var fullPath = Path.Combine(assemblyPath, @"Models\Yolo\yolov11s.onnx");

        // Fire it up! Create an instance of YoloDotNet and reuse it across your app's lifetime.
        // Prefer the 'using' pattern for automatic cleanup if you're done after a single run.
        yolo = new Yolo(new YoloOptions
        {
            //OnnxModel = "Models//Yolo//yolov11s.onnx",
            // Path to your trained model.
            // Ensure this model matches the preprocessing and training settings you use below.

            // OnnxModelBytes = modelBytes
            // Load model in byte[] format (e.g. for embedded scenarios)

            //ExecutionProvider = new CPUExecutionProvider("Models//Yolo//yolov11s.onnx"),
            ExecutionProvider = new OpenVinoExecutionProvider(fullPath),
            // Sets the execution backend.
            // Available options:
            //   - CpuExecutionProvider         → CPU-only (no GPU required)
            //   - CudaExecutionProvider        → GPU via CUDA (NVIDIA required)
            //   - TensorRtExecutionProvider    → GPU via NVIDIA TensorRT for maximum performance

            ImageResize = ImageResize.Proportional,
            // IMPORTANT: Match this to your model's training preprocessing.
            // Proportional = the dataset images were not distorted; their aspect ratio was preserved.
            // Stretched = the dataset images were resized directly to the model's input size, ignoring aspect ratio.

            SamplingOptions = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None) // YoloDotNet default
                                                                                             // IMPORTANT: This defines how pixel data is resampled when resizing the image.
                                                                                             // The choice of sampling method can directly affect detection accuracy, 
                                                                                             // as different resampling methods (Nearest, Bilinear, Cubic, etc.) slightly alter object shapes and edges.
                                                                                             // Check the benchmarks for examples and guidance: 
                                                                                             // https://github.com/NickSwardh/YoloDotNet/tree/master/test/YoloDotNet.Benchmarks
        });
    }

    public void Dispose()
    {
        yolo?.Dispose();
    }

    public void DoDetect(SKBitmap sourceImage)
    {
        if (yolo != null)
        {
            var results = yolo.RunObjectDetection(sourceImage, confidence: 0.20, iou: 0.7);
            sourceImage.Draw(results);
        }
    }
}
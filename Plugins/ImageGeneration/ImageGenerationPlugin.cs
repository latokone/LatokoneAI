using HPPH;
using HPPH.SkiaSharp;
using LatokoneAI.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using StableDiffusion.NET;
using System.Text;
using tiesky.com;

public class ImageGenerationPlugin : IDisposable
{
    private static ImageGenerationPlugin imgGeneration;
    private SharmNpc sm;
    DiffusionModel sd;

    static void Main(string[] args)
    {
        string ipcID = "ObjectDetectionPlugin";

        IConfiguration config = new ConfigurationBuilder()
                    .AddCommandLine(args)
                    .Build();

        ipcID = config["IpcID"] ?? ipcID;
        var accPriority = config["AcceleratiorPriority"];

        imgGeneration = new ImageGenerationPlugin(ipcID);
    }

    public ImageGenerationPlugin(string ipcID)
    {
        if (sm == null)
        {
            sm = new SharmNpc(ipcID, tiesky.com.SharmNpcInternals.PipeRole.Client, this.AsyncRemoteCallHandler, externalProcessing: false);
            Init(@"D:\Downloads\sdxs-512-tinySDdistilled_Q8_0.gguf");
        }

        while (true)
        {
            Thread.Sleep(100);
        }
    }

    private Tuple<bool, byte[]> AsyncRemoteCallHandler(byte[] data)
    {
        ImgGenPluginIPCMessageType messageType = (ImgGenPluginIPCMessageType)IPCMessage.GetMessageType(data);

        switch (messageType)
        {
            case ImgGenPluginIPCMessageType.UserInput:
                new Task(() =>
                {
                    string userInput = Encoding.UTF8.GetString(data, 4, data.Length - 4);
                    var img = TextToImage(userInput);

                    // Response
                    var imageData = img.ToPng();
                    sm.RemoteRequest(IPCMessage.CreateMessage((int)ImgGenPluginIPCMessageType.ImageReady, imageData));
                }).Start();
                break;
            default:
                break;
        }
        // Response
        return Tuple.Create(false, new byte[0]);
    }

    void Init(string modelPath)
    {
        // Enable the Log- and Progress-events
        StableDiffusionCpp.InitializeEvents();

        // Register the Log and Progress-events to capture stable-diffusion.cpp output
        StableDiffusionCpp.Log += (_, args) => Console.WriteLine($"LOG [{args.Level}]: {args.Text}");
        StableDiffusionCpp.Progress += (_, args) => Console.WriteLine($"PROGRESS {args.Step} / {args.Steps} ({(args.Progress * 100):N2} %) {args.IterationsPerSecond:N2} it/s ({args.Time})");

        DiffusionModel sd = new(DiffusionModelParameter.Create()
                                                               .WithModelPath(modelPath)
                                                               // .WithVae(@"<optional path to vae>")
                                                               .WithMultithreading()
                                                               .WithFlashAttention());
    }

    Image<ColorRGB> TextToImage(string text)
    {
        return sd.GenerateImage(ImageGenerationParameter.TextToImage(text).WithSDXLDefaults());
    }


    void Test(string text)
    {
        Image<ColorRGB>? treeWithTiger;
        // Load a StableDiffusion model in a using block to unload it again after the two images are created
        using (DiffusionModel sd = new(DiffusionModelParameter.Create()
                                                               .WithModelPath(@"D:\Downloads\sdxs-512-tinySDdistilled_Q8_0.gguf")
                                                               // .WithVae(@"<optional path to vae>")
                                                               .WithMultithreading()
                                                               .WithFlashAttention()))
        {
            // Create a image from a prompt
            Image<ColorRGB>? tree = sd.GenerateImage(ImageGenerationParameter.TextToImage(text).WithSDXLDefaults());
            // (optional) Save the image (requires the HPPH System.Dawing or SkiaSharp extension)
            File.WriteAllBytes("image1.png", tree.ToPng());

            // Use the previously created image for an image-to-image creation
            //treeWithTiger = sd.GenerateImage(ImageGenerationParameter.ImageToImage("A cute tiger in front of a tree on a small hill", tree).WithSDXLDefaults());
            //File.WriteAllBytes("image2.png", treeWithTiger.ToPng());
        }
        /*
        // Load the qwen image edit model
        using DiffusionModel qwenContext = new(DiffusionModelParameter.Create()
                                                                      .WithDiffusionModelPath(@"<Qwen-Image-Edit-2509-path>")
                                                                      .WithQwen2VLPath(@"<Qwen2.5-VL-7B-Instruct-path>")
                                                                      .WithQwen2VLVisionPath(@"<Qwen2.5-VL-7B-Instruct.mmproj-path>")
                                                                      .WithVae(@"<qwen_image_vae-path>")
                                                                      .WithMultithreading()
                                                                      .WithFlashAttention()
                                                                      .WithFlowShift(3)
                                                                      .WithOffloadedParamsToCPU()
                                                                      .WithImmediatelyFreedParams());

        // Perform an edit on the previously created image
        Image<ColorRGB>? tigerOnMoon = qwenContext.GenerateImage(ImageGenerationParameter.TextToImage("Remove the background and place the tree and the tiger on the moon.")
                                                                                         .WithSize(1024, 1024)
                                                                                         .WithCfg(2.5f)
                                                                                         .WithSampler(Sampler.Euler)
                                                                                         .WithRefImages(treeWithTiger));
        File.WriteAllBytes("image3.png", tigerOnMoon.ToPng());
        */
    }

    public void Dispose()
    {
        sd?.Dispose();
    }
}
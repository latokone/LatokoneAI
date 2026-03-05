
using LatokoneAI.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using OllamaSharp;
using System.Text;

class OllamaChatPlugin
{
    tiesky.com.ISharm sm = null;

    private string ipcID;
    private static OllamaChatPlugin ollamaInstance;

    static void Main(string[] args)
    {
        string ipcID = "OllamaChatPlugin";

        IConfiguration config = new ConfigurationBuilder()
                    .AddCommandLine(args)
                    .Build();

        ipcID = config["IpcID"] ?? ipcID;

        ollamaInstance = new OllamaChatPlugin(ipcID);
    }

    public OllamaChatPlugin(string ipcID)
    {
        this.ipcID = ipcID;

        if (sm == null)
        {
            Init();
            sm = new tiesky.com.SharmNpc(ipcID, tiesky.com.SharmNpcInternals.PipeRole.Client, this.AsyncRemoteCallHandler, externalProcessing: false);
        }

        while (true)
        {
            Thread.Sleep(100);
        }
    }

    private Tuple<bool, byte[]> AsyncRemoteCallHandler(byte[] data)
    {
        LlmPluginIPCMessageType messageType = (LlmPluginIPCMessageType)IPCMessage.GetMessageType(data);

        switch (messageType)
        {
            case LlmPluginIPCMessageType.UserInput:
                string userInput = Encoding.UTF8.GetString(data, 4, data.Length - 4);
                UserInput(userInput);
                break;
            case LlmPluginIPCMessageType.ClearHistory:
                ClearHistory();
                break;
            case LlmPluginIPCMessageType.StopTalking:
                StopTalking();
                break;
            case LlmPluginIPCMessageType.ResetState:
                ResetState();
                break;
        }
        return Tuple.Create(false, new byte[1]);
    }

    void Init()
    {
        // set up the client
        var uri = new Uri("http://localhost:11434");
        var ollama = new OllamaApiClient(uri);

        // select a model which should be used for further operations
        ollama.SelectedModel = "qwen3:4b";
    }

    private void ResetState()
    {

    }

    private void StopTalking()
    {

    }

    private void ClearHistory()
    {

    }

    private void UserInput(string userInput)
    {

    }
}
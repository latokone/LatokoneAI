using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;


class LLmaChatProcessPlugin
{
    tiesky.com.ISharm sm = null;

    private static LLmaChatProcessPlugin chatInstance;

    internal nint llamaHandle = 0;

    LLama.Common.ChatHistory chatHistory = new();
    ChatHistory chatHistorySC = new ChatHistory();
    LLamaWeights model;
    LLamaContext context;
    ChatSession session;
    private SessionState sessionInitialState;
    internal ConcurrentBag<string> todoList = new ConcurrentBag<string>();

    readonly ManualResetEvent workAvailable = new(false);
    LlmConfig config = new LlmConfig()
    {
        Models = new List<LlmModel>()
            {
                new LlmModel() { Url = @"https://huggingface.co/bartowski/Phi-3.5-mini-instruct_Uncensored-GGUF/resolve/main/Phi-3.5-mini-instruct_Uncensored-IQ2_M.gguf", Name = "Ahma-7B-Instruct.Q6_K", Filename = Path.Combine(LlmConfig.ModelPath, "Ahma-7B-Instruct.Q6_K.gguf") },
                new LlmModel() { Url = @"https://huggingface.co/bartowski/Phi-3.5-mini-instruct_Uncensored-GGUF/resolve/main/Phi-3.5-mini-instruct_Uncensored-Q6_K_L.gguf", Name = "Ahma-7B.Q4_K_S", Filename = Path.Combine(LlmConfig.ModelPath, "Ahma-7B.Q4_K_S.gguf") },
                new LlmModel() { Url = @"https://huggingface.co/bartowski/Phi-3.5-mini-instruct_Uncensored-GGUF/resolve/main/Phi-3.5-mini-instruct_Uncensored-Q6_K_L.gguf", Name = "Ahma-3B.Q6_K", Filename = Path.Combine(LlmConfig.ModelPath, "Ahma-3B.Q6_K.gguf") },
                new LlmModel() { Url = @"https://huggingface.co/bartowski/Phi-3.5-mini-instruct_Uncensored-GGUF/resolve/main/Phi-3.5-mini-instruct_Uncensored-Q6_K_L.gguf", Name = "Phi-3.5-mini-instruct_Uncensored-Q6_K_L", Filename = "D:\\Downloads\\Models\\Phi-3.5-mini-instruct_Uncensored-Q6_K_L.gguf" },

            },
        Accelerators = new List<LlmAccelerator>()
            {
                new LlmAccelerator() { Name = "AVX2", Library = AppContext.BaseDirectory + "\\runtimes\\win-x64\\native\\avx2\\llama.dll" },
                new LlmAccelerator() { Name = "Vulcan", Library = AppContext.BaseDirectory + ".\\runtimes\\win-x64\\native\\vulcan\\llama.dll" }
            },
        SelectedModel = 3,
        SelectedAccelerator = 0,

        SelectedLanguage = 0, // English

        SystemRoles = new string[]
{
                "Transcript of a dialog, where the User interacts with an Assistant named ReBuzz. ReBuzz is helpful, kind, honest, good at writing, and never fails to answer the User's requests immediately and with precision.",
                "Olet tekoälyavustaja. Vastaat aina mahdollisimman avuliaasti mutta lyhyesti. Vastauksesi eivät saa sisältää mitään haitallista, epäeettistä, rasistista, seksististä, vaarallista tai laitonta sisältöä. Jos kysymyksessä ei ole mitään järkeä tai se ei ole asiasisällöltään johdonmukainen, selitä miksi sen sijaan, että vastaisit jotain väärin. Jos et tiedä vastausta kysymykseen, älä kerro väärää tietoa.",
},

        ChatMessages = new string[][]
{
                new string[] {
                "Hi Assistant.",
                "Hi. How can I assist you today?",
                },
                new string[] {
                "Hei avustaja.",
                "Hei. Miten voin auttaa sinua tänään?",
                },
},

        AntiPromptLists = new List<string[]>()
            {
            new string[] { "User" },
            new string[] { "</s>", "[/Inst]", "User:", "Käyttäjä:" }
            },
    };

    private string ipcID;

    static void Main(string[] args)
    {
        string ipcID = "LlamaChatPlugin";

        IConfiguration config = new ConfigurationBuilder()
                    .AddCommandLine(args)
                    .Build();

        ipcID = config["IpcID"] ?? ipcID;

        chatInstance = new LLmaChatProcessPlugin(ipcID);
    }

    public LLmaChatProcessPlugin(string ipcID)
    {
        this.ipcID = ipcID;

        ClearHistory();
        InitLlama();

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

    Task? taskLLM = null;
    bool stopAnswering = false;

    internal void InitLlama()
    {
        running = false;
        stopAnswering = true;
        modelReady = false;
        workAvailable.Set();
        if (taskLLM != null)
        {
            while (!taskLLM.IsCompleted)
                Thread.Sleep(100);
        }

        Release();

        try
        {
            llamaHandle = NativeLibrary.Load(config.Accelerators[config.SelectedAccelerator].Library);
        }
        catch (Exception ex)
        {
            // Send error message
            // kamu.DCWriteLine(ex.Message);
            llamaHandle = 0;
        }

        string? modelFile = config.SelectedModel >= 0 ? config.Models[config.SelectedModel].Filename : null;
        if (modelFile == null || !File.Exists(modelFile) || llamaHandle == 0)
        {
            return;
        }

        var parameters = new ModelParams(modelFile)
        {
            ContextSize = 1024, // The longest length of chat as memory.
            GpuLayerCount = 5, // How many layers to offload to GPU. Please adjust it according to your GPU memory.
            Encoding = Encoding.UTF8,
            Threads = 4,
        };
        model = LLamaWeights.LoadFromFile(parameters);
        context = model.CreateContext(parameters);

        var executor = new InteractiveExecutor(context);

        session = new(executor, chatHistory);
        sessionInitialState = session.GetSessionState();

        InferenceParams inferenceParams = new InferenceParams()
        {
            MaxTokens = 512, // No more than 256 tokens should appear in answer. Remove it if antiprompt is enough for control.
            AntiPrompts = config.AntiPromptLists[config.SelectedLanguage], // Stop generation once antiprompts appear.

            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.9f
            }
        };

        running = true;
        workAvailable.Reset();

        taskLLM = Task.Factory.StartNew(async () =>
        {
            while (running)
            {
                workAvailable.WaitOne();
                workAvailable.Reset();
                stopAnswering = false;

                string responseText = "";
                while (todoList.TryTake(out string? userInput))
                {
                    // Save current state
                    sessionCurrentState = session.GetSessionState();
                    UserMessageProcessed?.Invoke(userInput);

                    await foreach (var text in session.ChatAsync(new LLama.Common.ChatHistory.Message(LLama.Common.AuthorRole.User, userInput),
                        inferenceParams))
                    {
                        if (stopAnswering)
                        {
                            break;
                        }
                        responseText += text;
                        //ResponseReceived?.Invoke(text);
                        sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.ResponseReceived, text));

                    }

                    if (!responseText.EndsWith("User:"))
                    {
                        // Send empty to create a new line
                        //ResponseReceived?.Invoke("\r\rUser:");
                        sm.RemoteRequestWithoutResponse(IPCMessage.CreateMessage((int)LlmPluginIPCMessageType.ResponseReceived, "\r\rUser:"));
                    }
                }
            }
        });

        modelReady = true;
    }

    //public event Action<string> ResponseReceived;
    public event Action<string> UserMessageProcessed;

    public void Release()
    {
        if (model != null)
        {
            model.Dispose();
            model = null;
        }
        if (context != null)
        {
            context.Dispose();
            context = null;
        }
        if (llamaHandle != 0)
        {
            NativeLibrary.Free(llamaHandle);
            llamaHandle = 0;
        }

    }

    internal bool modelReady;
    private bool running;
    private SessionState sessionCurrentState;

    internal static LLmaChatProcessPlugin ChatInstance { get => chatInstance; set => chatInstance = value; }

    public void UserInput(string text)
    {
        todoList.Add(text);
        workAvailable.Set();
    }

    public void ClearHistory()
    {
        todoList.Clear();
        stopAnswering = true;
        chatHistory.Messages.Clear();
        chatHistory.AddMessage(LLama.Common.AuthorRole.System, config.SystemRoles[config.SelectedLanguage]);
        chatHistory.AddMessage(LLama.Common.AuthorRole.User, config.ChatMessages[config.SelectedLanguage][0]);
        chatHistory.AddMessage(LLama.Common.AuthorRole.Assistant, config.ChatMessages[config.SelectedLanguage][1]);

        if (session != null && sessionInitialState != null)
        {
            session.LoadSession(sessionInitialState);
        }
    }

    public void StopTalking()
    {
        workAvailable.Reset();
        todoList.Clear();

        stopAnswering = true;

        if (session != null && sessionCurrentState != null)
        {
            session.LoadSession(sessionCurrentState);
        }
    }

    public void ResetState()
    {
        session.LoadSession(sessionInitialState);
    }
}

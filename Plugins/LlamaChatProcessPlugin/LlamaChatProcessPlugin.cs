using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using LatokoneAI.Common.Messaging;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;
using static LatokoneAI.Common.AcceleratorTypes;

namespace LatokoneAI.Plugins.LLmaChatProcessPlugin
{
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
        private Accelerator[] acceleratorPriority = new Accelerator[] { Accelerator.Cpu };

        readonly ManualResetEvent workAvailable = new(false);
        LlmModel llmModel = new LlmModel() { Url = @"https://huggingface.co/bartowski/Phi-3.5-mini-instruct_Uncensored-GGUF/resolve/main/Phi-3.5-mini-instruct_Uncensored-Q6_K_L.gguf", Name = "phi-2.Q5_K_M.gguf", Filename = @"D:\Downloads\Models\phi-2.Q5_K_M.gguf" };
        Dictionary<Accelerator, LlmAccelerator> llmAccelerators = new Dictionary<Accelerator, LlmAccelerator>()
        {
            [Accelerator.Cpu] = new LlmAccelerator() { Name = "AVX2", Library = "runtimes\\win-x64\\native\\avx2\\llama.dll" },
            [Accelerator.Vulcan] = new LlmAccelerator() { Name = "Vulcan", Library = "runtimes\\win-x64\\native\\vulcan\\llama.dll" }
        };

        LlmConfig config = new LlmConfig()
        {
            SelectedModel = 0,
            SelectedAccelerator = 0,
            SelectedLanguage = 0,

            SystemRole = "Transcript of a dialog, where the User interacts with an Assistant named Bob. Bob is helpful, kind, honest, good at writing, and never fails to answer the User's requests immediately and with precision.",

            ChatHistory = new string[][]
            {
                new string[] {
                "Hi Assistant.",
                "Hi. How can I assist you today?",
                }
            },

            AntiPromptLists = new List<string[]>()
            {
            new string[] { "User" },
            },
        };

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
                case LlmPluginIPCMessageType.Config:
                    string configString = Encoding.UTF8.GetString(data, 4, data.Length - 4);
                    XmlSerializer xmlSerializer = new XmlSerializer(config.GetType());

                    using (StringReader textReader = new StringReader(configString))
                    {
                        config = (LlmConfig)xmlSerializer.Deserialize(textReader);
                    }
                    break;
                case LlmPluginIPCMessageType.Initialize:
                    ClearHistory();
                    InitLlama();
                    break;
                case LlmPluginIPCMessageType.Setting:
                    CommonPluginSetting setting = (CommonPluginSetting)BitConverter.ToInt32(data, 4);
                    string accs = Encoding.UTF8.GetString(data, 8, data.Length - 8);
                    HandleSetting(setting, accs);
                    break;
            }
            return Tuple.Create(false, new byte[1]);
        }

        void HandleSetting(CommonPluginSetting setting, string accs)
        {
            switch (setting)
            {
                case CommonPluginSetting.AcceleratiorPriority:
                    List<Accelerator> apList = new List<Accelerator>();
                    foreach (string ac in accs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Enum.TryParse(ac, out Accelerator aEnum))
                        {
                            apList.Add(aEnum);
                        }
                    }
                    acceleratorPriority = apList.ToArray();

                    break;
                case CommonPluginSetting.ModelPath:
                    llmModel = new LlmModel() { Url = @"https://huggingface.co", Name = Path.GetFileName(accs), Filename = accs };
                    break;
            }
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
                // Todo: go through priority list properly.
                llamaHandle = NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, llmAccelerators[acceleratorPriority[0]].Library));
            }
            catch (Exception ex)
            {
                // Send error message
                // kamu.DCWriteLine(ex.Message);
                llamaHandle = 0;
            }

            string? modelFile = llmModel.Filename;
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
            chatHistory.AddMessage(LLama.Common.AuthorRole.System, config.SystemRole);
            chatHistory.AddMessage(LLama.Common.AuthorRole.User, config.ChatHistory[config.SelectedLanguage][0]);
            chatHistory.AddMessage(LLama.Common.AuthorRole.Assistant, config.ChatHistory[config.SelectedLanguage][1]);

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
}
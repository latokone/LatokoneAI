# LatokoneAI
LatokoneAI is a local AI inference orchestration framework that manages models, runtimes, and hardware resources on a user’s device, enabling fast, private, and coordinated execution of complex AI workloads without relying on the cloud.

LatokoneAI provides a unified API for running and chaining AI models on‑device, abstracting away hardware details, runtime differences, and scheduling complexity so developers can focus on building intelligent applications.

## 🚀 Features
🧠 Multi‑model orchestration
* Run multiple AI models (LLMs, vision, audio, object detection and classification) in coordinated pipelines.
* Stream data between models with low latency using IPC engine based on memory-mapped files and on NamedPipes.

### ⚙️ Runtime abstraction
* Unified interface over different inference backends.
* Multi-process architecture, asynchronous processing.
* Hot‑swap models without restarting your application.
* Support any type of AI experiences: LLMs, image generation, TTS, STT, Object detection/classification...

### 🖥️ Hardware‑aware scheduling
* Distributes workloads across CPU, GPU, and NPU.
* Prevents resource contention when multiple apps or models run simultaneously.

### 🔒 100% Local & Private
* All inference happens on your device.
* No cloud calls, no telemetry, no external dependencies.

### 🔌 Plugin architecture
* Add new models, runtimes, or pipeline components without modifying the core engine.

## 📦 Repository Structure
| Folder | Description |
| ---- | ---- |
| Engine/ | Core orchestration engine, schedulers, runtime adapters |
| Plugins/ | Plugins enabling different AI experiences | 
| Examples/ | Sample applications demonstrating usage |
| Common/ | Shared utilities and abstractions |

## 🧩 Getting Started
### Prerequisites
* .NET 10 SDK or later
* A supported AI model (GGUF, ONNX, etc. See plugin specific details)
* Optional: GPU/NPU drivers depending on your hardware

### Installation
```
git clone https://github.com/latokone/LatokoneAI.git
```
### Build:
```
dotnet build
```

## 🖥️ Example: Using LatokoneAI with ConsoleLLM
Below is a minimal example showing how to run an LLM locally using LatokoneAI’s orchestration layer.
This example assumes you have a Console‑style project referencing the LatokoneAI engine.

Note that you need to download specific AI Models to run the examples.
```
// Create engine instance
var latokoneAI = new LatokoneAI.Engine.Engine();

// Load LLM plugin process
var llmPlugin = latokoneAI.CreateLLMPlugin(@"..\..\Plugins\LlamaChatProcessPlugin\LlamaChatProcessPlugin.exe", "LlamaPlugin");

// Configure plugin, prioritize CPU
llmPlugin.
    WithSetting([Accelerator.Cpu, Accelerator.Vulcan]).
    WithSetting(CommonPluginSetting.ModelPath, @"D:\Downloads\Models\Distill-Qwen-7B-Uncensored.i1-Q4_K_M.gguf");

// Initialize and run
llmPlugin.InitializeAndRun();

// Get responses
llmPlugin.ResponseReceived += ChatLlm_ResponseReceived;

//
void ChatLlm_ResponseReceived(string text)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write(text);
}

Console.WriteLine("Ok, I'm ready to chat! Type 'exit' to quit.");

// Simple loop to get user input
while (true)
{   
    string input = Console.ReadLine();
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    Console.ForegroundColor = ConsoleColor.Yellow;
    llmPlugin.UserInput(input);
}
```

## 🛠️ Roadmap
* Multi‑GPU scheduling
* Built‑in model downloader
* More plugins! ([ONNX](https://huggingface.co/spaces/onnx-community/model-explorer), [Open Model Zoo](https://github.com/openvinotoolkit/open_model_zoo), etc.)

## 🤝 Contributing
Contributions are very welcome!
* Feel free to open issues, submit pull requests, or propose new plugins.

## 📄 License
This project is licensed under the MIT License.

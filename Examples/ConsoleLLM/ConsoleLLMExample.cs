
using LatokoneAI.Common.Interfaces;
using LatokoneAI.Plugins.LLmaChatProcessPlugin;
using static LatokoneAI.Common.AcceleratorTypes;

Console.WriteLine("Getting things ready. This might take a while...");

var latokoneAI = new LatokoneAI.Engine.Engine();

// In your real world app you would output all to one folder structure
var llmPlugin = latokoneAI.CreatePlugin(LatokoneAI.Common.PluginType.LatokonePluginType.LLM, new LLMPluginHost(), @"..\..\Plugins\LlamaChatProcessPlugin\LlamaChatProcessPlugin.exe", "LlamaPlugin");
llmPlugin.
    WithSetting([Accelerator.Cpu, Accelerator.Vulcan]).
    WithSetting(CommonPluginSetting.ModelPath, @"D:\Downloads\Models\Distill-Qwen-7B-Uncensored.i1-Q4_K_M.gguf");
llmPlugin.InitializeAndRun();

llmPlugin.DataReceived += ChatLlm_ResponseReceived;

Console.WriteLine("Ok, I'm ready to chat! Type 'exit' to quit.");

void ChatLlm_ResponseReceived(object text)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write((string)text);
}

while (true)
{   
    string input = Console.ReadLine();
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    Console.ForegroundColor = ConsoleColor.Yellow;
    llmPlugin.Input(input);
}

Console.ForegroundColor = ConsoleColor.White;
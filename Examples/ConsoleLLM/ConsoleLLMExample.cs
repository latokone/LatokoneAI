
using static LatokoneAI.Common.AcceleratorTypes;


Console.WriteLine("Getting things ready. This might take a while...");

var kamu = new LatokoneAI.Engine.Engine();
var llmPlugin = kamu.CreateLLMPlugin(@"..\..\..\..\..\Plugins\LlamaChatProcessPlugin\bin\Debug\net10.0\LlamaChatProcessPlugin.exe", "LlamaPlugin", [Accelerator.Cpu]);

llmPlugin.ResponseReceived += ChatLlm_ResponseReceived;

Console.WriteLine("Ok, I'm ready to chat! Type 'exit' to quit.");

void ChatLlm_ResponseReceived(string text)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write(text);
}

while (true)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    string input = Console.ReadLine();
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    llmPlugin.UserInput(input);
}

Console.ForegroundColor = ConsoleColor.White;
using LatokoneAI.Common.Interfaces;
using LatokoneAI.Plugins.LLmaChatProcessPlugin;
using System.Text.Json;
using System.Text.Json.Serialization;
using static LatokoneAI.Common.AcceleratorTypes;

// Use config file
string jsonFile = "config.json";
Config config;

try
{
    // Ensure the file exists
    if (!File.Exists(jsonFile))
    {
        Console.WriteLine($"Error: JSON file '{jsonFile}' not found.");
        return;
    }

    // Read JSON content from file
    string jsonContent = File.ReadAllText(jsonFile);

    // Deserialize into Config object
    config = JsonSerializer.Deserialize<Config>(jsonContent);
}
catch (Exception e)
{
    Console.WriteLine("config.json error: " + e.ToString());
    return;
}

Console.WriteLine("Getting things ready. This might take a while...");

var latokoneAI = new LatokoneAI.Engine.Engine();

// In your real world app you would output all to one folder structure
var llmPlugin = latokoneAI.CreatePlugin(LatokoneAI.Common.PluginType.LatokonePluginType.LLM, new LLMPluginHost(), @"..\..\Plugins\LlamaChatProcessPlugin\LlamaChatProcessPlugin.exe", "LlamaPlugin");

llmPlugin.
    WithSetting([Accelerator.Cpu, Accelerator.Vulcan]).
    WithSetting(CommonPluginSetting.ModelPath, config.LlmFilePath );
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

// Define a class that matches the JSON structure
public class Config
{
    [JsonPropertyName("LlmFilePath")]
    public string LlmFilePath { get; set; }
}
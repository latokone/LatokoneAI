
using LatokoneAI.Common;
using LatokoneAI.Common.Interfaces;
using LatokoneAI.Plugins.LLmaChatProcessPlugin;
using System.Text.Json;
using System.Text.Json.Serialization;
using static LatokoneAI.Common.AcceleratorTypes;

string poem1 = "";
string poem2 = "";

// This example demonstrates two LLM plugins having a discussion with each other. The first plugin is prompted to write a haiku about a forest, and the second plugin is prompted to change that haiku slightly.
// Then the first plugin is prompted to change the changed haiku slightly, and so on. The conversation continues indefinitely until the user stops the program.

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

// Create the LLM configuration. This configuration will be passed to the LLM plugins, and they will use it to load the model and set up the chat history.
LlmConfig configLlm = new LlmConfig()
{
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

Console.ForegroundColor = ConsoleColor.White;

Console.WriteLine("Getting things ready. This might take a while...");

var latokoneAI = new LatokoneAI.Engine.Engine();
var llmPlugin = latokoneAI.CreatePlugin(PluginType.LatokonePluginType.LLM, new LLMPluginHost(), @"..\..\Plugins\LlamaChatProcessPlugin\LlamaChatProcessPlugin.exe", "LlamaPlugin");
llmPlugin.WithSetting([Accelerator.Cpu, Accelerator.Vulcan]).WithSetting(CommonPluginSetting.ModelPath, config.LlmFilePath);
llmPlugin.WithConfig(configLlm);
llmPlugin.InitializeAndRun();

var llmPlugin2 = latokoneAI.CreatePlugin(PluginType.LatokonePluginType.LLM, new LLMPluginHost(), @"..\..\Plugins\LlamaChatProcessPlugin\LlamaChatProcessPlugin.exe", "LlamaPlugin2");
llmPlugin2.WithSetting([Accelerator.Cpu, Accelerator.Vulcan]).
    WithSetting([Accelerator.Cpu, Accelerator.Vulcan]).WithSetting(CommonPluginSetting.ModelPath, config.LlmFilePath);
llmPlugin2.WithConfig(configLlm);
llmPlugin2.InitializeAndRun();

Console.WriteLine("Let's start. Type CTRL+C to quit.\n\n");

llmPlugin2.Input("Write a haiku about a forest. Only write one the haiku, don't explain anything. Only the haiku.");

llmPlugin.DataReceived += ChatLlm_ResponseReceived;
llmPlugin2.DataReceived += ChatLlm_ResponseReceived2;

void ChatLlm_ResponseReceived(object obj)
{
    string text = (string)obj;
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write(text);
    poem2 += text;
    if (poem2.EndsWith("User:"))
    {
        poem2 = CleanLLMResponse(poem2);
        if (poem2.Trim().Length > 0)
        {
            llmPlugin2.Input("Change this haiku slightly. Only respond with one haiku. Haiku to change: " + poem2);
        }
        poem2 = "";
        Console.WriteLine();
    }
}


void ChatLlm_ResponseReceived2(object obj)
{
    string text = (string)obj;
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.Write(text);
    poem1 += text;
    if (poem1.Trim().EndsWith("User:"))
    {
        poem1 = CleanLLMResponse(poem1);
        llmPlugin.Input("Change this haiku slightly. Only respond with one haiku. Haiku to change: " + poem1);
        poem1 = "";
        Console.WriteLine();
    }
}

string CleanLLMResponse(string text)
{
    string res = text.Trim('\r', '\n');
    res = res.Substring(0, text.Length - "User:".Length);
    res = res.Trim('\r', '\n');
    if (res.Length >= "User".Length)
        res = res.Substring(0, res.Length - "User".Length);
    res = res.Trim('\r', '\n');
    if (res.StartsWith("Assistant:"))
        res = res.Substring("Assistant:".Length, res.Length - "Assistant:".Length);
    return res;
}

while (true)
{
    Thread.Sleep(100);
}

// Define a class that matches the JSON structure
public class Config
{
    [JsonPropertyName("LlmFilePath")]
    public string? LlmFilePath { get; set; }
}
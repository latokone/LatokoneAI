namespace LatokoneAI.Plugins.LLmaChatProcessPlugin
{
    public enum LlmPluginIPCMessageType
    {
        Initialize,
        UserInput,
        StopTalking,
        ClearHistory,
        ResetState,
        ResponseReceived,
        Config,
        Setting,
    }
}

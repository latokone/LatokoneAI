using static LatokoneAI.Common.AcceleratorTypes;

namespace LatokoneAI.Common.Interfaces
{
    public interface ILlmPlugin : ILatokonePlugin, IDisposable
    {
        public void InitializeAndRun();
        public void UserInput(string input);
        public void StopTalking();
        public void ClearHistory();
        public void ResetState();
        public void WithConfig(LlmConfig config);
        public ILlmPlugin WithSetting(Accelerator[] accelerators);
        public ILlmPlugin WithSetting(CommonPluginSetting setting, string value);

        public event Action<string> ResponseReceived;
    }

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

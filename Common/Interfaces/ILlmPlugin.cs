namespace LatokoneAI.Common.Interfaces
{
    public interface ILlmPlugin : IDisposable
    {
        public void Initialize();
        public void UserInput(string input);
        public void StopTalking();
        public void ClearHistory();
        public void ResetState();

        public event Action<string> ResponseReceived;
    }

    public enum LlmPluginIPCMessageType
    {
        UserInput,
        StopTalking,
        ClearHistory,
        ResetState,
        ResponseReceived,
    }
}

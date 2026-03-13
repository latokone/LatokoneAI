namespace LatokoneAI.Common.Interfaces
{
    public interface ITextToSpeech : ILatokonePlugin, IDisposable
    {
        public void Init();
        public void Start();

        public void InitializeAndRun();

        public void FillBuffer(float[] buffer, int offset, int count);

        public void StopTalking();

        public void AddPartOfASentence(string txt);

        public ITextToSpeech WithSetting(CommonPluginSetting setting, string value);
    }

    public enum TtsPluginIPCMessageType
    {
        Init,
        Start,
        FillBuffer,
        StopTalking,
        AddPartOfASentence,
        Release,
        AudioOutputAvailable,
        Setting
    }
}

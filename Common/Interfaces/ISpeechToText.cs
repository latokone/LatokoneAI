namespace LatokoneAI.Common.Interfaces
{
    public interface ISpeechToText
    {
        public event Action<string> TextRecognized;
        void Dispose();
    }

    public enum SttPluginIPCMessageType
    {
        TextRecognized,
        Release,
        ProcessAudioBuffer
    }
}

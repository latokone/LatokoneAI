using static LatokoneAI.Common.AcceleratorTypes;

namespace LatokoneAI.Common.Interfaces
{
    public interface ISpeechToText
    {
        public event Action<string> TextRecognized;
        public ISpeechToText WithSetting(Accelerator[] accelerators);
        public ISpeechToText WithSetting(CommonPluginSetting setting, string value);

        public void InitializeAndRun();
        void Dispose();
    }

    public enum SttPluginIPCMessageType
    {
        Initialize,
        Setting,
        TextRecognized,
        Release,
        ProcessAudioBuffer,
        AcceleratiorPriority,
        ModelPath
    }
}

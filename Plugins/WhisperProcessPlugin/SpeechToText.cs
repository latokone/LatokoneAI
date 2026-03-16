using static LatokoneAI.Common.AcceleratorTypes;

namespace WhisperProcessPlugin
{
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

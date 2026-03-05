namespace LatokoneAI.Engine.Audio
{
    internal interface IReBuzzAudioProvider
    {
        void ClearBuffer();
        CommonAudioProvider AudioSampleProvider { get; }
    }
}

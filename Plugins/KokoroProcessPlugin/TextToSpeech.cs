namespace KokoroProcessPlugin
{
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

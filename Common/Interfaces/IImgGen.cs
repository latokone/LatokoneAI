namespace LatokoneAI.Common.Interfaces
{
    public interface IImgGenPlugin : IDisposable
    {
        public void UserInput(string input);
    }

    public enum ImgGenPluginIPCMessageType
    {
        UserInput,
        ImageReady
    }
}

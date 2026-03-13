namespace LatokoneAI.Common.Interfaces
{
    public interface ILatokoneAI
    {
        public void DCWriteLine(string txt);
        public event Action<float[], int> AudioReceived;

        public void AudioOutputAvailable(float[] buffer, int count);

        public IEnumerable<IPluginConnection> Connections { get; }
        public IPluginConnection ConnectPlugins(ILatokonePlugin from, ILatokonePlugin to);
    }
}

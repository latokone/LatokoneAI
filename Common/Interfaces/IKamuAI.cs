namespace LatokoneAI.Common.Interfaces
{
    public interface IKamuAI
    {
        public void DCWriteLine(string txt);
        public event Action<float[], int> AudioReceived;

        public void AudioOutputAvailable(float[] buffer, int count);
    }
}

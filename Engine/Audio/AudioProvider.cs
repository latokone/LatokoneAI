using LatokoneAI.Common.WindowsRegistry;
using NAudio.Wave;

namespace LatokoneAI.Engine.Audio
{
    internal class AudioProvider : ISampleProvider, IReBuzzAudioProvider
    {
        public WaveFormat WaveFormat { get; }

        public CommonAudioProvider AudioSampleProvider { get; }

        public AudioProvider(
            Engine kamu,
          int sampleRate,
          int channels,
          int bufferSize,
          bool doubleBuffer,
          IRegistryEx registryEx)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            AudioSampleProvider = new CommonAudioProvider(kamu, sampleRate, channels, bufferSize, doubleBuffer, registryEx);
        }

        public void ClearBuffer()
        {
            AudioSampleProvider.ClearBuffer();
        }

        public int Read(float[] buffer, int offset, int count)
        {
            return AudioSampleProvider.Read(buffer, offset, count);
        }

        public void Stop()
        {
            AudioSampleProvider?.Stop();
        }
    }
}

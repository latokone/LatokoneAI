using LatokoneAI.Common.WindowsRegistry;


namespace LatokoneAI.Engine.Audio
{
    internal class CommonAudioProvider
    {

        public CommonAudioProvider(
          Engine kamu, int sampleRate,
          int channels,
          int bufferSize,
          bool doubleBuffer, IRegistryEx registryEx)
        {
            this.kamu = kamu;
            this.samplRate = sampleRate;
            this.channels = channels;

        }

        private Engine kamu;
        private int samplRate;
        private int channels;

        public int Read(float[] buffer, int offset, int count)
        {
            int retCount = count;
            int maxSamples = 512;

            foreach (var plugin in kamu.textToSpeechPlugins)
            {
                int bufferOffet = offset;
                int buffercount = count;

                while (buffercount > 0)
                {
                    int workSamples = Math.Min(maxSamples, buffercount);
                    float[] workBuffer = new float[workSamples];

                    plugin.FillBuffer(workBuffer, 0, workSamples);

                    for (int i = 0; i < workSamples; i++)
                    {
                        buffer[bufferOffet] = workBuffer[i];
                        bufferOffet++;
                    }

                    buffercount -= workSamples;
                }
            }

            return retCount;
        }

        public void Stop()
        {
        }

        internal void ClearBuffer()
        {

        }
    }
}

using LatokoneAI.Common.WindowsRegistry;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LatokoneAI.Engine.Audio
{
    public class AudioEngine
    {
        public enum AudioOutType
        {
            ASIO,
            Wasapi,
            DirectSound
        }

        public class AudioOutDevice
        {
            public string Name;
            public AudioOutType Type;
            public IWavePlayer WavePlayer;
        }

        public AudioOutDevice SelectedOutDevice { get; private set; }
        private readonly Engine kamu;
        private readonly IRegistryEx registryEx;
        private AudioProvider audioProvider;
        WasapiCapture wasapiCapture;

        int sampleRate = 44100;
        public int SampleRate
        {
            get => sampleRate; private set => sampleRate = value;
        }

        int sampleRateIn = 44100;
        public int SampleRateIn
        {
            get => sampleRateIn; private set => sampleRateIn = value;
        }

        public AudioEngine(Engine kamu)
        {
            this.kamu = kamu;
            registryEx = new WindowsRegistry();
        }
        public void CreateWasapiOut()
        {
            string wasapiDeviceID = registryEx.Read("DeviceID", "", "WASAPI");
            int wasapiDeviceSamplerate = registryEx.Read("SampleRate", 44100, "WASAPI");
            int wasapiMode = registryEx.Read("Mode", 0, "WASAPI");
            int wasapiPoll = registryEx.Read("Poll", 0, "WASAPI");
            int bufferSize = registryEx.Read("BufferSize", 2048, "WASAPI");

            string playbackDeviceID = "";

            var enumerator = new MMDeviceEnumerator();
            //MMDevice mMDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).FirstOrDefault(d => d.ID == wasapiDeviceID);
            MMDevice mMDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).FirstOrDefault(d => d.DeviceFriendlyName.Contains("Focus"));

            WasapiOut wasapiOut = null;
            if (mMDevice != null)
            {
                int latency = Math.Max(1, 1000 * bufferSize / wasapiDeviceSamplerate);
                wasapiOut = new WasapiOut(mMDevice, wasapiMode == 0 ? AudioClientShareMode.Shared : AudioClientShareMode.Exclusive, wasapiPoll == 1, latency);
                playbackDeviceID = mMDevice.ID;
            }
            else
            {
                wasapiOut = new WasapiOut();
            }

            audioProvider = new AudioProvider(kamu, wasapiDeviceSamplerate,
              wasapiOut.OutputWaveFormat.Channels, bufferSize, true, registryEx);

            bool success = InitWasapiOut(wasapiOut);
            if (!success)
            {
                wasapiOut = new WasapiOut(); // System defaults
                audioProvider = new AudioProvider(kamu, wasapiDeviceSamplerate, 2, bufferSize, true, registryEx);
                success = InitWasapiOut(wasapiOut);
            }
            if (!success)
                return;

            sampleRate = wasapiOut.OutputWaveFormat.SampleRate;

            try
            {
                string wasapiDeviceIDIn = registryEx.Read("DeviceIDIn", "", "WASAPI");
                enumerator = new MMDeviceEnumerator();
                // mMDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).FirstOrDefault(d => d.ID == wasapiDeviceIDIn);
                mMDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).First();
                if (mMDevice != null)
                {
                    sampleRateIn = mMDevice.AudioClient.MixFormat.SampleRate;
                    int latency = Math.Max(1, 2000 * bufferSize / sampleRateIn);
                    wasapiCapture = new WasapiCapture(mMDevice, wasapiMode == 0, latency);
                    wasapiCapture.DataAvailable += WasapiCapture_DataAvailable;
                    wasapiCapture.StartRecording();
                }
            }
            catch (Exception ex)
            {
                kamu.DCWriteLine("Wasap error: " + ex);
                wasapiCapture = null;
            }

            SelectedOutDevice = new AudioOutDevice() { Name = playbackDeviceID, Type = AudioOutType.Wasapi, WavePlayer = wasapiOut };
        }

        public void Play()
        {
            try
            {
                SelectedOutDevice?.WavePlayer?.Play();
            }
            catch (Exception e)
            {
                kamu.DCWriteLine("WavePlayer error: " + e.Message);
            }
        }

        readonly float[] audioInBuffer = new float[512];
        private void WasapiCapture_DataAvailable(object sender, WaveInEventArgs e)
        {
            int bytesRemaining = e.BytesRecorded;
            int srcByteOffset = 0;
            while (bytesRemaining > 0)
            {
                int copyCount = Math.Min(bytesRemaining, audioInBuffer.Length * 4);
                Buffer.BlockCopy(e.Buffer, srcByteOffset, audioInBuffer, 0, copyCount);
                kamu.AudioInputAvalable(audioInBuffer, copyCount >> 2);
                srcByteOffset += copyCount;
                bytesRemaining -= copyCount;
            }
        }

        bool InitWasapiOut(WasapiOut wasapiOut)
        {
            bool success = false;
            if (wasapiOut != null)
            {
                try
                {
                    wasapiOut.Init(audioProvider);
                    success = true;
                }
                catch (Exception ex)
                {
                    wasapiOut.Dispose();
                    audioProvider.Stop();
                    kamu.DCWriteLine("Wasap error: " + ex);
                }
            }
            return success;
        }
    }
}

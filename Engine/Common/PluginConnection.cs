using LatokoneAI.Common.Interfaces;
using static LatokoneAI.Common.PluginType;

namespace LatokoneAI.Engine.Common
{
    internal class PluginConnection : IPluginConnection
    {
        ILatokonePlugin from, to;
        public ILatokonePlugin From => from;

        public ILatokonePlugin To => to;

        public event Action<ConnectionEventData> DataAvailable;

        public PluginConnection(ILatokonePlugin from, ILatokonePlugin to)
        {
            this.from = from;
            this.to = to;

            if (from.Type == LatokonePluginType.STT)
            {   
                from.DataReceived += Plugin_SpeechToText;
            }
            else if (from.Type == LatokonePluginType.LLM)
            {   
                from.DataReceived += Plugin_LlmResponseReceived;
            }
            else if (from.Type == LatokonePluginType.ObjectDetection)
            {
                from.DataReceived += Plugin_ImageProcessed;
            }
        }

        private void Plugin_ImageProcessed(object obj)
        {
            ConnectionEventData ced = new ConnectionEventData(ConnectionEventDataType.Text);
            ced.Data = obj;
            DataAvailable?.Invoke(ced);
            if (!ced.Handled)
            {
                if (to.Type == LatokonePluginType.LLM)
                {
                    // ?
                }
            }
        }

        private void Plugin_LlmResponseReceived(object data)
        {
            HandleInput((string)data);
        }

        private void Plugin_SpeechToText(object text)
        {
            HandleInput((string)text);
        }

        void HandleInput(string text)
        {
            ConnectionEventData ced = new ConnectionEventData(ConnectionEventDataType.Text);
            ced.Data = text;
            DataAvailable?.Invoke(ced);
            if (!ced.Handled)
            {
                if (to.Type == LatokonePluginType.LLM)
                {   
                    to.Input(text);
                }
                else if (to.Type == LatokonePluginType.TTS)
                {
                    
                    to.Input(text);
                }
            }
        }

        public void Release()
        {
            if (from.Type == LatokonePluginType.LLM)
            {   
                from.DataReceived -= Plugin_LlmResponseReceived;
            }
            else if (from.Type == LatokonePluginType.STT)
            {   
                from.DataReceived -= Plugin_SpeechToText;
            }
            else if (from.Type == LatokonePluginType.ObjectDetection)
            {
                from.DataReceived -= Plugin_ImageProcessed;
            }
        }
    }
}

using LatokoneAI.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

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

            if (from is ISpeechToText)
            {
                ISpeechToText plugin = (ISpeechToText)from;
                plugin.TextRecognized += Plugin_SpeechToText;
            }
            else if (from is ILlmPlugin)
            {
                ILlmPlugin plugin = (ILlmPlugin)from;
                plugin.ResponseReceived += Plugin_LlmResponseReceived;
            }
            else if (from is IObjectDetection)
            {
                IObjectDetection plugin = (IObjectDetection)from;
                plugin.ImageProcessed += Plugin_ImageProcessed;
            }
        }

        private void Plugin_ImageProcessed(object obj)
        {
            ConnectionEventData ced = new ConnectionEventData(ConnectionEventDataType.Text);
            ced.Data = obj;
            DataAvailable?.Invoke(ced);
            if (!ced.Handled)
            {
                if (to is ILlmPlugin)
                {
                    // ?
                }
            }
        }

        private void Plugin_LlmResponseReceived(string text)
        {
            HandleInput(text);
        }

        private void Plugin_SpeechToText(string text)
        {
            HandleInput(text);
        }

        void HandleInput(string text)
        {
            ConnectionEventData ced = new ConnectionEventData(ConnectionEventDataType.Text);
            ced.Data = text;
            DataAvailable?.Invoke(ced);
            if (!ced.Handled)
            {
                if (to is ILlmPlugin)
                {
                    var llm = (ILlmPlugin)to;
                    llm.UserInput(text);
                }
                else if (to is ITextToSpeech)
                {
                    ITextToSpeech tts = (ITextToSpeech)to;
                    tts.AddPartOfASentence(text);
                }
            }
        }

        public void Release()
        {
            if (from is ILlmPlugin)
            {
                ILlmPlugin plugin = (ILlmPlugin)from;
                plugin.ResponseReceived -= Plugin_LlmResponseReceived;
            }
            else if (from is ISpeechToText)
            {
                ISpeechToText plugin = (ISpeechToText)from;
                plugin.TextRecognized -= Plugin_SpeechToText;
            }
            else if (from is IObjectDetection)
            {
                IObjectDetection plugin = (IObjectDetection)from;
                plugin.ImageProcessed -= Plugin_ImageProcessed;
            }
        }
    }
}

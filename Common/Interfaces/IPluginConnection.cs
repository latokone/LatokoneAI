using System;
using System.Collections.Generic;
using System.Text;

namespace LatokoneAI.Common.Interfaces
{
    public interface IPluginConnection
    {
        public ILatokonePlugin From {  get; }
        public ILatokonePlugin To { get; }
        public event Action<ConnectionEventData> DataAvailable;

        void Release();
    }

    public enum ConnectionEventDataType
    {
        Text,
        Audio,
        Image,
        Video
    }

    public class ConnectionEventData
    {
        public ConnectionEventDataType Type { get; }
        public bool Handled { get; set; }
        public object Data { get; set; }

        public ConnectionEventData(ConnectionEventDataType type)
        {
            Type = type;
        }
    }
}

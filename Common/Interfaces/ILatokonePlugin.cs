using System;
using System.Collections.Generic;
using System.Text;
using static LatokoneAI.Common.AcceleratorTypes;
using static LatokoneAI.Common.PluginType;

namespace LatokoneAI.Common.Interfaces
{
    public interface ILatokonePlugin : IDisposable
    {
        public string Name { get; }
        public LatokonePluginType Type { get; }
        public void InitializeAndRun();

        public void Stop() { }
        public void Reset() { }

        public void FillBuffer(float[] buffer, int offset, int count) { }

        public event Action<object> DataReceived;
        public void Input(object data) { }
        ILatokonePlugin WithConfig(LlmConfig config);
        public ILatokonePlugin WithSetting(AcceleratorTypes.Accelerator[] accelerators);
        public ILatokonePlugin WithSetting(CommonPluginSetting setting, string value);
    }
}

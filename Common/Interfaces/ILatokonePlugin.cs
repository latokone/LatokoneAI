using System;
using System.Collections.Generic;
using System.Text;
using static LatokoneAI.Common.AcceleratorTypes;

namespace LatokoneAI.Common.Interfaces
{
    public interface ILatokonePlugin
    {
        public string Name { get; }

        public void InitializeAndRun();
    }
}

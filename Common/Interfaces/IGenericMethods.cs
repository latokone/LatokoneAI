using System;
using System.Collections.Generic;
using System.Text;
using static LatokoneAI.Common.AcceleratorTypes;

namespace LatokoneAI.Common.Interfaces
{
    public interface IGenericMethods
    {
        public void Initialize(string modelPath, Accelerator[] accelerators);
    }
}

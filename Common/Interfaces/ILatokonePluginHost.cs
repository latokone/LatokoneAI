using System;
using System.Collections.Generic;
using System.Text;

namespace LatokoneAI.Common.Interfaces
{
    public interface ILatokonePluginHost
    {
        public ILatokonePlugin LoadPlugin(string path, ILatokoneAI engine, string ipcID);
    }
}

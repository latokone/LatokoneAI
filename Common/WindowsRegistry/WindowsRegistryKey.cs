using Microsoft.Win32;

namespace LatokoneAI.Common.WindowsRegistry
{
    public interface IRegistryKey
    {
        void SetValue(string name, object value);
    }

    public class WindowsRegistryKey(RegistryKey registryKey) : IRegistryKey
    {
        public void SetValue(string name, object value)
        {
            registryKey.SetValue(name, value);
        }
    }
}
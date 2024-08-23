using System.Collections.Concurrent;
using dboot.Builder.Options;
using dboot.Core.Http;

namespace dboot.Builder
{
    public record Context(UpdateClient UpdateClient, InstallOptions InstallOptions, ConcurrentDictionary<string, object?> Data)
    {
        public bool IsAlreadyInstalled { get; set; }
        
        public bool AddData<TValue>(string key, TValue value)
        {
            return Data.TryAdd(key, value);
        }

        public TValue? GetData<TValue>(string key)
        {
            if (Data.TryGetValue(key, out var value))
            {
                return (TValue?)value;
            }
            return default;
        }
    }
}
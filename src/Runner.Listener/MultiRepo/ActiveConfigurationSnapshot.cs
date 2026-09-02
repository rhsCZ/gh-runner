using System.Collections.Generic;

namespace GitHub.Runner.Listener.MultiRepo
{
    internal sealed class ActiveConfigurationSnapshot
    {
        public Dictionary<string, byte[]> Files { get; } = new();
    }
}

using GitHub.Runner.Common;

namespace GitHub.Runner.Listener.MultiRepo
{
    internal sealed class RunnerProfile
    {
        public string Name { get; init; }
        public string RootPath { get; init; }
        public RunnerSettings Settings { get; init; }
        public CredentialData Credential { get; init; }

        public override string ToString()
        {
            return $"{Name} ({Settings?.RepoOrOrgName ?? "unknown"})";
        }
    }
}

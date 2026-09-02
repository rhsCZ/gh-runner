using System.Threading.Tasks;
using GitHub.DistributedTask.WebApi;

namespace GitHub.Runner.Listener.MultiRepo
{
    internal sealed class RepositoryListenerContext
    {
        public RunnerProfile Profile { get; init; }
        public IMessageListener Listener { get; init; }
        public bool PendingWork { get; set; }
        public long PendingSequence { get; set; }
        public Task<TaskAgentMessage> PollTask { get; set; }
    }
}

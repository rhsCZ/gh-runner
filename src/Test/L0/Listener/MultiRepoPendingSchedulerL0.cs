using GitHub.Runner.Listener;
using GitHub.Runner.Listener.MultiRepo;
using Xunit;

namespace GitHub.Runner.Common.Tests.Listener
{
    public sealed class MultiRepoPendingSchedulerL0
    {
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Runner")]
        public void PreservesFifoOrderAcrossTwoListeners()
        {
            var scheduler = new MultiRepoPendingScheduler();
            var listenerA = new RepositoryListenerContext { Profile = new RunnerProfile { Name = "repo-a" } };
            var listenerB = new RepositoryListenerContext { Profile = new RunnerProfile { Name = "repo-b" } };

            Assert.True(scheduler.EnqueuePending(listenerA));
            Assert.True(scheduler.EnqueuePending(listenerB));

            Assert.True(scheduler.TryDequeue(out var first));
            Assert.True(scheduler.TryDequeue(out var second));

            Assert.Equal("repo-a", first.Profile.Name);
            Assert.Equal("repo-b", second.Profile.Name);
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Runner")]
        public void DoesNotQueueSameListenerTwiceWhilePending()
        {
            var scheduler = new MultiRepoPendingScheduler();
            var listener = new RepositoryListenerContext { Profile = new RunnerProfile { Name = "repo-a" } };

            Assert.True(scheduler.EnqueuePending(listener));
            Assert.False(scheduler.EnqueuePending(listener));
            Assert.Equal(1, scheduler.Count);
        }
    }
}

using System.Collections.Generic;

namespace GitHub.Runner.Listener.MultiRepo
{
    internal sealed class MultiRepoPendingScheduler
    {
        private readonly Queue<RepositoryListenerContext> _pendingQueue = new();
        private long _nextSequence;

        public int Count => _pendingQueue.Count;

        public bool EnqueuePending(RepositoryListenerContext context)
        {
            if (context.PendingWork)
            {
                return false;
            }

            context.PendingWork = true;
            context.PendingSequence = ++_nextSequence;
            _pendingQueue.Enqueue(context);
            return true;
        }

        public bool TryDequeue(out RepositoryListenerContext context)
        {
            if (_pendingQueue.Count == 0)
            {
                context = null;
                return false;
            }

            context = _pendingQueue.Dequeue();
            context.PendingWork = false;
            context.PendingSequence = 0;
            return true;
        }
    }
}

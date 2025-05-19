namespace LLMClient.UI.Component
{
    public class CustomSynchronizationContextTaskScheduler : TaskScheduler
    {
        private readonly SynchronizationContext _mSynchronizationContext;

        public CustomSynchronizationContextTaskScheduler(SynchronizationContext mSynchronizationContext)
        {
            _mSynchronizationContext = mSynchronizationContext;
        }

        protected override bool TryDequeue(Task task)
        {
            return base.TryDequeue(task);
        }

        protected override void QueueTask(Task task) =>
            this._mSynchronizationContext.Post((state =>
            {
                if (state is Task stateTask)
                {
                    this.TryExecuteTask(stateTask);
                }
            }), (object)task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            var tryExecuteTask = SynchronizationContext.Current == this._mSynchronizationContext &&
                                 this.TryExecuteTask(task);
            return tryExecuteTask;
        }

        protected override IEnumerable<Task>? GetScheduledTasks() => null;

        public override int MaximumConcurrencyLevel => 1;
    }
}
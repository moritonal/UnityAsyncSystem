namespace UnityAsyncSystem
{
    public class UnityTaskScheduler : TaskScheduler
    {
        public ConcurrentQueue<Task> Tasks { get; } = new ConcurrentQueue<Task>();

        public UnityTaskScheduler(string name)
        {
            Name = name;
        }

        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;

        public void Update()
        {
            var timeBudget = 1.0f / 10.0f;
            var totalTime = 0.0f;

            var count = Tasks.Count;

            var frameCount = AsyncSystem.FrameCount;

            _currentThreadIsProcessingItems = true;

            try
            {
                do
                {
                    var stopwatch = SysDiag.Stopwatch.StartNew();

                    if (this.Tasks.TryDequeue(out var result))
                    {
                        try
                        {
                            if (Thread.CurrentThread == AsyncSystem.MainThread)
                            {
                                AsyncSystem.Log($"Executing Task on \"{Name}\": {result.Id} on frame \"{Time.frameCount}\"");
                            }
                            else
                            {
                                AsyncSystem.Log($"Executing Task on \"{Name}\": {result.Id} on worker frame \"{frameCount}\"");
                            }

                            base.TryExecuteTask(result);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e);
                        }
                    }
                    else
                    {
                        break;
                    }

                    stopwatch.Stop();

                    var time = stopwatch.ElapsedMilliseconds / 1000.0f;

                    AsyncSystem.Log($"Executing Task on \"{Name}\": {result.Id} on worker frame \"{frameCount}\" took {Math.Round(time, 3)}s");

                    totalTime += time;
                }
                while (totalTime < timeBudget && count-- > 0);
            }
            finally
            {
                _currentThreadIsProcessingItems = false;
            }
        }

        protected override void QueueTask(Task task)
        {
            Tasks.Enqueue(task);
        }

        protected override bool TryDequeue(Task task)
        {
            //return Tasks.TryDequeue(task);
            return base.TryDequeue(task);
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return Tasks.ToList();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            if (!_currentThreadIsProcessingItems)
                return false;

            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued)
                return false;
            else
                return base.TryExecuteTask(task);
        }

        public override int MaximumConcurrencyLevel => 1;

        public string Name { get; }
    }
}
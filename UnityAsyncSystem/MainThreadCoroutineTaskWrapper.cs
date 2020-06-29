namespace UnityAsyncSystem
{
    public class MainThreadCoroutineTaskWrapper : CustomYieldInstruction
    {
        private readonly Task task;

        TaskCompletionSource<object> source = null;

        public MainThreadCoroutineTaskWrapper(Func<Task> task)
        {
            source = new TaskCompletionSource<object>();

            AsyncSystem.StartOnMainThread(async () =>
            {
                try
                {
                    await task();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }

                source.SetResult(null);
            });
        }

        public override bool keepWaiting => !this.source.Task.IsCompleted;
    }
}
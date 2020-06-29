
namespace UnityAsyncSystem
{
    public class CoroutineTaskWrapper : CustomYieldInstruction
    {
        readonly TaskCompletionSource<object> source = null;

        public CoroutineTaskWrapper(Func<Task> task)
        {
            source = new TaskCompletionSource<object>();

            AsyncSystem.StartOnWorkerThread(async () =>
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
namespace UnityAsyncSystem
{
    public struct RunOnMainThreadAwaitable : INotifyCompletion
    {
        public RunOnMainThreadAwaitable GetAwaiter() => this;

        public bool IsCompleted => false;

        public void OnCompleted(Action continuation)
        {
            AsyncSystem.StartOnMainThread(continuation);
        }

        public void GetResult() { }
    }
}
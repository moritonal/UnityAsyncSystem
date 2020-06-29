namespace UnityAsyncSystem
{
    public struct RunOnMultiThreadAwaitable : INotifyCompletion
    {
        public RunOnMultiThreadAwaitable GetAwaiter() => this;

        public bool IsCompleted => false;

        public void OnCompleted(Action continuation) => AsyncSystem.StartOnWorkerThread(continuation);

        public void GetResult() { }
    }
}
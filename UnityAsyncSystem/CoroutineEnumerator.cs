namespace UnityAsyncSystem
{
    public class CoroutineEnumerator : CustomYieldInstruction
    {
        private readonly Task task;

        bool Complete = false;

        public CoroutineEnumerator(Task task)
        {
            this.task = task;

            task.ContinueWith((t) =>
            {
                this.Complete = true;
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public override bool keepWaiting => !this.Complete;
    }
}
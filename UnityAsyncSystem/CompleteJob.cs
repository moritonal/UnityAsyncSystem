
namespace UnityAsyncSystem
{
    class CompleteJob : ISubJob
    {
        public Task Execute()
        {
            AsyncSystem.scheduler.Update();

            return Task.CompletedTask;
        }
    }
}
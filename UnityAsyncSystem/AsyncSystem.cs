using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using System.Runtime.CompilerServices;
using SysDiag = System.Diagnostics;

namespace UnityAsyncSystem
{
    public class AsyncSystem : MonoBehaviour
    {
        private readonly List<GCHandle> gcHandles = new List<GCHandle>();
        private readonly List<JobHandle> jobHandles = new List<JobHandle>();

        public static System.Threading.Thread MainThread;
        public static UnityTaskScheduler scheduler = new UnityTaskScheduler("Background");
        private static UnityTaskScheduler mainScheduler = new UnityTaskScheduler("Main");
        public static volatile int FrameCount = 0;
        public static bool _logging = false;

        public bool Logging = false;

        public static ConcurrentQueue<Task> mainThreadTasks = new ConcurrentQueue<Task>();
        static ConcurrentQueue<ISubJob> newTasks = new ConcurrentQueue<ISubJob>();

        static ProfilerMarker markerMain = new ProfilerMarker("Main Scheduler");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            markerMain = new ProfilerMarker("Main Scheduler");

            scheduler = new UnityTaskScheduler("Background");
            mainScheduler = new UnityTaskScheduler("Main");

            mainThreadTasks = new ConcurrentQueue<Task>();
            newTasks = new ConcurrentQueue<ISubJob>();

            FrameCount = 0;
        }

        private void Start()
        {
            AsyncSystem.MainThread = Thread.CurrentThread;

            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = 2;
        }

        public static void StartOnMainThread(Action t)
        {
            if (Thread.CurrentThread == AsyncSystem.MainThread)
            {
                t();
            }
            else
            {
                Task.Factory.StartNew(t,
                CancellationToken.None,
                TaskCreationOptions.RunContinuationsAsynchronously,
                mainScheduler);
            }
        }

        public static void StartOnWorkerThread(Action t)
        {
            AsyncSystem.Log("StartOnWorker");

            Task.Factory.StartNew(t,
                CancellationToken.None,
                TaskCreationOptions.RunContinuationsAsynchronously,
                scheduler);
        }

        public static void StartOnWorkerThread<T>(Func<T> t)
        {
            AsyncSystem.Log("StartOnWorker");

            Task.Factory.StartNew(t,
                CancellationToken.None,
                TaskCreationOptions.RunContinuationsAsynchronously,
                scheduler);
        }

        public static ConcurrentQueue<TaskCompletionSource<bool>> mainBlocks = new ConcurrentQueue<TaskCompletionSource<bool>>();

        public static async Task<T> RunOnWorkerThread<T>(Func<T> t)
        {
            var res = new TaskCompletionSource<T>();

            AsyncSystem.Log("RunOnWorker");

            StartOnWorkerThread(() =>
            {
                var ret = t();

                res.SetResult(ret);
            });

            return await res.Task;
        }

        public static async Task RunOnWorkerThread(Action t)
        {
            var res = new TaskCompletionSource<object>();

            AsyncSystem.Log("RunOnWorker");

            StartOnWorkerThread(() =>
            {
                t();

                res.SetResult(null);
            });

            await res.Task;
        }

        public static async Task RunOnMain(Action t)
        {
            if (Thread.CurrentThread == AsyncSystem.MainThread)
            {
                t();
            }
            else
            {
                var res = new TaskCompletionSource<bool>();

                AsyncSystem.Log("RunOnMain");

                StartOnMainThread(() =>
                {
                    try
                    {
                        t();

                        res.SetResult(true);
                    }
                    catch (Exception e)
                    {
                        AsyncSystem.LogError(e);
                    }
                });

                await res.Task;
            }
        }

        public static void Log(string message)
        {
            if (AsyncSystem._logging)
            {
                Debug.Log($"[AsyncSystem] {message}");
            }
        }

        public static void LogError(Exception message)
        {
            if (AsyncSystem._logging)
            {
                Debug.LogError($"[AsyncSystem] {message}");
            }
        }

        public static async Task<T> RunOnMain<T>(Func<T> t)
        {
            if (Thread.CurrentThread == AsyncSystem.MainThread)
            {
                return t();
            }
            else
            {
                var res = new TaskCompletionSource<T>();

                AsyncSystem.Log("StartOnMain");

                StartOnMainThread(() =>
                {
                    try
                    {
                        var ret = t();

                        res.SetResult(ret);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                });

                return await res.Task;
            }
        }

        private void ScheduleTask(ISubJob task)
        {
            if (System.Threading.Thread.CurrentThread == AsyncSystem.MainThread)
            {
                var pairing = new Tuple<ISubJob, AsyncSystem>(task, this);

                var gcHandle = GCHandle.Alloc(pairing);

                this.gcHandles.Add(gcHandle);

                var job = new Job()
                {
                    handle = gcHandle
                };

                // We remember the JobHandle so we can complete it later
                this.jobHandles.Add(job.Schedule());
            }
            else
            {
                newTasks.Enqueue(task);
            }
        }

        public static bool IsOnMain => System.Threading.Thread.CurrentThread == AsyncSystem.MainThread;

        private void Update()
        {
            AsyncSystem.FrameCount = Time.frameCount;
            AsyncSystem._logging = this.Logging;

            while (mainBlocks.TryDequeue(out var res))
            {
                res.SetResult(true);
            }

            using (markerMain.Auto())
            {
                mainScheduler.Update();
            }

            for (var i = 0; i < Math.Min(8, scheduler.Tasks.Count); i++)
            {
                this.ScheduleTask(new CompleteJob());
            }

            var gcHandlesToDelete = new List<GCHandle>();
            var jobHandlesToDelete = new List<JobHandle>();

            // Free and complete the scheduled jobs
            for (int i = 0; i < this.jobHandles.Count; ++i)
            {
                var handle = this.jobHandles[i];

                if (handle.IsCompleted)
                {
                    this.gcHandles[i].Free();

                    gcHandlesToDelete.Add(this.gcHandles[i]);
                    jobHandlesToDelete.Add(handle);
                }
            }

            gcHandlesToDelete.ForEach(i => this.gcHandles.Remove(i));
            jobHandlesToDelete.ForEach(i => this.jobHandles.Remove(i));
        }

        public void Enqueue(Task task)
        {
            mainThreadTasks.Enqueue(task);
        }

        private struct Job : IJob
        {
            public GCHandle handle;

            public void Execute()
            {
                var pairing = (Tuple<ISubJob, AsyncSystem>)handle.Target;

                var task = pairing.Item1;

                task.Execute();
            }
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Threading
{
    public class JobSystem : IDisposable
    {
        private readonly int _maxConcurrency;
        private readonly SemaphoreSlim _semaphore;
        private readonly TaskFactory _factory;
        private readonly CancellationTokenSource _cts = new();

        public JobSystem(int maxConcurrency)
        {
            _maxConcurrency = maxConcurrency;
            _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            var scheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, maxConcurrency).ConcurrentScheduler;
            _factory = new TaskFactory(
                _cts.Token,
                TaskCreationOptions.DenyChildAttach,
                TaskContinuationOptions.None,
                scheduler
            );
        }

        public void Dispatch(int totalItems, Action<int, int> workAction)
        {
            int numBatches = _maxConcurrency;
            int batchSize = (int)System.Math.Ceiling((double)totalItems / numBatches);

            Task[] tasks = new Task[numBatches];

            for (int i = 0; i < numBatches; i++)
            {
                int start = i * batchSize;
                int end = System.Math.Min(start + batchSize, totalItems);

                if (start >= totalItems) 
                {
                    tasks[i] = Task.CompletedTask;
                    continue;
                }

                tasks[i] = _factory.StartNew(() =>
                {
                    _semaphore.Wait(_cts.Token);
                    try
                    {
                        workAction(start, end);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[JobSystem Error]: {ex.Message}");
                        throw;
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, _cts.Token);
            }

            Task.WaitAll(tasks);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _semaphore.Dispose();
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
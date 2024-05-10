using System.Collections.Concurrent;

namespace MyAsyncTaskLibrary;

class Program
{
    static void Main(string[] args)
    {
        // code to run
        /*
            for(int i = 0; i < 40; i++)
            {
                var value = i;
                ThreadPool.QueueUserWorkItem(() => 
                {
                    Thread.Sleep(500);
                    Console.WriteLine(value);
                });
            }
        */

        AsyncLocal<int> asyncLocal = new();
        for(int i = 0; i < 40; i++)
        {
            asyncLocal.Value = i;
            MyThreadPool.QueueUserWorkItem(() => 
            {
                Thread.Sleep(500);
                Console.WriteLine(asyncLocal.Value);
            });
        }

        Console.WriteLine("End");
        Console.ReadLine();
    }
}

static class MyThreadPool
{
    private static readonly ConcurrentQueue<(Action callback, ExecutionContext? executionContext)> _queue = new();
    private const int maxThreads = 2;
    private static readonly SemaphoreSlim _semaphore = new(0);

    static MyThreadPool()
    {
        // TODO: implement maxThreads
        Run();
    }

    public static void QueueUserWorkItem(Action callBack)
    {
        _queue.Enqueue((callBack, ExecutionContext.Capture()));
        _semaphore.Release();
    }

    public static void Run()
    {
        for (int i = 0; i < maxThreads; i++)
        { 
            new Thread(() =>
            {
                while(true)
                {
                    _semaphore.Wait();
                    if (!_queue.TryDequeue(out var actionAndContext))
                    {
                        throw new InvalidOperationException("Queue is empty");
                    }

                    if (actionAndContext.executionContext != null)
                    {
                        ExecutionContext.Restore(actionAndContext.executionContext);
                    }

                    actionAndContext.callback();
                }
            }).Start();
        }
    }
}

class MyAsyncTask
{
}

using System.Collections.Concurrent;
using System.Diagnostics;

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

        // using MyThreadPool but no tasks
        // Console.WriteLine("using MyThreadPool but no tasks");
        // AsyncLocal<int> asyncLocal = new();
        // for(int i = 0; i < 40; i++)
        // {
        //     asyncLocal.Value = i;
        //     MyThreadPool.QueueUserWorkItem(() => 
        //     {
        //         Thread.Sleep(500);
        //         Console.WriteLine(asyncLocal.Value);
        //     });
        // }

        AsyncLocal<int> asyncLocal = new();
        var tasks = new MyAsyncTask[40];
        for(int i = 0; i < 40; i++)
        {
            asyncLocal.Value = i;
            tasks[i] = new MyAsyncTask(() => 
            {
                Thread.Sleep(500);
                Console.WriteLine(asyncLocal.Value);
            });
        }

        MyAsyncTask.WaitAll([.. tasks]).Wait();
        
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
    public bool IsCompleted { get; private set; } = false;
    public Exception? FaultException { get; private set; }
    private ConcurrentBag<Action<MyAsyncTask>>? _continuations;

    private ManualResetEvent? _resetEvent;

    public static MyAsyncTask WaitAll(params MyAsyncTask[] tasks)
    {
        var task = new MyAsyncTask(() => 
        {
            foreach (var t in tasks)
            {
                t.Wait();
            }
        });

        return task;
    }

    public MyAsyncTask(Action action)
    {
        MyThreadPool.QueueUserWorkItem(() => 
        {
            try
            {
                action();
                SetCompleted();
            }
            catch (Exception ex)
            {
                SetFaulted(ex);
            }
        });
    }

    public void SetCompleted()
    {
        IsCompleted = true;
        if (_continuations != null)
        {
            foreach (var continuation in _continuations)
            {
                MyThreadPool.QueueUserWorkItem(() =>
                {
                    continuation(this);
                });
            }
        }
    }

    public void SetFaulted(Exception exception)
    {
        IsCompleted = true;
        FaultException = exception;
    }

    public void Wait()
    {
        if (!IsCompleted)
        {
            Debug.Assert(_resetEvent == null);
            _resetEvent = new (false);
            this.ContinueWith(task => _resetEvent.Set());
            _resetEvent.WaitOne();
        }

        return;
    }

    public void ContinueWith(Action<MyAsyncTask> action)
    {
        Interlocked.CompareExchange(ref _continuations, new ConcurrentBag<Action<MyAsyncTask>>(), null);
        _continuations.Add(action);
    }
}

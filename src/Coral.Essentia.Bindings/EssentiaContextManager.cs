using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Coral.Essentia.Bindings;

public interface IEssentiaContextManager
{
    void CreateWorkers();
    Task GetEmbeddings(string filePath, Func<float[], Task> callback);
    Task TeardownWorkers();
}

public class EssentiaContextManager : IEssentiaContextManager
{
    private readonly int _maxInstances;
    private readonly ConcurrentDictionary<Guid, Worker> _workers = [];
    private readonly ILogger<EssentiaContextManager> _logger;
    private readonly SemaphoreSlim _workSemaphore;
    private readonly SemaphoreSlim _contextSemaphore = new SemaphoreSlim(1);
    private readonly string _modelPath;

    private class Worker
    {
        public bool Available { get; set; }
        public EssentiaService Instance { get; set; }
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Executions { get; set; }
        public bool Active { get; set; } = true;
        

        public Worker(bool available, EssentiaService instance)
        {
            Available = available;
            Instance = instance;
            Executions = 0;
        }
    };

    public EssentiaContextManager(ILogger<EssentiaContextManager> logger, int maxInstances = 10)
    {
        _logger = logger;
        _maxInstances = maxInstances;
        _workSemaphore = new SemaphoreSlim(10);
        _modelPath = @"C:\Users\bootie-\Downloads\discogs_track_embeddings-effnet-bs64-1.pb";
    }

    public async Task TeardownWorkers()
    {
        // Wait for all workers to become available
        while (!_workers.Select(t => t.Value).All(w => w.Available))
        {
            await Task.Delay(100);
        }

        foreach (var (_, worker) in _workers)
        {
            worker.Instance.Dispose();
        }
    }

    public void CreateWorkers()
    {
        if (_workers.Count == _maxInstances)
            return;
        
        foreach (var _ in Enumerable.Range(_workers.Count, _maxInstances))
        {
            CreateWorker();
        }
    }

    private Worker CreateWorker()
    {
        var instance = new EssentiaService();
        instance.LoadModel(_modelPath);
        var worker = new Worker(true, instance);
        _workers.AddOrUpdate(worker.Id, worker, (_, _) => worker);
        return worker;
    }
    
    private bool WorkersShouldBeRecycled() => _workers.Sum(t => t.Value.Executions) == _maxInstances * 4;

    public async Task GetEmbeddings(string filePath, Func<float[], Task> callback)
    {
        await _workSemaphore.WaitAsync();
        _ = Task.Run(async () =>
        {
            var worker = await GetWorker();

            try
            {
                Console.WriteLine($"Getting embeddings for track: {filePath}");
                var embeddings = worker.Instance.RunInference(filePath);
                worker.Available = true;
                _workers.AddOrUpdate(worker.Id, worker, (_, _) => worker);
                await callback.Invoke(embeddings);
                Console.WriteLine($"Got embeddings for track: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Getting embeddings for {filePath} failed\n: {ex}");
                worker.Available = true;
                _workers.AddOrUpdate(worker.Id, worker, (_, _) => worker);
                await callback.Invoke([]);
            }
            finally
            {
                UpdateExecutions(worker);
                _workSemaphore.Release();
            }
        });
    }

    private void UpdateExecutions(Worker worker)
    {
        var executions = worker.Executions;
        Interlocked.Increment(ref executions);
        worker.Executions = executions;
        _workers.AddOrUpdate(worker.Id, worker, (_, _) => worker);
        Console.WriteLine($"Context {worker.Instance.ContextId} has done {executions} executions");
    }

    private async Task<Worker> GetWorker()
    {
        await _contextSemaphore.WaitAsync();
        if (WorkersShouldBeRecycled())
        {
            while (!_workers.All(c => c.Value.Available))
            {
                await Task.Delay(2000);
                Console.WriteLine("Workers should be recycled to prevent memory leaks, waiting for workers to complete.");
            }

            foreach (var completedWorker in _workers.Values)
            {
                completedWorker.Instance.Dispose();
                if (_workers.TryRemove(completedWorker.Id, out _))
                {
                    Console.WriteLine($"Successfully disposed context: {completedWorker.Instance.ContextId}");
                }
            }
            
            CreateWorkers();
        }
        
        
        while (_workers.Select(t => t.Value).FirstOrDefault(w => w.Available) == null)
        {
            await Task.Delay(2000);
        }

        var worker = _workers.Select(t => t.Value).First(w => w.Available);
        worker.Available = false;
        _workers.AddOrUpdate(worker.Id, worker, (_, _) => worker);
        _contextSemaphore.Release();

        return worker;
    }
}
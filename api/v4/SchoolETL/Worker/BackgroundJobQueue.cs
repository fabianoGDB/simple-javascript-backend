using SchoolETL.Worker;
using System.Threading.Channels;

namespace SchoolETL.Worker;

public class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<ImportJob> _queue = Channel.CreateUnbounded<ImportJob>();
    public void Enqueue(ImportJob job) => _queue.Writer.TryWrite(job);
    public async ValueTask<ImportJob> DequeueAsync(CancellationToken ct) => await _queue.Reader.ReadAsync(ct);
}

public record ImportJob
{
    public Guid ImportId { get; init; }
    public int Progress { get; set; }
}


public interface IJobStore
{
    void Create(ImportJob job);
    ImportJob? Get(Guid id);
    void Update(ImportJob job);
}


public class InMemoryJobStore : IJobStore
{
    private readonly Dictionary<Guid, ImportJob> _jobs = new();
    public void Create(ImportJob job) => _jobs[job.ImportId] = job;
    public ImportJob? Get(Guid id) => _jobs.TryGetValue(id, out var j) ? j : null;
    public void Update(ImportJob job) => _jobs[job.ImportId] = job;
}
using System.Threading.Channels;

namespace SchoolETL.Worker;

public interface IBackgroundJobQueue
{
    void Enqueue(object job);
    ValueTask<object> DequeueAsync(CancellationToken ct);
}

public sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<object> _queue = Channel.CreateUnbounded<object>();
    public void Enqueue(object job) => _queue.Writer.TryWrite(job);
    public async ValueTask<object> DequeueAsync(CancellationToken ct) => await _queue.Reader.ReadAsync(ct);
}

// Tipos de job
public sealed record DispatchJob(Guid ImportId);
public sealed record StageProcessJob(Guid ImportId, int Etapa);

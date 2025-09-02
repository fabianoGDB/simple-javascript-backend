using System.Threading.Channels;

namespace SchoolETL.Worker;

// Jobs
public readonly record struct DispatchJob(Guid ImportId);
public readonly record struct StageJob(Guid ImportId, int EtapaId, string Name);

// Filas
public interface IDispatchQueue
{
    void Enqueue(DispatchJob job);
    ValueTask<DispatchJob> DequeueAsync(CancellationToken ct);
}

public interface IStageQueue
{
    void Enqueue(StageJob job);
    ValueTask<StageJob> DequeueAsync(CancellationToken ct);
}

// Implementações
public sealed class DispatchQueue : IDispatchQueue
{
    private readonly Channel<DispatchJob> _ch = Channel.CreateUnbounded<DispatchJob>();
    public void Enqueue(DispatchJob job) => _ch.Writer.TryWrite(job);
    public ValueTask<DispatchJob> DequeueAsync(CancellationToken ct) => _ch.Reader.ReadAsync(ct);
}

public sealed class StageQueue : IStageQueue
{
    private readonly Channel<StageJob> _ch = Channel.CreateUnbounded<StageJob>();
    public void Enqueue(StageJob job) => _ch.Writer.TryWrite(job);
    public ValueTask<StageJob> DequeueAsync(CancellationToken ct) => _ch.Reader.ReadAsync(ct);
}

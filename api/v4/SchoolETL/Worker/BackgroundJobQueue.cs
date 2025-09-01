using SchoolETL.Worker.DTOs;
using System.Threading.Channels;

namespace SchoolETL.Worker;
public class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<ImportJob> _queue = Channel.CreateUnbounded<ImportJob>();

    public void Enqueue(ImportJob job) => _queue.Writer.TryWrite(job);

    public async ValueTask<ImportJob> DequeueAsync(CancellationToken ct)
        => await _queue.Reader.ReadAsync(ct);
}
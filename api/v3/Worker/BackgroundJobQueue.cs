using System.Threading.Channels;

namespace SchoolETL.WorkerApi.Worker;

public interface IBackgroundJobQueue
{
    ValueTask QueueAsync(ImportJob job, CancellationToken ct = default);
    ValueTask<ImportJob> DequeueAsync(CancellationToken ct);
}

public class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<ImportJob> _channel;
    public BackgroundJobQueue()
    {
        var opts = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<ImportJob>(opts);
    }

    public ValueTask QueueAsync(ImportJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public ValueTask<ImportJob> DequeueAsync(CancellationToken ct)
        => _channel.Reader.ReadAsync(ct);
}

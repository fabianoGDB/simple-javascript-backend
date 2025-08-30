
namespace SchoolETL.Worker;


public interface IBackgroundJobQueue
{
    void Enqueue(ImportJob job);
    ValueTask<ImportJob> DequeueAsync(CancellationToken ct);
}
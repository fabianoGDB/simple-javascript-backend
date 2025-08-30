using SchoolETL.Services.Interfaces;
using SchoolETL.Worker;

public class ImportWorker : BackgroundService
{
    private readonly IBackgroundJobQueue _queue;
    private readonly IJobStore _store;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImportWorker> _log;

    public ImportWorker(
        IBackgroundJobQueue queue,
        IJobStore store,
        IServiceScopeFactory scopeFactory,
        ILogger<ImportWorker> log)
    {
        _queue = queue;
        _store = store;
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("ImportWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await _queue.DequeueAsync(stoppingToken);

            // cria ESCOPO por job (tudo Scoped vive aqui dentro)
            using var scope = _scopeFactory.CreateScope();
            var etl = scope.ServiceProvider.GetRequiredService<IExcelEtlRunner>();

            try
            {
                job.Status = JobStatus.Running;
                _store.Upsert(job);

                var summary = await etl.RunAsync(job.FilePath, job.Ano, job.Semestre, stoppingToken);

                job.Summary = summary;
                job.Status = JobStatus.Succeeded;
                job.FinishedAtUtc = DateTime.UtcNow;
                _store.Upsert(job);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Job {JobId} failed", job.Id);
                job.Status = JobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.FinishedAtUtc = DateTime.UtcNow;
                _store.Upsert(job);
            }
            finally
            {
                TryDelete(job.FilePath);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}

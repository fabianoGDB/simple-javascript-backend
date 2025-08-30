using NHibernate;
using SchoolETL.Core.Models;
using SchoolETL.Services;


namespace SchoolETL.Worker;


public class ImportWorkerNH : BackgroundService
{
    private readonly ILogger<ImportWorkerNH> _logger;
    private readonly IBackgroundJobQueue _queue;
    private readonly IJobStore _jobs;
    private readonly IServiceScopeFactory _scopeFactory;


    public ImportWorkerNH(ILogger<ImportWorkerNH> logger, IBackgroundJobQueue queue, IJobStore jobs, IServiceScopeFactory scopeFactory)
    { _logger = logger; _queue = queue; _jobs = jobs; _scopeFactory = scopeFactory; }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImportWorkerNH iniciado");
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await _queue.DequeueAsync(stoppingToken);
            using var scope = _scopeFactory.CreateScope();
            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var etl = scope.ServiceProvider.GetRequiredService<IExcelEtlRunner>();


            var import = await session.GetAsync<ImportBatch>(job.ImportId);
            if (import is null) continue;


            _jobs.Update(job with { Progress = 10 });
            await etl.RunAsync(import, stoppingToken);
            _jobs.Update(job with { Progress = 100 });
        }
    }
}
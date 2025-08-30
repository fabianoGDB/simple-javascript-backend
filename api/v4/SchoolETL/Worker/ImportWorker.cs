using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Data;
using SchoolETL.Services;

namespace SchoolETL.Worker;

public class ImportWorker : BackgroundService
{
    private readonly ILogger<ImportWorker> _logger;
    private readonly IBackgroundJobQueue _queue;
    private readonly IJobStore _jobs;
    private readonly IServiceScopeFactory _scopeFactory;

    public ImportWorker(ILogger<ImportWorker> logger, IBackgroundJobQueue queue, IJobStore jobs, IServiceScopeFactory scopeFactory)
    { _logger = logger; _queue = queue; _jobs = jobs; _scopeFactory = scopeFactory; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImportWorker iniciado");
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await _queue.DequeueAsync(stoppingToken);
            _logger.LogInformation("Processando import {Id}", job.ImportId);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DwContext>();
            var etl = scope.ServiceProvider.GetRequiredService<IExcelEtlRunner>();

            var import = await db.ImportBatches.FirstOrDefaultAsync(i => i.Id == job.ImportId, stoppingToken);
            if (import is null) continue;

            _jobs.Update(job with { Progress = 10 });
            await etl.RunAsync(import, stoppingToken);
            _jobs.Update(job with { Progress = 100 });
        }
    }
}


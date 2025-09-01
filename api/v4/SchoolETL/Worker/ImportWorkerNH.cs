using NHibernate;
using SchoolETL.Core.Models;
using SchoolETL.Services;
using ISession = NHibernate.ISession;

namespace SchoolETL.Worker;

/// <summary>
/// HostedService que consome a fila e executa o ETL para cada import.
/// </summary>
public class ImportWorkerNH : BackgroundService
{
    private readonly ILogger<ImportWorkerNH> _logger;
    private readonly IBackgroundJobQueue _queue;
    private readonly IJobStore _jobs;
    private readonly IServiceScopeFactory _scopeFactory;

    public ImportWorkerNH(
        ILogger<ImportWorkerNH> logger,
        IBackgroundJobQueue queue,
        IJobStore jobs,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _queue = queue;
        _jobs = jobs;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImportWorkerNH iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Aguarda um job da fila
            var job = await _queue.DequeueAsync(stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var etl = scope.ServiceProvider.GetRequiredService<IExcelEtlRunner>();

            // Busca o batch no banco
            var import = await session.GetAsync<ImportBatch>(job.ImportId, stoppingToken);
            if (import is null)
            {
                _logger.LogWarning("Import {ImportId} não encontrado; ignorando job", job.ImportId);
                continue;
            }

            try
            {
                _jobs.Update(job with { Progress = 10 });
                await etl.RunAsync(import, stoppingToken); // Executa o ETL (ClosedXML + NH)
                _jobs.Update(job with { Progress = 100 });
                _logger.LogInformation("Import {ImportId} finalizado", job.ImportId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar import {ImportId}", job.ImportId);
                _jobs.Update(job with { Progress = 100 }); // finaliza mesmo em erro; /status lê import.Error
            }
        }
    }
}

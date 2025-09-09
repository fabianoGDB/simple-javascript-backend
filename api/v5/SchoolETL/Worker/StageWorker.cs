using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;
using SchoolETL.Services;

namespace SchoolETL.Worker;

public sealed class StageWorker : BackgroundService
{
    private readonly ILogger<StageWorker> _log;
    private readonly IBackgroundJobQueue _q;
    private readonly ISessionFactory _sf;
    private readonly CsvEtlRunner _etl;

    public StageWorker(ILogger<StageWorker> log, IBackgroundJobQueue q, ISessionFactory sf, CsvEtlRunner etl)
    { _log = log; _q = q; _sf = sf; _etl = etl; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("StageWorker iniciado.");
        while (!ct.IsCancellationRequested)
        {
            var jobObj = await _q.DequeueAsync(ct);
            if (jobObj is not StageProcessJob job) continue;

            using var s = _sf.OpenSession();
            var imp = await s.GetAsync<ImportBatch>(job.ImportId, ct);
            if (imp is null || string.IsNullOrWhiteSpace(imp.WorkingDir)) continue;

            var csvDir = Path.Combine(imp.WorkingDir, "csv");
            var stage = await s.Query<ImportStage>().FirstOrDefaultAsync(x => x.ImportId == job.ImportId && x.EtapaId == job.Etapa, ct);
            if (stage is null) continue;

            try
            {
                _log.LogInformation("Iniciando etapa {E} do import {Id}", job.Etapa, job.ImportId);
                var total = await _etl.RunForImportAsync(job.ImportId, csvDir, ct);
                _log.LogInformation("Etapa {E} do import {Id} concluída. Registros: {T}", job.Etapa, job.ImportId, total);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha na etapa {E} do import {Id}", job.Etapa, job.ImportId);
                using var tx = s.BeginTransaction();
                stage.Status = 3; stage.Error = ex.Message; stage.UpdatedAtUtc = DateTime.UtcNow;
                await s.UpdateAsync(stage, ct);
                await tx.CommitAsync(ct);
            }
        }
    }
}

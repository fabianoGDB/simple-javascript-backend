using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;
using SchoolETL.Services;

namespace SchoolETL.Worker;

public sealed class DispatchWorker : BackgroundService
{
    private readonly ILogger<DispatchWorker> _log;
    private readonly IBackgroundJobQueue _q;
    private readonly ISessionFactory _sf;
    private readonly ExcelToCsvSplitter _splitter;

    public DispatchWorker(ILogger<DispatchWorker> log, IBackgroundJobQueue q, ISessionFactory sf, ExcelToCsvSplitter splitter)
    { _log = log; _q = q; _sf = sf; _splitter = splitter; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("DispatchWorker iniciado.");
        while (!ct.IsCancellationRequested)
        {
            var jobObj = await _q.DequeueAsync(ct);
            if (jobObj is not DispatchJob job) continue;

            using var s = _sf.OpenSession();
            var imp = await s.GetAsync<ImportBatch>(job.ImportId, ct);
            if (imp is null || string.IsNullOrWhiteSpace(imp.StorageUri) || !File.Exists(imp.StorageUri)) continue;

            var workDir = imp.WorkingDir ?? Path.Combine(AppContext.BaseDirectory, "staging", imp.Id.ToString("N"));
            Directory.CreateDirectory(workDir);
            var outCsv = Path.Combine(workDir, "csv");
            Directory.CreateDirectory(outCsv);

            try
            {
                using (var tx = s.BeginTransaction())
                {
                    imp.Status = 1; // Processando
                    imp.WorkingDir = workDir;
                    await s.UpdateAsync(imp, ct);

                    for (int e = 1; e <= 4; e++)
                    {
                        var st = await s.Query<ImportStage>().FirstOrDefaultAsync(x => x.ImportId == imp.Id && x.EtapaId == e, ct);
                        if (st is null)
                        {
                            st = new ImportStage
                            {
                                ImportId = imp.Id,
                                EtapaId = e,
                                Name = $"Etapa {e}",
                                Status = 1,
                                StartedAtUtc = DateTime.UtcNow,
                                UpdatedAtUtc = DateTime.UtcNow
                            };
                            await s.SaveAsync(st, ct);
                        }
                        else
                        {
                            st.Status = 1;
                            st.StartedAtUtc = DateTime.UtcNow;
                            st.UpdatedAtUtc = DateTime.UtcNow;
                            await s.UpdateAsync(st, ct);
                        }
                    }
                    await tx.CommitAsync(ct);
                }

                // Split XLSX em CSVs
                await _splitter.SplitAsync(imp.StorageUri!, outCsv, ct);

                // Atualiza SourcePath e enfileira etapas
                using (var tx2 = s.BeginTransaction())
                {
                    for (int e = 1; e <= 4; e++)
                    {
                        var st = await s.Query<ImportStage>().FirstOrDefaultAsync(x => x.ImportId == imp.Id && x.EtapaId == e, ct);
                        if (st is not null)
                        {
                            st.SourcePath = Path.Combine(outCsv, $"etapa_{e}.csv");
                            await s.UpdateAsync(st, ct);
                        }
                    }
                    await tx2.CommitAsync(ct);
                }

                for (int e = 1; e <= 4; e++)
                    _q.Enqueue(new StageProcessJob(imp.Id, e));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha no DispatchWorker para import {Imp}", imp.Id);
                using var tx = s.BeginTransaction();
                imp.Status = 3; imp.Error = ex.Message;
                await s.UpdateAsync(imp, ct);
                await tx.CommitAsync(ct);
            }
        }
    }
}

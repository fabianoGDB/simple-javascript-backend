using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;
using ISession = NHibernate.ISession;
namespace SchoolETL.Worker;

public class DispatchWorker : BackgroundService
{
    private readonly ILogger<DispatchWorker> _log;
    private readonly IDispatchQueue _dispatch;
    private readonly IStageQueue _stages;
    private readonly IServiceScopeFactory _scope;

    public DispatchWorker(ILogger<DispatchWorker> log, IDispatchQueue dispatch, IStageQueue stages, IServiceScopeFactory scope)
    { _log = log; _dispatch = dispatch; _stages = stages; _scope = scope; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("DispatchWorker iniciado");

        while (!ct.IsCancellationRequested)
        {
            var job = await _dispatch.DequeueAsync(ct);

            using var scope = _scope.CreateScope();
            var session = scope.ServiceProvider.GetRequiredService<ISession>();

            try
            {
                var import = await session.GetAsync<ImportBatch>(job.ImportId, ct);
                if (import is null) continue;

                // Stage "Registros" (0)
                await CreateOrResetStageAsync(session, import.Id, 0, "Registros", ct);
                _stages.Enqueue(new StageJob(import.Id, 0, "Registros"));

                // Etapas 1..4
                for (int etapa = 1; etapa <= 4; etapa++)
                {
                    await CreateOrResetStageAsync(session, import.Id, etapa, $"Etapa {etapa}", ct);
                    _stages.Enqueue(new StageJob(import.Id, etapa, $"Etapa {etapa}"));
                }

                _log.LogInformation("Import {Id}: estágios criados e enfileirados", import.Id);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha ao preparar estágios para import {Id}", job.ImportId);
                await MarkImportErrorAsync(session, job.ImportId, ex.Message, ct);
            }
        }
    }

    private static async Task CreateOrResetStageAsync(ISession session, Guid importId, int etapaId, string name, CancellationToken ct)
    {
        using var tx = session.BeginTransaction();

        var st = await session.Query<ImportStage>()
            .FirstOrDefaultAsync(s => s.ImportId == importId && s.EtapaId == etapaId, ct);

        if (st is null)
        {
            st = new ImportStage { ImportId = importId, EtapaId = etapaId, Name = name, Status = 1 };
            await session.SaveAsync(st, ct);
        }
        else
        {
            st.Status = 1;
            st.StartedAtUtc = null;
            st.FinishedAtUtc = null;
            st.Error = null;
            st.ProcessedRows = null;
            st.UpdatedAtUtc = DateTime.UtcNow;
            await session.UpdateAsync(st, ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task MarkImportErrorAsync(ISession session, Guid importId, string msg, CancellationToken ct)
    {
        using var tx = session.BeginTransaction();
        var imp = await session.GetAsync<ImportBatch>(importId, ct);
        if (imp != null) { imp.Status = 3; imp.Error = msg; await session.UpdateAsync(imp, ct); }
        await tx.CommitAsync(ct);
    }
}

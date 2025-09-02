using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NHibernate;
using NHibernate.Linq;
using Npgsql;
using SchoolETL.Core.Models;
using ISession = NHibernate.ISession;

namespace SchoolETL.Worker;

public sealed class StageWorker : BackgroundService
{
    private readonly string _cs;
    private readonly ILogger<StageWorker> _log;
    private readonly IStageQueue _queue;
    private readonly IServiceScopeFactory _scope;

    public StageWorker(
        IConfiguration config,
        ILogger<StageWorker> log,
        IStageQueue queue,
        IServiceScopeFactory scope)
    {
        _log = log;
        _queue = queue;
        _scope = scope;

        _cs = config.GetConnectionString("Postgres")
              ?? "Host=localhost;Port=32768;Database=bi_edu;Username=myuser;Password=mypassword";
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("StageWorker iniciado");

        while (!ct.IsCancellationRequested)
        {
            var job = await _queue.DequeueAsync(ct);

            using var scope = _scope.CreateScope();
            var session = scope.ServiceProvider.GetRequiredService<ISession>();

            await using var conn = new NpgsqlConnection(_cs);  // ← usa a mesma connection string
            await conn.OpenAsync(ct);

            if (!await TryAcquireLockAsync(conn, job.ImportId, job.EtapaId, ct))
            {
                _log.LogWarning("Lock não obtido para import {Id} etapa {Etapa}", job.ImportId, job.EtapaId);
                continue;
            }

            try
            {
                _log.LogInformation("Iniciando stage {Name} import {Id}", job.Name, job.ImportId);
                await UpdateStageStartAsync(session, job.ImportId, job.EtapaId, ct);

                // TODO: colocar sua lógica real (ler Excel). 
                // Enquanto isso, um stub para evidenciar a pipeline:
                var processed = await StubProcessAsync(session, job.ImportId, job.EtapaId, ct);

                await UpdateStageFinishAsync(session, job.ImportId, job.EtapaId, processed, null, ct);
                _log.LogInformation("Finalizado stage {Name} import {Id} (rows={Rows})", job.Name, job.ImportId, processed);

                if (await IsImportFullyDoneAsync(session, job.ImportId, ct))
                    await MarkImportFinishedAsync(session, job.ImportId, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha no job {Name} Import {Id}", job.Name, job.ImportId);
                await UpdateStageFinishAsync(session, job.ImportId, job.EtapaId, null, ex.Message, ct);
                await MarkImportErrorAsync(session, job.ImportId, "Erro em " + job.Name + ": " + ex.Message, ct);
            }
            finally
            {
                await ReleaseLockAsync(conn, job.ImportId, job.EtapaId, ct);
            }
        }
    }

    // ===== Locks (pg_advisory_lock int,int) =====
    private static (int key1, int key2) GetLockKeys(Guid importId, int etapaId)
    {
        var b = importId.ToByteArray();
        return (BitConverter.ToInt32(b, 0), etapaId);
    }

    private static async Task<bool> TryAcquireLockAsync(Npgsql.NpgsqlConnection conn, Guid importId, int etapaId, CancellationToken ct)
    {
        var (k1, k2) = GetLockKeys(importId, etapaId);
        using var cmd = new Npgsql.NpgsqlCommand("SELECT pg_try_advisory_lock(@k1::int, @k2::int);", conn);
        cmd.Parameters.AddWithValue("k1", NpgsqlTypes.NpgsqlDbType.Integer, k1);
        cmd.Parameters.AddWithValue("k2", NpgsqlTypes.NpgsqlDbType.Integer, k2);
        var res = await cmd.ExecuteScalarAsync(ct);
        return res is bool ok && ok;
    }

    private static async Task ReleaseLockAsync(Npgsql.NpgsqlConnection conn, Guid importId, int etapaId, CancellationToken ct)
    {
        var (k1, k2) = GetLockKeys(importId, etapaId);
        using var cmd = new Npgsql.NpgsqlCommand("SELECT pg_advisory_unlock(@k1::int, @k2::int);", conn);
        cmd.Parameters.AddWithValue("k1", NpgsqlTypes.NpgsqlDbType.Integer, k1);
        cmd.Parameters.AddWithValue("k2", NpgsqlTypes.NpgsqlDbType.Integer, k2);
        await cmd.ExecuteScalarAsync(ct);
    }

    // ===== Atualizações de Stage/Import =====
    private static async Task UpdateStageStartAsync(ISession s, Guid importId, int etapaId, CancellationToken ct)
    {
        using var tx = s.BeginTransaction();
        var st = await s.Query<ImportStage>().FirstAsync(x => x.ImportId == importId && x.EtapaId == etapaId, ct);
        st.Status = 2; // Processando
        st.StartedAtUtc = DateTime.UtcNow;
        st.UpdatedAtUtc = DateTime.UtcNow;
        await s.UpdateAsync(st, ct);
        await tx.CommitAsync(ct);
    }

    private static async Task UpdateStageFinishAsync(ISession s, Guid importId, int etapaId, int? processed, string? error, CancellationToken ct)
    {
        using var tx = s.BeginTransaction();
        var st = await s.Query<ImportStage>().FirstAsync(x => x.ImportId == importId && x.EtapaId == etapaId, ct);
        st.Status = string.IsNullOrEmpty(error) ? (short)3 : (short)4; // 3=Finalizado, 4=Erro
        st.FinishedAtUtc = DateTime.UtcNow;
        st.UpdatedAtUtc = DateTime.UtcNow;
        st.ProcessedRows = processed;
        st.Error = error;
        await s.UpdateAsync(st, ct);
        await tx.CommitAsync(ct);
    }

    private static async Task<bool> IsImportFullyDoneAsync(ISession s, Guid importId, CancellationToken ct)
    {
        var pend = await s.Query<ImportStage>()
            .Where(x => x.ImportId == importId && (x.Status == 1 || x.Status == 2))
            .AnyAsync(ct);
        var erro = await s.Query<ImportStage>()
            .Where(x => x.ImportId == importId && x.Status == 4)
            .AnyAsync(ct);
        return !pend && !erro;
    }

    private static async Task MarkImportFinishedAsync(ISession s, Guid importId, CancellationToken ct)
    {
        using var tx = s.BeginTransaction();
        var imp = await s.GetAsync<ImportBatch>(importId, ct);
        if (imp != null) { imp.Status = 2; imp.Error = null; await s.UpdateAsync(imp, ct); }
        await tx.CommitAsync(ct);
    }

    private static async Task MarkImportErrorAsync(ISession s, Guid importId, string msg, CancellationToken ct)
    {
        using var tx = s.BeginTransaction();
        var imp = await s.GetAsync<ImportBatch>(importId, ct);
        if (imp != null) { imp.Status = 3; imp.Error = msg; await s.UpdateAsync(imp, ct); }
        await tx.CommitAsync(ct);
    }

    // ====== STUB (troque pelo processamento real do Excel) ======
    private static async Task<int> StubProcessAsync(ISession s, Guid importId, int etapaId, CancellationToken ct)
    {
        if (etapaId == 0)
        {
            using var tx = s.BeginTransaction();
            var a = new Aluno { Nome = "Aluno (exemplo)", Matricula = "0001", ImportId = importId };
            await s.SaveAsync(a, ct);
            await tx.CommitAsync(ct);
            return 1;
        }
        // Etapas 1..4: aqui você gravaria fato_nota de cada disciplina/linha
        await Task.Delay(50, ct);
        return 0;
    }
}

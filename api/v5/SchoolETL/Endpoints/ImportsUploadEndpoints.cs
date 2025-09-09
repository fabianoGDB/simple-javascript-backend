using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;
using SchoolETL.Worker;
using ISession = NHibernate.ISession;

namespace SchoolETL.Endpoints;

public static class ImportsUploadEndpoints
{
    public static IEndpointRouteBuilder MapImportsUploadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/imports", async (
            HttpRequest http,
            ISession session,
            IBackgroundJobQueue queue,
            CancellationToken ct) =>
        {
            if (!http.HasFormContentType) return Results.BadRequest("multipart/form-data esperado");
            var form = await http.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0) return Results.BadRequest("arquivo 'file' obrigatório");
            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("apenas arquivos .xlsx são aceitos");

            var stagingRoot = Path.Combine(AppContext.BaseDirectory, "staging");
            Directory.CreateDirectory(stagingRoot);
            var id = Guid.NewGuid();
            var workDir = Path.Combine(stagingRoot, id.ToString("N"));
            var uploads = Path.Combine(workDir, "uploads");
            Directory.CreateDirectory(uploads);

            var storedName = $"{Guid.NewGuid()}_{file.FileName}";
            var fullPath = Path.Combine(uploads, storedName);
            await using (var fs = File.Create(fullPath)) await file.CopyToAsync(fs, ct);

            // período letivo inferido pelo mês atual (simples)
            var ano = DateTime.UtcNow.Year;
            var semestre = DateTime.UtcNow.Month <= 6 ? 1 : 2;

            using var tx = session.BeginTransaction();
            var pl = await session.Query<PeriodoLetivo>().FirstOrDefaultAsync(x => x.Ano == ano && x.Semestre == semestre, ct);
            if (pl is null)
            {
                pl = new PeriodoLetivo { Ano = ano, Semestre = semestre };
                await session.SaveAsync(pl, ct);
            }

            var import = new ImportBatch
            {
                CreatedAtUtc = DateTime.UtcNow,
                OriginalFileName = file.FileName,
                StorageUri = fullPath,
                Status = 1,
                PeriodoLetivoId = pl.Id,
                WorkingDir = workDir
            };
            await session.SaveAsync(import, ct);

            // cria registros de stage pendentes
            for (int e = 1; e <= 4; e++)
            {
                var st = new ImportStage
                {
                    ImportId = import.Id,
                    EtapaId = e,
                    Name = $"Etapa {e}",
                    Status = 1,
                    StartedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await session.SaveAsync(st, ct);
            }

            await tx.CommitAsync(ct);

            // coloca job de dispatch (que fará split e enfileirará etapas)
            queue.Enqueue(new DispatchJob(import.Id));

            return Results.Accepted($"/api/imports/{import.Id}/status", new { jobId = import.Id });
        }).WithOpenApi();
        ;

        return app;
    }
}

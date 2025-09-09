using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NHibernate;
using NHibernate.Linq;
using ISession = NHibernate.ISession;

namespace SchoolETL.Endpoints;

public static class ImportsQueryEndpoints
{
    public static IEndpointRouteBuilder MapImportsQueryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/imports/{id:guid}/status", async (Guid id, ISession session, CancellationToken ct) =>
        {
            session.DefaultReadOnly = true;
            var imp = await session.GetAsync<SchoolETL.Core.Models.ImportBatch>(id, ct);
            if (imp is null) return Results.NotFound();

            var stages = await session.Query<SchoolETL.Core.Models.ImportStage>()
                .Where(s => s.ImportId == id)
                .OrderBy(s => s.EtapaId ?? 0)
                .Select(s => new { s.EtapaId, s.Name, s.Status, s.Error, s.ProcessedRows, s.SourcePath, s.UpdatedAtUtc })
                .ToListAsync(ct);

            var overall = stages.Any() && stages.All(s => s.Status == 2) ? 2 :
                          stages.Any(s => s.Status == 3) ? 3 : imp.Status;

            return Results.Ok(new
            {
                imp.Id,
                imp.OriginalFileName,
                imp.Status,
                imp.Error,
                stages,
                overallStatus = overall
            });
        });

        // opcional: lista todos imports resumidos
        app.MapGet("/api/imports", async (ISession session, CancellationToken ct) =>
        {
            var list = await session.Query<SchoolETL.Core.Models.ImportBatch>()
                .OrderByDescending(i => i.CreatedAtUtc)
                .Select(i => new { i.Id, i.OriginalFileName, i.CreatedAtUtc, i.Status, i.Error })
                .ToListAsync(ct);
            return Results.Ok(list);
        }).WithOpenApi();

        return app;
    }
}

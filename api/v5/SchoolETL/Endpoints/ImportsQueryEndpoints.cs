using Microsoft.AspNetCore.Mvc;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;
using ISession = NHibernate.ISession;
using SchoolETL.DTOs;

namespace SchoolETL.Endpoints;

public static class ImportsQueryEndpoints
{
    public static IEndpointRouteBuilder MapImportsQueries(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/imports");

        g.MapGet("", GetImports);                         // GET /api/imports
        g.MapGet("{id:guid}/status", GetImportStatus);    // GET /api/imports/{id}/status
        g.MapGet("{id:guid}/alunos", GetImportAlunos);    // GET /api/imports/{id}/alunos

        // detalhe do aluno (fora do /imports)
        app.MapGet("/api/alunos/{alunoId:int}", GetAlunoDetalhe);

        return app;
    }

    // GET /api/imports
    private static async Task<IResult> GetImports(ISession session, CancellationToken ct)
    {
        session.FlushMode = FlushMode.Manual;
        session.DefaultReadOnly = true;

        var list = await session.Query<ImportBatch>()
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new ImportedSpreadsheetDto
            {
                Id = i.Id,
                OriginalFileName = i.OriginalFileName ?? string.Empty,
                CreatedAtUtc = i.CreatedAtUtc,
                Status = i.Status,
                Error = i.Error,
                Alunos = 0
            })
            .ToListAsync(ct);

        // conta alunos só para finalizados (status=2), usando SQL nativa (seguro para concorrência)
        // const string sql = @"
        //     SELECT COUNT(*)::int
        //     FROM (
        //       SELECT a.id FROM aluno a WHERE a.import_id = :iid
        //       UNION
        //       SELECT DISTINCT f.aluno_id FROM fato_nota f WHERE f.import_id = :iid
        //     ) s;";

        const string sql = @"
            SELECT CAST(COUNT(*) AS int)
            FROM (
            SELECT a.id FROM aluno a WHERE a.import_id = :iid
            UNION
            SELECT DISTINCT f.aluno_id FROM fato_nota f WHERE f.import_id = :iid
            ) s;
        ";

        foreach (var item in list)
        {
            if (item.Status != 2) continue;

            var q = session.CreateSQLQuery(sql);
            // é Guid; deixe o NH inferir ou especifique o tipo
            q.SetParameter("iid", item.Id /*, NHibernateUtil.Guid*/);

            item.Alunos = q.UniqueResult<int>();  // funciona sem AddScalar
        }

        return Results.Ok(list);
    }

    // GET /api/imports/{id}/status
    private static async Task<IResult> GetImportStatus(Guid id, ISession session, CancellationToken ct)
    {
        session.FlushMode = FlushMode.Manual;
        session.DefaultReadOnly = true;

        var imp = await session.GetAsync<ImportBatch>(id, ct);
        if (imp is null) return Results.NotFound();

        var stages = await session.Query<ImportStage>()
            .Where(s => s.ImportId == id)
            .OrderBy(s => s.EtapaId ?? 0)
            .Select(s => new { s.Name, s.EtapaId, s.Status, s.Error, s.ProcessedRows, s.UpdatedAtUtc })
            .ToListAsync(ct);

        // overallStatus só pra UI (1=pendente/rodando, 2=ok, 3=erro)
        var anyError = stages.Any(s => s.Status == 4);
        var allDone = stages.Any() && stages.All(s => s.Status == 3);
        var overall = anyError ? 3 : (allDone ? 2 : 1);

        return Results.Ok(new { imp.Id, imp.Status, imp.Error, stages, overallStatus = overall });
    }

    // GET /api/imports/{id}/alunos
    private static async Task<IResult> GetImportAlunos(Guid id, ISession session, CancellationToken ct)
    {
        session.FlushMode = FlushMode.Manual;
        session.DefaultReadOnly = true;

        var alunos = await session.Query<Aluno>()
            .Where(a => a.ImportId == id || session.Query<FatoNota>().Any(f => f.ImportId == id && f.AlunoId == a.Id))
            .OrderBy(a => a.Nome)
            .Select(a => new { a.Id, a.Nome, a.Matricula })
            .ToListAsync(ct);

        return Results.Ok(alunos);
    }

    // GET /api/alunos/{alunoId}?importId=
    private static async Task<IResult> GetAlunoDetalhe(int alunoId, [FromQuery] Guid? importId, ISession session, CancellationToken ct)
    {
        session.FlushMode = FlushMode.Manual;
        session.DefaultReadOnly = true;

        var aluno = await session.GetAsync<Aluno>(alunoId, ct);
        if (aluno is null) return Results.NotFound();

        var q = session.Query<FatoNota>().Where(f => f.AlunoId == alunoId);
        if (importId is not null) q = q.Where(f => f.ImportId == importId);

        var fatos = await q
            .Fetch(f => f.Disciplina)
            .Fetch(f => f.Situacao)
            .OrderBy(f => f.PeriodoAvaliativoId)
            .Select(f => new
            {
                disciplina = f.Disciplina!.Sigla,
                f.PeriodoAvaliativoId,
                f.Nota,
                situacao = f.Situacao != null ? f.Situacao!.Descricao : null
            })
            .ToListAsync(ct);

        return Results.Ok(new { aluno.Id, aluno.Nome, aluno.Matricula, fatos });
    }
}

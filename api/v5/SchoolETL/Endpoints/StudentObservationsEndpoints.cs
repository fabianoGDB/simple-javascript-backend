using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;
using ISession = NHibernate.ISession;

namespace SchoolETL.Endpoints;

public static class StudentObservationsEndpoints
{
    public sealed record CreateObservationDto(string Texto, Guid? ImportId);
    public sealed record ObservationResultDto(int Id, int AlunoId, string Texto, DateTime CriadoEmUtc, Guid? ImportId);

    public static IEndpointRouteBuilder MapStudentObservationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/alunos")
                       .WithTags("Students")
                       .WithOpenApi();

        // ============= POST (já existia no seu projeto) =============
        group.MapPost("/{alunoId:int}/observacoes", async (
            int alunoId,
            CreateObservationDto input,
            ISession session,
            CancellationToken ct) =>
        {
            if (input is null || string.IsNullOrWhiteSpace(input.Texto))
                return Results.BadRequest("Texto da observação é obrigatório.");

            if (!await session.Query<Aluno>().AnyAsync(a => a.Id == alunoId, ct))
                return Results.NotFound($"Aluno {alunoId} não encontrado.");

            using var tx = session.BeginTransaction();
            var obs = new AlunoObservacao
            {
                AlunoId = alunoId,
                Texto = input.Texto.Trim(),
                CriadoEmUtc = DateTime.UtcNow,
                ImportId = input.ImportId
            };
            var id = (int)await session.SaveAsync(obs, ct);
            await tx.CommitAsync(ct);

            var dto = new ObservationResultDto(id, alunoId, obs.Texto, obs.CriadoEmUtc, obs.ImportId);
            return Results.Created($"/api/alunos/{alunoId}/observacoes/{id}", dto);
        })
        .WithName("CreateStudentObservation")
        .WithSummary("Cria uma observação para o aluno")
        .WithDescription("Insere uma observação textual vinculada ao aluno e, opcionalmente, a um import específico (importId).")
        .Produces<ObservationResultDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // ============= GET: lista/pesquisa observações =============
        group.MapGet("/{alunoId:int}/observacoes", async (
            int alunoId,
            Guid? importId,
            DateTime? startUtc,
            DateTime? endUtc,
            int page,
            int pageSize,
            ISession session,
            CancellationToken ct) =>
        {
            // sane defaults
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 || pageSize > 200 ? 50 : pageSize;

            session.FlushMode = FlushMode.Manual;
            session.DefaultReadOnly = true;

            // valida existência do aluno
            var exists = await session.Query<Aluno>().AnyAsync(a => a.Id == alunoId, ct);
            if (!exists) return Results.NotFound($"Aluno {alunoId} não encontrado.");

            // query base
            var q = session.Query<AlunoObservacao>()
                           .Where(o => o.AlunoId == alunoId);

            if (importId is not null)
                q = q.Where(o => o.ImportId == importId);

            if (startUtc is not null)
                q = q.Where(o => o.CriadoEmUtc >= startUtc);

            if (endUtc is not null)
                q = q.Where(o => o.CriadoEmUtc <= endUtc);

            // total para paginação
            var total = await q.CountAsync(ct);

            // ordena mais recentes primeiro
            var data = await q.OrderByDescending(o => o.CriadoEmUtc)
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize)
                              .Select(o => new ObservationResultDto(
                                  o.Id, o.AlunoId, o.Texto, o.CriadoEmUtc, o.ImportId))
                              .ToListAsync(ct);

            var result = new
            {
                page,
                pageSize,
                total,
                items = data
            };

            return Results.Ok(result);
        })
        .WithName("ListStudentObservations")
        .WithSummary("Lista observações de um aluno")
        .WithDescription("Suporta filtros por importId e intervalo de datas (startUtc/endUtc) e paginação (page/pageSize).")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // ============= GET: observação específica (opcional) =============
        group.MapGet("/{alunoId:int}/observacoes/{obsId:int}", async (
            int alunoId,
            int obsId,
            ISession session,
            CancellationToken ct) =>
        {
            session.FlushMode = FlushMode.Manual;
            session.DefaultReadOnly = true;

            var obs = await session.GetAsync<AlunoObservacao>(obsId, ct);
            if (obs is null || obs.AlunoId != alunoId)
                return Results.NotFound();

            var dto = new ObservationResultDto(obs.Id, obs.AlunoId, obs.Texto, obs.CriadoEmUtc, obs.ImportId);
            return Results.Ok(dto);
        })
        .WithName("GetStudentObservation")
        .WithSummary("Busca uma observação específica")
        .WithDescription("Retorna a observação pelo identificador, validando o vínculo com o aluno.")
        .Produces<ObservationResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        return app;
    }
}

using Microsoft.AspNetCore.Http.HttpResults;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.DTOs;
using SchoolETL.Core.Models;
using ISession = NHibernate.ISession;

namespace SchoolETL.Endpoints;

public static class DisciplinasEndpoints
{
    public static IEndpointRouteBuilder MapDisciplinasEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/disciplinas").WithTags("Disciplinas");

        // LISTAR (filtros + paginação)
        g.MapGet("",
    async Task<Ok<object>> (ISession s, int? areaId, string? q, int page = 1, int pageSize = 50) =>
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 50;

        var qry = s.Query<Disciplina>().AsQueryable();

        if (areaId.HasValue)
            qry = qry.Where(d => d.AreaId == areaId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToUpper();
            qry = qry.Where(d => d.Nome.ToUpper().Contains(term) || d.Sigla.ToUpper().Contains(term));
        }

        var total = await qry.CountAsync();

        var items = await qry
            .OrderBy(d => d.Nome).ThenBy(d => d.Sigla)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return TypedResults.Ok<object>(new { page, pageSize, total, items });
    });
        // DETALHE
        g.MapGet("/{id:int}", async Task<Results<Ok<DisciplinaDto>, NotFound>>
            (int id, ISession s) =>
        {
            var d = await s.GetAsync<Disciplina>(id);
            if (d is null) return TypedResults.NotFound();

            AreaConhecimento? area = null;
            if (d.AreaId.HasValue) area = await s.GetAsync<AreaConhecimento>(d.AreaId.Value);

            return TypedResults.Ok(new DisciplinaDto(
                d.Id, d.Nome, d.Sigla, d.AreaId, area?.Nome, area?.CorHex, d.CargaHorariaRotulo));
        });

        // CRIAR
        g.MapPost("", async Task<Results<Created<DisciplinaDto>, BadRequest<string>, NotFound>>
            (DisciplinaCreateDto dto, ISession s) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Nome)) return TypedResults.BadRequest("Nome é obrigatório.");
            if (string.IsNullOrWhiteSpace(dto.Sigla)) return TypedResults.BadRequest("Sigla é obrigatória.");

            AreaConhecimento? area = null;
            if (dto.AreaId.HasValue)
            {
                area = await s.GetAsync<AreaConhecimento>(dto.AreaId.Value);
                if (area is null) return TypedResults.NotFound();
            }

            var d = new Disciplina
            {
                Nome = dto.Nome.Trim(),
                Sigla = dto.Sigla.Trim(),
                AreaId = dto.AreaId,
                CargaHorariaRotulo = dto.CargaHorariaRotulo
            };

            using var tx = s.BeginTransaction();
            await s.SaveAsync(d);
            await tx.CommitAsync();

            return TypedResults.Created($"/api/disciplinas/{d.Id}",
                new DisciplinaDto(d.Id, d.Nome, d.Sigla, d.AreaId, area?.Nome, area?.CorHex, d.CargaHorariaRotulo));
        });

        // ATUALIZAR
        g.MapPut("/{id:int}", async Task<Results<Ok<DisciplinaDto>, NotFound, BadRequest<string>>>
            (int id, DisciplinaUpdateDto dto, ISession s) =>
        {
            var d = await s.GetAsync<Disciplina>(id);
            if (d is null) return TypedResults.NotFound();

            if (string.IsNullOrWhiteSpace(dto.Nome)) return TypedResults.BadRequest("Nome é obrigatório.");
            if (string.IsNullOrWhiteSpace(dto.Sigla)) return TypedResults.BadRequest("Sigla é obrigatória.");

            AreaConhecimento? area = null;
            if (dto.AreaId.HasValue)
            {
                area = await s.GetAsync<AreaConhecimento>(dto.AreaId.Value);
                if (area is null) return TypedResults.BadRequest("AreaId inválido.");
            }

            d.Nome = dto.Nome.Trim();
            d.Sigla = dto.Sigla.Trim();
            d.AreaId = dto.AreaId;
            d.CargaHorariaRotulo = dto.CargaHorariaRotulo;

            using var tx = s.BeginTransaction();
            await s.UpdateAsync(d);
            await tx.CommitAsync();

            return TypedResults.Ok(new DisciplinaDto(d.Id, d.Nome, d.Sigla, d.AreaId, area?.Nome, area?.CorHex, d.CargaHorariaRotulo));
        });

        // EXCLUIR
        g.MapDelete("/{id:int}", async Task<Results<NoContent, NotFound>>
            (int id, ISession s) =>
        {
            var d = await s.GetAsync<Disciplina>(id);
            if (d is null) return TypedResults.NotFound();

            using var tx = s.BeginTransaction();
            await s.DeleteAsync(d);
            await tx.CommitAsync();

            return TypedResults.NoContent();
        });

        return app;
    }
}

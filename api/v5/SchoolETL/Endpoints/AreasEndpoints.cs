using Microsoft.AspNetCore.Http.HttpResults;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.DTOs;
using SchoolETL.Core.Models;
using ISession = NHibernate.ISession;

namespace SchoolETL.Endpoints;

public static class AreasEndpoints
{
    public static IEndpointRouteBuilder MapAreasEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/areas").WithTags("Areas");

        // LISTAR (com contagem de disciplinas)
        g.MapGet("", async (ISession s) =>
        {
            var areas = await s.Query<AreaConhecimento>()
                .OrderBy(a => a.Ordem ?? int.MaxValue).ThenBy(a => a.Nome)
                .ToListAsync();

            // contagem por área
            var counts = await s.Query<Disciplina>()
                .Where(d => d.AreaId != null)
                .GroupBy(d => d.AreaId!.Value)
                .Select(gp => new { AreaId = gp.Key, C = gp.Count() })
                .ToListAsync();

            var byId = counts.ToDictionary(x => x.AreaId, x => x.C);
            var dto = areas.Select(a => new AreaListItemDto(
                a.Id, a.Nome, a.CorHex, a.Ordem, a.Ativo,
                byId.TryGetValue(a.Id, out var c) ? c : 0
            ));

            return Results.Ok(dto);
        });

        // DETALHE
        g.MapGet("/{id:int}", async Task<Results<Ok<AreaDto>, NotFound>> (int id, ISession s) =>
        {
            var a = await s.GetAsync<AreaConhecimento>(id);
            if (a is null) return TypedResults.NotFound();
            return TypedResults.Ok(new AreaDto(a.Id, a.Nome, a.CorHex, a.Ordem, a.Ativo));
        });

        // CRIAR
        g.MapPost("", async Task<Results<Created<AreaDto>, BadRequest<string>>>
            (AreaCreateDto dto, ISession s) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Nome))
                return TypedResults.BadRequest("Nome é obrigatório.");

            var a = new AreaConhecimento
            {
                Nome = dto.Nome.Trim(),
                CorHex = dto.CorHex?.Trim(),
                Ordem = dto.Ordem,
                Ativo = dto.Ativo
            };

            using var tx = s.BeginTransaction();
            await s.SaveAsync(a);
            await tx.CommitAsync();

            return TypedResults.Created($"/api/areas/{a.Id}",
                new AreaDto(a.Id, a.Nome, a.CorHex, a.Ordem, a.Ativo));
        });

        // ATUALIZAR
        g.MapPut("/{id:int}", async Task<Results<Ok<AreaDto>, NotFound, BadRequest<string>>>
            (int id, AreaUpdateDto dto, ISession s) =>
        {
            var a = await s.GetAsync<AreaConhecimento>(id);
            if (a is null) return TypedResults.NotFound();

            if (string.IsNullOrWhiteSpace(dto.Nome))
                return TypedResults.BadRequest("Nome é obrigatório.");

            a.Nome = dto.Nome.Trim();
            a.CorHex = dto.CorHex?.Trim();
            a.Ordem = dto.Ordem;
            a.Ativo = dto.Ativo;

            using var tx = s.BeginTransaction();
            await s.UpdateAsync(a);
            await tx.CommitAsync();

            return TypedResults.Ok(new AreaDto(a.Id, a.Nome, a.CorHex, a.Ordem, a.Ativo));
        });

        // EXCLUIR (bloqueia se houver disciplinas associadas)
        g.MapDelete("/{id:int}", async Task<Results<NoContent, Conflict<string>, NotFound>>
            (int id, ISession s) =>
        {
            var a = await s.GetAsync<AreaConhecimento>(id);
            if (a is null) return TypedResults.NotFound();

            var hasDeps = await s.Query<Disciplina>().AnyAsync(d => d.AreaId == id);
            if (hasDeps) return TypedResults.Conflict("Área possui disciplinas associadas.");

            using var tx = s.BeginTransaction();
            await s.DeleteAsync(a);
            await tx.CommitAsync();

            return TypedResults.NoContent();
        });

        return app;
    }
}

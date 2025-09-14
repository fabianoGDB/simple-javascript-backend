using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;
using SchoolETL.DTOs;
using ISession = NHibernate.ISession;

namespace SchoolETL.Endpoints;

public static class StudentsEndpoints
{
    public static IEndpointRouteBuilder MapStudentsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/imports/{id:guid}/alunos", async (Guid id, ISession session, CancellationToken ct) =>
        {
            session.DefaultReadOnly = true;

            var alunos = await session.Query<Aluno>()
                .Where(a => a.ImportId == id || session.Query<FatoNota>().Any(f => f.ImportId == id && f.AlunoId == a.Id))
                .OrderBy(a => a.Nome)
                .Select(a => new { a.Id, a.Nome, a.Matricula })
                .ToListAsync(ct);

            return Results.Ok(alunos);
        }).WithOpenApi();

        app.MapGet("/api/alunos/{alunoId:int}", async (
    int alunoId, Guid? importId, ISession session, CancellationToken ct) =>
        {
            session.FlushMode = FlushMode.Manual;
            session.DefaultReadOnly = true;

            var aluno = await session.GetAsync<Aluno>(alunoId, ct);
            if (aluno is null) return Results.NotFound();

            // query base de fatos para o aluno
            var fatosQ = session.Query<FatoNota>().Where(f => f.AlunoId == alunoId);
            if (importId is not null) fatosQ = fatosQ.Where(f => f.ImportId == importId);

            var dados = await fatosQ
                .Fetch(f => f.Disciplina)
                .Fetch(f => f.Situacao)
                .Select(f => new
                {
                    f.PeriodoAvaliativoId,
                    f.Nota,
                    f.Frequencia,
                    Sit = f.Situacao != null ? f.Situacao!.Descricao : null,
                    DiscSigla = f.Disciplina!.Sigla,
                    Area = f.Disciplina!.Nome
                })
                .ToListAsync(ct);

            // monta DTO de fatos
            var fatos = dados.Select(d => new AlunoFatoDto
            {
                Disciplina = d.DiscSigla,
                Area = d.Area,
                PeriodoAvaliativoId = d.PeriodoAvaliativoId,
                Nota = d.Nota,
                Situacao = d.Sit
            }).ToList();

            // frequência média (ignora null)
            var freq = dados.Where(d => d.Frequencia.HasValue)
                            .Select(d => d.Frequencia!.Value)
                            .DefaultIfEmpty()
                            .Average();

            // situação consolidada
            static string? ConsolidarSituacao(IEnumerable<string?> sits)
            {
                var s = sits.Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => x!.Trim().ToUpperInvariant())
                            .ToHashSet();

                if (s.Contains("REP")) return "REP";
                if (s.Contains("CAN")) return "CAN";
                if (s.Contains("CUR")) return "CUR";
                if (s.Contains("APR")) return "APR";
                return s.FirstOrDefault();
            }

            var situacaoGeral = ConsolidarSituacao(dados.Select(d => d.Sit));

            var dto = new AlunoDetalheDto
            {
                Id = aluno.Id,
                Nome = aluno.Nome,
                Matricula = aluno.Matricula,
                FotoUrl = aluno.FotoPath,        // se for URL absoluto, mapeie aqui
                Frequencia = Math.Round((decimal?)freq ?? 0m, 2),
                Situacao = situacaoGeral,
                Fatos = fatos
            };

            return Results.Ok(dto);
        }).WithOpenApi();


        app.MapGet("/api/alunos/{alunoId:int}/resumo", async (
    int alunoId, Guid? importId, ISession session, CancellationToken ct) =>
        {
            session.FlushMode = FlushMode.Manual;
            session.DefaultReadOnly = true;

            var q = session.Query<FatoNota>().Where(f => f.AlunoId == alunoId && f.PeriodoAvaliativoId >= 1 && f.PeriodoAvaliativoId <= 4);
            if (importId is not null) q = q.Where(f => f.ImportId == importId);

            var list = await q
                .Fetch(f => f.Disciplina)
                .Fetch(f => f.Situacao)
                .Select(f => new
                {
                    f.PeriodoAvaliativoId,
                    Area = f.Disciplina!.Nome,
                    DisciplinaId = f.DisciplinaId,
                    Sit = f.Situacao != null ? f.Situacao!.Descricao : null
                })
                .ToListAsync(ct);

            string BimLabel(int e) => e switch
            {
                1 => "1º",
                2 => "2º",
                3 => "3º",
                4 => "4º",
                _ => e.ToString()
            };

            var resumos = Enumerable.Range(1, 4).Select(e =>
            {
                var bloco = list.Where(x => x.PeriodoAvaliativoId == e).ToList();
                var areas = bloco.Select(x => x.Area?.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                var dis = bloco.Select(x => x.DisciplinaId).Distinct().Count();
                var apr = bloco.Count(x => string.Equals(x.Sit, "APR", StringComparison.OrdinalIgnoreCase));
                var rep = bloco.Count(x => string.Equals(x.Sit, "REP", StringComparison.OrdinalIgnoreCase));

                return new BimestreResumoDto
                {
                    Bimestre = BimLabel(e),
                    Areas = areas,
                    Disciplinas = dis,
                    Aprovados = apr,
                    Reprovados = rep
                };
            }).ToList();

            return Results.Ok(new { bimestres = resumos });
        });

        return app;
    }
}

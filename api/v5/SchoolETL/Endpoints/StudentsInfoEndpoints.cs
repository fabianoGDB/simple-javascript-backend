// Endpoints/StudentsInfoEndpoints.cs
using System.Text;
using Microsoft.AspNetCore.Http;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;
using ISession = NHibernate.ISession;


namespace SchoolETL.Endpoints;

public static class StudentsInfoEndpoints
{
    public static IEndpointRouteBuilder MapStudentsInfoCsvEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/imports/{importId:guid}/alunos/info");

        // EXPORT
        g.MapGet("export", async (Guid importId, ISession session, CancellationToken ct) =>
        {
            var exists = await session.Query<ImportBatch>().AnyAsync(i => i.Id == importId, ct);
            if (!exists) return Results.NotFound();

            var alunos = await session.Query<Aluno>()
                .Where(a => a.ImportId == importId)
                .OrderBy(a => a.Nome)
                .Select(a => new { a.Id, a.Nome, a.Matricula, a.FotoPath })
                .ToListAsync(ct);

            var sb = new StringBuilder();
            sb.AppendLine("id;nome;matricula;foto"); // cabeçalho do modelo  :contentReference[oaicite:3]{index=3}
            foreach (var a in alunos)
            {
                // Normaliza ; e quebras de linha mínimas (modelo simples: sem aspas)
                string esc(string? s) => (s ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
                sb.Append(a.Id).Append(';')
                  .Append(esc(a.Nome)).Append(';')
                  .Append(esc(a.Matricula)).Append(';')
                  .Append(esc(a.FotoPath)).AppendLine();
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"alunos-info-{importId}.csv";
            return Results.File(bytes, "text/csv; charset=utf-8", fileName);
        })
        .WithName("ExportAlunosInfoCsv")
        .WithSummary("Exporta CSV de dados complementares de alunos (nome, matrícula, foto)")
        .Produces<string>(StatusCodes.Status200OK, "text/csv")
        .Produces(StatusCodes.Status404NotFound);

        // IMPORT
        g.MapPost("import", async Task<IResult> (Guid importId, HttpRequest http, ISession session, CancellationToken ct) =>
        {
            if (!http.HasFormContentType) return Results.BadRequest("Envie multipart/form-data com o arquivo em 'file'.");
            var form = await http.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0) return Results.BadRequest("Arquivo 'file' é obrigatório.");

            // Lê CSV em memória (UTF-8)
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);

            string? line;
            int lineNo = 0;
            int updated = 0;

            // valida cabeçalho esperado
            line = await reader.ReadLineAsync();
            lineNo++;
            if (line is null) return Results.BadRequest("CSV vazio.");
            var header = line.Trim();
            if (!header.Equals("id;nome;matricula;foto", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Cabeçalho inválido. Esperado: 'id;nome;matricula;foto'.");

            using var tx = session.BeginTransaction();

            // cache de alunos do import para lookup rápido
            var alunosList = await session.Query<Aluno>()
                .Where(a => a.ImportId == importId)
                .ToListAsync(ct);

            var alunos = alunosList.ToDictionary(a => a.Id);


            while ((line = await reader.ReadLineAsync()) is not null)
            {
                lineNo++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split simples por ';' (compatível com seus exemplos)  :contentReference[oaicite:4]{index=4}
                var cols = line.Split(';');
                if (cols.Length < 4) continue; // ignora linhas incompletas

                if (!int.TryParse(cols[0].Trim(), out var alunoId))
                    continue; // ignora linha inválida

                var nome = cols[1].Trim();
                var matricula = string.IsNullOrWhiteSpace(cols[2]) ? null : cols[2].Trim();
                var foto = string.IsNullOrWhiteSpace(cols[3]) ? null : cols[3].Trim();

                if (!alunos.TryGetValue(alunoId, out var aluno))
                    continue; // aluno não pertence a este import

                // Atualiza campos — nome opcionalmente pode vir diferente; aqui só matrícula/foto
                aluno.Matricula = matricula;
                aluno.FotoPath = foto;

                await session.UpdateAsync(aluno, ct);
                updated++;
            }

            await tx.CommitAsync(ct);

            return Results.Ok(new { importId, updated });
        })
        .WithName("ImportAlunosInfoCsv")
        .WithSummary("Importa CSV (id;nome;matricula;foto) para atualizar matrícula e foto de alunos do import informado")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;
using SchoolETL.Worker;
using ISession = NHibernate.ISession;

namespace SchoolETL.Endpoints;

public static class ImportsUploadEndpoints
{
    public static IEndpointRouteBuilder MapImportsUpload(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/imports", UploadSpreadsheet);
        return app;
    }

    private static async Task<IResult> UploadSpreadsheet(
        HttpRequest http,
        ISession session,
        IDispatchQueue queue,                // ↔ se usa IBackgroundJobQueue, troque aqui
        ILogger<Program> logger,
        CancellationToken ct)
    {
        logger.LogInformation("POST /api/imports");

        if (!http.HasFormContentType)
            return Results.BadRequest("multipart/form-data esperado");

        var form = await http.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
            return Results.BadRequest("arquivo 'file' obrigatório");

        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("apenas arquivos .xlsx são aceitos");

        // 1) Salvar arquivo
        var uploads = Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(uploads);
        var storedName = $"{Guid.NewGuid()}_{file.FileName}";
        var fullPath = Path.Combine(uploads, storedName);
        await using (var fs = File.Create(fullPath))
            await file.CopyToAsync(fs, ct);

        // 2) Inferir período letivo
        var (ano, semestre) = InferPeriodoLetivo(file.FileName, DateTime.UtcNow);

        using var tx = session.BeginTransaction();

        var periodo = await session.Query<PeriodoLetivo>()
            .FirstOrDefaultAsync(p => p.Ano == ano && p.Semestre == semestre, ct);

        if (periodo is null)
        {
            periodo = new PeriodoLetivo { Ano = ano, Semestre = semestre };
            await session.SaveAsync(periodo, ct);
        }

        // 3) Criar import_batch (Id gerado pelo NH)
        var import = new ImportBatch
        {
            CreatedAtUtc = DateTime.UtcNow,
            OriginalFileName = file.FileName,
            StorageUri = fullPath,
            Status = 1, // Processando
            PeriodoLetivoId = periodo.Id
        };

        await session.SaveAsync(import, ct);
        await tx.CommitAsync(ct);

        // 4) Enfileirar o dispatcher
        queue.Enqueue(new DispatchJob(import.Id));   // ↔ se usa IBackgroundJobQueue, troque por queue.Enqueue(new DispatchJob(import.Id));

        logger.LogInformation("Import {ImportId} criado para {Arquivo} (Período {Ano}/{Sem})",
            import.Id, file.FileName, ano, semestre);

        return Results.Accepted($"/api/imports/{import.Id}/status", new { jobId = import.Id });
    }

    /// <summary>
    /// Tenta extrair "ano" e "semestre" a partir do nome do arquivo (ex.: 2025-1, 2025_2, 2025 1, 2025.2).
    /// Se não encontrar, usa o mês atual (1..6 → 1; 7..12 → 2).
    /// </summary>
    private static (int ano, int semestre) InferPeriodoLetivo(string fileName, DateTime nowUtc)
    {
        var name = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;

        var m = Regex.Match(name, @"(?<!\d)(20\d{2})\D*([12])(?!\d)");
        if (m.Success &&
            int.TryParse(m.Groups[1].Value, out var ay) &&
            int.TryParse(m.Groups[2].Value, out var sm) &&
            (sm == 1 || sm == 2))
        {
            return (ay, sm);
        }

        var ano = nowUtc.Year;
        var semestre = (nowUtc.Month <= 6) ? 1 : 2;
        return (ano, semestre);
    }
}

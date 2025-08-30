using System.Net.Mime;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using SchoolETL.Api.Services;
using SchoolETL.Data;
using SchoolETL.DTOs;
using SchoolETL.Repositories;
using SchoolETL.Repositories.Alunos;
using SchoolETL.Repositories.Dimensoes;
using SchoolETL.Repositories.Imports;
using SchoolETL.Repositories.Notas;
using SchoolETL.Services;
using SchoolETL.Services.Interfaces;
using SchoolETL.Worker;

var builder = WebApplication.CreateBuilder(args);

// CORS
builder.Services.AddCors(o => o.AddPolicy("FrontendPolicy", p =>
    p.WithOrigins(builder.Configuration.GetSection("AllowedCors").Get<string[]>() ?? Array.Empty<string>())
     .AllowAnyHeader()
     .AllowAnyMethod()
));

// DbContext
builder.Services.AddDbContext<DwContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
       .UseSnakeCaseNamingConvention());

// Repositories / UoW
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IImportRepository, ImportRepository>();
builder.Services.AddScoped<IAlunoRepository, AlunoRepository>();
builder.Services.AddScoped<IPeriodoLetivoRepository, PeriodoLetivoRepository>();
builder.Services.AddScoped<ISituacaoRepository, SituacaoRepository>();
builder.Services.AddScoped<IFatoNotaRepository, FatoNotaRepository>();

// Runner / Queue / Worker
builder.Services.AddScoped<IExcelEtlRunner, ExcelEtlRunner>();
builder.Services.AddSingleton<IBackgroundJobQueue, BackgroundJobQueue>();
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
builder.Services.AddHostedService<ImportWorker>();

var app = builder.Build();
app.UseCors("FrontendPolicy");

// ---------- Endpoints ----------

// POST /api/imports  (recebe .xlsx e enfileira)
app.MapPost("/api/imports", async (
    HttpRequest req,
    IBackgroundJobQueue queue,
    IJobStore store,
    ILoggerFactory logFactory,
    CancellationToken ct) =>
{
    var log = logFactory.CreateLogger("UploadImport");
    if (!req.HasFormContentType) return Results.BadRequest("Use multipart/form-data");

    var form = await req.ReadFormAsync(ct);
    var file = form.Files["file"];
    if (file is null) return Results.BadRequest("Campo 'file' (xlsx) é obrigatório.");

    var ano = int.TryParse(form["ano"], out var a) ? a : DateTime.UtcNow.Year;
    var semestre = int.TryParse(form["semestre"], out var s) ? s : 1;
    if (semestre is < 1 or > 2) semestre = 1;

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext != ".xlsx") return Results.BadRequest("Formato não suportado. Envie um .xlsx.");

    var uploadRoot = Path.Combine(AppContext.BaseDirectory, "uploads");
    Directory.CreateDirectory(uploadRoot);

    var id = Guid.NewGuid();
    var tmpPath = Path.Combine(uploadRoot, id + ext);
    await using (var fs = File.Create(tmpPath)) await file.CopyToAsync(fs, ct);

    var job = new ImportJob
    {
        Id = id,
        FilePath = tmpPath,
        OriginalFileName = file.FileName,
        Ano = ano,
        Semestre = semestre,
        Status = JobStatus.Queued,
        CreatedAtUtc = DateTime.UtcNow
    };

    store.Upsert(job);
    await queue.QueueAsync(job, ct);
    log.LogInformation("Upload aceito {Id} - {File}", id, file.FileName);

    return Results.Accepted($"/api/imports/{id}", new ImportRequestResult(id));
})
.Accepts<IFormFile>(MediaTypeNames.Multipart.FormData)
.Produces<ImportRequestResult>(StatusCodes.Status202Accepted)
.ProducesProblem(StatusCodes.Status400BadRequest)
.DisableAntiforgery()
.WithName("UploadImport");

// GET /api/imports -> todas as planilhas importadas
app.MapGet("/api/imports", async (DwContext db) =>
{
    var list = await db.Imports
        .OrderByDescending(i => i.CreatedAtUtc)
        .Select(i => new {
            i.Id,
            i.OriginalFileName,
            i.CreatedAtUtc,
            Status = (int)i.Status,
            i.Error
        }).ToListAsync();

    return Results.Ok(list);
});

// GET /api/imports/{id}/status
app.MapGet("/api/imports/{id:guid}/status", (Guid id, IJobStore store, DwContext db) =>
{
    var job = store.Get(id);
    if (job is not null)
        return Results.Ok(new ImportStatusDto(job.Id, job.Status.ToString(), job.Summary, job.ErrorMessage));

    var batch = db.Imports.FirstOrDefault(i => i.Id == id);
    if (batch is null) return Results.NotFound();

    return Results.Ok(new ImportStatusDto(batch.Id, batch.Status.ToString(), null, batch.Error));
});

// GET /api/imports/{id}/alunos
app.MapGet("/api/imports/{id:guid}/alunos", async (Guid id, IAlunoRepository repo) =>
{
    var alunos = await repo.QueryByImport(id)
        .OrderBy(a => a.Nome)
        .Select(a => new { a.Id, a.Nome, a.Matricula })
        .ToListAsync();
    return Results.Ok(alunos);
});

// GET /api/alunos/{alunoId}
app.MapGet("/api/alunos/{alunoId:int}", async (int alunoId, Guid? importId,
    IFatoNotaRepository fatos, IRepository<SchoolETL.Models.Aluno> alunos) =>
{
    var a = await alunos.GetByIdAsync(alunoId);
    if (a is null) return Results.NotFound();

    var q = fatos.QueryByAluno(alunoId, importId);
    var notas = await q.Select(f => new {
        f.PeriodoAvaliativoId,
        Disciplina = f.Disciplina!.Sigla,
        f.Nota,
        Situacao = f.Situacao != null ? f.Situacao.Descricao : null
    }).ToListAsync();

    return Results.Ok(new { Aluno = new { a.Id, a.Nome, a.Matricula }, Notas = notas });
});

// Preflight CORS
app.MapMethods("{*path}", new[] { "OPTIONS" }, () => Results.Ok());

app.Run();

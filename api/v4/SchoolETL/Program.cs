using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;
using SchoolETL.Infrastructure;
using SchoolETL.Services;
using SchoolETL.Worker;

var builder = WebApplication.CreateBuilder(args);

// CORS
const string CorsPolicy = "FrontendPolicy";
var allowedOrigins = builder.Configuration.GetSection("AllowedCors").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// NHibernate + Postgres
var cs = builder.Configuration.GetConnectionString("Postgres")
         ?? "Host=localhost;Port=5432;Database=school_etl;Username=postgres;Password=postgres";
builder.Services.AddNHibernate(cs);

// Services
builder.Services.AddScoped<IExcelEtlRunner, ExcelEtlRunnerNH>();

// Worker (background queue)
builder.Services.AddSingleton<IBackgroundJobQueue, BackgroundJobQueue>();
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
builder.Services.AddHostedService<ImportWorkerNH>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors(CorsPolicy);
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));

// =========== Endpoints ===========
// POST /api/imports – upload excel and enqueue job
app.MapPost("/api/imports", async (
    HttpRequest http,
    int? ano, int? semestre,
    NHibernate.ISession session,
    IBackgroundJobQueue queue,
    IJobStore jobs) =>
{
    if (!http.HasFormContentType) return Results.BadRequest("multipart/form-data esperado");
    var form = await http.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0) return Results.BadRequest("arquivo 'file' obrigatório");

    var uploads = Path.Combine(AppContext.BaseDirectory, "uploads");
    Directory.CreateDirectory(uploads);

    var id = Guid.NewGuid();
    var storedName = $"{id}_{file.FileName}";
    var fullPath = Path.Combine(uploads, storedName);
    await using (var fs = File.Create(fullPath))
        await file.CopyToAsync(fs);

    var anoVal = ano ?? DateTime.UtcNow.Year;
    var semVal = semestre ?? 1;

    using (var tx = session.BeginTransaction())
    {
        // Get or create PeriodoLetivo
        var periodo = await session.Query<PeriodoLetivo>()
            .FirstOrDefaultAsync(p => p.Ano == anoVal && p.Semestre == semVal);
        if (periodo is null)
        {
            periodo = new PeriodoLetivo { Ano = anoVal, Semestre = semVal };
            await session.SaveAsync(periodo);
        }

        var import = new ImportBatch
        {
            Id = id,
            CreatedAtUtc = DateTime.UtcNow,
            OriginalFileName = file.FileName,
            StorageUri = fullPath,
            Status = 1,
            PeriodoLetivoId = periodo.Id
        };
        await session.SaveAsync(import);
        await tx.CommitAsync();
    }

    var job = new ImportJob { ImportId = id };
    jobs.Create(job);
    queue.Enqueue(job);

    return Results.Accepted($"/api/imports/{id}/status", new { jobId = id });
});

// GET /api/imports – list imports
app.MapGet("/api/imports", async (NHibernate.ISession session) =>
{
    var data = await session.Query<ImportBatch>()
        .OrderByDescending(i => i.CreatedAtUtc)
        .Select(i => new {
            i.Id,
            i.OriginalFileName,
            i.CreatedAtUtc,
            i.Status,
            i.Error
        }).ToListAsync();
    return Results.Ok(data);
});

// GET /api/imports/{id}/status – polling
app.MapGet("/api/imports/{id:guid}/status", async (Guid id, NHibernate.ISession session, IJobStore jobs) =>
{
    var imp = await session.GetAsync<ImportBatch>(id);
    if (imp is null) return Results.NotFound();
    var job = jobs.Get(id);
    return Results.Ok(new { imp.Id, imp.Status, imp.Error, progress = job?.Progress ?? 0 });
});

// GET /api/imports/{id}/alunos – alunos por import
app.MapGet("/api/imports/{id:guid}/alunos", async (Guid id, NHibernate.ISession session) =>
{
    var alunos = await session.Query<Aluno>()
        .Where(a => a.ImportId == id || session.Query<FatoNota>().Any(f => f.ImportId == id && f.AlunoId == a.Id))
        .OrderBy(a => a.Nome)
        .Select(a => new { a.Id, a.Nome, a.Matricula })
        .ToListAsync();
    return Results.Ok(alunos);
});

// GET /api/alunos/{alunoId}?importId=
app.MapGet("/api/alunos/{alunoId:int}", async (int alunoId, Guid? importId, NHibernate.ISession session) =>
{
    var aluno = await session.GetAsync<Aluno>(alunoId);
    if (aluno is null) return Results.NotFound();

    var query = session.Query<FatoNota>()
        .Where(f => f.AlunoId == alunoId);
    if (importId is not null) query = query.Where(f => f.ImportId == importId);

    var fatos = await query
        .Fetch(f => f.Disciplina)
        .Fetch(f => f.Situacao)
        .OrderBy(f => f.PeriodoAvaliativoId)
        .Select(f => new {
            disciplina = f.Disciplina!.Sigla,
            f.PeriodoAvaliativoId,
            f.Nota,
            situacao = f.Situacao != null ? f.Situacao!.Descricao : null
        }).ToListAsync();

    return Results.Ok(new { aluno.Id, aluno.Nome, aluno.Matricula, fatos });
});

app.Run();

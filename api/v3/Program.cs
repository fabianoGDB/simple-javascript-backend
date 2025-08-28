using Microsoft.AspNetCore.Http.Features;
using SchoolETL.WorkerApi.Worker;
using SchoolETL.WorkerApi.Services;
using SchoolETL.WorkerApi.Services.Interfaces;
using SchoolETL.WorkerApi.DTOs;
using SchoolETL.Repositories;
using SchoolETL.Repositories.Alunos;
using SchoolETL.Repositories.Imports;
using SchoolETL.Repositories.Notas;
using SchoolETL.Repositories.Disciplinas;
using SchoolETL.Repositories.Cursos;
using SchoolETL.Repositories.Dimensoes;
using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Data;
using SchoolETL.Core.Models;
using SchoolETL.Api.Services;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<DwContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

builder.Services.AddScoped<IImportRepository, ImportRepository>();
builder.Services.AddScoped<IImportSheetRepository, ImportSheetRepository>();
builder.Services.AddScoped<IAlunoRepository, AlunoRepository>();
builder.Services.AddScoped<IFatoNotaRepository, FatoNotaRepository>();
builder.Services.AddScoped<IDisciplinaRepository, DisciplinaRepository>();
builder.Services.AddScoped<ICursoRepository, CursoRepository>();
builder.Services.AddScoped<IPeriodoLetivoRepository, PeriodoLetivoRepository>();
builder.Services.AddScoped<ISituacaoRepository, SituacaoRepository>();



// uploads grandes (ajuste se necessário)
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 1024L * 1024 * 300); // 300 MB

// Fila de background baseada em Channel
builder.Services.AddSingleton<IBackgroundJobQueue, BackgroundJobQueue>();

// Armazenamento simples de status em memória (troque por DB se quiser)
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();

// Worker
builder.Services.AddHostedService<ImportWorker>();

// Serviço de ETL (plugue seu runner real aqui)
builder.Services.AddScoped<IExcelEtlRunner, ExcelEtlRunner>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();


app.MapGet("/api/imports", async (IImportRepository repo) =>
{
    var list = await repo.ListWithPeriodo()
        .Select(i => new
        {
            i.Id,
            i.FileName,
            i.CreatedAtUtc,
            Periodo = i.PeriodoLetivo != null ? i.PeriodoLetivo.Descricao : null
        }).ToListAsync();

    return Results.Ok(list);
});


app.MapGet("/api/imports/{importId:guid}/alunos", async (Guid importId, IAlunoRepository repo) =>
{
    var alunos = await repo.QueryByImport(importId)
        .OrderBy(a => a.Nome)
        .Select(a => new { a.Id, a.Nome, a.Matricula, a.FrequenciaGeral, a.SituacaoCurso })
        .ToListAsync();
    return Results.Ok(alunos);
});

app.MapGet("/api/alunos/{alunoId:int}", async (int alunoId, Guid? importId,
    IRepository<Aluno> alunos, IFatoNotaRepository fatos) =>
{
    var aluno = await alunos.GetByIdAsync(alunoId);
    if (aluno is null) return Results.NotFound();

    var q = fatos.QueryByAluno(alunoId, importId);

    var medias = await q.GroupBy(f => f.BimestreId)
        .Select(g => new { Bimestre = g.Key, Media = Math.Round(g.Average(x => x.Nota ?? 0), 2) })
        .OrderBy(x => x.Bimestre).ToListAsync();

    var notas = await q.OrderBy(f => f.BimestreId).ThenBy(f => f.EtapaId)
        .Select(f => new
        {
            Disciplina = f.Disciplina!.Sigla,
            f.EtapaId,
            f.BimestreId,
            f.Nota,
            Situacao = f.Situacao != null ? f.Situacao.Descricao : null
        }).ToListAsync();

    return Results.Ok(new
    {
        Aluno = new { aluno.Id, aluno.Nome, aluno.Matricula, aluno.FrequenciaGeral, aluno.SituacaoCurso },
        ImportIdUsado = importId,
        MediasPorBimestre = medias,
        Notas = notas
    });
});



// POST /api/imports  -> recebe planilha e enfileira o ETL
app.MapPost("/api/imports", async (HttpRequest req, IBackgroundJobQueue queue, IJobStore store) =>
{
    if (!req.HasFormContentType) return Results.BadRequest("Use multipart/form-data");

    var form = await req.ReadFormAsync();
    var file = form.Files["file"];
    if (file is null) return Results.BadRequest("Campo 'file' (xlsx) é obrigatório.");

    var ano = int.TryParse(form["ano"], out var a) ? a : DateTime.UtcNow.Year;
    var semestre = int.TryParse(form["semestre"], out var s) ? s : 1;

    // salva temporário
    var uploadRoot = Path.Combine(AppContext.BaseDirectory, "uploads");
    Directory.CreateDirectory(uploadRoot);
    var tempPath = Path.Combine(uploadRoot, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Path.GetFileName(file.FileName)}");
    await using (var fs = File.Create(tempPath))
        await file.CopyToAsync(fs);

    // cria job
    var job = new ImportJob
    {
        Id = Guid.NewGuid(),
        FilePath = tempPath,
        Ano = ano,
        Semestre = semestre,
        Status = JobStatus.Queued,
        CreatedAtUtc = DateTime.UtcNow
    };

    store.Upsert(job);          // guarda status inicial
    await queue.QueueAsync(job); // enfileira

    return Results.Accepted($"/api/imports/{job.Id}", new ImportRequestResult(job.Id));
})
.Accepts<IFormFile>("multipart/form-data")
.Produces<ImportRequestResult>(202)
.WithName("UploadImport");

// GET /api/imports/{jobId} -> status do job
app.MapGet("/api/imports/{jobId:guid}", (Guid jobId, IJobStore store) =>
{
    var job = store.Get(jobId);
    if (job is null) return Results.NotFound();

    var dto = new ImportStatusDto(
        JobId: job.Id,
        Status: job.Status.ToString(),
        Summary: job.Summary,
        Error: job.ErrorMessage
    );
    return Results.Ok(dto);
})
.Produces<ImportStatusDto>(200);


app.UseCors("AllowAll");
app.Run();

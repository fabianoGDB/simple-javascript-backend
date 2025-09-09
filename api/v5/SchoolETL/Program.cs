using SchoolETL.Infrastructure;
using SchoolETL.Endpoints;
using SchoolETL.Services;
using SchoolETL.Worker;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// CORS
const string CorsPolicy = "FrontendPolicy";
var allowed = builder.Configuration.GetSection("AllowedCors").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(allowed).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// NH + Npgsql DataSource (pool)
var cs = builder.Configuration.GetConnectionString("Postgres")
         ?? "Host=localhost;Port=32768;Database=bi_edu;Username=myuser;Password=mypassword";
builder.Services.AddNHibernate(cs);
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(cs)); // para StageWorker locks/sql nativa

builder.Services.AddTransient<ExcelToCsvSplitter>();
builder.Services.AddTransient<CsvEtlRunner>();   // se você tiver um runner para CSV -> Transient

// ------------ Infra de fila e jobs (Singleton) ------------
builder.Services.AddSingleton<IBackgroundJobQueue, BackgroundJobQueue>();

// ------------ Hosted Services (sempre Singleton) ------------
// IMPORTANTE: eles agora resolvem serviços Scoped/Transient via IServiceScopeFactory
builder.Services.AddHostedService<DispatchWorker>();
builder.Services.AddHostedService<StageWorker>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors(CorsPolicy);
app.UseSwagger();
app.UseSwaggerUI();


app.MapRootEndpoints();

// Endpoints (5 partes)
app.MapImportsUploadEndpoints();    // [1] POST /api/imports (upload + split CSV)
app.MapImportsProcessEndpoints();   // [2] POST /api/imports/{id}/process
app.MapImportsQueryEndpoints();     // [3] GET  /api/imports/{id}/status
app.MapStudentsEndpoints();         // [4] GET  /api/imports/{id}/alunos && [5] GET  /api/alunos/{alunoId}
app.MapStudentObservationsEndpoints();
app.MapStudentsInfoCsvEndpoints();

app.Run();

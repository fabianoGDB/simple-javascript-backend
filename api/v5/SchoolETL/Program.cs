using NHibernate;
using SchoolETL.Infrastructure;
using SchoolETL.Services;
using SchoolETL.Worker;
using SchoolETL.Endpoints;


var builder = WebApplication.CreateBuilder(args);

// CORS (inclui porta 5149)
const string CorsPolicy = "FrontendPolicy";
var allowed = builder.Configuration.GetSection("AllowedCors").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(allowed).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var cs = builder.Configuration.GetConnectionString("Postgres")
         ?? "Host=localhost;Port=32768;Database=bi_edu;Username=myuser;Password=mypassword";

builder.Services.AddNHibernate(cs);


// Serviços e ETL
builder.Services.AddScoped<IExcelEtlRunner, ExcelEtlRunnerNH>();

// Fila e Workers (Dispatcher + Stage)
builder.Services.AddSingleton<IDispatchQueue, DispatchQueue>();
builder.Services.AddSingleton<IStageQueue, StageQueue>();
builder.Services.AddHostedService<DispatchWorker>();
builder.Services.AddHostedService<StageWorker>();

builder.Logging.SetMinimumLevel(LogLevel.Information);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors(CorsPolicy);
app.UseSwagger();
app.UseSwaggerUI();

app.MapRootEndpoints();        // "/"
app.MapImportsUpload();        // POST /api/imports
app.MapImportsQueries();       // GETs /api/imports*, /api/alunos/{id}

app.Run();

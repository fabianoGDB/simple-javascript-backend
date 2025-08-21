using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolETL.ConsoleApp.Data;
using SchoolETL.ConsoleApp.Service;

// ======= Parse args simples =======
string? file = null; int ano = DateTime.Now.Year; int semestre = 1;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--file": file = args[++i]; break;
        case "--ano": ano = int.Parse(args[++i]); break;
        case "--semestre": semestre = int.Parse(args[++i]); break;
    }
}

if (string.IsNullOrWhiteSpace(file) || !System.IO.File.Exists(file))
{
    System.Console.WriteLine("Uso: dotnet run -- --file caminho/planilha.xlsx [--ano 2025] [--semestre 1|2]");
    return;
}

// ======= Config/DI =======
var cfg = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection()
    .AddLogging(b => b.AddSimpleConsole(o => { o.TimestampFormat = "HH:mm:ss "; }))
    .AddDbContext<DwContext>(opt => opt.UseNpgsql(cfg.GetConnectionString("Postgres")))
    .AddSingleton<IConfiguration>(cfg)
    .AddScoped<ExcelEtlRunner>()
    .BuildServiceProvider();

var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("ETL");

try
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DwContext>();
    // Garante DB/migrations aplicadas pelo EF se desejar (opcional):
    // db.Database.Migrate();

    var runner = scope.ServiceProvider.GetRequiredService<ExcelEtlRunner>();
    var summary = await runner.RunAsync(file!, ano, semestre);

    logger.LogInformation("ImportId: {ImportId}", summary.ImportId);
    logger.LogInformation("Alunos: {A} | Disciplinas: {D} | Notas: {N} | Ignoradas: {I}",
        summary.AlunosInseridos, summary.DisciplinasInseridas, summary.NotasInseridas, summary.LinhasIgnoradas);

    if (summary.Avisos.Count > 0)
    {
        logger.LogWarning("Avisos:");
        foreach (var a in summary.Avisos) logger.LogWarning(" - {Aviso}", a);
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Falha no ETL");
    Environment.ExitCode = -1;
}

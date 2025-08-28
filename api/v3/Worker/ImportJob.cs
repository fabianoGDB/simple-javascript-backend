using SchoolETL.WorkerApi.DTOs;

namespace SchoolETL.WorkerApi.Worker;

public enum JobStatus { Queued, Running, Succeeded, Failed }

public class ImportJob
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = default!;
    public int Ano { get; set; }
    public int Semestre { get; set; }
    public JobStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }

    // Resultado do ETL
    public ImportSummary? Summary { get; set; }
}

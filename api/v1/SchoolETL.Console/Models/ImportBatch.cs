namespace SchoolETL.ConsoleApp.Models;

public class ImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? FileName { get; set; }
    public int? PeriodoLetivoId { get; set; }
    public PeriodoLetivo? PeriodoLetivo { get; set; }
}
using SchoolETL.Enums;

namespace SchoolETL.Models;

public class ImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? OriginalFileName { get; set; }
    public string? StorageUri { get; set; }
    public SpreadsheetsStatus Status { get; set; } = SpreadsheetsStatus.Processando;
    public string? Error { get; set; }
    public string? FileHash { get; set; }
    public int? PeriodoLetivoId { get; set; }
    public PeriodoLetivo? PeriodoLetivo { get; set; }
    public ICollection<ImportSheet> Sheets { get; set; } = new List<ImportSheet>();
}
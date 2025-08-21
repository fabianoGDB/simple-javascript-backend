namespace SchoolETL.ConsoleApp.Models;

public class ImportSheet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImportId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ImportBatch? ImportBatch { get; set; }
}
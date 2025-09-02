namespace SchoolETL.Core.Models;

public class ImportBatch
{
    public virtual Guid Id { get; protected set; }   // ok manter protected set
    public virtual DateTime CreatedAtUtc { get; set; }
    public virtual string? OriginalFileName { get; set; }
    public virtual string? StorageUri { get; set; }
    public virtual short Status { get; set; }
    public virtual string? Error { get; set; }
    public virtual string? FileHash { get; set; }
    public virtual int? PeriodoLetivoId { get; set; }
}


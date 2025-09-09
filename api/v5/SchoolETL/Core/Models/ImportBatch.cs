namespace SchoolETL.Core.Models;

public class ImportBatch
{
    public virtual Guid Id { get; protected set; }            // GuidComb (gerado no NH)
    public virtual DateTime CreatedAtUtc { get; set; }
    public virtual string? OriginalFileName { get; set; }
    public virtual string? StorageUri { get; set; }           // caminho do .xlsx
    public virtual short Status { get; set; }                 // 1=Processando,2=Finalizado,3=Erro
    public virtual string? Error { get; set; }
    public virtual string? FileHash { get; set; }
    public virtual int? PeriodoLetivoId { get; set; }

    public virtual string? WorkingDir { get; set; }           // pasta staging do split
}

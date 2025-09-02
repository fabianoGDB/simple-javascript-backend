namespace SchoolETL.Core.Models;

public class ImportStage
{
    // Torne o ctor sem parâmetros PÚBLICO
    public ImportStage() { }

    public virtual int Id { get; protected set; }
    public virtual Guid ImportId { get; set; }
    public virtual int? EtapaId { get; set; }
    public virtual string Name { get; set; } = string.Empty;

    // 1=Pendente, 2=Processando, 3=Finalizado, 4=Erro
    public virtual short Status { get; set; }

    public virtual DateTime? StartedAtUtc { get; set; }
    public virtual DateTime? FinishedAtUtc { get; set; }
    public virtual DateTime? UpdatedAtUtc { get; set; }
    public virtual int? ProcessedRows { get; set; }
    public virtual string? Error { get; set; }
}

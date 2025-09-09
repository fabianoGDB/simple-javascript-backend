namespace SchoolETL.Core.Models;

public class ImportStage
{
    public virtual int Id { get; protected set; }
    public virtual Guid ImportId { get; set; }
    public virtual int? EtapaId { get; set; }     // 1..4; null = Registros
    public virtual string Name { get; set; } = "";
    public virtual short Status { get; set; }     // 1=Processando,2=Finalizado,3=Erro
    public virtual string? Error { get; set; }
    public virtual int ProcessedRows { get; set; }
    public virtual DateTime? StartedAtUtc { get; set; }
    public virtual DateTime? FinishedAtUtc { get; set; }
    public virtual DateTime? UpdatedAtUtc { get; set; }

    public virtual string? SourcePath { get; set; } // caminho do CSV desta etapa
}

namespace SchoolETL.Core.Models;

public class AlunoStatusImport
{
    public virtual int Id { get; protected set; }
    public virtual Guid ImportId { get; set; }
    public virtual int AlunoId { get; set; }
    public virtual int? PeriodoLetivoId { get; set; }
    public virtual decimal? FrequenciaGeral { get; set; } // % (se houver na aba "Registros")
    public virtual string? SituacaoCurso { get; set; }
    public virtual DateTime CriadoEmUtc { get; set; }
}

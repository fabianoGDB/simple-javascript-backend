namespace SchoolETL.Core.Models;

public class PeriodoLetivo
{
    public virtual int Id { get; protected set; }
    public virtual int Ano { get; set; }
    public virtual int Semestre { get; set; }
    public virtual string? Descricao { get; set; }
}

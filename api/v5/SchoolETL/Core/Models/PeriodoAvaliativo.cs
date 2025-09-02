namespace SchoolETL.Core.Models;

public class PeriodoAvaliativo
{
    public virtual int Id { get; protected set; } // 1..4, 99
    public virtual string Nome { get; set; } = string.Empty;
    public virtual bool Final { get; set; }
}

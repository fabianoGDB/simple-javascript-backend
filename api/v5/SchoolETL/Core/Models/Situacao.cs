namespace SchoolETL.Core.Models;

public class Situacao
{
    public virtual int Id { get; protected set; }
    public virtual string Descricao { get; set; } = string.Empty; // APR/REP/CAN/CUR/OUT
}

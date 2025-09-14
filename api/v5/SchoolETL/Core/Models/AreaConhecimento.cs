namespace SchoolETL.Core.Models;

public class AreaConhecimento
{
    public virtual int Id { get; protected set; }
    public virtual string Nome { get; set; } = null!;
    public virtual string? CorHex { get; set; }
    public virtual int? Ordem { get; set; }
    public virtual bool Ativo { get; set; } = true;
}
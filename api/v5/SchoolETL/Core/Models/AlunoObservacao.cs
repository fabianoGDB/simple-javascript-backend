namespace SchoolETL.Core.Models;

public class AlunoObservacao
{
    public virtual int Id { get; protected set; }
    public virtual int AlunoId { get; set; }
    public virtual string Texto { get; set; } = string.Empty;
    public virtual DateTime CriadoEmUtc { get; set; }
    public virtual Guid? ImportId { get; set; }
}

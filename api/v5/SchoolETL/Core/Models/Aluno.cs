namespace SchoolETL.Core.Models;

public class Aluno
{
    public virtual int Id { get; protected set; }
    public virtual Guid? ImportId { get; set; }
    public virtual string Nome { get; set; } = string.Empty;
    public virtual string? Matricula { get; set; }
    public virtual string? FotoPath { get; set; }
}

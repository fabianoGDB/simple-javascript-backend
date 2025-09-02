namespace SchoolETL.Core.Models;

public class Disciplina
{
    public virtual int Id { get; protected set; }
    public virtual Guid? ImportId { get; set; }
    public virtual string Sigla { get; set; } = string.Empty;
    public virtual string? NomeArea { get; set; }
    public virtual string? CargaHorariaRotulo { get; set; }
}

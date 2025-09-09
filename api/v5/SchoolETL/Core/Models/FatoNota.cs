namespace SchoolETL.Core.Models;


public class FatoNota
{
    public virtual int Id { get; protected set; }
    public virtual Guid ImportId { get; set; }
    public virtual int AlunoId { get; set; }
    public virtual int DisciplinaId { get; set; }
    public virtual int PeriodoAvaliativoId { get; set; }
    public virtual int? SituacaoId { get; set; }
    public virtual int PeriodoLetivoId { get; set; }
    public virtual decimal? Nota { get; set; }
    public virtual decimal? Frequencia { get; set; } // aqui guardamos "F" (faltas)
    public virtual Disciplina? Disciplina { get; set; }
    public virtual Situacao? Situacao { get; set; }
}

namespace SchoolETL.Models;
public class FatoNota
{
    public long Id { get; set; }
    public Guid ImportId { get; set; }
    public int AlunoId { get; set; }
    public int DisciplinaId { get; set; }
    public int PeriodoAvaliativoId { get; set; }
    public int? SituacaoId { get; set; }
    public int? PeriodoLetivoId { get; set; }
    public decimal? Nota { get; set; }
    public decimal? Frequencia { get; set; }

    public Aluno? Aluno { get; set; }
    public Disciplina? Disciplina { get; set; }
    public PeriodoAvaliativo? PeriodoAvaliativo { get; set; }
    public Situacao? Situacao { get; set; }
    public PeriodoLetivo? PeriodoLetivo { get; set; }
}

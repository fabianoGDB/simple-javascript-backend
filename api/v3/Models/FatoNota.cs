namespace SchoolETL.Core.Models;

public class FatoNota
{
    public int Id { get; set; }

    // Rastreabilidade: sempre aponta para o lote que gerou o fato
    public Guid ImportId { get; set; }

    public int AlunoId { get; set; }
    public Aluno? Aluno { get; set; }

    public int DisciplinaId { get; set; }
    public Disciplina? Disciplina { get; set; }

    public int BimestreId { get; set; }
    public Bimestre? Bimestre { get; set; }

    public int EtapaId { get; set; }
    public Etapa? Etapa { get; set; }

    public int? CursoId { get; set; }
    public Curso? Curso { get; set; }

    public int? SituacaoId { get; set; }
    public Situacao? Situacao { get; set; }

    public int? PeriodoLetivoId { get; set; }
    public PeriodoLetivo? PeriodoLetivo { get; set; }

    public decimal? Nota { get; set; }        // 0..10 (flexível)
    public decimal? Frequencia { get; set; }  // % por disciplina (se houver)
}
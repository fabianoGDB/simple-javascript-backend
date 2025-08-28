namespace SchoolETL.Core.Models;

public class Aluno
{
    public int Id { get; set; }
    public Guid? ImportId { get; set; }             // lote que criou o aluno (opcional)
    public string? Nome { get; set; }
    public string? Matricula { get; set; }          // pode ser nula
    public decimal? FrequenciaGeral { get; set; }   // % 0..100
    public string? SituacaoCurso { get; set; }      // Matriculado/Evasão/Cancelado etc.
    public string? FotoPath { get; set; }

    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
    public ICollection<AlunoObservacao> Observacoes { get; set; } = new List<AlunoObservacao>();
}
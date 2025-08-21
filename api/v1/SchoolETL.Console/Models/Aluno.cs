namespace SchoolETL.ConsoleApp.Models;

public class Aluno
{
    public int Id { get; set; }
    public Guid? ImportId { get; set; }
    public string? Nome { get; set; }
    public string? Matricula { get; set; } // pode ser nulo
    public decimal? FrequenciaGeral { get; set; }
    public string? SituacaoCurso { get; set; }
    public string? FotoPath { get; set; }

    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
    public ICollection<AlunoObservacao> Observacoes { get; set; } = new List<AlunoObservacao>();
}
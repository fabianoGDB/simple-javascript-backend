namespace SchoolETL.ConsoleApp.Models;

public class AlunoObservacao
{
    public int Id { get; set; }
    public int AlunoId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;

    public Aluno? Aluno { get; set; }
}
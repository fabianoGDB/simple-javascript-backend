namespace SchoolETL.Models;

public class Aluno
{
    public int Id { get; set; }
    public Guid? ImportId { get; set; }
    public string Nome { get; set; } = "";
    public string? Matricula { get; set; }
    public string? FotoPath { get; set; }
}
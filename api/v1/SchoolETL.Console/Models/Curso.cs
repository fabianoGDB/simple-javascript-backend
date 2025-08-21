namespace SchoolETL.ConsoleApp.Models;

public class Curso
{
    public int Id { get; set; }
    public Guid? ImportId { get; set; }
    public string Sigla { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
}
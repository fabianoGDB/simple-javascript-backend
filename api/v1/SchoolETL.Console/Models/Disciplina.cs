namespace SchoolETL.ConsoleApp.Models;

public class Disciplina
{
    public int Id { get; set; }
    public Guid? ImportId { get; set; }
    public string Sigla { get; set; } = string.Empty;      // pode receber mesmo código do Curso
    public string? NomeArea { get; set; }
    public string? CargaHorariaRotulo { get; set; }

    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
}
namespace SchoolETL.ConsoleApp.Models;

public class Bimestre
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
}
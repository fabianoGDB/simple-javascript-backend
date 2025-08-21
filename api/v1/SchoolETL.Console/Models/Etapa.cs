namespace SchoolETL.ConsoleApp.Models;

public class Etapa
{
    public int Id { get; set; } // 1..4, 99
    public string Nome { get; set; } = string.Empty;
    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
}

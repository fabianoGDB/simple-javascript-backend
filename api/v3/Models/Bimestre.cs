namespace SchoolETL.Core.Models;

public class Bimestre
{
    public int Id { get; set; }        // 1..4
    public string Nome { get; set; } = string.Empty;
    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
}
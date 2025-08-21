namespace SchoolETL.ConsoleApp.Models;

public class PeriodoLetivo
{
    public int Id { get; set; }
    public int Ano { get; set; }
    public int Semestre { get; set; }
    public string? Descricao { get; set; }

    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
}
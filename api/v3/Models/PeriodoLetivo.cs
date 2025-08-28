namespace SchoolETL.Core.Models;

public class PeriodoLetivo
{
    public int Id { get; set; }
    public int Ano { get; set; }
    public int Semestre { get; set; }         // 1 ou 2
    public string? Descricao { get; set; }    // "1º/2025"

    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
}
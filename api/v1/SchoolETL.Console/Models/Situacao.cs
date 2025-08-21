namespace SchoolETL.ConsoleApp.Models;

public class Situacao
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty; // APR, REP, CAN, CUR, OUT
    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
}
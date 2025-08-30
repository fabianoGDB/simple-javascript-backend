namespace SchoolETL.Models;

public class Disciplina
{
    public int Id { get; set; }
    public Guid? ImportId { get; set; }
    public string Sigla { get; set; } = "";
    public string? NomeArea { get; set; }
    public string? CargaHorariaRotulo { get; set; }
}
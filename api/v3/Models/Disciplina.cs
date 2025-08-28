namespace SchoolETL.Core.Models;

public class Disciplina
{
    public int Id { get; set; }
    public Guid? ImportId { get; set; }         // lote que criou a disciplina (opcional)
    public string Sigla { get; set; } = string.Empty; // ex: FIL, IHEG, GBD1
    public string? NomeArea { get; set; }             // Humanas, Linguagens, etc.
    public string? CargaHorariaRotulo { get; set; }   // "20H de 80H" (se existir)

    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
}
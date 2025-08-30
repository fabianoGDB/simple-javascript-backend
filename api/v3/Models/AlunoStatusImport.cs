namespace SchoolETL.Models;
public class AlunoStatusImport
{
    public long Id { get; set; }
    public Guid ImportId { get; set; }
    public int AlunoId { get; set; }
    public int? PeriodoLetivoId { get; set; }
    public decimal? FrequenciaGeral { get; set; }
    public string? SituacaoCurso { get; set; }
    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;

    public Aluno? Aluno { get; set; }
    public ImportBatch? Import { get; set; }
    public PeriodoLetivo? PeriodoLetivo { get; set; }
}

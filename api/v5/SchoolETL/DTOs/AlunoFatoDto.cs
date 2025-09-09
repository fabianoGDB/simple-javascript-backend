namespace SchoolETL.DTOs
{
    public sealed class AlunoFatoDto
    {
        public string Disciplina { get; set; } = "";
        public string? Area { get; set; }      // NomeArea da disciplina
        public int PeriodoAvaliativoId { get; set; } // 1..4
        public decimal? Nota { get; set; }     // 0..10 (ou null)
        public string? Situacao { get; set; }  // APR/REP/CAN/CUR/OUT
    }
}

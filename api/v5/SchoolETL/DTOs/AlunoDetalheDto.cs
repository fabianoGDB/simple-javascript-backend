namespace SchoolETL.DTOs
{
    public sealed class AlunoDetalheDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string? Matricula { get; set; }
        public string? FotoUrl { get; set; }
        public decimal? Frequencia { get; set; } // média % (0..100)
        public string? Situacao { get; set; }    // regra de consolidação abaixo
        public List<AlunoFatoDto> Fatos { get; set; } = new();
    }
}

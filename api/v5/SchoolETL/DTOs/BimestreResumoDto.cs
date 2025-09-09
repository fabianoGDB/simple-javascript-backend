namespace SchoolETL.DTOs
{
    public sealed class BimestreResumoDto
    {
        public string Bimestre { get; set; } = ""; // "1º", "2º", "3º", "4º"
        public int Areas { get; set; }
        public int Disciplinas { get; set; }
        public int Aprovados { get; set; }
        public int Reprovados { get; set; }
    }
}

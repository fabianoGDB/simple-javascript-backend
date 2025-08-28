namespace SchoolETL.Core.Models;

public class Situacao
{
    public int Id { get; set; }               // 1=APR, 2=REP, 3=CAN, 4=CUR, 5=OUT
    public string Descricao { get; set; } = string.Empty;
    public ICollection<FatoNota> Notas { get; set; } = new List<FatoNota>();
}
namespace SchoolETL.Core.Models;

public class PeriodoLetivo
{
    public int Id { get; set; }
    public int Ano { get; set; }
    public int Semestre { get; set; }
    public string? Descricao { get; set; }
}

public class Aluno
{
    public int Id { get; set; }
    public Guid? ImportId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Matricula { get; set; }
    public string? FotoPath { get; set; }
}

public class Disciplina
{
    public int Id { get; set; }
    public Guid? ImportId { get; set; }
    public string Sigla { get; set; } = string.Empty;
    public string? NomeArea { get; set; }
    public string? CargaHorariaRotulo { get; set; }
}

public class Situacao
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty; // APR/REP/CAN/CUR/OUT
}

public class PeriodoAvaliativo
{
    public int Id { get; set; } // 1..4, 99
    public string Nome { get; set; } = string.Empty;
    public bool Final { get; set; }
}

public class AlunoObservacao
{
    public int Id { get; set; }
    public int AlunoId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public DateTime CriadoEmUtc { get; set; }
    public Guid? ImportId { get; set; }
}

public class FatoNota
{
    public int Id { get; set; }
    public Guid ImportId { get; set; }
    public int AlunoId { get; set; }
    public int DisciplinaId { get; set; }
    public int PeriodoAvaliativoId { get; set; }
    public int? SituacaoId { get; set; }
    public int PeriodoLetivoId { get; set; }
    public decimal? Nota { get; set; }
    public decimal? Frequencia { get; set; }

    public Disciplina? Disciplina { get; set; }
    public Situacao? Situacao { get; set; }
}

public class ImportBatch
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? OriginalFileName { get; set; }
    public string? StorageUri { get; set; }
    public short Status { get; set; } // 1=Processando,2=Finalizado,3=Erro
    public string? Error { get; set; }
    public string? FileHash { get; set; }
    public int? PeriodoLetivoId { get; set; }
}

public class AlunoStatusImport
{
    public int Id { get; set; }
    public Guid ImportId { get; set; }
    public int AlunoId { get; set; }
    public int? PeriodoLetivoId { get; set; }
    public decimal? FrequenciaGeral { get; set; }
    public string? SituacaoCurso { get; set; }
    public DateTime CriadoEmUtc { get; set; }
}

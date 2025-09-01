namespace SchoolETL.Core.Models;

public class PeriodoLetivo
{
    public virtual int Id { get; set; }
    public virtual int Ano { get; set; }
    public virtual int Semestre { get; set; }
    public virtual string? Descricao { get; set; }
}

public class Aluno
{
    public virtual int Id { get; set; }
    public virtual Guid? ImportId { get; set; }
    public virtual string Nome { get; set; } = string.Empty;
    public virtual string? Matricula { get; set; }
    public virtual string? FotoPath { get; set; }
}

public class Disciplina
{
    public virtual int Id { get; set; }
    public virtual Guid? ImportId { get; set; }
    public virtual string Sigla { get; set; } = string.Empty;
    public virtual string? NomeArea { get; set; }
    public virtual string? CargaHorariaRotulo { get; set; }
}

public class Situacao
{
    public virtual int Id { get; set; }
    public virtual string Descricao { get; set; } = string.Empty; // APR/REP/CAN/CUR/OUT
}

public class PeriodoAvaliativo
{
    public virtual int Id { get; set; } // 1..4, 99
    public virtual string Nome { get; set; } = string.Empty;
    public virtual bool Final { get; set; }
}

public class AlunoObservacao
{
    public virtual int Id { get; set; }
    public virtual int AlunoId { get; set; }
    public virtual string Texto { get; set; } = string.Empty;
    public virtual DateTime CriadoEmUtc { get; set; }
    public virtual Guid? ImportId { get; set; }
}

public class FatoNota
{
    public virtual int Id { get; set; }
    public virtual Guid ImportId { get; set; }
    public virtual int AlunoId { get; set; }
    public virtual int DisciplinaId { get; set; }
    public virtual int PeriodoAvaliativoId { get; set; }
    public virtual int? SituacaoId { get; set; }
    public virtual int PeriodoLetivoId { get; set; }
    public virtual decimal? Nota { get; set; }
    public virtual decimal? Frequencia { get; set; }

    public virtual Disciplina? Disciplina { get; set; }
    public virtual Situacao? Situacao { get; set; }
}

public class ImportBatch
{
    public virtual Guid Id { get; set; }
    public virtual DateTime CreatedAtUtc { get; set; }
    public virtual string? OriginalFileName { get; set; }
    public virtual string? StorageUri { get; set; }
    public virtual short Status { get; set; } // 1=Processando,2=Finalizado,3=Erro
    public virtual string? Error { get; set; }
    public virtual string? FileHash { get; set; }
    public virtual int? PeriodoLetivoId { get; set; }
}

public class AlunoStatusImport
{
    public virtual int Id { get; set; }
    public virtual Guid ImportId { get; set; }
    public virtual int AlunoId { get; set; }
    public virtual int? PeriodoLetivoId { get; set; }
    public virtual decimal? FrequenciaGeral { get; set; }
    public virtual string? SituacaoCurso { get; set; }
    public virtual DateTime CriadoEmUtc { get; set; }
}

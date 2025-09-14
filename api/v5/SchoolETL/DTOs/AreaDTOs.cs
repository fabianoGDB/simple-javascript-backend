namespace SchoolETL.Core.DTOs;

public record AreaCreateDto(string Nome, string? CorHex = null, int? Ordem = null, bool Ativo = true);
public record AreaUpdateDto(string Nome, string? CorHex = null, int? Ordem = null, bool Ativo = true);

public record AreaListItemDto(int Id, string Nome, string? CorHex, int? Ordem, bool Ativo, int DisciplinasCount);
public record AreaDto(int Id, string Nome, string? CorHex, int? Ordem, bool Ativo);

public record DisciplinaCreateDto(string Nome, string Sigla, int? AreaId, string? CargaHorariaRotulo = null);
public record DisciplinaUpdateDto(string Nome, string Sigla, int? AreaId, string? CargaHorariaRotulo = null);

public record DisciplinaListItemDto(
    int Id, string Nome, string Sigla, int? AreaId, string? AreaNome, string? AreaCorHex, string? CargaHorariaRotulo);

public record DisciplinaDto(
    int Id, string Nome, string Sigla, int? AreaId, string? AreaNome, string? AreaCorHex, string? CargaHorariaRotulo);

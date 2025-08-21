using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SchoolETL.ConsoleApp.Data;
using SchoolETL.ConsoleApp.Models;
using System.Text.RegularExpressions;

namespace SchoolETL.ConsoleApp.Service;

public record ImportSummary(Guid ImportId, int AlunosInseridos, int DisciplinasInseridas, int NotasInseridas, int LinhasIgnoradas, List<string> Avisos);

public class ExcelEtlRunner
{
    private readonly DwContext _db;
    public ExcelEtlRunner(DwContext db) => _db = db;

    public async Task<ImportSummary> RunAsync(string path, int ano, int semestre)
    {
        using var fs = File.OpenRead(path);
        using var wb = new XLWorkbook(fs);
        var avisos = new List<string>();

        var periodo = await _db.Periodos.FirstOrDefaultAsync(p => p.Ano == ano && p.Semestre == semestre)
                      ?? new PeriodoLetivo { Ano = ano, Semestre = semestre, Descricao = $"{semestre}º/{ano}" };
        if (periodo.Id == 0) { _db.Periodos.Add(periodo); await _db.SaveChangesAsync(); }

        var batch = new ImportBatch { FileName = Path.GetFileName(path), PeriodoLetivoId = periodo.Id };
        _db.Imports.Add(batch);
        await _db.SaveChangesAsync();

        int notasInseridas = 0, disciplinasInseridas = 0, linhasIgnoradas = 0;

        // Registros (frequência geral, situação curso)
        if (wb.Worksheets.TryGetWorksheet("Registros", out var wsReg))
        {
            _db.ImportSheets.Add(new ImportSheet { ImportId = batch.Id, Name = wsReg.Name });
            await _db.SaveChangesAsync();
            ImportRegistros(wsReg, avisos, batch.Id);
            await _db.SaveChangesAsync();
        }
        else avisos.Add("Aba 'Registros' não encontrada.");

        // Etapas 1..4
        for (int e = 1; e <= 4; e++)
        {
            if (wb.Worksheets.TryGetWorksheet($"Etapa {e}", out var ws))
            {
                _db.ImportSheets.Add(new ImportSheet { ImportId = batch.Id, Name = ws.Name });
                await _db.SaveChangesAsync();

                notasInseridas += await ImportEtapaAsync(ws, etapaId: e, periodo.Id, batch.Id,
                    ref disciplinasInseridas, ref linhasIgnoradas, avisos);
            }
        }

        // Etapa Final
        if (wb.Worksheets.TryGetWorksheet("Etapa Final", out var wsFinal))
        {
            _db.ImportSheets.Add(new ImportSheet { ImportId = batch.Id, Name = wsFinal.Name });
            await _db.SaveChangesAsync();

            notasInseridas += await ImportEtapaAsync(wsFinal, etapaId: 99, periodo.Id, batch.Id,
                ref disciplinasInseridas, ref linhasIgnoradas, avisos);
        }

        var alunosInseridos = await _db.Alunos.CountAsync();
        return new ImportSummary(batch.Id, alunosInseridos, disciplinasInseridas, notasInseridas, linhasIgnoradas, avisos);
    }

    private void ImportRegistros(IXLWorksheet ws, List<string> avisos, Guid batchId)
    {
        var used = ws.RangeUsed();
        if (used is null) { avisos.Add("'Registros' vazia."); return; }

        var headerRow = used.Rows().FirstOrDefault(r => r.Cells().Any(c => (c.GetString() ?? string.Empty).Trim().Equals("Aluno", StringComparison.OrdinalIgnoreCase)))
                      ?? used.FirstRowUsed();

        var headers = headerRow.Cells().ToDictionary(c => NameNormalizer.Key(c.GetString()), c => c.Address.ColumnNumber);
        int colNome = headers.GetValueOrDefault(NameNormalizer.Key("Aluno"));
        int colMat = headers.GetValueOrDefault(NameNormalizer.Key("Matrícula"));
        int colFreq = headers.GetValueOrDefault(NameNormalizer.Key("Frequência no Período"));
        int colSitCurso = headers.GetValueOrDefault(NameNormalizer.Key("Situação no Curso"));

        foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var nome = NameNormalizer.Clean(row.Cell(colNome).GetString());
            if (string.IsNullOrWhiteSpace(nome)) continue;

            var key = NameNormalizer.Key(nome);
            var aluno = _db.Alunos.Local.FirstOrDefault(a => NameNormalizer.Key(a.Nome) == key)
                        ?? _db.Alunos.FirstOrDefault(a => NameNormalizer.Key(a.Nome) == key);
            if (aluno is null)
            {
                aluno = new Aluno { Nome = nome, ImportId = batchId };
                _db.Alunos.Add(aluno);
            }

            aluno.Matricula = NameNormalizer.Clean(row.Cell(colMat).GetString());
            aluno.FrequenciaGeral = TryParsePercent(row.Cell(colFreq).GetString());
            aluno.SituacaoCurso = NameNormalizer.Clean(row.Cell(colSitCurso).GetString());
        }
    }

    private async Task<int> ImportEtapaAsync(IXLWorksheet ws, int etapaId, int periodoId, Guid batchId,
        ref int disciplinasInseridas, ref int linhasIgnoradas, List<string> avisos)
    {
        var used = ws.RangeUsed();
        if (used is null) return 0;

        var headerRow = used.Rows().FirstOrDefault(r => r.Cells().Any(c => c.GetString().Trim().Equals("Aluno", StringComparison.OrdinalIgnoreCase)));
        if (headerRow is null) { avisos.Add($"Cabeçalho 'Aluno' não encontrado na aba {ws.Name}."); return 0; }

        int colAluno = headerRow.Cells().First(c => c.GetString().Trim().Equals("Aluno", StringComparison.OrdinalIgnoreCase)).Address.ColumnNumber;
        var topHeaderRow = ws.Row(headerRow.RowNumber() - 1);
        var subHeaderRow = headerRow.RowBelow();

        var triplets = new List<(int startCol, string label)>();
        var lastCol = used.RangeAddress.LastAddress.ColumnNumber;
        int c = colAluno + 1;
        while (c <= lastCol - 2)
        {
            var n = subHeaderRow.Cell(c).GetString().Trim().ToUpperInvariant();
            var f = subHeaderRow.Cell(c + 1).GetString().Trim().ToUpperInvariant();
            var s = subHeaderRow.Cell(c + 2).GetString().Trim().ToUpperInvariant();
            if (n == "N" && (f.StartsWith("F") || f == "F." || f == "FAL" || f == "FALTAS") && s.StartsWith("S"))
            {
                var rawLabel = (topHeaderRow.Cell(c).GetString() + " " + topHeaderRow.Cell(c + 1).GetString()).Trim();
                if (string.IsNullOrWhiteSpace(rawLabel)) rawLabel = ws.Cell(1, c).GetString();
                triplets.Add((c, rawLabel));
                c += 3;
            }
            else c++;
        }

        var bimestreId = etapaId is >= 1 and <= 4 ? etapaId : 4;
        int inserted = 0;

        foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var nome = NameNormalizer.Clean(row.Cell(colAluno).GetString());
            if (string.IsNullOrWhiteSpace(nome)) { linhasIgnoradas++; continue; }

            var key = NameNormalizer.Key(nome);
            var aluno = _db.Alunos.Local.FirstOrDefault(a => NameNormalizer.Key(a.Nome) == key)
                        ?? await _db.Alunos.FirstOrDefaultAsync(a => NameNormalizer.Key(a.Nome) == key);
            if (aluno is null)
            {
                aluno = new Aluno { Nome = nome, ImportId = batchId };
                _db.Alunos.Add(aluno);
                await _db.SaveChangesAsync();
            }

            foreach (var (startCol, label) in triplets)
            {
                var notaTxt = row.Cell(startCol).GetString();
                var sitTxt = row.Cell(startCol + 2).GetString();

                decimal? nota = TryParseDecimal(notaTxt);
                int? situacaoId = await ResolveSituacaoIdAsync(sitTxt);

                var (curso, disciplina) = await ResolveCursoEDisciplinaAsync(label, batchId);
                if (curso.WasInserted || disciplina.WasInserted) disciplinasInseridas++;

                var fato = new FatoNota
                {
                    ImportId = batchId,
                    AlunoId = aluno.Id,
                    DisciplinaId = disciplina.Entity.Id,
                    BimestreId = bimestreId,
                    EtapaId = etapaId,
                    CursoId = curso.Entity.Id,
                    SituacaoId = situacaoId,
                    PeriodoLetivoId = periodoId,
                    Nota = nota
                };
                _db.FatoNotas.Add(fato);
                inserted++;
            }
        }

        await _db.SaveChangesAsync();
        return inserted;
    }

    private async Task<(bool WasInserted, Curso Entity)> EnsureCursoAsync(string sigla, string? descricao, Guid batchId)
    {
        sigla = NameNormalizer.Clean(sigla) ?? sigla;
        var curso = await _db.Cursos.FirstOrDefaultAsync(c => c.Sigla == sigla);
        if (curso is null)
        {
            curso = new Curso { Sigla = sigla, Descricao = descricao, ImportId = batchId };
            _db.Cursos.Add(curso);
            await _db.SaveChangesAsync();
            return (true, curso);
        }
        return (false, curso);
    }

    private async Task<(bool WasInserted, Disciplina Entity)> EnsureDisciplinaAsync(string sigla, string? carga, Guid batchId)
    {
        sigla = NameNormalizer.Clean(sigla) ?? sigla;
        var d = await _db.Disciplinas.FirstOrDefaultAsync(x => x.Sigla == sigla);
        if (d is null)
        {
            d = new Disciplina { Sigla = sigla, CargaHorariaRotulo = carga, ImportId = batchId };
            _db.Disciplinas.Add(d);
            await _db.SaveChangesAsync();
            return (true, d);
        }
        return (false, d);
    }

    private async Task<((bool WasInserted, Curso Entity) Curso, (bool WasInserted, Disciplina Entity) Disciplina)>
        ResolveCursoEDisciplinaAsync(string raw, Guid batchId)
    {
        var txt = NameNormalizer.Clean(raw) ?? raw;
        var codeMatch = Regex.Match(txt, "[A-Z]{2,}\\.?\\d+");
        var cargaMatch = Regex.Match(txt, "\\d+H\\s*de\\s*\\d+H", RegexOptions.IgnoreCase);
        var sigla = codeMatch.Success ? codeMatch.Value : txt;
        var carga = cargaMatch.Success ? cargaMatch.Value.ToUpperInvariant() : null;

        var curso = await EnsureCursoAsync(sigla, carga, batchId);
        var disc = await EnsureDisciplinaAsync(sigla, carga, batchId);
        return (curso, disc);
    }

    private static decimal? TryParseDecimal(string? txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return null;
        txt = txt.Replace('%', ' ').Trim();
        if (decimal.TryParse(txt, System.Globalization.NumberStyles.Any,
            new System.Globalization.CultureInfo("pt-BR"), out var v))
            return v;
        if (decimal.TryParse(txt, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out v))
            return v;
        return null;
    }

    private static decimal? TryParsePercent(string? txt)
    {
        var d = TryParseDecimal(txt);
        if (d is null) return null;
        return d > 1 ? d : d * 100m;
    }

    private async Task<int?> ResolveSituacaoIdAsync(string? sit)
    {
        sit = (sit ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(sit)) return null;
        var s = await _db.Situacoes.FirstOrDefaultAsync(x => x.Descricao == sit);
        return s?.Id;
    }
}

using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Data;
using SchoolETL.Core.Models;
using SchoolETL.WorkerApi.DTOs;
using SchoolETL.WorkerApi.Services; // ImportSummary / IExcelEtlRunner
using SchoolETL.WorkerApi.Services.Interfaces;
using System.Text;
using System.Text.RegularExpressions;

namespace SchoolETL.WorkerApi.Data;

public class ExcelEtlRunnerEf : IExcelEtlRunner
{
    private readonly DwContext _db;
    private readonly ILogger<ExcelEtlRunnerEf> _log;

    public ExcelEtlRunnerEf(DwContext db, ILogger<ExcelEtlRunnerEf> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ImportSummary> RunAsync(string filePath, int ano, int semestre, CancellationToken ct = default)
    {
        using var fs = File.OpenRead(filePath);
        using var wb = new XLWorkbook(fs);
        var avisos = new List<string>();

        // ===== 1) Período letivo =====
        var periodo = await _db.Periodos
            .FirstOrDefaultAsync(p => p.Ano == ano && p.Semestre == semestre, ct);
        if (periodo is null)
        {
            periodo = new PeriodoLetivo { Ano = ano, Semestre = semestre, Descricao = $"{semestre}º/{ano}" };
            _db.Periodos.Add(periodo);
            await _db.SaveChangesAsync(ct);
        }

        // ===== 2) ImportBatch =====
        var batch = new ImportBatch { FileName = Path.GetFileName(filePath), PeriodoLetivoId = periodo.Id };
        _db.Imports.Add(batch);
        await _db.SaveChangesAsync(ct);

        int notasInseridas = 0, disciplinasNovas = 0, linhasIgnoradas = 0;

        // ===== 3) Registros (frequência/situação/matrícula) =====
        if (wb.Worksheets.TryGetWorksheet("Registros", out var wsReg))
        {
            await RegistrarAbaAsync(batch.Id, wsReg.Name, ct);
            ImportRegistros(wsReg, batch.Id, avisos);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            avisos.Add("Aba 'Registros' não encontrada – frequências e matrículas não serão carregadas.");
        }

        // ===== 4) Etapas 1..4 =====
        for (int etapa = 1; etapa <= 4; etapa++)
        {
            if (wb.Worksheets.TryGetWorksheet($"Etapa {etapa}", out var ws))
            {
                await RegistrarAbaAsync(batch.Id, ws.Name, ct);
                var (ins, discNovas, ign) = await ImportEtapaAsync(ws, etapa, periodo.Id, batch.Id, ct, avisos);
                notasInseridas += ins; disciplinasNovas += discNovas; linhasIgnoradas += ign;
            }
        }

        // ===== 5) Etapa Final =====
        if (wb.Worksheets.TryGetWorksheet("Etapa Final", out var wsFinal))
        {
            await RegistrarAbaAsync(batch.Id, wsFinal.Name, ct);
            var (ins, discNovas, ign) = await ImportEtapaAsync(wsFinal, 99, periodo.Id, batch.Id, ct, avisos);
            notasInseridas += ins; disciplinasNovas += discNovas; linhasIgnoradas += ign;
        }

        // ===== 6) Retorno =====
        // (contagem de alunos apenas ilustrativa; ajuste se quiser contar só do lote)
        var alunos = await _db.Alunos.CountAsync(ct);
        return new ImportSummary(batch.Id, alunos, disciplinasNovas, notasInseridas, linhasIgnoradas, avisos);
    }

    // ========================= helpers =========================

    private async Task RegistrarAbaAsync(Guid importId, string name, CancellationToken ct)
    {
        _db.ImportSheets.Add(new ImportSheet { ImportId = importId, Name = name });
        await _db.SaveChangesAsync(ct);
    }

    private void ImportRegistros(IXLWorksheet ws, Guid batchId, List<string> avisos)
    {
        var used = ws.RangeUsed();
        if (used is null) { avisos.Add("'Registros' vazia."); return; }

        var header = used.FirstRowUsed();
        var map = header.Cells().ToDictionary(
            c => Key(c.GetString()),
            c => c.Address.ColumnNumber);

        int colNome = map.GetValueOrDefault(Key("Nome"), map.GetValueOrDefault(Key("Aluno")));
        if (colNome == 0) { avisos.Add("Coluna 'Aluno/Nome' não encontrada em 'Registros'."); return; }

        int colMat = map.GetValueOrDefault(Key("Matrícula"));
        int colFreq = map.GetValueOrDefault(Key("Frequência no Período"));
        int colSit = map.GetValueOrDefault(Key("Situação no Curso"));

        foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
        {
            var nome = Clean(row.Cell(colNome).GetString());
            if (string.IsNullOrWhiteSpace(nome)) continue;

            var aluno = FindAlunoByNome(nome) ?? new Aluno { Nome = nome, ImportId = batchId };
            aluno.Matricula = colMat > 0 ? Clean(row.Cell(colMat).GetString()) : aluno.Matricula;
            aluno.FrequenciaGeral = colFreq > 0 ? TryPercent(row.Cell(colFreq).GetString()) : aluno.FrequenciaGeral;
            aluno.SituacaoCurso = colSit > 0 ? Clean(row.Cell(colSit).GetString()) : aluno.SituacaoCurso;

            if (aluno.Id == 0) _db.Alunos.Add(aluno);
        }
    }

    private async Task<(int Inseridas, int DisciplinasNovas, int LinhasIgnoradas)> ImportEtapaAsync(
        IXLWorksheet ws, int etapaId, int periodoId, Guid batchId, CancellationToken ct, List<string> avisos)
    {
        var used = ws.RangeUsed();
        if (used is null) return (0, 0, 0);

        var headerRow = used.Rows()
            .FirstOrDefault(r => r.Cells().Any(c => c.GetString().Trim()
                .Equals("Aluno", StringComparison.OrdinalIgnoreCase)));
        if (headerRow is null)
        {
            avisos.Add($"Cabeçalho 'Aluno' não encontrado na aba {ws.Name}.");
            return (0, 0, 0);
        }

        int colAluno = headerRow.Cells()
            .First(c => c.GetString().Trim().Equals("Aluno", StringComparison.OrdinalIgnoreCase))
            .Address.ColumnNumber;

        var topHeaderRow = ws.Row(headerRow.RowNumber() - 1);
        var subHeaderRow = headerRow.RowBelow();

        // detectar triplas N|F|S
        var triplets = new List<(int Col, string Label)>();
        int lastCol = used.RangeAddress.LastAddress.ColumnNumber;
        int c = colAluno + 1;
        while (c <= lastCol - 2)
        {
            var n = subHeaderRow.Cell(c).GetString().Trim().ToUpperInvariant();
            var f = subHeaderRow.Cell(c + 1).GetString().Trim().ToUpperInvariant();
            var s = subHeaderRow.Cell(c + 2).GetString().Trim().ToUpperInvariant();
            if (n == "N" && (f.StartsWith("F") || f is "F." or "FAL" or "FALTAS") && s.StartsWith("S"))
            {
                var raw = (topHeaderRow.Cell(c).GetString() + " " + topHeaderRow.Cell(c + 1).GetString()).Trim();
                if (string.IsNullOrWhiteSpace(raw)) raw = ws.Cell(1, c).GetString();
                triplets.Add((c, raw));
                c += 3;
            }
            else c++;
        }

        int bimestreId = etapaId is >= 1 and <= 4 ? etapaId : 4;
        int inseridas = 0, discNovas = 0, ignoradas = 0;

        foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var nome = Clean(row.Cell(colAluno).GetString());
            if (string.IsNullOrWhiteSpace(nome)) { ignoradas++; continue; }

            var aluno = FindAlunoByNome(nome);
            if (aluno is null)
            {
                aluno = new Aluno { Nome = nome, ImportId = batchId };
                _db.Alunos.Add(aluno);
                await _db.SaveChangesAsync(ct); // precisa do Id para FK
            }

            foreach (var (startCol, label) in triplets)
            {
                var notaTxt = row.Cell(startCol).GetString();
                var sitTxt = row.Cell(startCol + 2).GetString();

                decimal? nota = TryDecimal(notaTxt);
                int? situacaoId = await ResolveSituacaoIdAsync(sitTxt, ct);

                var (curso, disc, anyNew) = await ResolveCursoDisciplinaAsync(label, batchId, ct);
                if (anyNew) discNovas++;

                _db.FatoNotas.Add(new FatoNota
                {
                    ImportId = batchId,
                    AlunoId = aluno.Id,
                    DisciplinaId = disc.Id,
                    BimestreId = bimestreId,
                    EtapaId = etapaId,
                    CursoId = curso?.Id,
                    SituacaoId = situacaoId,
                    PeriodoLetivoId = periodoId,
                    Nota = nota
                });
                inseridas++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return (inseridas, discNovas, ignoradas);
    }

    // ========================= resolvers =========================

    private Aluno? FindAlunoByNome(string nome)
    {
        var key = Key(nome);
        return _db.Alunos.Local.FirstOrDefault(a => Key(a.Nome) == key)
            ?? _db.Alunos.FirstOrDefault(a => Key(a.Nome) == key);
    }

    private async Task<int?> ResolveSituacaoIdAsync(string? sit, CancellationToken ct)
    {
        sit = (sit ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(sit)) return null;
        var s = await _db.Situacoes.FirstOrDefaultAsync(x => x.Descricao == sit, ct);
        return s?.Id;
    }

    private async Task<(Curso? curso, Disciplina disc, bool anyNew)> ResolveCursoDisciplinaAsync(string label, Guid batchId, CancellationToken ct)
    {
        var txt = Clean(label) ?? label;
        var codeMatch = Regex.Match(txt, @"[A-Z]{2,}\.?\d+");       // ex: IHEG, GBD1 etc.
        var cargaMatch = Regex.Match(txt, @"\d+H\s*de\s*\d+H", RegexOptions.IgnoreCase);
        var sigla = codeMatch.Success ? codeMatch.Value : txt;
        var carga = cargaMatch.Success ? cargaMatch.Value.ToUpperInvariant() : null;

        bool anyNew = false;

        // Curso (opcional)
        Curso? curso = await _db.Cursos.FirstOrDefaultAsync(c => c.Sigla == sigla, ct);
        if (curso is null)
        {
            curso = new Curso { Sigla = sigla, Descricao = carga, ImportId = batchId };
            _db.Cursos.Add(curso);
            await _db.SaveChangesAsync(ct);
            anyNew = true;
        }

        // Disciplina
        var disc = await _db.Disciplinas.FirstOrDefaultAsync(d => d.Sigla == sigla, ct);
        if (disc is null)
        {
            disc = new Disciplina { Sigla = sigla, CargaHorariaRotulo = carga, ImportId = batchId };
            _db.Disciplinas.Add(disc);
            await _db.SaveChangesAsync(ct);
            anyNew = true;
        }

        return (curso, disc, anyNew);
    }

    // ========================= utils =========================
    private static string Key(string? s)
    {
        s ??= string.Empty;
        s = s.ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var arr = s.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                   .ToArray();
        return new string(arr).Replace("'", "").Replace("\"", "").Trim();
    }
    private static string? Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Replace('\u00A0', ' ').Trim();
        return Regex.Replace(s, "\\s+", " ");
    }
    private static decimal? TryDecimal(string? txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return null;
        txt = txt.Replace("%", "").Trim();
        if (decimal.TryParse(txt, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out var v)) return v;
        if (decimal.TryParse(txt, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v)) return v;
        return null;
    }
    private static decimal? TryPercent(string? txt)
    {
        var d = TryDecimal(txt);
        if (d is null) return null;
        return d > 1 ? d : d * 100m;
    }

    Task<ImportSummary> IExcelEtlRunner.RunAsync(string filePath, int ano, int semestre, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

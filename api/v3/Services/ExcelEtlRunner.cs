// Services/ExcelEtlRunner.cs
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Models;
using SchoolETL.Repositories;
using SchoolETL.Repositories.Alunos;
using SchoolETL.Repositories.Cursos;
using SchoolETL.Repositories.Dimensoes;
using SchoolETL.Repositories.Disciplinas;
using SchoolETL.Repositories.Imports;
using SchoolETL.Repositories.Notas;
using SchoolETL.WorkerApi.DTOs;
using SchoolETL.WorkerApi.Services.Interfaces;
using System.Text;
using System.Text.RegularExpressions;

namespace SchoolETL.Api.Services;

public class ExcelEtlRunner : IExcelEtlRunner
{
    private readonly IPeriodoLetivoRepository _periodos;
    private readonly IImportRepository _imports;
    private readonly IImportSheetRepository _sheets;
    private readonly IAlunoRepository _alunos;
    private readonly ICursoRepository _cursos;
    private readonly IDisciplinaRepository _disciplinas;
    private readonly ISituacaoRepository _situacoes;
    private readonly IFatoNotaRepository _fatos;
    private readonly IRepository<AlunoObservacao> _obsRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ExcelEtlRunner> _log;

    public ExcelEtlRunner(
        IPeriodoLetivoRepository periodos,
        IImportRepository imports,
        IImportSheetRepository sheets,
        IAlunoRepository alunos,
        ICursoRepository cursos,
        IDisciplinaRepository disciplinas,
        ISituacaoRepository situacoes,
        IFatoNotaRepository fatos,
        IRepository<AlunoObservacao> obsRepo,
        IUnitOfWork uow,
        ILogger<ExcelEtlRunner> log)
    {
        _periodos = periodos;
        _imports = imports;
        _sheets = sheets;
        _alunos = alunos;
        _cursos = cursos;
        _disciplinas = disciplinas;
        _situacoes = situacoes;
        _fatos = fatos;
        _obsRepo = obsRepo;
        _uow = uow;
        _log = log;
    }

    public async Task<ImportSummary> RunAsync(string filePath, int ano, int semestre, CancellationToken ct = default)
    {
        var avisos = new List<string>();
        int notasInseridas = 0, disciplinasNovas = 0, linhasIgnoradas = 0;
        int alunosInseridos = 0;

        using var fs = File.OpenRead(filePath);
        using var wb = new XLWorkbook(fs);

        // 1) Período letivo
        var periodo = await _periodos.GetOrCreateAsync(ano, semestre, ct);

        // 2) ImportBatch
        var batch = await _imports.CreateAsync(Path.GetFileName(filePath), periodo.Id, ct);
        await _uow.SaveChangesAsync(ct); // garante GUID

        // 3) Registros
        if (wb.Worksheets.TryGetWorksheet("Registros", out var wsReg))
        {
            await _sheets.AddAsync(batch.Id, wsReg.Name, ct);
            await _uow.SaveChangesAsync(ct);

            var (ins, ign) = await ImportRegistrosAsync(wsReg, batch.Id, avisos, ct);
            alunosInseridos += ins;
            linhasIgnoradas += ign;
        }
        else
        {
            avisos.Add("Aba 'Registros' não encontrada – frequências/matrículas podem ficar incompletas.");
        }

        // 4) Etapas 1..4
        for (int etapa = 1; etapa <= 4; etapa++)
        {
            if (wb.Worksheets.TryGetWorksheet($"Etapa {etapa}", out var ws))
            {
                await _sheets.AddAsync(batch.Id, ws.Name, ct);
                await _uow.SaveChangesAsync(ct);

                var r = await ImportEtapaAsync(ws, etapa, periodo.Id, batch.Id, ct, avisos);
                notasInseridas += r.Inseridas;
                disciplinasNovas += r.DisciplinasNovas;
                linhasIgnoradas += r.LinhasIgnoradas;
            }
        }

        // 5) Etapa Final (99)
        if (wb.Worksheets.TryGetWorksheet("Etapa Final", out var wsFinal))
        {
            await _sheets.AddAsync(batch.Id, wsFinal.Name, ct);
            await _uow.SaveChangesAsync(ct);

            var r = await ImportEtapaAsync(wsFinal, 99, periodo.Id, batch.Id, ct, avisos);
            notasInseridas += r.Inseridas;
            disciplinasNovas += r.DisciplinasNovas;
            linhasIgnoradas += r.LinhasIgnoradas;
        }

        await _uow.SaveChangesAsync(ct);

        return new ImportSummary(batch.Id, alunosInseridos, disciplinasNovas, notasInseridas, linhasIgnoradas, avisos);
    }

    // ========================= Registros =========================

    private async Task<(int Inseridos, int LinhasIgnoradas)> ImportRegistrosAsync(
        IXLWorksheet ws, Guid importId, List<string> avisos, CancellationToken ct)
    {
        var used = ws.RangeUsed();
        if (used is null) { avisos.Add("'Registros' vazia."); return (0, 0); }

        var header = used.FirstRowUsed();
        var map = header.Cells().ToDictionary(c => Key(c.GetString()), c => c.Address.ColumnNumber);

        int colNome = Col(map, "Aluno") ?? Col(map, "Nome") ?? 0;
        if (colNome == 0) { avisos.Add("Coluna 'Aluno/Nome' não encontrada em 'Registros'."); return (0, 0); }

        int colMat = Col(map, "Matrícula") ?? 0;
        int colFreq = Col(map, "Frequência no Período") ?? 0;
        int colSit = Col(map, "Situação no Curso") ?? 0;

        int ins = 0, ign = 0;

        foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
        {
            var nome = Clean(row.Cell(colNome).GetString());
            if (string.IsNullOrWhiteSpace(nome)) { ign++; continue; }

            // chave simplificada para exemplo (use o seu normalizador)
            var aluno = await _alunos.Query()
                .FirstOrDefaultAsync(a => a.Nome != null && a.Nome.ToUpper() == nome.ToUpper(), ct);

            if (aluno is null)
            {
                aluno = new Aluno { Nome = nome, ImportId = importId };
                await _alunos.AddAsync(aluno, ct);
                ins++;
            }

            if (colMat > 0) aluno.Matricula = Clean(row.Cell(colMat).GetString()) ?? aluno.Matricula;
            if (colFreq > 0) aluno.FrequenciaGeral = TryPercent(row.Cell(colFreq).GetString()) ?? aluno.FrequenciaGeral;
            if (colSit > 0) aluno.SituacaoCurso = Clean(row.Cell(colSit).GetString()) ?? aluno.SituacaoCurso;
        }

        await _uow.SaveChangesAsync(ct);
        return (ins, ign);
    }

    // ========================= Etapas =========================

    private async Task<(int Inseridas, int DisciplinasNovas, int LinhasIgnoradas)> ImportEtapaAsync(
     IXLWorksheet ws, int etapaId, int periodoId, Guid importId, CancellationToken ct, List<string> avisos)
    {
        var used = ws.RangeUsed();
        if (used is null) return (0, 0, 0);

        // acha a linha cujo cabeçalho contenha "Aluno"
        var headerRow = used.Rows().FirstOrDefault(r =>
            r.Cells().Any(c => c.GetString().Trim().Equals("Aluno", StringComparison.OrdinalIgnoreCase)));

        if (headerRow is null)
        {
            avisos.Add($"Cabeçalho 'Aluno' não encontrado na aba {ws.Name}.");
            return (0, 0, 0);
        }

        // coluna do "Aluno"
        int colAluno = headerRow.Cells()
            .First(c => c.GetString().Trim().Equals("Aluno", StringComparison.OrdinalIgnoreCase))
            .Address.ColumnNumber;

        // IMPORTANTE: pegar IXLRow (da planilha), não IXLRangeRow
        var topHeaderRow = ws.Row(headerRow.RowNumber() - 1);     // IXLRow
        var subHeaderRow = ws.Row(headerRow.RowNumber() + 1);     // IXLRow  (em vez de headerRow.RowBelow())

        // limites de varredura
        int lastCol = used.RangeAddress.LastAddress.ColumnNumber;

        // detecta as trincas N|F|S por disciplina
        var triplets = DetectTriplets(topHeaderRow, subHeaderRow, colAluno, lastCol, ws);
        if (triplets.Count == 0)
        {
            avisos.Add($"Nenhum bloco 'N|F|S' detectado na aba {ws.Name}. Linhas serão ignoradas.");
            return (0, 0, 0);
        }

        // regra para bimestre
        int bimestreId = (etapaId >= 1 && etapaId <= 4) ? etapaId : 4;

        int inseridas = 0, discNovas = 0, ignoradas = 0;
        int saveEvery = 200; // salva periodicamente para reduzir roundtrips
        int pending = 0;

        // percorre linhas de dados
        foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            ct.ThrowIfCancellationRequested();

            var nome = Clean(row.Cell(colAluno).GetString());
            if (string.IsNullOrWhiteSpace(nome)) { ignoradas++; continue; }

            // tenta localizar aluno por nome (normalize conforme sua regra)
            var aluno = await _alunos.Query()
                .FirstOrDefaultAsync(a => a.Nome != null && a.Nome.ToUpper() == nome.ToUpper(), ct);

            if (aluno is null)
            {
                aluno = new Aluno { Nome = nome, ImportId = importId };
                await _alunos.AddAsync(aluno, ct);
                await _uow.SaveChangesAsync(ct); // precisa do Id para fatos
            }

            foreach (var t in triplets)
            {
                var notaTxt = row.Cell(t.Col).GetString();
                var sitTxt = row.Cell(t.Col + 2).GetString();

                var nota = TryDecimal(notaTxt);
                var situacaoId = await _situacoes.TryResolveIdAsync(sitTxt, ct);

                var (curso, disc, anyNew) = await ResolveCursoEDisciplinaAsync(t.Label, importId, ct);
                if (anyNew) discNovas++;

                await _fatos.AddAsync(new FatoNota
                {
                    ImportId = importId,
                    AlunoId = aluno.Id,
                    DisciplinaId = disc.Id,
                    BimestreId = bimestreId,
                    EtapaId = etapaId,
                    CursoId = curso?.Id,
                    SituacaoId = situacaoId,
                    PeriodoLetivoId = periodoId,
                    Nota = nota
                }, ct);

                inseridas++;
                pending++;

                if (pending >= saveEvery)
                {
                    await _uow.SaveChangesAsync(ct);
                    pending = 0;
                }
            }
        }

        if (pending > 0)
            await _uow.SaveChangesAsync(ct);

        return (inseridas, discNovas, ignoradas);
    }

    // ========================= Resolvers / Helpers =========================

    private async Task<(Curso? curso, Disciplina disc, bool anyNew)> ResolveCursoEDisciplinaAsync(string label, Guid importId, CancellationToken ct)
    {
        var txt = Clean(label) ?? label;
        var codeMatch = Regex.Match(txt, @"[A-Z]{2,}\d*");          // FIL, IHEG, GBD1...
        var cargaMatch = Regex.Match(txt, @"\d+\s*H\s*de\s*\d+\s*H", RegexOptions.IgnoreCase);

        var sigla = codeMatch.Success ? codeMatch.Value : txt;
        var carga = cargaMatch.Success ? cargaMatch.Value.ToUpperInvariant() : null;

        bool anyNew = false;

        Curso? curso = await _cursos.FindBySiglaAsync(sigla, ct);
        if (curso is null)
        {
            curso = new Curso { Sigla = sigla, Descricao = carga, ImportId = importId };
            await _cursos.AddAsync(curso, ct);
            anyNew = true;
            await _uow.SaveChangesAsync(ct);
        }

        var disc = await _disciplinas.FindBySiglaAsync(sigla, ct);
        if (disc is null)
        {
            disc = new Disciplina { Sigla = sigla, CargaHorariaRotulo = carga, ImportId = importId };
            await _disciplinas.AddAsync(disc, ct);
            anyNew = true;
            await _uow.SaveChangesAsync(ct);
        }

        return (curso, disc!, anyNew);
    }

    private sealed record Triplet(int Col, string Label);

    private static List<Triplet> DetectTriplets(IXLRow topHeaderRow, IXLRow subHeaderRow, int colAluno, int lastCol, IXLWorksheet ws)
    {
        var list = new List<Triplet>();
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
                list.Add(new Triplet(c, raw));
                c += 3;
            }
            else c++;
        }
        return list;
    }

    private static int? Col(Dictionary<string, int> map, string name)
        => map.TryGetValue(Key(name), out var col) ? col : null;

    private static string Key(string? s)
    {
        s ??= string.Empty;
        s = s.ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var arr = s.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
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
}

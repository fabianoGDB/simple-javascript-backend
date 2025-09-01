using System.Text.RegularExpressions;
using ClosedXML.Excel;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;
using ISession = NHibernate.ISession;

namespace SchoolETL.Services;

public class ExcelEtlRunnerNH : IExcelEtlRunner
{
    private readonly ISession _session;
    public ExcelEtlRunnerNH(ISession session) => _session = session;

    public async Task RunAsync(ImportBatch import, CancellationToken ct)
    {
        await UpdateImportStatus(import, 1, null, ct); // Processando

        try
        {
            if (string.IsNullOrWhiteSpace(import.StorageUri) || !File.Exists(import.StorageUri))
                throw new FileNotFoundException("Arquivo do import não encontrado", import.StorageUri);

            using var wb = new XLWorkbook(import.StorageUri);
            using var tx = _session.BeginTransaction();

            // ===== percorre SOMENTE as etapas 1..4 (ignora "Final") =====
            foreach (var ws in wb.Worksheets.Where(IsEtapa14))
                await ImportEtapaAsync(ws, import, ct);

            import.Status = 2; import.Error = null;
            await _session.UpdateAsync(import, ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await UpdateImportStatus(import, 3, ex.Message, ct);
            throw;
        }
    }

    // ---------------------------------------------------------------
    // Importa UMA aba de etapa (1/2/3/4) percorrendo:
    //   linhas = alunos
    //   blocos 3 colunas = disciplinas  (N | F | Sit.)
    // ---------------------------------------------------------------
    private async Task ImportEtapaAsync(IXLWorksheet ws, ImportBatch import, CancellationToken ct)
    {
        var used = ws.RangeUsed();
        if (used is null) return;

        // localizar cabeçalho "Aluno" (mesma linha dos subheaders N/F/Sit.)
        var alunoHeader = FindHeaderCell(ws, "aluno");
        if (alunoHeader is null) return;

        int rowHeader = alunoHeader.Address.RowNumber;
        int colAluno = alunoHeader.Address.ColumnNumber;

        // detectar blocos N | F | Sit. a partir da coluna após "Aluno"
        var blocos = DetectDiscBlocks(ws, rowHeader, colAluno + 1);
        if (blocos.Count == 0) return;

        int etapaId = MapEtapa14(ws.Name);
        int firstDataRow = rowHeader + 1;
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? firstDataRow;

        for (int r = firstDataRow; r <= lastRow; r++)
        {
            ct.ThrowIfCancellationRequested();

            var nome = Normalize(ws.Cell(r, colAluno).GetString());
            if (string.IsNullOrWhiteSpace(nome) || nome == "#" || IsOnlyDigits(nome))
                continue;

            var aluno = await GetOrCreateAlunoByNomeAsync(nome, import, ct);

            foreach (var b in blocos)
            {
                // valores do bloco (N, F, Sit.)
                var notaTxt = ws.Cell(r, b.NotaCol).GetString();
                var faltasTxt = ws.Cell(r, b.FaltasCol).GetString();
                var sitTxt = ws.Cell(r, b.SitCol).GetString();

                var nota = ParseDecimal(notaTxt);          // "8,5" -> 8.5
                var faltas = ParseFaltasNumber(faltasTxt);   // "-" -> null ; "2" -> 2
                var sit = await TryResolveSituacaoAsync(sitTxt, ct); // APR/REP/CAN/CUR/OUT

                // se não veio nada, pula a matéria
                if (nota is null && faltas is null && sit is null) continue;

                // rótulo da disciplina fica 1~2 linhas acima do 'N'
                var sigla = ReadUpperHeader(ws, rowHeader, b.NotaCol);
                if (string.IsNullOrWhiteSpace(sigla)) sigla = $"DISC_{b.NotaCol}";
                var disc = await GetOrCreateDisciplinaAsync(sigla, import, ct);

                // insere UM fato por aluno×disciplina×etapa
                var fato = new FatoNota
                {
                    ImportId = import.Id,
                    AlunoId = aluno.Id,
                    DisciplinaId = disc.Id,
                    PeriodoAvaliativoId = etapaId,
                    Nota = nota,
                    // usamos o campo 'Frequencia' para guardar o NÚMERO DE FALTAS da etapa
                    Frequencia = faltas,
                    SituacaoId = sit?.Id,
                    PeriodoLetivoId = import.PeriodoLetivoId!.Value
                };

                await _session.SaveAsync(fato, ct);
            }
        }
    }

    // ========================== Helpers ==========================

    // Só etapas 1..4 (pula Final)
    private static bool IsEtapa14(IXLWorksheet ws)
    {
        var n = Normalize(ws.Name).ToLowerInvariant();
        return n.StartsWith("1") || n.StartsWith("2") || n.StartsWith("3") || n.StartsWith("4");
    }

    private static int MapEtapa14(string sheetName)
    {
        var n = Normalize(sheetName);
        if (n.StartsWith("1")) return 1;
        if (n.StartsWith("2")) return 2;
        if (n.StartsWith("3")) return 3;
        if (n.StartsWith("4")) return 4;
        throw new InvalidOperationException($"Aba '{sheetName}' não é etapa 1..4.");
    }

    private sealed record DiscBlock(int NotaCol, int FaltasCol, int SitCol);

    private static List<DiscBlock> DetectDiscBlocks(IXLWorksheet ws, int headerRow, int startCol)
    {
        var blocks = new List<DiscBlock>();
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? startCol;

        int c = startCol;
        while (c + 2 <= lastCol)
        {
            var hN = Normalize(ws.Cell(headerRow, c).GetString()).ToUpperInvariant();
            var hF = Normalize(ws.Cell(headerRow, c + 1).GetString()).ToUpperInvariant();
            var hSt = Normalize(ws.Cell(headerRow, c + 2).GetString()).ToUpperInvariant();

            // exige exatamente N | F | Sit. (aceita variações mínimas)
            if (!(IsNotaHeader(hN) && IsFHeader(hF) && IsSitHeader(hSt)))
                break;

            blocks.Add(new DiscBlock(c, c + 1, c + 2));
            c += 3;
        }
        return blocks;
    }

    // cabeçalho acima do subheader (ex.: "INT.09889 (VTPLPR1)")
    private static string ReadUpperHeader(IXLWorksheet ws, int headerRow, int col)
    {
        for (int r = headerRow - 1; r >= Math.Max(1, headerRow - 2); r--)
        {
            var t = Normalize(ws.Cell(r, col).GetString());
            if (!string.IsNullOrWhiteSpace(t))
                return t;
        }
        return string.Empty;
    }

    private static IXLCell? FindHeaderCell(IXLWorksheet ws, string header)
    {
        var h = Normalize(header);
        return ws.CellsUsed().FirstOrDefault(c =>
            Normalize(c.GetString()).Equals(h, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Aluno> GetOrCreateAlunoByNomeAsync(string nome, ImportBatch import, CancellationToken ct)
    {
        var a = await _session.Query<Aluno>()
            .FirstOrDefaultAsync(x => x.Nome.ToLower() == nome.ToLower(), ct);
        if (a is not null) return a;

        a = new Aluno { Nome = nome, ImportId = import.Id };
        await _session.SaveAsync(a, ct);
        return a;
    }

    private async Task<Disciplina> GetOrCreateDisciplinaAsync(string sigla, ImportBatch import, CancellationToken ct)
    {
        var d = await _session.Query<Disciplina>().FirstOrDefaultAsync(x => x.Sigla == sigla, ct);
        if (d is not null) return d;

        d = new Disciplina { Sigla = sigla, ImportId = import.Id };
        await _session.SaveAsync(d, ct);
        return d;
    }

    private async Task<Situacao?> TryResolveSituacaoAsync(string? raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var txt = Normalize(raw).ToUpperInvariant(); // APR/REP/CAN/CUR/OUT
        return await _session.Query<Situacao>()
            .FirstOrDefaultAsync(s => s.Descricao.ToUpper() == txt, ct);
    }

    // headers aceitos
    private static bool IsNotaHeader(string t) => t == "N" || t.StartsWith("NOT");
    private static bool IsFHeader(string t) => t == "F" || t.StartsWith("FAL"); // F = faltas
    private static bool IsSitHeader(string t)
    {
        t = t.Replace(".", "");
        return t == "SIT" || t.StartsWith("S");
    }

    // parsing
    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Trim();
        return Regex.Replace(s, @"\s+", " ");
    }

    private static bool IsOnlyDigits(string s) => s.Length > 0 && s.All(char.IsDigit);

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "-") return null;
        var t = raw.Replace("%", "").Replace(",", ".").Trim();
        return decimal.TryParse(t, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? d : (decimal?)null;
    }

    private static decimal? ParseFaltasNumber(string? raw) => ParseDecimal(raw);

    private async Task UpdateImportStatus(ImportBatch import, short status, string? error, CancellationToken ct)
    {
        using var tx = _session.BeginTransaction();
        import.Status = status;
        import.Error = error;
        await _session.UpdateAsync(import, ct);
        await tx.CommitAsync(ct);
    }
}

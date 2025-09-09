using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace SchoolETL.Services;

public sealed class ExcelToCsvSplitter
{
    private readonly ILogger<ExcelToCsvSplitter> _log;
    private readonly CultureInfo _ptBR = new("pt-BR");

    public ExcelToCsvSplitter(ILogger<ExcelToCsvSplitter> log) => _log = log;

    public sealed record CsvSplitResult(
        string SourcePath,
        string OutputDir,
        IReadOnlyList<(int Etapa, string FilePath, int Rows)> Files);

    public async Task<CsvSplitResult> SplitAsync(string xlsxPath, string outputDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);
        var produced = new List<(int Etapa, string FilePath, int Rows)>();

        using var wb = new XLWorkbook(xlsxPath);
        foreach (var ws in wb.Worksheets)
        {
            ct.ThrowIfCancellationRequested();
            var etapa = MapEtapa(ws.Name);
            if (etapa is null) { _log.LogDebug("Aba ignorada: {Sheet}", ws.Name); continue; }

            var headerRow = FindHeaderRow(ws);
            if (headerRow == 0) { _log.LogWarning("Sem cabeçalho 'Aluno' em {Sheet}", ws.Name); continue; }

            var subHeaderRow = headerRow;
            var topHeaderRow = Math.Max(1, subHeaderRow - 1);
            var trios = DetectTrios(ws, subHeaderRow);
            if (trios.Count == 0) { _log.LogWarning("Sem trios N/F/Sit. em {Sheet}", ws.Name); continue; }

            var csvPath = Path.Combine(outputDir, $"etapa_{etapa.Value}.csv");
            var rowsWritten = 0;

            await using var fs = File.Create(csvPath);
            await using var sw = new StreamWriter(fs, new UTF8Encoding(false));

            await sw.WriteLineAsync("aluno,disciplina,etapa,nota,faltas,situacao");

            var alunoCol = FindAlunoColumn(ws, subHeaderRow);
            if (alunoCol == 0) alunoCol = 1;

            var r = subHeaderRow + 1;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var aluno = ws.Cell(r, alunoCol).GetString().Trim();
                if (string.IsNullOrWhiteSpace(aluno)) break;
                if (IsNoiseRow(aluno)) { r++; continue; }

                foreach (var (nCol, fCol, sCol) in trios)
                {
                    var disciplinaRaw = ws.Cell(topHeaderRow, nCol).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(disciplinaRaw))
                        disciplinaRaw = ws.Cell(Math.Max(1, topHeaderRow - 1), nCol).GetString().Trim();

                    var disciplina = NormalizeDisciplina(disciplinaRaw);
                    if (string.IsNullOrWhiteSpace(disciplina)) continue;

                    var notaStr = ws.Cell(r, nCol).GetString().Trim();
                    var faltasStr = ws.Cell(r, fCol).GetString().Trim();
                    var sit = NormalizeSituacao(ws.Cell(r, sCol).GetString().Trim());

                    var nota = ParseDecimalOrNull(notaStr);
                    var faltas = ParseIntOrNull(faltasStr);
                    if (nota is null && faltas is null && string.IsNullOrWhiteSpace(sit)) continue;

                    var line = string.Join(',',
                        Csv(aluno),
                        Csv(disciplina),
                        etapa.Value.ToString(CultureInfo.InvariantCulture),
                        nota?.ToString(_ptBR) ?? "",
                        faltas?.ToString(CultureInfo.InvariantCulture) ?? "",
                        Csv(sit)
                    );
                    await sw.WriteLineAsync(line);
                    rowsWritten++;
                }

                r++;
            }

            await sw.FlushAsync();
            produced.Add((etapa.Value, csvPath, rowsWritten));
            _log.LogInformation("CSV gerado: {CsvPath} ({Rows} linhas)", csvPath, rowsWritten);
        }

        return new CsvSplitResult(xlsxPath, outputDir, produced);
    }

    // Helpers -------------
    private static int FindHeaderRow(IXLWorksheet ws)
    {
        for (int r = 1; r <= 20; r++)
            for (int c = 1; c <= 200; c++)
                if (ws.Cell(r, c).GetString().Equals("Aluno", StringComparison.OrdinalIgnoreCase))
                    return r;
        return 0;
    }

    private static int FindAlunoColumn(IXLWorksheet ws, int headerRow)
    {
        for (int c = 1; c <= 200; c++)
            if (ws.Cell(headerRow, c).GetString().Equals("Aluno", StringComparison.OrdinalIgnoreCase))
                return c;
        return 0;
    }

    private static List<(int N, int F, int Sit)> DetectTrios(IXLWorksheet ws, int subHeaderRow)
    {
        var list = new List<(int, int, int)>();
        for (int c = 1; c <= 200; c++)
        {
            var v = ws.Cell(subHeaderRow, c).GetString().Trim();
            if (!v.Equals("N", StringComparison.OrdinalIgnoreCase)) continue;
            var f = c + 1; var s = c + 2;
            var vf = ws.Cell(subHeaderRow, f).GetString().Trim();
            var vs = ws.Cell(subHeaderRow, s).GetString().Trim();
            if (vf.Equals("F", StringComparison.OrdinalIgnoreCase) &&
                (vs.Equals("S", StringComparison.OrdinalIgnoreCase) || vs.StartsWith("Sit", StringComparison.OrdinalIgnoreCase)))
            { list.Add((c, f, s)); c += 2; }
        }
        return list;
    }

    private static bool IsNoiseRow(string alunoCell)
    {
        var v = alunoCell.Trim();
        if (string.IsNullOrWhiteSpace(v)) return true;
        var noise = new[] { "Carga Horária", "Abaixo da média", "Média", "Aluno" };
        return noise.Any(n => v.StartsWith(n, StringComparison.OrdinalIgnoreCase))
               || v == "#" || int.TryParse(v, out _);
    }

    private static string NormalizeDisciplina(string raw)
        => raw.Replace('\n', ' ').Replace('\r', ' ').Trim();

    private static string NormalizeSituacao(string sit)
    {
        var s = sit?.Trim().ToUpperInvariant() ?? "";
        return s switch
        {
            "APR" or "APROV" or "APROVADO" => "APR",
            "REP" or "REPROV" or "REPROVADO" => "REP",
            "CAN" or "CANCEL" or "CANCELADA" => "CAN",
            "CUR" or "CURS" => "CUR",
            "" => "",
            _ => s
        };
    }

    private decimal? ParseDecimalOrNull(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var x = v.Trim();
        if (x == "-" || x.Equals("NA", StringComparison.OrdinalIgnoreCase)) return null;
        if (decimal.TryParse(x, NumberStyles.Any, _ptBR, out var d)) return d;
        if (decimal.TryParse(x, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
        return null;
    }

    private static int? ParseIntOrNull(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var x = v.Trim();
        if (x == "-") return null;
        if (int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
        if (int.TryParse(x, NumberStyles.Integer, new CultureInfo("pt-BR"), out i)) return i;
        return null;
    }

    private static int? MapEtapa(string sheetName)
    {
        var n = sheetName.Trim().ToLowerInvariant();
        if (n.Contains("final")) return null;
        for (int i = 1; i <= 4; i++)
            if (n.Contains(i.ToString()) || n.Contains($"{i}º") || n.Contains($"{i}o") || n.Contains($"{i}ª"))
                return i;
        return null;
    }

    private static string Csv(string? value)
    {
        var s = value ?? "";
        var needsQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        return needsQuote ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }
}

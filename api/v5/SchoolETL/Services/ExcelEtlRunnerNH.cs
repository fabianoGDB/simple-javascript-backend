using System.Data;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using NHibernate;
using NHibernate.Linq;
using Npgsql;
using SchoolETL.Core.Models;
using ISession = NHibernate.ISession;

namespace SchoolETL.Services;

public partial class ExcelEtlRunnerNH : IExcelEtlRunner
{
    private readonly ISession _session;
    private readonly ILogger<ExcelEtlRunnerNH> _log;
    public ExcelEtlRunnerNH(ISession session, ILogger<ExcelEtlRunnerNH> log)
    { _session = session; _log = log; }

    // ========= modo "monolito": chama Registros + Etapas 1..4 =========
    public async Task RunAsync(ImportBatch import, CancellationToken ct)
    {
        await UpdateImport(import, 1, null, ct); // Processando
        try
        {
            await RunRegistrosOnly(import, _session, ct);
            foreach (var etapa in new[] { 1, 2, 3, 4 })
                await RunEtapaOnly(import, etapa, _session, ct);

            await UpdateImport(import, 2, null, ct); // Finalizado
        }
        catch (Exception ex)
        {
            await UpdateImport(import, 3, ex.Message, ct); // Erro
            throw;
        }
    }

    // ======================= apenas "Registros" =======================
    public async Task RunRegistrosOnly(ImportBatch import, ISession session, CancellationToken ct)
    {
        _log.LogInformation("Registros START Import {ImportId}", import.Id);

        if (string.IsNullOrWhiteSpace(import.StorageUri) || !File.Exists(import.StorageUri))
            throw new FileNotFoundException("Arquivo do import não encontrado", import.StorageUri);

        using var wb = new XLWorkbook(import.StorageUri);
        var ws = wb.Worksheets.FirstOrDefault(s =>
            Normalize(s.Name).Equals("registros", StringComparison.OrdinalIgnoreCase));
        if (ws is null) { _log.LogInformation("Registros não encontrados."); return; }

        var alunosByNome = await LoadAlunosCacheAsync(ct);
        var bufferSnap = new List<AlunoStatusRow>(2000);

        var alunoHeader = FindHeaderCell(ws, "aluno");
        if (alunoHeader is null) return;

        int rowHeader = alunoHeader.Address.RowNumber;
        int colAluno = alunoHeader.Address.ColumnNumber;
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? rowHeader;

        int? colFreq = FindHeaderColumn(ws, rowHeader, new[] { "frequência", "frequencia", "frequência geral", "frequencia geral" });
        int? colSit = FindHeaderColumn(ws, rowHeader, new[] { "situação", "situacao", "situação no curso", "situacao no curso" });

        for (int r = rowHeader + 1; r <= lastRow; r++)
        {
            var nome = Normalize(ws.Cell(r, colAluno).GetString());
            if (string.IsNullOrWhiteSpace(nome) || nome == "#" || IsOnlyDigits(nome)) continue;

            int alunoId = GetOrCreateAlunoId(nome, import, alunosByNome);

            decimal? freqPercent = colFreq.HasValue ? ParseDecimal(ws.Cell(r, colFreq.Value).GetString().Replace("%", "")) : null;
            string? situacaoCurso = colSit.HasValue ? Normalize(ws.Cell(r, colSit.Value).GetString()) : null;
            if (string.IsNullOrWhiteSpace(situacaoCurso)) situacaoCurso = null;

            bufferSnap.Add(new AlunoStatusRow
            {
                ImportId = import.Id,
                AlunoId = alunoId,
                PeriodoLetivoId = import.PeriodoLetivoId,
                FrequenciaGeral = freqPercent,
                SituacaoCurso = situacaoCurso,
                CriadoEmUtc = DateTime.UtcNow
            });
        }

        await CopyAlunoStatusAsync(bufferSnap, ct);

        _log.LogInformation("Registros END   Import {ImportId}", import.Id);
    }

    // ========================= apenas UMA etapa ========================
    public async Task RunEtapaOnly(ImportBatch import, int etapaId, ISession session, CancellationToken ct)
    {
        _log.LogInformation("Etapa {Etapa} START Import {ImportId}", etapaId, import.Id);

        if (string.IsNullOrWhiteSpace(import.StorageUri) || !File.Exists(import.StorageUri))
            throw new FileNotFoundException("Arquivo do import não encontrado", import.StorageUri);

        using var wb = new XLWorkbook(import.StorageUri);
        var ws = wb.Worksheets.FirstOrDefault(w => IsEtapa14(w) && MapEtapa14(w.Name) == etapaId);
        if (ws is null) { _log.LogInformation("Aba da etapa {Etapa} não encontrada.", etapaId); return; }

        // Idempotência simples: apaga fatos da etapa do import antes de inserir
        await using (var conn = (NpgsqlConnection)session.Connection!)
        {
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("DELETE FROM fato_nota WHERE import_id=@i AND periodo_avaliativo_id=@e", conn);
            cmd.Parameters.AddWithValue("i", import.Id);
            cmd.Parameters.AddWithValue("e", etapaId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var alunosByNome = await LoadAlunosCacheAsync(ct);
        var disciplinasBySigla = await LoadDisciplinasCacheAsync(ct);
        var situacoesByDesc = await LoadSituacoesCacheAsync(ct);

        var used = ws.RangeUsed();
        if (used is null) return;

        var alunoHeader = FindHeaderCell(ws, "aluno");
        if (alunoHeader is null) return;

        int rowHeader = alunoHeader.Address.RowNumber;
        int colAluno = alunoHeader.Address.ColumnNumber;

        var blocos = DetectDiscBlocks(ws, rowHeader, colAluno + 1);
        if (blocos.Count == 0) return;

        int firstDataRow = rowHeader + 1;
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? firstDataRow;
        var bufferFatos = new List<FatoNotaRow>(20000);

        for (int r = firstDataRow; r <= lastRow; r++)
        {
            var nome = Normalize(ws.Cell(r, colAluno).GetString());
            if (string.IsNullOrWhiteSpace(nome) || nome == "#" || IsOnlyDigits(nome)) continue;

            int alunoId = GetOrCreateAlunoId(nome, import, alunosByNome);

            foreach (var b in blocos)
            {
                var notaTxt = ws.Cell(r, b.NotaCol).GetString();
                var faltasTxt = ws.Cell(r, b.FaltasCol).GetString();
                var sitTxt = ws.Cell(r, b.SitCol).GetString();

                var nota = ParseDecimal(notaTxt);
                var faltas = ParseDecimal(faltasTxt);
                var sitKey = Normalize(sitTxt).ToUpperInvariant();

                if (nota is null && faltas is null && string.IsNullOrWhiteSpace(sitKey)) continue;

                // Lê cabeçalho "superior" da disciplina na coluna de Nota
                var headerText = ReadUpperHeader(ws, rowHeader, b.NotaCol);
                if (string.IsNullOrWhiteSpace(headerText))
                    headerText = $"DISC_{b.NotaCol}";

                // Separa sigla e carga horária
                var (discSigla, carga) = ParseDisciplinaHeader(headerText);

                // Garante persistência de disciplina com sigla correta e carga horária no campo certo
                int disciplinaId = GetOrCreateDisciplinaId(discSigla, carga, import, disciplinasBySigla);

                int? situacaoId = null;
                if (!string.IsNullOrWhiteSpace(sitKey) && situacoesByDesc.TryGetValue(sitKey, out var id))
                    situacaoId = id;

                bufferFatos.Add(new FatoNotaRow
                {
                    ImportId = import.Id,
                    AlunoId = alunoId,
                    DisciplinaId = disciplinaId,
                    EtapaId = etapaId,
                    SituacaoId = situacaoId,
                    PeriodoLetivoId = import.PeriodoLetivoId!.Value,
                    Nota = nota,
                    Faltas = faltas
                });
            }
        }

        await CopyFatosAsync(bufferFatos, ct);

        _log.LogInformation("Etapa {Etapa} END   Import {ImportId}", etapaId, import.Id);
    }

    // ========================= Helpers & COPY =========================

    private async Task UpdateImport(ImportBatch import, short status, string? error, CancellationToken ct)
    {
        using var tx = _session.BeginTransaction();
        import.Status = status;
        import.Error = error;
        await _session.UpdateAsync(import, ct);
        await tx.CommitAsync(ct);
    }

    private async Task<Dictionary<string, int>> LoadAlunosCacheAsync(CancellationToken ct) =>
        (await _session.Query<Aluno>().Select(a => new { a.Id, a.Nome }).ToListAsync(ct))
        .GroupBy(x => Normalize(x.Nome), StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

    private async Task<Dictionary<string, int>> LoadDisciplinasCacheAsync(CancellationToken ct) =>
        (await _session.Query<Disciplina>().Select(d => new { d.Id, d.Sigla }).ToListAsync(ct))
        .GroupBy(x => Normalize(x.Sigla), StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

    private async Task<Dictionary<string, int>> LoadSituacoesCacheAsync(CancellationToken ct) =>
        (await _session.Query<Situacao>().Select(s => new { s.Id, s.Descricao }).ToListAsync(ct))
        .GroupBy(x => Normalize(x.Descricao).ToUpperInvariant())
        .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

    private int GetOrCreateAlunoId(string nome, ImportBatch import, Dictionary<string, int> cache)
    {
        var key = Normalize(nome);
        if (cache.TryGetValue(key, out var id)) return id;
        var a = new Aluno { Nome = nome, ImportId = import.Id };
        _session.Save(a);
        cache[key] = a.Id;
        return a.Id;
    }

    // Agora recebe também a carga horária para gravar no campo correto
    private int GetOrCreateDisciplinaId(string siglaRaw, string? cargaHoraria, ImportBatch import, Dictionary<string, int> cache)
    {
        var key = Normalize(siglaRaw);
        if (cache.TryGetValue(key, out var id)) return id;

        var d = new Disciplina
        {
            Sigla = siglaRaw,                  // apenas a sigla/código (ex.: "INT.09889")
            CargaHorariaRotulo = cargaHoraria, // ex.: "44H de 160H"
            ImportId = import.Id
        };

        _session.Save(d);
        cache[key] = d.Id;
        return d.Id;
    }

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

    private static IXLCell? FindHeaderCell(IXLWorksheet ws, string header)
    {
        var h = Normalize(header);
        return ws.CellsUsed().FirstOrDefault(c =>
            Normalize(c.GetString()).Equals(h, StringComparison.OrdinalIgnoreCase));
    }

    private static int? FindHeaderColumn(IXLWorksheet ws, int headerRow, IEnumerable<string> candidates)
    {
        foreach (var cell in ws.Row(headerRow).CellsUsed())
        {
            var t = Normalize(cell.GetString()).ToLowerInvariant();
            if (candidates.Any(c => t.Contains(Normalize(c).ToLowerInvariant())))
                return cell.Address.ColumnNumber;
        }
        return null;
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
            var hS = Normalize(ws.Cell(headerRow, c + 2).GetString()).ToUpperInvariant();

            if (!(IsNotaHeader(hN) && IsFHeader(hF) && IsSitHeader(hS))) break;

            blocks.Add(new DiscBlock(c, c + 1, c + 2));
            c += 3;
        }
        return blocks;
    }

    private static string ReadUpperHeader(IXLWorksheet ws, int headerRow, int col)
    {
        for (int r = headerRow - 1; r >= Math.Max(1, headerRow - 2); r--)
        {
            var t = Normalize(ws.Cell(r, col).GetString());
            if (!string.IsNullOrWhiteSpace(t)) return t;
        }
        return string.Empty;
    }

    // headers
    private static bool IsNotaHeader(string t) => t == "N" || t.StartsWith("NOT");
    private static bool IsFHeader(string t) => t == "F" || t.StartsWith("FAL");
    private static bool IsSitHeader(string t) { t = t.Replace(".", ""); return t == "SIT" || t.StartsWith("S"); }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Trim();
        return Regex.Replace(s, @"\s+", " ");
    }
    private static string NormalizeDisciplina(string raw)
        => raw.Replace('\n', ' ').Replace('\r', ' ').Trim();

    private static bool IsOnlyDigits(string s) => s.Length > 0 && s.All(char.IsDigit);

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "-") return null;
        var t = raw.Replace("%", "").Replace(",", ".").Trim();
        return decimal.TryParse(t, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? d : (decimal?)null;
    }

    // Extrai (sigla, "44H de 160H") do cabeçalho
    private (string sigla, string? carga) ParseDisciplinaHeader(string header)
    {
        // Normaliza e remove quebras de linha
        var text = NormalizeDisciplina(header);

        // encontra padrão de carga horária
        string? carga = null;
        var m = Regex.Match(text, @"\b\d+H\s+de\s+\d+H\b", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            carga = m.Value.Trim();                  // "44H de 160H"
            text = text.Replace(m.Value, "").Trim(); // remove do texto
        }

        // limpa espaços duplicados e parênteses soltos
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        text = text.Trim('(', ')').Trim();

        // se sobrar vazio, usa o original
        var sigla = string.IsNullOrWhiteSpace(text) ? header.Trim() : text;
        return (sigla, carga);
    }

    // ======================= COPY (bulk insert) =======================
    private sealed class FatoNotaRow
    {
        public Guid ImportId { get; init; }
        public int AlunoId { get; init; }
        public int DisciplinaId { get; init; }
        public int EtapaId { get; init; }
        public int? SituacaoId { get; init; }
        public int PeriodoLetivoId { get; init; }
        public decimal? Nota { get; init; }
        public decimal? Faltas { get; init; }
    }

    private sealed class AlunoStatusRow
    {
        public Guid ImportId { get; init; }
        public int AlunoId { get; init; }
        public int? PeriodoLetivoId { get; init; }
        public decimal? FrequenciaGeral { get; init; }
        public string? SituacaoCurso { get; init; }
        public DateTime CriadoEmUtc { get; init; }
    }

    private async Task CopyFatosAsync(List<FatoNotaRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        var conn = (NpgsqlConnection)_session.Connection!;
        var needClose = conn.State != ConnectionState.Open;
        if (needClose) await conn.OpenAsync(ct);

        await using var wr = conn.BeginBinaryImport(
            "COPY fato_nota (import_id, aluno_id, disciplina_id, periodo_avaliativo_id, situacao_id, periodo_letivo_id, nota, frequencia) FROM STDIN (FORMAT BINARY)");
        foreach (var r in rows)
        {
            wr.StartRow();
            wr.Write(r.ImportId, NpgsqlTypes.NpgsqlDbType.Uuid);
            wr.Write(r.AlunoId, NpgsqlTypes.NpgsqlDbType.Integer);
            wr.Write(r.DisciplinaId, NpgsqlTypes.NpgsqlDbType.Integer);
            wr.Write(r.EtapaId, NpgsqlTypes.NpgsqlDbType.Integer);
            if (r.SituacaoId.HasValue) wr.Write(r.SituacaoId.Value, NpgsqlTypes.NpgsqlDbType.Integer); else wr.WriteNull();
            wr.Write(r.PeriodoLetivoId, NpgsqlTypes.NpgsqlDbType.Integer);
            if (r.Nota.HasValue) wr.Write(r.Nota.Value, NpgsqlTypes.NpgsqlDbType.Numeric); else wr.WriteNull();
            if (r.Faltas.HasValue) wr.Write(r.Faltas.Value, NpgsqlTypes.NpgsqlDbType.Numeric); else wr.WriteNull();
        }
        await wr.CompleteAsync(ct);

        if (needClose) await conn.CloseAsync();
    }

    private async Task CopyAlunoStatusAsync(List<AlunoStatusRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        var conn = (NpgsqlConnection)_session.Connection!;
        var needClose = conn.State != ConnectionState.Open;
        if (needClose) await conn.OpenAsync(ct);

        await using var wr = conn.BeginBinaryImport(
            "COPY aluno_status_import (import_id, aluno_id, periodo_letivo_id, frequencia_geral, situacao_curso, criado_em_utc) FROM STDIN (FORMAT BINARY)");
        foreach (var r in rows)
        {
            wr.StartRow();
            wr.Write(r.ImportId, NpgsqlTypes.NpgsqlDbType.Uuid);
            wr.Write(r.AlunoId, NpgsqlTypes.NpgsqlDbType.Integer);
            if (r.PeriodoLetivoId.HasValue) wr.Write(r.PeriodoLetivoId.Value, NpgsqlTypes.NpgsqlDbType.Integer); else wr.WriteNull();
            if (r.FrequenciaGeral.HasValue) wr.Write(r.FrequenciaGeral.Value, NpgsqlTypes.NpgsqlDbType.Numeric); else wr.WriteNull();
            if (r.SituacaoCurso is not null) wr.Write(r.SituacaoCurso, NpgsqlTypes.NpgsqlDbType.Text); else wr.WriteNull();
            wr.Write(r.CriadoEmUtc, NpgsqlTypes.NpgsqlDbType.TimestampTz);
        }
        await wr.CompleteAsync(ct);

        if (needClose) await conn.CloseAsync();
    }
}

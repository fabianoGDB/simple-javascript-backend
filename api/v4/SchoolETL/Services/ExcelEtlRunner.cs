using System.Data;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using NHibernate;
using NHibernate.Linq;
using Npgsql;
using SchoolETL.Core.Models;
using ISession = NHibernate.ISession;


namespace SchoolETL.Services;

/// <summary>
/// ETL de planilhas: Etapas 1..4 (N|F|Sit.) -> fato_nota
/// e "Registros" -> aluno_status_import.
/// Usa NHibernate para lookups e Npgsql COPY para inserir em lote.
/// </summary>
public class ExcelEtlRunnerNH : IExcelEtlRunner
{
    private readonly ISession _session;
    public ExcelEtlRunnerNH(ISession session) => _session = session;

    // ===== Public entry =====
    public async Task RunAsync(ImportBatch import, CancellationToken ct)
    {
        await UpdateImport(import, status: 1, error: null, ct); // Processando

        try
        {
            if (string.IsNullOrWhiteSpace(import.StorageUri) || !File.Exists(import.StorageUri))
                throw new FileNotFoundException("Arquivo do import não encontrado", import.StorageUri);

            using var wb = new XLWorkbook(import.StorageUri);

            // caches (case-insensitive)
            var alunosByNome = await LoadAlunosCacheAsync(ct);
            var disciplinasBySigla = await LoadDisciplinasCacheAsync(ct);
            var situacoesByDesc = await LoadSituacoesCacheAsync(ct);

            // buffers p/ COPY
            var bufferFatos = new List<FatoNotaRow>(capacity: 50_000);
            var bufferSnap = new List<AlunoStatusRow>(capacity: 5_000);

            // 1) Registros (opcional)
            ImportRegistrosIfAny(wb, import, alunosByNome, bufferSnap);

            // 2) Etapas 1..4
            foreach (var ws in wb.Worksheets.Where(IsEtapa14))
            {
                ImportEtapa(ws, import, alunosByNome, disciplinasBySigla, situacoesByDesc, bufferFatos);
            }

            // 3) Persistir buffers via COPY
            await CopyAlunoStatusAsync(bufferSnap, ct);
            await CopyFatosAsync(bufferFatos, ct);

            await UpdateImport(import, status: 2, error: null, ct); // Finalizado
        }
        catch (Exception ex)
        {
            await UpdateImport(import, status: 3, error: ex.Message, ct); // Erro
            throw;
        }
    }

    // =====================================================================
    // ===================== Import "Registros" (snapshot) ==================
    // =====================================================================
    private void ImportRegistrosIfAny(
        XLWorkbook wb,
        ImportBatch import,
        Dictionary<string, int> alunosByNome,
        List<AlunoStatusRow> bufferSnap)
    {
        var ws = wb.Worksheets.FirstOrDefault(s =>
            Normalize(s.Name).Equals("registros", StringComparison.OrdinalIgnoreCase));
        if (ws is null) return;

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
    }

    // =====================================================================
    // ========================= Import Etapas 1..4 =========================
    // =====================================================================
    private void ImportEtapa(
        IXLWorksheet ws,
        ImportBatch import,
        Dictionary<string, int> alunosByNome,
        Dictionary<string, int> disciplinasBySigla,
        Dictionary<string, int> situacoesByDesc,
        List<FatoNotaRow> bufferFatos)
    {
        var used = ws.RangeUsed();
        if (used is null) return;

        var alunoHeader = FindHeaderCell(ws, "aluno");
        if (alunoHeader is null) return;

        int rowHeader = alunoHeader.Address.RowNumber;
        int colAluno = alunoHeader.Address.ColumnNumber;

        var blocos = DetectDiscBlocks(ws, rowHeader, colAluno + 1);
        if (blocos.Count == 0) return;

        int etapaId = MapEtapa14(ws.Name);
        int firstDataRow = rowHeader + 1;
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? firstDataRow;

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

                var sigla = ReadUpperHeader(ws, rowHeader, b.NotaCol);
                if (string.IsNullOrWhiteSpace(sigla)) sigla = $"DISC_{b.NotaCol}";
                int disciplinaId = GetOrCreateDisciplinaId(sigla, import, disciplinasBySigla);

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
                    Faltas = faltas // usamos o campo 'frequencia' para guardar NÚMERO de faltas da etapa
                });
            }
        }
    }

    // =====================================================================
    // ============================ COPY helpers ============================
    // =====================================================================
    private async Task CopyFatosAsync(List<FatoNotaRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        var conn = (NpgsqlConnection)_session.Connection!;
        var needClose = false;
        if (conn.State != ConnectionState.Open) { await conn.OpenAsync(ct); needClose = true; }

        await using var wr = conn.BeginBinaryImport(
            "COPY fato_nota (import_id, aluno_id, disciplina_id, periodo_avaliativo_id, situacao_id, periodo_letivo_id, nota, frequencia) FROM STDIN (FORMAT BINARY)");
        foreach (var r in rows)
        {
            wr.StartRow();
            wr.Write(r.ImportId, NpgsqlTypes.NpgsqlDbType.Uuid);
            wr.Write(r.AlunoId, NpgsqlTypes.NpgsqlDbType.Integer);
            wr.Write(r.DisciplinaId, NpgsqlTypes.NpgsqlDbType.Integer);
            wr.Write(r.EtapaId, NpgsqlTypes.NpgsqlDbType.Integer);
            if (r.SituacaoId.HasValue) wr.Write(r.SituacaoId.Value, NpgsqlTypes.NpgsqlDbType.Integer);
            else wr.WriteNull();
            wr.Write(r.PeriodoLetivoId, NpgsqlTypes.NpgsqlDbType.Integer);
            if (r.Nota.HasValue) wr.Write(r.Nota.Value, NpgsqlTypes.NpgsqlDbType.Numeric);
            else wr.WriteNull();
            if (r.Faltas.HasValue) wr.Write(r.Faltas.Value, NpgsqlTypes.NpgsqlDbType.Numeric);
            else wr.WriteNull();
        }
        await wr.CompleteAsync(ct);

        if (needClose) await conn.CloseAsync();
    }

    private async Task CopyAlunoStatusAsync(List<AlunoStatusRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        var conn = (NpgsqlConnection)_session.Connection!;
        var needClose = false;
        if (conn.State != ConnectionState.Open) { await conn.OpenAsync(ct); needClose = true; }

        await using var wr = conn.BeginBinaryImport(
            "COPY aluno_status_import (import_id, aluno_id, periodo_letivo_id, frequencia_geral, situacao_curso, criado_em_utc) FROM STDIN (FORMAT BINARY)");
        foreach (var r in rows)
        {
            wr.StartRow();
            wr.Write(r.ImportId, NpgsqlTypes.NpgsqlDbType.Uuid);
            wr.Write(r.AlunoId, NpgsqlTypes.NpgsqlDbType.Integer);
            if (r.PeriodoLetivoId.HasValue) wr.Write(r.PeriodoLetivoId.Value, NpgsqlTypes.NpgsqlDbType.Integer);
            else wr.WriteNull();
            if (r.FrequenciaGeral.HasValue) wr.Write(r.FrequenciaGeral.Value, NpgsqlTypes.NpgsqlDbType.Numeric);
            else wr.WriteNull();
            if (r.SituacaoCurso is not null) wr.Write(r.SituacaoCurso, NpgsqlTypes.NpgsqlDbType.Text);
            else wr.WriteNull();
            wr.Write(r.CriadoEmUtc, NpgsqlTypes.NpgsqlDbType.TimestampTz);
        }
        await wr.CompleteAsync(ct);

        if (needClose) await conn.CloseAsync();
    }

    // =====================================================================
    // ============================== Caches ===============================
    // =====================================================================
    private async Task<Dictionary<string, int>> LoadAlunosCacheAsync(CancellationToken ct)
    {
        var all = await _session.Query<Aluno>()
            .Select(a => new { a.Id, a.Nome })
            .ToListAsync(ct);

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in all)
        {
            var key = Normalize(a.Nome);
            if (!dict.ContainsKey(key))
                dict[key] = a.Id;
        }
        return dict;
    }

    private async Task<Dictionary<string, int>> LoadDisciplinasCacheAsync(CancellationToken ct)
    {
        var all = await _session.Query<Disciplina>()
            .Select(d => new { d.Id, d.Sigla })
            .ToListAsync(ct);

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in all)
        {
            var key = Normalize(d.Sigla);
            if (!dict.ContainsKey(key))
                dict[key] = d.Id;
        }
        return dict;
    }

    private async Task<Dictionary<string, int>> LoadSituacoesCacheAsync(CancellationToken ct)
    {
        var all = await _session.Query<Situacao>()
            .Select(s => new { s.Id, s.Descricao })
            .ToListAsync(ct);

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in all)
        {
            var key = Normalize(s.Descricao).ToUpperInvariant();
            if (!dict.ContainsKey(key))
                dict[key] = s.Id;
        }
        return dict;
    }

    private int GetOrCreateAlunoId(string nome, ImportBatch import, Dictionary<string, int> cache)
    {
        var key = Normalize(nome);
        if (cache.TryGetValue(key, out var id)) return id;

        var a = new Aluno { Nome = nome, ImportId = import.Id };
        _session.Save(a); // síncrono: estamos só gerando ID; COPY é para os fatos
        cache[key] = a.Id;
        return a.Id;
    }

    private int GetOrCreateDisciplinaId(string sigla, ImportBatch import, Dictionary<string, int> cache)
    {
        var key = Normalize(sigla);
        if (cache.TryGetValue(key, out var id)) return id;

        var d = new Disciplina { Sigla = sigla, ImportId = import.Id };
        _session.Save(d);
        cache[key] = d.Id;
        return d.Id;
    }

    // =====================================================================
    // ========================= Utilidades de parse =======================
    // =====================================================================
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

    private static bool IsNotaHeader(string t) => t == "N" || t.StartsWith("NOT");
    private static bool IsFHeader(string t) => t == "F" || t.StartsWith("FAL");
    private static bool IsSitHeader(string t)
    {
        t = t.Replace(".", "");
        return t == "SIT" || t.StartsWith("S");
    }

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

    private async Task UpdateImport(ImportBatch import, short status, string? error, CancellationToken ct)
    {
        using var tx = _session.BeginTransaction();
        import.Status = status;
        import.Error = error;
        await _session.UpdateAsync(import, ct);
        await tx.CommitAsync(ct);
    }

    // ========================== DTOs p/ COPY ==========================
    private sealed class FatoNotaRow
    {
        public Guid ImportId { get; init; }
        public int AlunoId { get; init; }
        public int DisciplinaId { get; init; }
        public int EtapaId { get; init; }                  // periodo_avaliativo_id
        public int? SituacaoId { get; init; }
        public int PeriodoLetivoId { get; init; }
        public decimal? Nota { get; init; }
        public decimal? Faltas { get; init; }              // salva em 'frequencia' (número de faltas)
    }

    private sealed class AlunoStatusRow
    {
        public Guid ImportId { get; init; }
        public int AlunoId { get; init; }
        public int? PeriodoLetivoId { get; init; }
        public decimal? FrequenciaGeral { get; init; }     // aqui é %, se existir na aba "Registros"
        public string? SituacaoCurso { get; init; }
        public DateTime CriadoEmUtc { get; init; }
    }
}

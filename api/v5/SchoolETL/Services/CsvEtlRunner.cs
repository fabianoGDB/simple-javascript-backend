using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using NHibernate;
using NHibernate.Linq;
using SchoolETL.Core.Models;

namespace SchoolETL.Services;

public sealed class CsvEtlRunner
{
    private readonly ISessionFactory _sf;

    public CsvEtlRunner(ISessionFactory sf) => _sf = sf;

    private sealed record FatoCsv(
        string aluno,
        string disciplina,
        int etapa,
        decimal? nota,
        int? faltas,
        string? situacao
    );

    public async Task<int> RunForImportAsync(Guid importId, string csvDir, CancellationToken ct)
    {
        var total = 0;

        using var s = _sf.OpenSession();
        var imp = await s.GetAsync<ImportBatch>(importId, ct) ?? throw new("Import não encontrado.");
        var situacoes = (await s.Query<Situacao>()
            .ToListAsync(ct)) // NHibernate tem ToListAsync
            .ToDictionary(x => x.Descricao.Trim(), x => x.Id, StringComparer.OrdinalIgnoreCase);

        var alunoCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var discCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        async Task<int> GetAlunoIdAsync(string nome)
        {
            if (alunoCache.TryGetValue(nome, out var id)) return id;
            var found = await s.Query<Aluno>()
                .Where(a => a.Nome.ToLower() == nome.ToLower())
                .Select(a => new { a.Id })
                .FirstOrDefaultAsync(ct);
            if (found is null)
            {
                var ent = new Aluno { ImportId = importId, Nome = nome };
                id = (int)await s.SaveAsync(ent, ct);
            }
            else id = found.Id;
            alunoCache[nome] = id;
            return id;
        }
        async Task<int> GetDiscIdAsync(string sigla)
        {
            if (discCache.TryGetValue(sigla, out var id)) return id;
            var found = await s.Query<Disciplina>()
                .Where(d => d.Sigla == sigla)
                .Select(d => new { d.Id })
                .FirstOrDefaultAsync(ct);
            if (found is null)
            {
                var ent = new Disciplina { ImportId = importId, Sigla = sigla };
                id = (int)await s.SaveAsync(ent, ct);
            }
            else id = found.Id;
            discCache[sigla] = id;
            return id;
        }

        for (int etapa = 1; etapa <= 4; etapa++)
        {
            var csvPath = Path.Combine(csvDir, $"etapa_{etapa}.csv");
            if (!File.Exists(csvPath)) continue;

            using var ss = _sf.OpenStatelessSession();
            using var tx = ss.BeginTransaction();

            var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null,
                MissingFieldFound = null
            };

            using var sr = new StreamReader(csvPath);
            using var csv = new CsvReader(sr, cfg);
            await foreach (var r in csv.GetRecordsAsync<FatoCsv>(ct))
            {
                if (r.etapa != etapa) continue;

                var alunoId = await GetAlunoIdAsync((r.aluno ?? "").Trim());
                var discId = await GetDiscIdAsync((r.disciplina ?? "").Trim());

                int? situId = null;
                if (!string.IsNullOrWhiteSpace(r.situacao) && situacoes.TryGetValue(r.situacao.Trim(), out var sid))
                    situId = sid;

                var fato = new FatoNota
                {
                    ImportId = importId,
                    AlunoId = alunoId,
                    DisciplinaId = discId,
                    PeriodoAvaliativoId = etapa,
                    SituacaoId = situId,
                    PeriodoLetivoId = imp.PeriodoLetivoId!.Value,
                    Nota = r.nota,
                    Frequencia = r.faltas
                };

                await ss.InsertAsync(fato, ct);
                total++;
            }

            await tx.CommitAsync(ct);

            using var txs = s.BeginTransaction();
            var st = await s.Query<ImportStage>().FirstOrDefaultAsync(x => x.ImportId == importId && x.EtapaId == etapa, ct);
            if (st is not null)
            {
                st.Status = 2;
                st.ProcessedRows = total;
                st.UpdatedAtUtc = DateTime.UtcNow;
                st.FinishedAtUtc = DateTime.UtcNow;
                await s.UpdateAsync(st, ct);
            }
            await txs.CommitAsync(ct);
        }

        using (var txf = s.BeginTransaction())
        {
            var stages = await s.Query<ImportStage>()
                .Where(x => x.ImportId == importId && x.EtapaId != null)
                .Select(x => x.Status)
                .ToListAsync(ct);

            var impDb = await s.GetAsync<ImportBatch>(importId, ct);
            if (impDb is not null)
            {
                impDb.Status = stages.Count > 0 && stages.All(x => x == 2) ? (short)2 : (short)1;
                await s.UpdateAsync(impDb, ct);
            }
            await txf.CommitAsync(ct);
        }

        return total;
    }
}

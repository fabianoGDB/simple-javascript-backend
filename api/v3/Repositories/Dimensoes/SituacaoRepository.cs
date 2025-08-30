using Microsoft.EntityFrameworkCore;
using SchoolETL.Data;
using SchoolETL.Models;

namespace SchoolETL.Repositories.Dimensoes;

public class SituacaoRepository : EfRepository<Situacao>, ISituacaoRepository
{
    public SituacaoRepository(DwContext db) : base(db) { }

    public async Task<int?> TryResolveIdAsync(string? sigla, CancellationToken ct = default)
    {
        sigla = (sigla ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(sigla)) return null;
        var s = await _db.Situacoes.FirstOrDefaultAsync(x => x.Descricao == sigla, ct);
        return s?.Id;
    }
}

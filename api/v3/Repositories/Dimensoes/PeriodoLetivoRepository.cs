using Microsoft.EntityFrameworkCore;
using SchoolETL.Data;
using SchoolETL.Models;

namespace SchoolETL.Repositories.Dimensoes;

public class PeriodoLetivoRepository : EfRepository<PeriodoLetivo>, IPeriodoLetivoRepository
{
    public PeriodoLetivoRepository(DwContext db) : base(db) { }

    public async Task<PeriodoLetivo> GetOrCreateAsync(int ano, int semestre, CancellationToken ct = default)
    {
        var p = await _db.Periodos.FirstOrDefaultAsync(x => x.Ano == ano && x.Semestre == semestre, ct);
        if (p is not null) return p;

        p = new PeriodoLetivo { Ano = ano, Semestre = semestre, Descricao = $"{semestre}º/{ano}" };
        await _db.Periodos.AddAsync(p, ct);
        await _db.SaveChangesAsync(ct);    
        return p;
    }
}

using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Data;
using SchoolETL.Core.Models;

namespace SchoolETL.Repositories.Disciplinas;

public class DisciplinaRepository : EfRepository<Disciplina>, IDisciplinaRepository
{
    public DisciplinaRepository(DwContext db) : base(db) { }
    public Task<Disciplina?> FindBySiglaAsync(string sigla, CancellationToken ct = default)
        => _db.Disciplinas.FirstOrDefaultAsync(d => d.Sigla == sigla, ct);
}

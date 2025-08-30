using Microsoft.EntityFrameworkCore;
using SchoolETL.Data;
using SchoolETL.Models;

namespace SchoolETL.Repositories.Cursos;

public class CursoRepository : EfRepository<Curso>, ICursoRepository
{
    public CursoRepository(DwContext db) : base(db) { }
    public Task<Curso?> FindBySiglaAsync(string sigla, CancellationToken ct = default)
        => _db.Cursos.FirstOrDefaultAsync(c => c.Sigla == sigla, ct);
}

using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Data;
using SchoolETL.Core.Models;

namespace SchoolETL.Repositories.Notas;

public class FatoNotaRepository : EfRepository<FatoNota>, IFatoNotaRepository
{
    public FatoNotaRepository(DwContext db) : base(db) { }

    public IQueryable<FatoNota> QueryByAluno(int alunoId, Guid? importId = null)
    {
        var q = _db.FatoNotas.AsNoTracking()
            .Include(f => f.Disciplina)
            .Include(f => f.Situacao)
            .Where(f => f.AlunoId == alunoId);

        if (importId is { } g && g != Guid.Empty)
            q = q.Where(f => f.ImportId == g);

        return q;
    }
}

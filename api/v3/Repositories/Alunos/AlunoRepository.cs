using Microsoft.EntityFrameworkCore;
using SchoolETL.Data;
using SchoolETL.Models;

namespace SchoolETL.Repositories.Alunos;

public class AlunoRepository : EfRepository<Aluno>, IAlunoRepository
{
    public AlunoRepository(DwContext db) : base(db) { }

    // Aqui consideramos que a normalização do nome foi feita fora (chave)
    public Task<Aluno?> FindByNomeAsync(string key, CancellationToken ct = default)
        => _db.Alunos.FirstOrDefaultAsync(a => a.Nome != null && a.Nome.ToUpper() == key, ct);

    public IQueryable<Aluno> QueryByImport(Guid importId)
        => _db.Alunos.Where(a => a.ImportId == importId
              || _db.FatoNotas.Any(f => f.ImportId == importId && f.AlunoId == a.Id))
            .AsNoTracking();
}

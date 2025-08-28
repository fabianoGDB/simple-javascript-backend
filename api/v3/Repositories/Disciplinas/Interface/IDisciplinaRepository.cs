using SchoolETL.Core.Models;

namespace SchoolETL.Repositories.Disciplinas;

public interface IDisciplinaRepository : IRepository<Disciplina>
{
    Task<Disciplina?> FindBySiglaAsync(string sigla, CancellationToken ct = default);
}

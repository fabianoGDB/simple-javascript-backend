using SchoolETL.Models;

namespace SchoolETL.Repositories.Cursos;

public interface ICursoRepository : IRepository<Curso>
{
    Task<Curso?> FindBySiglaAsync(string sigla, CancellationToken ct = default);
}

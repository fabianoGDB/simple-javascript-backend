using SchoolETL.Core.Models;

namespace SchoolETL.Repositories.Alunos;

public interface IAlunoRepository : IRepository<Aluno>
{
    Task<Aluno?> FindByNomeAsync(string nomeNormalizadoKey, CancellationToken ct = default);
    IQueryable<Aluno> QueryByImport(Guid importId);
}

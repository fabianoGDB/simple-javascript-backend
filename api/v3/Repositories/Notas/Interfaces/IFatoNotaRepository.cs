using SchoolETL.Core.Models;

namespace SchoolETL.Repositories.Notas;

public interface IFatoNotaRepository : IRepository<FatoNota>
{
    IQueryable<FatoNota> QueryByAluno(int alunoId, Guid? importId = null);
}

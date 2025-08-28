using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Data;
using SchoolETL.Core.Models;

namespace SchoolETL.Repositories.Dimensoes;

public interface IPeriodoLetivoRepository : IRepository<PeriodoLetivo>
{
    Task<PeriodoLetivo> GetOrCreateAsync(int ano, int semestre, CancellationToken ct = default);
}

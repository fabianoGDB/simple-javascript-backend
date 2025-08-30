using Microsoft.EntityFrameworkCore;
using SchoolETL.Data;
using SchoolETL.Models;

namespace SchoolETL.Repositories.Dimensoes;

public interface IPeriodoLetivoRepository : IRepository<PeriodoLetivo>
{
    Task<PeriodoLetivo> GetOrCreateAsync(int ano, int semestre, CancellationToken ct = default);
}

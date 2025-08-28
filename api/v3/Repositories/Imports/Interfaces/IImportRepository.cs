using SchoolETL.Core.Models;

namespace SchoolETL.Repositories.Imports;

public interface IImportRepository : IRepository<ImportBatch>
{
    Task<ImportBatch> CreateAsync(string? fileName, int? periodoLetivoId, CancellationToken ct = default);
    IQueryable<ImportBatch> ListWithPeriodo();
}

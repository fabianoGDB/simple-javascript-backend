using SchoolETL.Core.Models;

namespace SchoolETL.Repositories.Imports;

public interface IImportSheetRepository : IRepository<ImportSheet>
{
    Task<ImportSheet> AddAsync(Guid importId, string name, CancellationToken ct = default);
}

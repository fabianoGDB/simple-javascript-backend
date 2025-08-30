using SchoolETL.Enums;
using SchoolETL.Models;

namespace SchoolETL.Repositories.Imports;
public interface IImportRepository : IRepository<ImportBatch>
{
    Task<ImportBatch> CreateAsync(string? originalFileName, int? periodoLetivoId, CancellationToken ct = default);
    Task<ImportBatch> CreateWithIdAsync(Guid id, string? originalFileName, int? periodoLetivoId, CancellationToken ct = default);
    Task SetStatusAsync(Guid id, SpreadsheetsStatus status, string? error = null, CancellationToken ct = default);
}

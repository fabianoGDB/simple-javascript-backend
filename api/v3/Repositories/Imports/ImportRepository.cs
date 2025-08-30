using Microsoft.EntityFrameworkCore;
using SchoolETL.Data;
using SchoolETL.Enums;
using SchoolETL.Models;

namespace SchoolETL.Repositories.Imports;
public class ImportRepository : EfRepository<ImportBatch>, IImportRepository
{
    public ImportRepository(DwContext db) : base(db) { }

    public async Task<ImportBatch> CreateAsync(string? originalFileName, int? periodoLetivoId, CancellationToken ct = default)
    {
        var b = new ImportBatch { OriginalFileName = originalFileName, PeriodoLetivoId = periodoLetivoId, Status = SpreadsheetsStatus.Processando };
        await _set.AddAsync(b, ct);
        return b;
    }

    public async Task<ImportBatch> CreateWithIdAsync(Guid id, string? originalFileName, int? periodoLetivoId, CancellationToken ct = default)
    {
        var existing = await _set.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing != null) return existing;

        var b = new ImportBatch { Id = id, OriginalFileName = originalFileName, PeriodoLetivoId = periodoLetivoId, Status = SpreadsheetsStatus.Processando };
        await _set.AddAsync(b, ct);
        return b;
    }

    public async Task SetStatusAsync(Guid id, SpreadsheetsStatus status, string? error = null, CancellationToken ct = default)
    {
        var b = await _set.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return;
        b.Status = status; b.Error = error;
        await _db.SaveChangesAsync(ct);
    }
}

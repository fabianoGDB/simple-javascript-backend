using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Data;
using SchoolETL.Core.Models;

namespace SchoolETL.Repositories.Imports;

public class ImportRepository : EfRepository<ImportBatch>, IImportRepository
{
    public ImportRepository(DwContext db) : base(db) { }

    public async Task<ImportBatch> CreateAsync(string? fileName, int? periodoLetivoId, CancellationToken ct = default)
    {
        ImportBatch batch;
        if (periodoLetivoId is null)
            batch = new ImportBatch { FileName = fileName };
        else
            batch = new ImportBatch { FileName = fileName, PeriodoLetivoId = periodoLetivoId };

        await _set.AddAsync(batch, ct);
        return batch;
    }

    public IQueryable<ImportBatch> ListWithPeriodo() =>
        _db.Imports.AsNoTracking()
           .Include(i => i.PeriodoLetivo)
           .OrderByDescending(i => i.CreatedAtUtc);
}

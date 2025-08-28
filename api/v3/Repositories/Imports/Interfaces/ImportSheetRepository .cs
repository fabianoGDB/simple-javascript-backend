using SchoolETL.Core.Data;
using SchoolETL.Core.Models;

namespace SchoolETL.Repositories.Imports;

public class ImportSheetRepository : EfRepository<ImportSheet>, IImportSheetRepository
{
    public ImportSheetRepository(DwContext db) : base(db) { }

    public async Task<ImportSheet> AddAsync(Guid importId, string name, CancellationToken ct = default)
    {
        var s = new ImportSheet { ImportId = importId, Name = name };
        await _set.AddAsync(s, ct);
        return s;
    }
}

using SchoolETL.Data;
namespace SchoolETL.Repositories;
public class UnitOfWork : IUnitOfWork
{
    private readonly DwContext _db;
    public UnitOfWork(DwContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

using Microsoft.EntityFrameworkCore;
using SchoolETL.Data;

namespace SchoolETL.Repositories;
public class EfRepository<T> : IRepository<T> where T : class
{
    protected readonly DwContext _db;
    protected readonly DbSet<T> _set;
    public EfRepository(DwContext db) { _db = db; _set = db.Set<T>(); }
    public Task<T?> GetByIdAsync(object id, CancellationToken ct = default) => _set.FindAsync([id], ct).AsTask();
    public Task<List<T>> ListAsync(CancellationToken ct = default) => _set.AsNoTracking().ToListAsync(ct);
    public Task AddAsync(T entity, CancellationToken ct = default) => _set.AddAsync(entity, ct).AsTask();
    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) => _set.AddRangeAsync(entities, ct);
    public void Update(T entity) => _set.Update(entity);
    public void Remove(T entity) => _set.Remove(entity);
    public IQueryable<T> Query() => _set.AsQueryable();
}

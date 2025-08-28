using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Data;

namespace SchoolETL.Repositories;

public class EfRepository<T> : IRepository<T> where T : class
{
    protected readonly DwContext _db;
    protected readonly DbSet<T> _set;

    public EfRepository(DwContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => await _set.FindAsync([id], ct);

    public virtual Task<List<T>> ListAsync(CancellationToken ct = default)
        => _set.AsNoTracking().ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await _set.AddAsync(entity, ct);

    public virtual Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
        => _set.AddRangeAsync(entities, ct);

    public virtual void Update(T entity) => _set.Update(entity);

    public virtual void Remove(T entity) => _set.Remove(entity);

    public virtual IQueryable<T> Query() => _set.AsQueryable();
}

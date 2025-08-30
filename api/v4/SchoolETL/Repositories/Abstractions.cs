using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Data;
using SchoolETL.Core.Models;

namespace SchoolETL.Repositories;

public interface IRepository<T> where T : class
{
    Task<T> AddAsync(T entity);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class EfRepository<T> : IRepository<T> where T : class
{
    protected readonly DwContext _db;
    protected readonly DbSet<T> _set;
    public EfRepository(DwContext db) { _db = db; _set = db.Set<T>(); }
    public async Task<T> AddAsync(T entity) { await _set.AddAsync(entity); return entity; }
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken ct = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly DwContext _db;
    public UnitOfWork(DwContext db) => _db = db;
    public Task<int> CommitAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public interface IPeriodoLetivoRepository
{
    Task<PeriodoLetivo> GetOrCreateAsync(int ano, int semestre);
}

public class PeriodoLetivoRepository : IPeriodoLetivoRepository
{
    private readonly DwContext _db;
    public PeriodoLetivoRepository(DwContext db) => _db = db;
    public async Task<PeriodoLetivo> GetOrCreateAsync(int ano, int semestre)
    {
        var p = await _db.PeriodosLetivos.FirstOrDefaultAsync(x => x.Ano == ano && x.Semestre == semestre);
        if (p is not null) return p;
        p = new PeriodoLetivo { Ano = ano, Semestre = semestre };
        _db.PeriodosLetivos.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }
}


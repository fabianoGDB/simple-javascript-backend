using System.Collections.Concurrent;
using SchoolETL.Services.Interfaces;
using SchoolETL.Worker;

namespace SchoolETL.Services;

public class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<Guid, ImportJob> _jobs = new();
    public ImportJob? Get(Guid id) => _jobs.TryGetValue(id, out var j) ? j : null;
    public void Upsert(ImportJob job) => _jobs.AddOrUpdate(job.Id, job, (_, __) => job);
}

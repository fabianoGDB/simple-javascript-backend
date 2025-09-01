using SchoolETL.Worker.DTOs;

public class InMemoryJobStore : IJobStore
{
    private readonly Dictionary<Guid, ImportJob> _jobs = new();

    public void Create(ImportJob job) => _jobs[job.ImportId] = job;

    public ImportJob? Get(Guid id) => _jobs.TryGetValue(id, out var j) ? j : null;

    public void Update(ImportJob job) => _jobs[job.ImportId] = job;
}
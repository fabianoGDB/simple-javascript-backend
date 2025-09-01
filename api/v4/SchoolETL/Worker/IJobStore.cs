using SchoolETL.Worker.DTOs;

public interface IJobStore
{
    void Create(ImportJob job);
    ImportJob? Get(Guid id);
    void Update(ImportJob job);
}
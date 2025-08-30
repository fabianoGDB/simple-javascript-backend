using SchoolETL.Core.Models;

namespace SchoolETL.Services;

public interface IExcelEtlRunner
{
    Task RunAsync(ImportBatch import, CancellationToken ct);
}

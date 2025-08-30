using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SchoolETL.DTOs;

namespace SchoolETL.Services.Interfaces;

public interface IExcelEtlRunner
{
    Task<ImportSummary> RunAsync(string filePath, int ano, int semestre, CancellationToken ct = default);

}

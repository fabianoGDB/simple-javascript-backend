using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace SchoolETL.WorkerApi.DTOs;


public record ImportStatusDto(Guid JobId, string Status, ImportSummary? Summary, string? Error);
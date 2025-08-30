using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace SchoolETL.DTOs;


public record ImportStatusDto(Guid JobId, string Status, ImportSummaryDto? Summary, string? Error);

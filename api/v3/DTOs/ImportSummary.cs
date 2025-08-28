using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolETL.WorkerApi.DTOs;

public record ImportSummary(Guid ImportId, int AlunosInseridos, int DisciplinasInseridas, int NotasInseridas, int LinhasIgnoradas, List<string> Avisos);
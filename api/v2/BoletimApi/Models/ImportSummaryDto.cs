using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoletimApi.Models
{
    public record ImportSummaryDto(Guid ImportId, int AlunosInseridos, int DisciplinasInseridas, int NotasInseridas, int LinhasIgnoradas, List<string> Avisos);

}
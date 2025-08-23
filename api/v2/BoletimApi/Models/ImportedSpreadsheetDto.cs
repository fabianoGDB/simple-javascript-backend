using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoletimApi.Models
{
    public record ImportedSpreadsheetDto(int Id, string ClassName, int Year, DateTime ImportedAt, int StudentCount);

}
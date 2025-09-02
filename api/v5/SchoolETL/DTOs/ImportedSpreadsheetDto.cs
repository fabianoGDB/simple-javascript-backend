using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolETL.DTOs
{
    public sealed class ImportedSpreadsheetDto
    {
        public Guid Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public int Status { get; set; }
        public string? Error { get; set; }
        public int Alunos { get; set; }
    }
}
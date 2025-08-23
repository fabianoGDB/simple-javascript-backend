using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoletimApi.Models
{
    public class StudentReportDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? PhotoUrl { get; set; }
        public double Frequency { get; set; }
        public Dictionary<string, Dictionary<string, List<double?>>> Grades { get; set; } = new();
        public List<BimesterSummaryDto> Summary { get; set; } = new();
        public StudentStatusDto Status { get; set; } = new();
        public StudentRectificationDto Rectification { get; set; } = new();
    }
}
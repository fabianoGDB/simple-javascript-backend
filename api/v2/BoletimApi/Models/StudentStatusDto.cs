using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoletimApi.Models
{
    public class StudentStatusDto
    {
        public string? FinalSituation { get; set; }
        public List<string> AreasInCouncil { get; set; } = new();
    }
}
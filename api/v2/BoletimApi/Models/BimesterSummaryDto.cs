using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoletimApi.Models
{
    public class BimesterSummaryDto
    {
        public string Bim { get; set; } = "";
        public int? Areas { get; set; }
        public int? Disciplines { get; set; }
    }
}
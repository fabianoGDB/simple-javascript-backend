using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoletimApi.Models
{
    public class StudentRectificationDto
    {
        public string? FrequencyRectified { get; set; }
        public string? Result { get; set; }
        public string? PeriodToRectify { get; set; }
    }
}
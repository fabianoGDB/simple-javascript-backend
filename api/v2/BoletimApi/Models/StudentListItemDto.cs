using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoletimApi.Models
{
    public record StudentListItemDto(int Id, string Name, string? PhotoUrl, double? Frequency, string? Status, DateTime CreatedAt);

}
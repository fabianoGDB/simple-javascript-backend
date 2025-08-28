using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Data;
using SchoolETL.Core.Models;

namespace SchoolETL.Repositories.Dimensoes;

public interface ISituacaoRepository : IRepository<Situacao>
{
    Task<int?> TryResolveIdAsync(string? sigla, CancellationToken ct = default);
}
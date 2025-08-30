using Microsoft.EntityFrameworkCore;
using SchoolETL.Data;
using SchoolETL.Models;

namespace SchoolETL.Repositories.Dimensoes;

public interface ISituacaoRepository : IRepository<Situacao>
{
    Task<int?> TryResolveIdAsync(string? sigla, CancellationToken ct = default);
}
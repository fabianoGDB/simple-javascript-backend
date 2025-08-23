using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoletimApi.Services
{
    public interface ILocalStorageService
    {
        Task<string> SaveFile(IFormFile file);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoletimApi.Services
{
    public class LocalStorageService : ILocalStorageService
    {
        public async Task<string> SaveFile(IFormFile file)
        {
            var tempPath = Path.GetTempPath();

            var fileToSavePath = Path.Combine(tempPath, file.FileName);
            using (var fileStream = System.IO.File.Create(fileToSavePath))
            {
                await fileStream.CopyToAsync(fileStream);
            }

            return fileToSavePath;
        }
    }
}
using ImportadorNotasApp.DTOs;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Json;

namespace ImportadorNotasApp.Services
{
    public class ImportsService
    {
        private readonly HttpClient httpClient;

        public ImportsService(IHttpClientFactory httpClientFactory)
        {
            httpClient = httpClientFactory.CreateClient(nameof(ImportsService));
        }

        public async Task<List<ImportedSpreadsheetDto>> GetImports()
        {
            try
            {
                var response = await httpClient.GetAsync("api/imports");
                response.EnsureSuccessStatusCode();
                var importedSpreadsheet = await response.Content.ReadFromJsonAsync<List<ImportedSpreadsheetDto>>();

                return importedSpreadsheet ?? [];
            }
            catch (Exception ex)
            {
                // log ou tratamento
                throw new ApplicationException("Erro ao carregar planilhas.", ex);
            }
        }
    }
}

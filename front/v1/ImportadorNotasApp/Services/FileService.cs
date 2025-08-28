using ImportadorNotasApp.DTOs;
using Microsoft.AspNetCore.Components.Forms;
using System.Net;

namespace ImportadorNotasApp.Services
{
    public class FileService
    {
        private readonly HttpClient _http;

        public FileService(HttpClient http) => _http = http;

        public async Task<ImportResponse> UploadFile(IBrowserFile file, CancellationToken ct = default)
        {
            if (file is null)
            return new ImportResponse((int)HttpStatusCode.BadRequest, "Nenhum arquivo selecionado.");


            const long MaxSize = 20 * 1024 * 1024; // 20MB

            using var stream = file.OpenReadStream(MaxSize, ct);
            using var form = new MultipartFormDataContent();
            form.Add(new StreamContent(stream), "file", file.Name);

            try
            {
                using var resp = await _http.PostAsync("api/imports", form, ct);

                if (resp.StatusCode == HttpStatusCode.Accepted)
                {
                    // 202 -> job enfileirado
                    return new ImportResponse((int)HttpStatusCode.Accepted,
                        "Processo iniciado. Aguarde o processamento.");
                }

                if (resp.IsSuccessStatusCode)
                {
                    // 200 -> processado síncrono
                    return new ImportResponse((int)HttpStatusCode.OK,
                        "Planilha importada com sucesso!");
                }

                // tenta extrair mensagem do corpo (problem details, etc.)
                var body = await resp.Content.ReadAsStringAsync(ct);
                var msg = string.IsNullOrWhiteSpace(body)
                    ? $"Falha na importação"
                    : $"Falha na importação";

                return new ImportResponse((int)resp.StatusCode, msg);
            }
            catch (OperationCanceledException)
            {
                return new ImportResponse((int)HttpStatusCode.RequestTimeout, "Envio cancelado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new ImportResponse((int)HttpStatusCode.InternalServerError, $"Erro no servidor");
            }
        }
    }
}

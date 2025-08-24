using Microsoft.AspNetCore.Components.Forms;

namespace ImportadorNotasApp.Services
{
    public class FileService
    {
        private readonly HttpClient httpClient;

        public FileService(IHttpClientFactory httpClientFactory)
        {
            httpClient = httpClientFactory.CreateClient(nameof(FileService));
        }

        public async Task<string> UploadFile(IBrowserFile browserFile)
        {
            byte[] bytes;
            using (Stream stream = browserFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024))
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memoryStream);
                    bytes = memoryStream.ToArray();
                }
            }

            var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(bytes), "file", browserFile.Name);

            try
            {
                var response = await httpClient.PostAsync("api/imports", form);
                response.EnsureSuccessStatusCode();
                var filePath = await response.Content.ReadAsStringAsync();

                return $"File uploaded successfully. File path: {filePath}";
            }
            catch (Exception ex)
            {

                return ex.Message;
            }
        }
    }
}

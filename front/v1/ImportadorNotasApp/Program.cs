using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ImportadorNotasApp;
using ImportadorNotasApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var uri = new Uri("http://localhost:5155");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = uri });



builder.Services.AddHttpClient(nameof(FileService), client =>
{
    client.BaseAddress = uri;
});

builder.Services.AddHttpClient(nameof(ImportsService), client =>
{
    client.BaseAddress = uri;
});

builder.Services.AddScoped<FileService>();

builder.Services.AddScoped<ImportsService>();

await builder.Build().RunAsync();

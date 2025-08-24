using BoletimApi.Models;
using BoletimApi.Services;
using Microsoft.AspNetCore.Mvc;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();

var app = builder.Build();

app.UseCors();

app.MapPost("/api/imports", async ([FromForm] IFormFile file, ILocalStorageService service) =>
{
    var fileSavedPath = await service.SaveFile(file);
    return fileSavedPath;
})
.Accepts<IFormFile>("multipart/form-data")
.WithName("UploadPlanilha");


app.MapGet("/api", () =>
{
    return Results.Ok("Online");
});

app.MapGet("/api/imports/{id}/students", ([FromRoute] int id) =>
{
    var students = new List<StudentListItemDto>
    {
        new(1, "JOÃO SILVA SANTOS", "https://via.placeholder.com/110x140", 89.23, "Conselho", DateTime.Now.AddDays(-3)),
        new(2, "MARIA OLIVEIRA COSTA", "https://via.placeholder.com/110x140", 95.00, "Aprovado", DateTime.Now.AddDays(-2)),
        new(3, "VITORIA BRITO MOTA", "https://via.placeholder.com/110x140", 94.67, "Aprovado", DateTime.Now.AddDays(-1))
    };
    return Results.Ok(students);
});

app.MapGet("/api/students/{id}", ([FromRoute] int id) =>
{
    var report = new StudentReportDto
    {
        Id = id,
        Name = "JOÃO SILVA SANTOS",
        PhotoUrl = "https://via.placeholder.com/110x140",
        Frequency = 89.23,
        Grades = new()
        {
            ["C. HUMANAS"] = new()
            {
                ["FIL"] = new() { 8.5, 9.0, 7.5, 8.8 },
                ["IHEG"] = new() { 7.8, 8.5, 9.0, 8.2 },
                ["Area"] = new() { 8.15, 8.75, 8.25, 8.5 }
            },
            ["Ciências da Natureza"] = new()
            {
                ["IBEQ"] = new() { 7.5, 7.8, 8.8, 9.1 },
                ["FIS"] = new() { 8.0, 8.0, 8.5, 8.9 },
                ["Area"] = new() { 7.75, 8.0, 8.65, 9.0 }
            },
            ["Linguagens"] = new()
            {
                ["ART"] = new() { 9.0, 8.8, 9.2, 9.5 },
                ["EFI"] = new() { 8.5, 9.0, 9.0, 9.2 },
                ["ING"] = new() { 7.8, 8.1, 8.6, 8.8 },
                ["LPR"] = new() { 8.2, 8.5, 8.9, 9.1 },
                ["Area"] = new() { 8.38, 8.58, 8.93, 9.15 }
            },
            ["MAT"] = new()
            {
                ["ALPO"] = new() { 7.5, 8.0, 8.6, 9.0 },
                ["Area"] = new() { 8.25, 8.58, 8.78, 9.15 }
            },
            ["D. Técnicas"] = new()
            {
                ["DWS1"] = new() { null, 9.0, 9.2, 8.8 },
                ["GBD1"] = new() { null, 8.5, 8.9, 9.2 },
                ["SOP"] = new() { 8.5, 9.0, 9.2, 8.8 },
                ["IHD"] = new() { 8.8, 8.5, 9.1, 9.0 },
                ["MAAC"] = new() { 9.2, 9.5, 9.8, 9.6 },
                ["Area"] = new() { 8.83, 9.0, 9.37, 9.13 }
            }
        },
        Summary = new()
        {
            new() { Bim = "1º", Areas = 5, Disciplines = 15 },
            new() { Bim = "2º", Areas = 5, Disciplines = 15 },
            new() { Bim = "3º", Areas = 5, Disciplines = 15 },
            new() { Bim = "4º", Areas = 5, Disciplines = 14 },
            new() { Bim = "Rec", Areas = null, Disciplines = null },
            new() { Bim = "Ano", Areas = 5, Disciplines = 15 }
        },
        Status = new()
        {
            FinalSituation = "Conselho",
            AreasInCouncil = new() { "MAT", "C. HUMANAS" }
        },
        Rectification = new()
        {
            FrequencyRectified = "",
            Result = "",
            PeriodToRectify = ""
        }
    };

    return Results.Ok(report);
});



app.Run();

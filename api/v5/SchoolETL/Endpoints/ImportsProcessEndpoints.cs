using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SchoolETL.Worker;


namespace SchoolETL.Endpoints;

public static class ImportsProcessEndpoints
{
    public static IEndpointRouteBuilder MapImportsProcessEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/imports/{id:guid}/process", (Guid id, IBackgroundJobQueue queue) =>
        {
            queue.Enqueue(new DispatchJob(id));
            return Results.Accepted($"/api/imports/{id}/status", new { jobId = id });
        }).WithOpenApi();
        ;

        return app;
    }
}

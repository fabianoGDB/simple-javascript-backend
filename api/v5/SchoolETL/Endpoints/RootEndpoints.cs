using Microsoft.AspNetCore.Routing;

namespace SchoolETL.Endpoints;

public static class RootEndpoints
{
    public static IEndpointRouteBuilder MapRootEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Redirect("/swagger")).WithOpenApi();
        ;
        return app;
    }
}


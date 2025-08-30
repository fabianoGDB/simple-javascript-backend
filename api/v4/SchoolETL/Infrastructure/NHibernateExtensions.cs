using Microsoft.Extensions.DependencyInjection;
using NHibernate;

namespace SchoolETL.Infrastructure;

public static class NHibernateExtensions
{
    public static IServiceCollection AddNHibernate(this IServiceCollection services, string connectionString)
    {
        var factory = NHibernateConfig.BuildSessionFactory(connectionString);
        services.AddSingleton(factory);
        services.AddScoped(provider => provider.GetRequiredService<ISessionFactory>().OpenSession());
        return services;
    }
}


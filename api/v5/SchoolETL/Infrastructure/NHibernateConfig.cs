using System.Reflection;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using NHibernate;
using NHibernate.Tool.hbm2ddl;
using SchoolETL.Infrastructure.Mappings; // << para enxergar ImportBatchMap

public static class NHibernateConfig
{
    public static ISessionFactory BuildSessionFactory(string connectionString, bool createSchema = false)
    {
        var db = PostgreSQLConfiguration.PostgreSQL82
            .ConnectionString(connectionString)
            .AdoNetBatchSize(100)
            .ShowSql();

        return Fluently.Configure()
            .Database(db)
            .Mappings(m =>
            {
                // varre a assembly onde estão os mapeamentos FluentNHibernate
                m.FluentMappings.AddFromAssemblyOf<ImportBatchMap>();
                // se tiver mais assemblies, pode encadear AddFromAssembly(...)
                // m.FluentMappings.AddFromAssembly(typeof(OutraMap).Assembly);
            })
            .ExposeConfiguration(cfg =>
            {
                cfg.SetProperty(NHibernate.Cfg.Environment.CommandTimeout, "60");
                if (createSchema)
                {
                    // cuidado: Create(true) derruba e recria tudo
                    new SchemaExport(cfg).Create(false, true);
                }
            })
            .BuildSessionFactory();
    }
}

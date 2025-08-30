using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using NHibernate;
using NHibernate.Tool.hbm2ddl;
using SchoolETL.Infrastructure.Mappings;

namespace SchoolETL.Infrastructure;

public static class NHibernateConfig
{
    public static ISessionFactory BuildSessionFactory(string connectionString, bool createSchema = false)
    {
        var dbConfig = PostgreSQLConfiguration.PostgreSQL82
            .ConnectionString(connectionString)
            .AdoNetBatchSize(50)
            .ShowSql();

        return Fluently.Configure()
         .Database(dbConfig)
         .Mappings(m => m.FluentMappings.AddFromAssemblyOf<PeriodoLetivoMap>())
         .ExposeConfiguration(cfg =>
         {
             cfg.SetProperty(NHibernate.Cfg.Environment.CommandTimeout, "60");
             if (createSchema)
             {
                 new SchemaExport(cfg).Create(false, true);
             }
         })
         .BuildSessionFactory();
    }
}


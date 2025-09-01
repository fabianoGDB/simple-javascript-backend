using System.Text;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using NHibernate;
using NHibernate.Tool.hbm2ddl;
using SchoolETL.Infrastructure.Mappings; // <— garanta este using!

namespace SchoolETL.Infrastructure;

public static class NHibernateConfig
{
    public static ISessionFactory BuildSessionFactory(string connectionString, bool createSchema = false)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string de Postgres está vazia/nula.");

        var dbConfig = PostgreSQLConfiguration.PostgreSQL82
            .ConnectionString(connectionString)
            .Dialect<NHibernate.Dialect.PostgreSQL82Dialect>()   // reforça dialeto
            .Driver<NHibernate.Driver.NpgsqlDriver>()             // reforça driver
            .AdoNetBatchSize(50)
            .ShowSql();

        try
        {
            return Fluently.Configure()
                .Database(dbConfig)
                // IMPORTANTE: aponte para UMA classe que esteja no mesmo assembly de TODOS os mappings
                .Mappings(m => m.FluentMappings.AddFromAssemblyOf<PeriodoLetivoMap>())
                .ExposeConfiguration(cfg =>
                {
                    // timeouts etc
                    cfg.SetProperty(NHibernate.Cfg.Environment.CommandTimeout, "60");

                    // se quiser validar o schema em tempo de boot (útil para diagnosticar):
                    // new SchemaValidator(cfg).Validate();

                    if (createSchema)
                    {
                        // CUIDADO: isso cria/derruba objetos. Use apenas em dev.
                        new SchemaExport(cfg).Create(useStdOut: false, execute: true);
                    }
                })
                .BuildSessionFactory();
        }
        catch (FluentNHibernate.Cfg.FluentConfigurationException fex)
        {
            // Mostra motivos “humanos” + inner exception
            var sb = new StringBuilder();
            sb.AppendLine("Falha ao construir o NHibernate SessionFactory.");
            if (fex.PotentialReasons?.Count > 0)
            {
                sb.AppendLine("Possíveis motivos:");
                foreach (var r in fex.PotentialReasons) sb.AppendLine($" - {r}");
            }
            if (fex.InnerException is not null)
            {
                sb.AppendLine("InnerException:");
                sb.AppendLine(fex.InnerException.ToString());
            }
            throw new InvalidOperationException(sb.ToString(), fex);
        }
    }
}

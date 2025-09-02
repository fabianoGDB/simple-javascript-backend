using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;

namespace SchoolETL.Infrastructure.Mappings;

public class ImportStageMap : ClassMap<ImportStage>
{
    public ImportStageMap()
    {
        Table("import_stage");

        Id(x => x.Id).GeneratedBy.Identity();

        Map(x => x.ImportId).Column("import_id").Not.Nullable();
        Map(x => x.EtapaId).Column("etapa_id").Nullable();
        Map(x => x.Name).Column("name").Length(120).Not.Nullable();

        Map(x => x.Status).Column("status").Not.Nullable();
        Map(x => x.StartedAtUtc).Column("started_at_utc").Nullable();
        Map(x => x.FinishedAtUtc).Column("finished_at_utc").Nullable();
        Map(x => x.UpdatedAtUtc).Column("updated_at_utc").Nullable();
        Map(x => x.ProcessedRows).Column("processed_rows").Nullable();
        Map(x => x.Error).Column("error").Nullable().CustomSqlType("text");

        DynamicUpdate();
    }
}

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
        Map(x => x.EtapaId).Column("etapa_id");
        Map(x => x.Name).Not.Nullable();
        Map(x => x.Status).Not.Nullable();
        Map(x => x.Error);
        Map(x => x.ProcessedRows).Column("processed_rows");
        Map(x => x.StartedAtUtc).Column("started_at_utc");
        Map(x => x.FinishedAtUtc).Column("finished_at_utc");
        Map(x => x.UpdatedAtUtc).Column("updated_at_utc");
        Map(x => x.SourcePath).Column("source_path");
    }
}

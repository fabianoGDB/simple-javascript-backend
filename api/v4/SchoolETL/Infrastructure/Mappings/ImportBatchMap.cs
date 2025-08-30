using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;


namespace SchoolETL.Infrastructure.Mappings;


public class ImportBatchMap : ClassMap<ImportBatch>
{
    public ImportBatchMap()
    {
        Table("import_batch");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.CreatedAtUtc).Column("created_at_utc").Not.Nullable();
        Map(x => x.OriginalFileName).Column("original_file_name");
        Map(x => x.StorageUri).Column("storage_uri");
        Map(x => x.Status).Not.Nullable();
        Map(x => x.Error);
        Map(x => x.FileHash).Column("file_hash");
        Map(x => x.PeriodoLetivoId).Column("periodo_letivo_id");
    }
}
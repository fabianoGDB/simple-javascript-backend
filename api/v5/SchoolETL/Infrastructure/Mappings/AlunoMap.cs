using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;

namespace SchoolETL.Infrastructure.Mappings;

public class AlunoMap : ClassMap<Aluno>
{
    public AlunoMap()
    {
        Table("aluno");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.ImportId).CustomType<Guid?>().Column("import_id");
        Map(x => x.Nome).Not.Nullable();
        Map(x => x.Matricula).Nullable();
        Map(x => x.FotoPath).Column("foto_path").Nullable();
        DynamicUpdate();
    }
}

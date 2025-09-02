using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;

namespace SchoolETL.Infrastructure.Mappings;

public class AlunoObservacaoMap : ClassMap<AlunoObservacao>
{
    public AlunoObservacaoMap()
    {
        Table("aluno_observacao");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.AlunoId).Not.Nullable().Column("aluno_id");
        Map(x => x.Texto).Not.Nullable();
        Map(x => x.CriadoEmUtc).Not.Nullable().Column("criado_em_utc");
        Map(x => x.ImportId).CustomType<Guid?>().Column("import_id");
    }
}

using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;


namespace SchoolETL.Infrastructure.Mappings;


public class AlunoObservacaoMap : ClassMap<AlunoObservacao>
{
    public AlunoObservacaoMap()
    {
        Table("aluno_observacao");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.AlunoId).Not.Nullable();
        Map(x => x.Texto).Not.Nullable();
        Map(x => x.CriadoEmUtc).Column("criado_em_utc").Not.Nullable();
        Map(x => x.ImportId).Column("import_id");
    }
}
using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;

public class AreaConhecimentoMap : ClassMap<AreaConhecimento>
{
    public AreaConhecimentoMap()
    {
        Table("area_conhecimento");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.Nome).Column("nome").Not.Nullable();
        Map(x => x.CorHex).Column("corhex").Nullable().Length(16);
        Map(x => x.Ordem).Column("ordem").Nullable();
        Map(x => x.Ativo).Column("ativo").Not.Nullable();
    }
}

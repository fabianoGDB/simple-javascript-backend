using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;

namespace SchoolETL.Infrastructure.Mappings;

public class SituacaoMap : ClassMap<Situacao>
{
    public SituacaoMap()
    {
        Table("situacao");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.Descricao).Not.Nullable();
    }
}

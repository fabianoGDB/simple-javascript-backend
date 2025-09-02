using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;

namespace SchoolETL.Infrastructure.Mappings;

public class PeriodoAvaliativoMap : ClassMap<PeriodoAvaliativo>
{
    public PeriodoAvaliativoMap()
    {
        Table("periodo_avaliativo");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.Nome).Not.Nullable();
        Map(x => x.Final).Not.Nullable();
    }
}

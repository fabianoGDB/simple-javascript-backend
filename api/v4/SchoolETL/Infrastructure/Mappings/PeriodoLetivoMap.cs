using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;

namespace SchoolETL.Infrastructure.Mappings;

public class PeriodoLetivoMap : ClassMap<PeriodoLetivo>
{
    public PeriodoLetivoMap()
    {
        Table("periodo_letivo");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.Ano).Not.Nullable();
        Map(x => x.Semestre).Not.Nullable();
        Map(x => x.Descricao);
        DynamicUpdate();
    }
}

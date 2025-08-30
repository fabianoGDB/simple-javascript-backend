using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;


namespace SchoolETL.Infrastructure.Mappings;


public class DisciplinaMap : ClassMap<Disciplina>
{
    public DisciplinaMap()
    {
        Table("disciplina");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.ImportId).Column("import_id");
        Map(x => x.Sigla).Not.Nullable().Unique();
        Map(x => x.NomeArea).Column("nome_area");
        Map(x => x.CargaHorariaRotulo).Column("carga_horaria_rotulo");
    }
}
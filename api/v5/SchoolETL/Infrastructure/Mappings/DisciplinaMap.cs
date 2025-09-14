using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;

namespace SchoolETL.Infrastructure.Mappings;

public class DisciplinaMap : ClassMap<Disciplina>
{
    public DisciplinaMap()
    {
        Table("disciplina");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.ImportId).Column("import_id").Nullable();
        Map(x => x.Nome).Column("nome").Not.Nullable();
        Map(x => x.Sigla).Column("sigla").Not.Nullable().Unique();
        Map(x => x.AreaId).Column("area_id").Nullable();               // <<-- AQUI É O PONTO
        Map(x => x.NomeArea).Column("nome_area").Nullable();             // (legado, se ainda existir)
        Map(x => x.CargaHorariaRotulo).Column("carga_horaria_rotulo").Nullable();

    }
}

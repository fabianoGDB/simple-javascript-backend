using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;

namespace SchoolETL.Infrastructure.Mappings;

public class FatoNotaMap : ClassMap<FatoNota>
{
    public FatoNotaMap()
    {
        Table("fato_nota");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.ImportId).Not.Nullable().Column("import_id");
        Map(x => x.AlunoId).Not.Nullable().Column("aluno_id");
        Map(x => x.DisciplinaId).Not.Nullable().Column("disciplina_id");
        Map(x => x.PeriodoAvaliativoId).Not.Nullable().Column("periodo_avaliativo_id");
        Map(x => x.SituacaoId).Nullable().Column("situacao_id");
        Map(x => x.PeriodoLetivoId).Not.Nullable().Column("periodo_letivo_id");
        Map(x => x.Nota).Nullable();
        Map(x => x.Frequencia).Nullable();
        References(x => x.Disciplina).Column("disciplina_id").Not.Insert().Not.Update();
        References(x => x.Situacao).Column("situacao_id").Not.Insert().Not.Update();
        DynamicUpdate();
    }
}

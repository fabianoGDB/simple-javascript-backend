using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;

namespace SchoolETL.Infrastructure.Mappings;

public class FatoNotaMap : ClassMap<FatoNota>
{
    public FatoNotaMap()
    {
        Table("fato_nota");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.ImportId).Column("import_id").Not.Nullable();
        Map(x => x.AlunoId).Column("aluno_id").Not.Nullable();
        Map(x => x.DisciplinaId).Column("disciplina_id").Not.Nullable();
        Map(x => x.PeriodoAvaliativoId).Column("periodo_avaliativo_id").Not.Nullable();
        Map(x => x.SituacaoId).Column("situacao_id");
        Map(x => x.PeriodoLetivoId).Column("periodo_letivo_id").Not.Nullable();
        Map(x => x.Nota).Column("nota");
        Map(x => x.Frequencia).Column("frequencia");
        References(x => x.Disciplina).Column("disciplina_id").Not.Insert().Not.Update();
        References(x => x.Situacao).Column("situacao_id").Not.Insert().Not.Update();
    }
}

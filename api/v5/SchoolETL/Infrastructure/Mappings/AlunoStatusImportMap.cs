using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;

namespace SchoolETL.Infrastructure.Mappings;

public class AlunoStatusImportMap : ClassMap<AlunoStatusImport>
{
    public AlunoStatusImportMap()
    {
        Table("aluno_status_import");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.ImportId).Not.Nullable().Column("import_id");
        Map(x => x.AlunoId).Not.Nullable().Column("aluno_id");
        Map(x => x.PeriodoLetivoId).Column("periodo_letivo_id");
        Map(x => x.FrequenciaGeral).Column("frequencia_geral");
        Map(x => x.SituacaoCurso).Column("situacao_curso");
        Map(x => x.CriadoEmUtc).Not.Nullable().Column("criado_em_utc");
        DynamicUpdate();
    }
}

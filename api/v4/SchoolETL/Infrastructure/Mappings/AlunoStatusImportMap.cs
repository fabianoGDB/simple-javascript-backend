using FluentNHibernate.Mapping;
using SchoolETL.Core.Models;


namespace SchoolETL.Infrastructure.Mappings;


public class AlunoStatusImportMap : ClassMap<AlunoStatusImport>
{
    public AlunoStatusImportMap()
    {
        Table("aluno_status_import");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.ImportId).Column("import_id").Not.Nullable();
        Map(x => x.AlunoId).Column("aluno_id").Not.Nullable();
        Map(x => x.PeriodoLetivoId).Column("periodo_letivo_id");
        Map(x => x.FrequenciaGeral).Column("frequencia_geral");
        Map(x => x.SituacaoCurso).Column("situacao_curso");
        Map(x => x.CriadoEmUtc).Column("criado_em_utc").Not.Nullable();
    }
}
using Microsoft.EntityFrameworkCore;
using SchoolETL.ConsoleApp.Models;

public class DwContext : DbContext
{
    public DwContext(DbContextOptions<DwContext> options) : base(options) { }

    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<AlunoObservacao> AlunoObservacoes => Set<AlunoObservacao>();
    public DbSet<Bimestre> Bimestres => Set<Bimestre>();
    public DbSet<Curso> Cursos => Set<Curso>();
    public DbSet<Disciplina> Disciplinas => Set<Disciplina>();
    public DbSet<Etapa> Etapas => Set<Etapa>();
    public DbSet<FatoNota> FatoNotas => Set<FatoNota>();
    public DbSet<ImportBatch> Imports => Set<ImportBatch>();
    public DbSet<ImportSheet> ImportSheets => Set<ImportSheet>();
    public DbSet<PeriodoLetivo> Periodos => Set<PeriodoLetivo>();
    public DbSet<Situacao> Situacoes => Set<Situacao>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Bimestre>().HasData(
            new Bimestre { Id = 1, Nome = "1º" },
            new Bimestre { Id = 2, Nome = "2º" },
            new Bimestre { Id = 3, Nome = "3º" },
            new Bimestre { Id = 4, Nome = "4º" }
        );

        mb.Entity<Etapa>().HasData(
            new Etapa { Id = 1, Nome = "Etapa 1" },
            new Etapa { Id = 2, Nome = "Etapa 2" },
            new Etapa { Id = 3, Nome = "Etapa 3" },
            new Etapa { Id = 4, Nome = "Etapa 4" },
            new Etapa { Id = 99, Nome = "Etapa Final" }
        );

        mb.Entity<Situacao>().HasData(
            new Situacao { Id = 1, Descricao = "APR" },
            new Situacao { Id = 2, Descricao = "REP" },
            new Situacao { Id = 3, Descricao = "CAN" },
            new Situacao { Id = 4, Descricao = "CUR" },
            new Situacao { Id = 5, Descricao = "OUT" }
        );

        mb.Entity<Aluno>(e =>
        {
            e.Property(p => p.Nome).HasMaxLength(255);
            e.Property(p => p.Matricula).HasMaxLength(50);
            e.HasMany(p => p.Observacoes)
             .WithOne(o => o.Aluno!)
             .HasForeignKey(o => o.AlunoId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Disciplina>(e =>
        {
            e.Property(p => p.Sigla).HasMaxLength(50).IsRequired();
            e.HasIndex(p => p.Sigla).IsUnique();
        });

        mb.Entity<Curso>(e =>
        {
            e.Property(p => p.Sigla).HasMaxLength(50).IsRequired();
            e.HasIndex(p => p.Sigla).IsUnique();
        });

        mb.Entity<FatoNota>(e =>
        {
            e.HasIndex(i => i.ImportId);
            e.HasIndex(i => i.AlunoId);
            e.HasIndex(i => i.DisciplinaId);
            e.HasIndex(i => i.BimestreId);
            e.HasIndex(i => i.EtapaId);
            e.HasIndex(i => i.SituacaoId);
        });

        mb.Entity<ImportBatch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
        });

        mb.Entity<ImportSheet>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            e.HasIndex(x => new { x.ImportId, x.Name }).IsUnique();
        });
    }
}

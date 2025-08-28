// Data/DwContext.cs
using Microsoft.EntityFrameworkCore;
using SchoolETL.Core.Models;
using System.Reflection.Emit;

namespace SchoolETL.Core.Data;

public class DwContext : DbContext
{
    public DwContext(DbContextOptions<DwContext> options) : base(options) { }

    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<AlunoObservacao> AlunoObservacoes => Set<AlunoObservacao>();
    public DbSet<Curso> Cursos => Set<Curso>();
    public DbSet<Disciplina> Disciplinas => Set<Disciplina>();
    public DbSet<Bimestre> Bimestres => Set<Bimestre>();
    public DbSet<Etapa> Etapas => Set<Etapa>();
    public DbSet<Situacao> Situacoes => Set<Situacao>();
    public DbSet<PeriodoLetivo> Periodos => Set<PeriodoLetivo>();
    public DbSet<FatoNota> FatoNotas => Set<FatoNota>();
    public DbSet<ImportBatch> Imports => Set<ImportBatch>();
    public DbSet<ImportSheet> ImportSheets => Set<ImportSheet>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        foreach (var entity in mb.Model.GetEntityTypes())
        {
            // Tabela
            entity.SetTableName(ToSnakeCase(entity.GetTableName()!));

            // Colunas
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.GetColumnName()!));
        }
        // ===== Aluno =====
        mb.Entity<Aluno>(e =>
        {
            e.Property(p => p.Nome).HasMaxLength(255);
            e.Property(p => p.Matricula).HasMaxLength(50);
            e.HasIndex(p => p.ImportId);
            e.HasIndex(p => p.Matricula);
            // opcional: índice trigram em SQL
            e.HasMany(p => p.Observacoes)
             .WithOne(o => o.Aluno!)
             .HasForeignKey(o => o.AlunoId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Curso =====
        mb.Entity<Curso>(e =>
        {
            e.Property(p => p.Sigla).HasMaxLength(50).IsRequired();
            e.HasIndex(p => p.Sigla).IsUnique();
            e.HasIndex(p => p.ImportId);
        });

        // ===== Disciplina =====
        mb.Entity<Disciplina>(e =>
        {
            e.Property(p => p.Sigla).HasMaxLength(50).IsRequired();
            e.HasIndex(p => p.Sigla).IsUnique();
            e.HasIndex(p => p.ImportId);
        });

        // ===== FatoNota =====
        mb.Entity<FatoNota>(e =>
        {
            e.HasIndex(i => i.ImportId);
            e.HasIndex(i => i.AlunoId);
            e.HasIndex(i => i.DisciplinaId);
            e.HasIndex(i => i.BimestreId);
            e.HasIndex(i => i.EtapaId);
            e.HasIndex(i => i.SituacaoId);
            e.HasIndex(i => i.PeriodoLetivoId);

            // relações (FKs já detectadas por convenção, mas deixo explícito)
            e.HasOne(f => f.Aluno).WithMany(a => a.Notas).HasForeignKey(f => f.AlunoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Disciplina).WithMany(d => d.Notas).HasForeignKey(f => f.DisciplinaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Bimestre).WithMany(b => b.Notas).HasForeignKey(f => f.BimestreId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Etapa).WithMany(et => et.Notas).HasForeignKey(f => f.EtapaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Curso).WithMany(c => c.Notas).HasForeignKey(f => f.CursoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(f => f.Situacao).WithMany(s => s.Notas).HasForeignKey(f => f.SituacaoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(f => f.PeriodoLetivo).WithMany(p => p.Notas).HasForeignKey(f => f.PeriodoLetivoId).OnDelete(DeleteBehavior.SetNull);
        });

        // ===== ImportBatch / ImportSheet =====
        mb.Entity<ImportBatch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            e.HasOne(i => i.PeriodoLetivo).WithMany().HasForeignKey(i => i.PeriodoLetivoId).OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<ImportSheet>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            e.HasIndex(x => new { x.ImportId, x.Name }).IsUnique();
            e.HasOne(s => s.ImportBatch).WithMany(b => b.Sheets).HasForeignKey(s => s.ImportId).OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Seeds fixos =====
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


    }

    private static string ToSnakeCase(string name) =>
    string.Concat(
        name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString())
    ).ToLower();

}

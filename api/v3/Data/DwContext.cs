using Microsoft.EntityFrameworkCore;
using SchoolETL.Enums;
using SchoolETL.Models;

namespace SchoolETL.Data;

public class DwContext : DbContext
{
    public DwContext(DbContextOptions<DwContext> opt) : base(opt) { }

    public DbSet<ImportBatch> Imports => Set<ImportBatch>();
    public DbSet<ImportSheet> ImportSheets => Set<ImportSheet>();
    public DbSet<PeriodoLetivo> Periodos => Set<PeriodoLetivo>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<Disciplina> Disciplinas => Set<Disciplina>();
    public DbSet<Situacao> Situacoes => Set<Situacao>();
    public DbSet<PeriodoAvaliativo> PeriodosAvaliativos => Set<PeriodoAvaliativo>();
    public DbSet<FatoNota> FatoNotas => Set<FatoNota>();
    public DbSet<AlunoStatusImport> AlunoStatusImports => Set<AlunoStatusImport>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ImportBatch
        mb.Entity<ImportBatch>(e =>
        {
            e.ToTable("import_batch");
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc")
                .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            e.Property(x => x.OriginalFileName).HasColumnName("original_file_name");
            e.Property(x => x.StorageUri).HasColumnName("storage_uri");
            e.Property(x => x.Status).HasConversion<int>().HasColumnName("status");
            e.Property(x => x.Error).HasColumnName("error");
            e.Property(x => x.FileHash).HasColumnName("file_hash");
            e.HasOne(i => i.PeriodoLetivo).WithMany()
                .HasForeignKey(i => i.PeriodoLetivoId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(b => b.Sheets).WithOne(s => s.ImportBatch!)
                .HasForeignKey(s => s.ImportId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => x.Status);
        });

        // ImportSheet
        mb.Entity<ImportSheet>(e =>
        {
            e.ToTable("import_sheet");
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc")
                .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            e.Property(x => x.ImportId).HasColumnName("import_id").IsRequired();
            e.HasIndex(x => new { x.ImportId, x.Name }).IsUnique();
        });

        mb.Entity<PeriodoLetivo>().ToTable("periodo_letivo");
        mb.Entity<Aluno>().ToTable("aluno");
        mb.Entity<Disciplina>(e =>
        {
            e.ToTable("disciplina");
            e.HasIndex(x => x.Sigla).IsUnique();
        });
        mb.Entity<Situacao>().ToTable("situacao");
        mb.Entity<PeriodoAvaliativo>().ToTable("periodo_avaliativo");

        mb.Entity<FatoNota>(e =>
        {
            e.ToTable("fato_nota");
            e.HasOne(f => f.Aluno).WithMany().HasForeignKey(f => f.AlunoId);
            e.HasOne(f => f.Disciplina).WithMany().HasForeignKey(f => f.DisciplinaId);
            e.HasOne(f => f.PeriodoAvaliativo).WithMany().HasForeignKey(f => f.PeriodoAvaliativoId);
            e.HasOne(f => f.Situacao).WithMany().HasForeignKey(f => f.SituacaoId);
            e.HasOne(f => f.PeriodoLetivo).WithMany().HasForeignKey(f => f.PeriodoLetivoId);
        });

        mb.Entity<AlunoStatusImport>(e =>
        {
            e.ToTable("aluno_status_import");
            e.HasIndex(x => new { x.ImportId, x.AlunoId }).IsUnique();
        });

        base.OnModelCreating(mb);
    }
}

//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Design;
//using SchoolETL.Core.Models;
//using SchoolETL.Repositories;

//namespace SchoolETL.Core.Data;

//public class DwContext : DbContext
//{
//    public DwContext(DbContextOptions<DwContext> options) : base(options) { }

//    // DbSets
//    public DbSet<PeriodoLetivo> PeriodosLetivos => Set<PeriodoLetivo>();
//    public DbSet<Aluno> Alunos => Set<Aluno>();
//    public DbSet<Disciplina> Disciplinas => Set<Disciplina>();
//    public DbSet<Situacao> Situacoes => Set<Situacao>();
//    public DbSet<PeriodoAvaliativo> PeriodosAvaliativos => Set<PeriodoAvaliativo>();
//    public DbSet<AlunoObservacao> AlunoObservacoes => Set<AlunoObservacao>();
//    public DbSet<FatoNota> FatoNotas => Set<FatoNota>();
//    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
//    public DbSet<AlunoStatusImport> AlunoStatusImports => Set<AlunoStatusImport>();

//    protected override void OnModelCreating(ModelBuilder b)
//    {
//        b.HasPostgresExtension("pg_trgm");
//        b.HasPostgresExtension("pgcrypto");

//        b.Entity<PeriodoLetivo>().HasIndex(p => new { p.Ano, p.Semestre }).IsUnique();
//        b.Entity<Disciplina>().HasIndex(d => d.Sigla).IsUnique();

//        // Seeds
//        b.Entity<Situacao>().HasData(
//            new Situacao { Id = 1, Descricao = "APR" },
//            new Situacao { Id = 2, Descricao = "REP" },
//            new Situacao { Id = 3, Descricao = "CAN" },
//            new Situacao { Id = 4, Descricao = "CUR" },
//            new Situacao { Id = 5, Descricao = "OUT" }
//        );
//        b.Entity<PeriodoAvaliativo>().HasData(
//            new PeriodoAvaliativo { Id = 1, Nome = "1", Final = false },
//            new PeriodoAvaliativo { Id = 2, Nome = "2", Final = false },
//            new PeriodoAvaliativo { Id = 3, Nome = "3", Final = false },
//            new PeriodoAvaliativo { Id = 4, Nome = "4", Final = false },
//            new PeriodoAvaliativo { Id = 99, Nome = "Final", Final = true }
//        );
//    }
//}

//public class DwContextFactory : IDesignTimeDbContextFactory<DwContext>
//{
//    public DwContext CreateDbContext(string[] args)
//    {
//        var opts = new DbContextOptionsBuilder<DwContext>()
//            .UseNpgsql("Host=localhost;Port=5432;Database=school_etl;Username=postgres;Password=postgres")
//            .UseSnakeCaseNamingConvention()
//            .Options;
//        return new DwContext(opts);
//    }
//}

//public static class DwContextRepoExtensions
//{
//    public static IPeriodoLetivoRepository PeriodoLetivoRepo(this DwContext db) => new PeriodoLetivoRepository(db);
//}
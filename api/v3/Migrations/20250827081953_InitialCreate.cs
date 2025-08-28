using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace v3.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alunos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    import_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    matricula = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    frequencia_geral = table.Column<decimal>(type: "numeric", nullable: true),
                    situacao_curso = table.Column<string>(type: "text", nullable: true),
                    foto_path = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alunos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bimestres",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bimestres", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cursos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    import_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cursos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "disciplinas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    import_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome_area = table.Column<string>(type: "text", nullable: true),
                    carga_horaria_rotulo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disciplinas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "etapas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_etapas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "periodos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    semestre = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periodos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "situacoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    descricao = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_situacoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "aluno_observacoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    aluno_id = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    criado_em_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aluno_observacoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_aluno_observacoes_alunos_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "alunos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    file_name = table.Column<string>(type: "text", nullable: true),
                    periodo_letivo_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imports", x => x.id);
                    table.ForeignKey(
                        name: "FK_imports_periodos_periodo_letivo_id",
                        column: x => x.periodo_letivo_id,
                        principalTable: "periodos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "fato_notas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aluno_id = table.Column<int>(type: "integer", nullable: false),
                    disciplina_id = table.Column<int>(type: "integer", nullable: false),
                    bimestre_id = table.Column<int>(type: "integer", nullable: false),
                    etapa_id = table.Column<int>(type: "integer", nullable: false),
                    curso_id = table.Column<int>(type: "integer", nullable: true),
                    situacao_id = table.Column<int>(type: "integer", nullable: true),
                    periodo_letivo_id = table.Column<int>(type: "integer", nullable: true),
                    nota = table.Column<decimal>(type: "numeric", nullable: true),
                    frequencia = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fato_notas", x => x.id);
                    table.ForeignKey(
                        name: "FK_fato_notas_alunos_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "alunos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fato_notas_bimestres_bimestre_id",
                        column: x => x.bimestre_id,
                        principalTable: "bimestres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fato_notas_cursos_curso_id",
                        column: x => x.curso_id,
                        principalTable: "cursos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fato_notas_disciplinas_disciplina_id",
                        column: x => x.disciplina_id,
                        principalTable: "disciplinas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fato_notas_etapas_etapa_id",
                        column: x => x.etapa_id,
                        principalTable: "etapas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fato_notas_periodos_periodo_letivo_id",
                        column: x => x.periodo_letivo_id,
                        principalTable: "periodos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fato_notas_situacoes_situacao_id",
                        column: x => x.situacao_id,
                        principalTable: "situacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "import_sheets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_sheets", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_sheets_imports_import_id",
                        column: x => x.import_id,
                        principalTable: "imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "bimestres",
                columns: new[] { "id", "nome" },
                values: new object[,]
                {
                    { 1, "1º" },
                    { 2, "2º" },
                    { 3, "3º" },
                    { 4, "4º" }
                });

            migrationBuilder.InsertData(
                table: "etapas",
                columns: new[] { "id", "nome" },
                values: new object[,]
                {
                    { 1, "Etapa 1" },
                    { 2, "Etapa 2" },
                    { 3, "Etapa 3" },
                    { 4, "Etapa 4" },
                    { 99, "Etapa Final" }
                });

            migrationBuilder.InsertData(
                table: "situacoes",
                columns: new[] { "id", "descricao" },
                values: new object[,]
                {
                    { 1, "APR" },
                    { 2, "REP" },
                    { 3, "CAN" },
                    { 4, "CUR" },
                    { 5, "OUT" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_aluno_observacoes_aluno_id",
                table: "aluno_observacoes",
                column: "aluno_id");

            migrationBuilder.CreateIndex(
                name: "IX_alunos_import_id",
                table: "alunos",
                column: "import_id");

            migrationBuilder.CreateIndex(
                name: "IX_alunos_matricula",
                table: "alunos",
                column: "matricula");

            migrationBuilder.CreateIndex(
                name: "IX_cursos_import_id",
                table: "cursos",
                column: "import_id");

            migrationBuilder.CreateIndex(
                name: "IX_cursos_sigla",
                table: "cursos",
                column: "sigla",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_disciplinas_import_id",
                table: "disciplinas",
                column: "import_id");

            migrationBuilder.CreateIndex(
                name: "IX_disciplinas_sigla",
                table: "disciplinas",
                column: "sigla",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fato_notas_aluno_id",
                table: "fato_notas",
                column: "aluno_id");

            migrationBuilder.CreateIndex(
                name: "IX_fato_notas_bimestre_id",
                table: "fato_notas",
                column: "bimestre_id");

            migrationBuilder.CreateIndex(
                name: "IX_fato_notas_curso_id",
                table: "fato_notas",
                column: "curso_id");

            migrationBuilder.CreateIndex(
                name: "IX_fato_notas_disciplina_id",
                table: "fato_notas",
                column: "disciplina_id");

            migrationBuilder.CreateIndex(
                name: "IX_fato_notas_etapa_id",
                table: "fato_notas",
                column: "etapa_id");

            migrationBuilder.CreateIndex(
                name: "IX_fato_notas_import_id",
                table: "fato_notas",
                column: "import_id");

            migrationBuilder.CreateIndex(
                name: "IX_fato_notas_periodo_letivo_id",
                table: "fato_notas",
                column: "periodo_letivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_fato_notas_situacao_id",
                table: "fato_notas",
                column: "situacao_id");

            migrationBuilder.CreateIndex(
                name: "IX_import_sheets_import_id_name",
                table: "import_sheets",
                columns: new[] { "import_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_imports_periodo_letivo_id",
                table: "imports",
                column: "periodo_letivo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aluno_observacoes");

            migrationBuilder.DropTable(
                name: "fato_notas");

            migrationBuilder.DropTable(
                name: "import_sheets");

            migrationBuilder.DropTable(
                name: "alunos");

            migrationBuilder.DropTable(
                name: "bimestres");

            migrationBuilder.DropTable(
                name: "cursos");

            migrationBuilder.DropTable(
                name: "disciplinas");

            migrationBuilder.DropTable(
                name: "etapas");

            migrationBuilder.DropTable(
                name: "situacoes");

            migrationBuilder.DropTable(
                name: "imports");

            migrationBuilder.DropTable(
                name: "periodos");
        }
    }
}

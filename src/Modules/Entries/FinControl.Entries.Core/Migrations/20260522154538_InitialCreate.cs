using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinControl.Entries.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        private static readonly string[] _idxCriadoPorData = ["criado_por", "data_lancamento"];
        private static readonly string[] _idxDataCategory = ["data_lancamento", "modalidade"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Entries");

            migrationBuilder.CreateTable(
                name: "Entries",
                schema: "Entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    modalidade = table.Column<int>(type: "integer", nullable: false),
                    valor = table.Column<long>(type: "bigint", nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    data_lancamento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_por = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    criado_por_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    criado_por_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    atualizado_por = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    atualizado_por_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    atualizado_por_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    deletado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deletado_por = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    navigation_id = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_lancamento_criado_por",
                schema: "Entries",
                table: "Entries",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "idx_lancamento_criado_por_data",
                schema: "Entries",
                table: "Entries",
                columns: _idxCriadoPorData);

            migrationBuilder.CreateIndex(
                name: "idx_lancamento_data",
                schema: "Entries",
                table: "Entries",
                column: "data_lancamento");

            migrationBuilder.CreateIndex(
                name: "idx_lancamento_data_modalidade",
                schema: "Entries",
                table: "Entries",
                columns: _idxDataCategory);

            migrationBuilder.CreateIndex(
                name: "idx_lancamento_deletado",
                schema: "Entries",
                table: "Entries",
                column: "deletado_em");

            migrationBuilder.CreateIndex(
                name: "idx_lancamento_navigation_id",
                schema: "Entries",
                table: "Entries",
                column: "navigation_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Entries",
                schema: "Entries");
        }
    }
}


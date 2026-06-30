using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinControl.Transactions.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adiciona nullable para suportar linhas já existentes
            migrationBuilder.AddColumn<Guid>(
                name: "idempotency_key",
                schema: "lancamentos",
                table: "lancamentos",
                type: "uuid",
                nullable: true);

            // Atribui UUIDs únicos a todas as linhas existentes antes de aplicar a restrição
            migrationBuilder.Sql(
                "UPDATE lancamentos.lancamentos SET idempotency_key = gen_random_uuid() WHERE idempotency_key IS NULL;");

            // Torna NOT NULL após preencher os valores
            migrationBuilder.AlterColumn<Guid>(
                name: "idempotency_key",
                schema: "lancamentos",
                table: "lancamentos",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_lancamento_idempotency_key",
                schema: "lancamentos",
                table: "lancamentos",
                column: "idempotency_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_lancamento_idempotency_key",
                schema: "lancamentos",
                table: "lancamentos");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                schema: "lancamentos",
                table: "lancamentos");
        }
    }
}

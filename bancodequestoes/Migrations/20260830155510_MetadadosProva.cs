using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class MetadadosProva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ano",
                table: "Provas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataAplicacao",
                table: "Provas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observacoes",
                table: "Provas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Semestre",
                table: "Provas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TempoEstimadoMinutos",
                table: "Provas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "Provas",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ano",
                table: "Provas");

            migrationBuilder.DropColumn(
                name: "DataAplicacao",
                table: "Provas");

            migrationBuilder.DropColumn(
                name: "Observacoes",
                table: "Provas");

            migrationBuilder.DropColumn(
                name: "Semestre",
                table: "Provas");

            migrationBuilder.DropColumn(
                name: "TempoEstimadoMinutos",
                table: "Provas");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Provas");
        }
    }
}

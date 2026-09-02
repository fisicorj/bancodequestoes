using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class VisibilidadeQuestaoEInstituicaoProfessor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nota: "Privada" é o valor que corresponde ao default do enum em C#
            // (VisibilidadeQuestao.Privada = 0), mas o gerador de migração do EF
            // não converte esse default automaticamente pra coluna convertida em
            // string — por isso é preciso escrever "Privada" aqui manualmente,
            // em vez de deixar como estava gerado ("").
            migrationBuilder.AddColumn<string>(
                name: "Visibilidade",
                table: "Questoes",
                type: "text",
                nullable: false,
                defaultValue: "Privada");

            migrationBuilder.AddColumn<int>(
                name: "InstituicaoId",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_InstituicaoId",
                table: "AspNetUsers",
                column: "InstituicaoId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Instituicoes_InstituicaoId",
                table: "AspNetUsers",
                column: "InstituicaoId",
                principalTable: "Instituicoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Instituicoes_InstituicaoId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_InstituicaoId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Visibilidade",
                table: "Questoes");

            migrationBuilder.DropColumn(
                name: "InstituicaoId",
                table: "AspNetUsers");
        }
    }
}

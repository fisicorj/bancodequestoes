using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BancoQuestoes.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirVisibilidadeQuestaoVazia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Correção da migração anterior (VisibilidadeQuestaoEInstituicaoProfessor):
            // o AddColumn foi gerado com defaultValue: "" (string vazia) em vez de
            // "Privada", porque o EF não converte o default do enum em C# pro valor
            // convertido automaticamente. Resultado: toda questão que já existia no
            // banco ficou com Visibilidade = '', um valor que não corresponde a
            // nenhum membro do enum VisibilidadeQuestao — o que quebra a leitura
            // dessas questões (Enum.Parse falha em runtime).
            //
            // Esse UPDATE só corrige as linhas que pegaram esse valor inválido,
            // atribuindo "Compartilhada" (o comportamento que essas questões já
            // tinham antes de existir o conceito de visibilidade: visíveis pra
            // todo mundo). Não toca em nenhuma questão que já tenha um valor válido
            // (por exemplo, criada/editada depois que a migração rodou).
            migrationBuilder.Sql(@"UPDATE ""Questoes"" SET ""Visibilidade"" = 'Compartilhada' WHERE ""Visibilidade"" = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não há como (nem por que) desfazer isso: reverter deixaria as
            // questões de novo com um valor inválido no banco.
        }
    }
}

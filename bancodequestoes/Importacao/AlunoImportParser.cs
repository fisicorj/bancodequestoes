using System.Text;

namespace BancoQuestoes.Importacao;

// Interpreta um arquivo CSV de alunos — só INTERPRETA, nunca toca no banco (isso é
// AlunoService.ImportarAsync). Mesmo parser CSV simples (com suporte a aspas) do ItemMatrizImportParser.
public static class AlunoImportParser
{
    // Cabeçalho obrigatório: "Nome" (Email e Matricula opcionais, qualquer ordem,
    // case-insensitive), separador vírgula com suporte a aspas.
    public static ResultadoImportacaoAlunos ParseCsv(string texto)
    {
        var resultado = new ResultadoImportacaoAlunos();
        var linhas = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(l => l.Length > 0)
            .ToList();

        if (linhas.Count == 0)
        {
            resultado.Erros.Add("Arquivo vazio.");
            return resultado;
        }

        var cabecalho = DividirLinhaCsv(linhas[0]).Select(c => c.Trim()).ToList();

        int Indice(params string[] nomes) =>
            cabecalho.FindIndex(c => nomes.Any(n => string.Equals(c, n, StringComparison.OrdinalIgnoreCase)));

        var iNome = Indice("Nome");
        var iEmail = Indice("Email", "E-mail");
        var iMatricula = Indice("Matricula", "Matrícula");

        if (iNome < 0)
        {
            resultado.Erros.Add("Cabeçalho do CSV precisa ter a coluna \"Nome\" (Email e Matricula são opcionais).");
            return resultado;
        }

        for (var i = 1; i < linhas.Count; i++)
        {
            var campos = DividirLinhaCsv(linhas[i]);
            if (campos.All(string.IsNullOrWhiteSpace))
            {
                continue; // linha em branco no meio do arquivo — ignora silenciosamente
            }

            string CampoOuVazio(int indice) => indice >= 0 && indice < campos.Count ? campos[indice].Trim() : "";

            var nome = CampoOuVazio(iNome);
            if (string.IsNullOrWhiteSpace(nome))
            {
                resultado.Erros.Add($"Linha {i + 1}: faltando \"Nome\" — linha ignorada.");
                continue;
            }

            var email = CampoOuVazio(iEmail);
            var matricula = CampoOuVazio(iMatricula);

            resultado.Alunos.Add(new AlunoImportado
            {
                Nome = nome,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                Matricula = string.IsNullOrWhiteSpace(matricula) ? null : matricula,
            });
        }

        return resultado;
    }

    // Parser CSV simples que respeita aspas (campo entre aspas pode conter vírgula;
    // "" vira " literal) — não é RFC 4180 completo, só o suficiente pro Excel/Sheets.
    private static List<string> DividirLinhaCsv(string linha)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var dentroDeAspas = false;

        for (var i = 0; i < linha.Length; i++)
        {
            var c = linha[i];

            if (dentroDeAspas)
            {
                if (c == '"' && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    dentroDeAspas = false;
                }
                else
                {
                    atual.Append(c);
                }
            }
            else if (c == '"')
            {
                dentroDeAspas = true;
            }
            else if (c == ',')
            {
                campos.Add(atual.ToString());
                atual.Clear();
            }
            else
            {
                atual.Append(c);
            }
        }

        campos.Add(atual.ToString());
        return campos;
    }
}

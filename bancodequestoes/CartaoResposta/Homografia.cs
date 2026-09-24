namespace BancoQuestoes.CartaoResposta;

// Transformação de perspectiva 2D (homografia) a partir de 4 pares de pontos correspondentes.
// Usada pra mapear um ponto conhecido no CARTÃO ORIGINAL (coordenadas do layout do PDF) pro
// pixel correspondente na FOTO (que pode estar torta/em ângulo) — assim dá pra amostrar a
// bolha certa sem precisar "endireitar" a imagem inteira.
public sealed class Homografia
{
    private readonly double[] h; // h[0..7], h33 implícito = 1

    private Homografia(double[] h) => this.h = h;

    // origem[i] -> destino[i], i = 0..3. Aqui usamos origem = pontos no layout do cartão
    // (página) e destino = pixels detectados na foto, então Aplicar(pontoDaPagina) devolve o
    // pixel correspondente na foto.
    public static Homografia DeCorrespondencias((double X, double Y)[] origem, (double X, double Y)[] destino)
    {
        if (origem.Length != 4 || destino.Length != 4)
        {
            throw new ArgumentException("São necessários exatamente 4 pares de pontos.");
        }

        // Sistema linear 8x8: cada par de pontos gera 2 equações (ver derivação padrão de
        // homografia planar via DLT simplificado, h33 fixado em 1 já que só precisamos de
        // 8 graus de liberdade pra 4 correspondências).
        var a = new double[8, 8];
        var b = new double[8];

        for (var i = 0; i < 4; i++)
        {
            var (x, y) = origem[i];
            var (u, v) = destino[i];

            a[2 * i, 0] = x; a[2 * i, 1] = y; a[2 * i, 2] = 1;
            a[2 * i, 3] = 0; a[2 * i, 4] = 0; a[2 * i, 5] = 0;
            a[2 * i, 6] = -x * u; a[2 * i, 7] = -y * u;
            b[2 * i] = u;

            a[2 * i + 1, 0] = 0; a[2 * i + 1, 1] = 0; a[2 * i + 1, 2] = 0;
            a[2 * i + 1, 3] = x; a[2 * i + 1, 4] = y; a[2 * i + 1, 5] = 1;
            a[2 * i + 1, 6] = -x * v; a[2 * i + 1, 7] = -y * v;
            b[2 * i + 1] = v;
        }

        var solucao = ResolverSistemaLinear(a, b)
            ?? throw new InvalidOperationException("Os 4 pontos informados são degenerados (colineares ou repetidos) — não dá pra calcular a perspectiva.");

        return new Homografia(solucao);
    }

    public (double X, double Y) Aplicar(double x, double y)
    {
        var denominador = h[6] * x + h[7] * y + 1;
        var u = (h[0] * x + h[1] * y + h[2]) / denominador;
        var v = (h[3] * x + h[4] * y + h[5]) / denominador;
        return (u, v);
    }

    // Eliminação de Gauss com pivoteamento parcial — 8x8 é pequeno o bastante pra não
    // precisar de nada mais sofisticado. Retorna null se o sistema for singular.
    private static double[]? ResolverSistemaLinear(double[,] a, double[] b)
    {
        const int n = 8;
        for (var col = 0; col < n; col++)
        {
            var linhaPivo = col;
            for (var linha = col + 1; linha < n; linha++)
            {
                if (Math.Abs(a[linha, col]) > Math.Abs(a[linhaPivo, col]))
                {
                    linhaPivo = linha;
                }
            }

            if (Math.Abs(a[linhaPivo, col]) < 1e-9)
            {
                return null;
            }

            if (linhaPivo != col)
            {
                for (var k = 0; k < n; k++)
                {
                    (a[col, k], a[linhaPivo, k]) = (a[linhaPivo, k], a[col, k]);
                }
                (b[col], b[linhaPivo]) = (b[linhaPivo], b[col]);
            }

            for (var linha = col + 1; linha < n; linha++)
            {
                var fator = a[linha, col] / a[col, col];
                for (var k = col; k < n; k++)
                {
                    a[linha, k] -= fator * a[col, k];
                }
                b[linha] -= fator * b[col];
            }
        }

        var x = new double[n];
        for (var linha = n - 1; linha >= 0; linha--)
        {
            var soma = b[linha];
            for (var k = linha + 1; k < n; k++)
            {
                soma -= a[linha, k] * x[k];
            }
            x[linha] = soma / a[linha, linha];
        }

        return x;
    }
}

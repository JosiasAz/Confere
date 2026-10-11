using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Conferencia.Core;

public sealed class LeituraDanfe
{
    public List<NotaFiscal> Notas { get; init; } = [];
    public List<string> Avisos { get; init; } = [];
}

public static class DanfeParser
{
    static readonly Regex ItemRe = new(
        @"^\s*(?:\d{1,3}\s+)?(?<cod>\d{4,8})\s+(?<desc>.*?)\s+(?<ncm>\d{4}\.?\d{2}\.?\d{2})\s+(?<cst>\d{1,4})\s+(?<cfop>\d\.?\d{3})\s+(?<und>[A-Z]{1,6})\.?\s+(?<qtd>[\d.]+(?:,\d+)?)\s+(?<vu>[\d.,]+)\s+(?<vt>[\d.,]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex ItemGlobalRe = new(
        @"(?<!\d)(?<cod>\d{4,8})\s+(?<desc>[\s\S]{0,240}?)(?<ncm>\d{4}\.?\d{2}\.?\d{2})\s+(?<cst>\d{1,4})\s+(?<cfop>\d\.?\d{3})\s+(?<und>[A-Z]{1,6})\.?\s+(?<qtd>[\d.]+(?:,\d+)?)\s+(?<vu>[\d.,]+)\s+(?<vt>[\d.,]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex RecebemosRe = new(@"RECEBEMOS\s+DE", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex DestinatarioRe = new(@"DESTINAT[AÁ]RIO\s*/\s*REMETENTE", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex LoteRe = new(@"L(?:OTE|T)\.?\s*:?\s*([A-Z0-9\-/]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex FabRe = new(@"FAB\.?\s*:?\s*(\d{2}/\d{2}/\d{4})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex ValRe = new(@"VAL\.?\s*:?\s*(\d{2}/\d{2}/\d{4})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex PularRe = new(@"^\s*(C[oó]digo|ICMS|IPI)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex FimRe = new(@"FIM DOS PRODUTOS|C[AÁ]LCULO DO ISSQN|DADOS\s+ADICIONAIS", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex ProdutoRe = new(@"DADOS DO[S]? PRODUTO", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex QtdRe = new(@"QTD\.?\s*([\d.,]+)\s*([A-Z]{1,4})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex QtdFimRe = new(@"QTD\.?\s*[\d.,]*\s*[A-Z]{0,4}\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex EmbRe = new(@"((?:\d+\s*X\s*)+\d+(?:[.,]\d+)?\s*(?:G|KG|ML|L|UN))\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex UpvRe = new(@"((?:\d+X)+)\d+(?:[.,]\d+)?(G|KG|ML|L|UN)\b", RegexOptions.Compiled);
    static readonly Regex ValorRe = new(@"\d,\d{2}$", RegexOptions.Compiled);
    static readonly Regex PlacaRe = new(@"\b([A-Z]{3}-?\d[A-Z0-9]\d{2})\b", RegexOptions.Compiled);
    static readonly Regex CodigoInicioRe = new(@"^\s*(?:\d{1,3}\s+)?\d{4,8}\b", RegexOptions.Compiled);

    sealed class Peca
    {
        public string Str = "";
        public double X, Y, W;
    }

    sealed class LinhaTexto
    {
        public double Y;
        public List<Peca> Itens = [];
        public string Texto = "";
    }

    sealed class PaginaBruta
    {
        public List<Peca> Palavras = [];
        public List<Peca> Letras = [];
    }

    public static LeituraDanfe Ler(byte[] pdf)
    {
        using var doc = Abrir(pdf);
        var paginas = new List<PaginaBruta>();
        var avisos = new List<string>();
        var falhas = 0;
        foreach (var page in doc.GetPages())
        {
            try
            {
                paginas.Add(Extrair(page));
            }
            catch
            {
                falhas++;
            }
        }

        if (paginas.Count == 0 || paginas.All(p => p.Palavras.Count == 0 && p.Letras.Count == 0))
            throw new InvalidOperationException("O PDF não contém texto. Use o DANFE baixado do sistema, não uma foto ou um scan.");

        if (falhas > 0)
            avisos.Add("Uma parte do PDF não pôde ser lida; as notas reconhecidas foram aproveitadas.");

        var tentativas = new (double Tol, double Gap, bool Letras)[]
        {
            (2.2, 1.5, false),
            (5.0, 1.5, false),
            (2.2, 1.5, true),
            (4.5, 2.2, true)
        };

        List<NotaFiscal> melhor = [];
        var melhorNota = -1;
        foreach (var t in tentativas)
        {
            List<NotaFiscal> notas;
            try
            {
                var linhas = MontarLinhas(paginas, t.Tol, t.Gap, t.Letras);
                notas = Analisar(linhas);
            }
            catch
            {
                continue;
            }
            var nota = Pontuar(notas);
            if (nota > melhorNota)
            {
                melhorNota = nota;
                melhor = notas;
            }
        }

        var comProduto = melhor.Where(n => n.Itens.Count > 0).ToList();
        var semProduto = melhor.Count - comProduto.Count;
        if (semProduto > 0 && comProduto.Count > 0)
            avisos.Add(semProduto + " nota(s) sem produtos foram ignoradas.");
        if (comProduto.Count == 0 && melhor.Count > 0)
            avisos.Add("O número da nota foi encontrado, mas nenhum produto pôde ser lido. Confira se o arquivo é a DANFE completa.");
        if (comProduto.Count == 0 && melhor.Count == 0)
            avisos.Add("Nenhuma NF com produtos encontrada (o PDF é uma DANFE?).");

        return new LeituraDanfe { Notas = comProduto, Avisos = avisos };
    }

    static PdfDocument Abrir(byte[] pdf)
    {
        try
        {
            return PdfDocument.Open(pdf, new ParsingOptions { UseLenientParsing = true });
        }
        catch
        {
            return PdfDocument.Open(pdf, new ParsingOptions { UseLenientParsing = true, SkipMissingFonts = true });
        }
    }

    static int Pontuar(List<NotaFiscal> notas)
    {
        var s = 0;
        foreach (var nf in notas)
        {
            s += nf.Itens.Count * 10;
            if (!string.IsNullOrWhiteSpace(nf.Cliente)) s += 2;
            if (!string.IsNullOrWhiteSpace(nf.Carregamento) && nf.Carregamento != "SEM CARREG.") s += 3;
            if (nf.Itens.Any(i => !string.IsNullOrEmpty(i.Lote) && i.Lote != "S/ LOTE")) s += 2;
            if (nf.Valor > 0) s += 1;
        }
        return s;
    }

    static PaginaBruta Extrair(Page page)
    {
        var bruta = new PaginaBruta();
        foreach (var word in page.GetWords())
        {
            if (string.IsNullOrWhiteSpace(word.Text)) continue;
            double x, y, w;
            if (word.Letters.Count > 0)
            {
                var a = word.Letters[0];
                var z = word.Letters[^1];
                x = a.StartBaseLine.X;
                y = a.StartBaseLine.Y;
                w = z.StartBaseLine.X + z.Width - x;
                if (w <= 0) w = word.BoundingBox.Width;
            }
            else
            {
                x = word.BoundingBox.Left;
                y = word.BoundingBox.Bottom;
                w = word.BoundingBox.Width;
            }
            bruta.Palavras.Add(new Peca { Str = word.Text.Trim(), X = x, Y = y, W = w });
        }

        foreach (var letter in page.Letters)
        {
            if (string.IsNullOrWhiteSpace(letter.Value)) continue;
            var w = letter.Width > 0 ? letter.Width : letter.BoundingBox.Width;
            bruta.Letras.Add(new Peca
            {
                Str = letter.Value,
                X = letter.StartBaseLine.X,
                Y = letter.StartBaseLine.Y,
                W = w
            });
        }
        return bruta;
    }

    static List<LinhaTexto> MontarLinhas(List<PaginaBruta> paginas, double tolY, double gapLetra, bool usarLetras)
    {
        var linhas = new List<LinhaTexto>();
        foreach (var pagina in paginas)
        {
            var pecas = usarLetras && pagina.Letras.Count > 0 ? pagina.Letras : pagina.Palavras;
            if (pecas.Count == 0) pecas = pagina.Palavras.Count > 0 ? pagina.Palavras : pagina.Letras;
            var juntar = usarLetras && pagina.Letras.Count > 0 && ReferenceEquals(pecas, pagina.Letras);
            linhas.AddRange(LinhasDaPagina(pecas, tolY, juntar ? gapLetra : 0));
        }
        return linhas;
    }

    static List<LinhaTexto> LinhasDaPagina(List<Peca> pecas, double tolY, double gapLetra)
    {
        var rows = new List<LinhaTexto>();
        foreach (var peca in pecas)
        {
            var r = rows.Find(x => Math.Abs(x.Y - peca.Y) < tolY);
            if (r == null)
            {
                r = new LinhaTexto { Y = peca.Y };
                rows.Add(r);
            }
            r.Itens.Add(peca);
        }
        rows.Sort((a, b) => b.Y.CompareTo(a.Y));
        foreach (var r in rows)
        {
            var itens = r.Itens.OrderBy(i => i.X).ToList();
            if (gapLetra > 0) itens = Juntar(itens, gapLetra);
            r.Itens = itens;
            var sb = new StringBuilder();
            double? fim = null;
            foreach (var i in itens)
            {
                if (fim != null)
                {
                    var gap = i.X - fim.Value;
                    if (gap > 1.5) sb.Append(gap > 12 ? "   " : " ");
                }
                sb.Append(i.Str);
                fim = i.X + i.W;
            }
            r.Texto = sb.ToString().TrimEnd();
        }
        rows.RemoveAll(r => r.Texto.Length == 0);
        return rows;
    }

    static List<Peca> Juntar(List<Peca> pecas, double gapMax)
    {
        var saida = new List<Peca>();
        Peca? atual = null;
        var fim = 0d;
        foreach (var peca in pecas)
        {
            if (atual != null && peca.X - fim <= gapMax)
            {
                atual.Str += peca.Str;
                atual.W = peca.X + peca.W - atual.X;
                fim = peca.X + peca.W;
            }
            else
            {
                atual = new Peca { Str = peca.Str, X = peca.X, Y = peca.Y, W = peca.W };
                saida.Add(atual);
                fim = peca.X + peca.W;
            }
        }
        return saida;
    }

    static List<NotaFiscal> Analisar(List<LinhaTexto> todas)
    {
        var inicios = new List<int>();
        for (int i = 0; i < todas.Count; i++)
            if (RecebemosRe.IsMatch(todas[i].Texto)) inicios.Add(i);
        if (inicios.Count == 0)
        {
            for (int i = 0; i < todas.Count; i++)
                if (DestinatarioRe.IsMatch(todas[i].Texto)) inicios.Add(i);
        }
        if (inicios.Count == 0) inicios.Add(0);

        var nfs = new List<NotaFiscal>();
        for (int b = 0; b < inicios.Count; b++)
        {
            int de = inicios[b];
            int ate = b < inicios.Count - 1 ? inicios[b + 1] : todas.Count;
            var nf = LerBloco(todas.GetRange(de, ate - de));
            if (nf != null) nfs.Add(nf);
        }

        var melhor = new Dictionary<string, NotaFiscal>();
        foreach (var nf in nfs)
        {
            if (!melhor.TryGetValue(nf.Numero, out var atual) || nf.Itens.Count > atual.Itens.Count)
                melhor[nf.Numero] = nf;
        }
        return melhor.Values.ToList();
    }

    static NotaFiscal? LerBloco(List<LinhaTexto> linhas)
    {
        var full = string.Join("\n", linhas.Select(l => l.Texto));
        var numero = AcharNumero(full);
        if (numero == null) return null;

        var nf = new NotaFiscal { Numero = numero };
        int i = linhas.FindIndex(l => Regex.IsMatch(l.Texto, @"NOME\s*/\s*RAZ", RegexOptions.IgnoreCase));
        if (i >= 0)
        {
            nf.Cliente = Abaixo(linhas, i, "NOME");
            nf.Cnpj = Abaixo(linhas, i, "CNPJ");
            nf.Emissao = Abaixo(linhas, i, "DATA DA EMISS");
            if (string.IsNullOrWhiteSpace(nf.Cliente) && i + 1 < linhas.Count)
            {
                var prox = Regex.Replace(linhas[i + 1].Texto, @"\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}.*", "").Trim();
                if (prox.Length > 3) nf.Cliente = prox;
            }
        }
        i = linhas.FindIndex(l => l.Texto.Contains("BAIRRO", StringComparison.OrdinalIgnoreCase));
        if (i >= 0) nf.Bairro = Abaixo(linhas, i, "BAIRRO");
        i = linhas.FindIndex(l => Regex.IsMatch(l.Texto, @"MUNIC[IÍ]PIO", RegexOptions.IgnoreCase) && l.Texto.Contains("CEP", StringComparison.OrdinalIgnoreCase));
        if (i >= 0) nf.Cidade = Abaixo(linhas, i, "MUNIC");
        i = linhas.FindIndex(l => l.Texto.Contains("VALOR TOTAL DA NOTA", StringComparison.OrdinalIgnoreCase));
        if (i >= 0 && Localizar(linhas[i], "TOTAL DA NOTA", out double hx, out _))
        {
            double v = 0;
            for (int k = 1; k <= 4 && i + k < linhas.Count && v == 0; k++)
            {
                var cands = linhas[i + k].Itens.Where(x => x.X + x.W >= hx - 10 && ValorRe.IsMatch(x.Str.Trim())).ToList();
                if (cands.Count > 0) v = Formatacao.Numero(cands[^1].Str);
            }
            nf.Valor = v;
        }
        i = linhas.FindIndex(l => Regex.IsMatch(l.Texto, @"PLACA DO VE", RegexOptions.IgnoreCase));
        if (i >= 0) nf.Placa = Abaixo(linhas, i, "PLACA");
        i = linhas.FindIndex(l => l.Texto.Contains("PESO BRUTO", StringComparison.OrdinalIgnoreCase));
        if (i >= 0)
        {
            nf.PesoBruto = Formatacao.Numero(Abaixo(linhas, i, "PESO BRUTO"));
            nf.PesoLiq = Formatacao.Numero(Abaixo(linhas, i, "PESO L"));
        }

        CompletarCabecalho(nf, full);
        LerItens(nf, linhas, full);
        return nf;
    }

    static void CompletarCabecalho(NotaFiscal nf, string full)
    {
        if (string.IsNullOrWhiteSpace(nf.Carregamento) || nf.Carregamento == "SEM CARREG.")
        {
            nf.Carregamento = Grupo(full, @"NUM\.?\s*CARREG(?:AMENTO)?\.?\s*:?\s*(\d+)")
                ?? Grupo(full, @"N\s*[º°o]\.?\s*CARREG(?:AMENTO)?\.?\s*:?\s*(\d+)")
                ?? Grupo(full, @"CARREGAMENTO\s*[.:]?\s*(\d{3,})")
                ?? "SEM CARREG.";
        }

        if (string.IsNullOrWhiteSpace(nf.CodCliente))
            nf.CodCliente = Grupo(full, @"COD\.?\s*CLIENTE\.?\s*:?\s*(\d+)");
        if (string.IsNullOrWhiteSpace(nf.Fantasia))
            nf.Fantasia = Grupo(full, @"FANTASIA\s*:\s*([^\n]+)");
        if (string.IsNullOrWhiteSpace(nf.Pedido))
            nf.Pedido = Grupo(full, @"NR\.?\s*PEDIDO\s*:?\s*(\d+)");
        if (string.IsNullOrWhiteSpace(nf.Vendedor))
            nf.Vendedor = Grupo(full, @"VENDEDOR\s*:\s*([^\n]+)");
        if (string.IsNullOrWhiteSpace(nf.Cobranca))
            nf.Cobranca = Grupo(full, @"COD\.?\s*COB\.?\s*:\s*([^/\n]+)");
        if (string.IsNullOrWhiteSpace(nf.Emissao))
            nf.Emissao = Grupo(full, @"EMISS[AÃ]O[\s\S]{0,40}?(\d{2}/\d{2}/\d{4})");
        if (string.IsNullOrWhiteSpace(nf.Placa))
        {
            var placa = PlacaRe.Match(full);
            if (placa.Success) nf.Placa = placa.Groups[1].Value;
        }
        if (nf.Valor <= 0)
            nf.Valor = Formatacao.Numero(Grupo(full, @"VALOR TOTAL DA NOTA[\s\S]{0,80}?(\d{1,3}(?:\.\d{3})*,\d{2})"));
        if (nf.PesoBruto <= 0)
            nf.PesoBruto = Formatacao.Numero(Grupo(full, @"PESO BRUTO[\s\S]{0,40}?(\d{1,3}(?:\.\d{3})*,\d{1,3}|\d+,\d+)"));
    }

    static void LerItens(NotaFiscal nf, List<LinhaTexto> linhas, string full)
    {
        int s = linhas.FindIndex(l => ProdutoRe.IsMatch(l.Texto));
        int e = -1;
        if (s >= 0)
        {
            for (int i = s + 1; i < linhas.Count; i++)
                if (FimRe.IsMatch(linhas[i].Texto)) { e = i; break; }
        }
        else
        {
            s = linhas.FindIndex(l => CodigoInicioRe.IsMatch(l.Texto) || ItemRe.IsMatch(l.Texto));
            if (s > 0) s--;
            e = linhas.Count;
        }
        if (s >= 0)
        {
            if (e < 0) e = linhas.Count;
            ItemNota? it = null;
            for (int k = s + 1; k < e; k++)
            {
                var t = linhas[k].Texto;
                var juntada = t;
                var pulo = 0;
                var achou = ItemRe.Match(juntada);
                if (!achou.Success && CodigoInicioRe.IsMatch(t))
                {
                    for (int n = 1; n <= 2 && k + n < e; n++)
                    {
                        var prox = linhas[k + n].Texto;
                        if (LoteRe.IsMatch(prox) || FimRe.IsMatch(prox) || PularRe.IsMatch(prox) || CodigoInicioRe.IsMatch(prox))
                            break;
                        juntada += " " + prox.Trim();
                        achou = ItemRe.Match(juntada);
                        pulo = n;
                        if (achou.Success) break;
                    }
                }
                if (achou.Success)
                {
                    it = CriarItem(achou);
                    nf.Itens.Add(it);
                    k += pulo;
                    continue;
                }
                if (it == null) continue;
                var lt = LoteRe.Match(t);
                if (lt.Success)
                {
                    it.Lote = lt.Groups[1].Value;
                    var f = FabRe.Match(t);
                    var v = ValRe.Match(t);
                    it.Fab = f.Success ? f.Groups[1].Value : "";
                    it.Val = v.Success ? v.Groups[1].Value : "";
                    continue;
                }
                if (PularRe.IsMatch(t)) continue;
                if (string.IsNullOrEmpty(it.Lote)) it.Desc += " " + t.Trim();
            }
        }

        var globais = ItensGlobais(full);
        if (globais.Count > nf.Itens.Count)
            nf.Itens = globais;

        foreach (var item in nf.Itens)
            CompletarItem(item);
    }

    static List<ItemNota> ItensGlobais(string full)
    {
        var lista = new List<ItemNota>();
        var matches = ItemGlobalRe.Matches(full);
        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var fim = m.Index + m.Length;
            var ate = i + 1 < matches.Count ? matches[i + 1].Index : Math.Min(full.Length, fim + 220);
            var depois = full[fim..ate];
            var it = CriarItem(m);
            var lt = LoteRe.Match(m.Groups["desc"].Value + " " + depois);
            if (lt.Success) it.Lote = lt.Groups[1].Value;
            var f = FabRe.Match(depois);
            var v = ValRe.Match(depois);
            it.Fab = f.Success ? f.Groups[1].Value : "";
            it.Val = v.Success ? v.Groups[1].Value : "";
            lista.Add(it);
        }
        return lista;
    }

    static ItemNota CriarItem(Match m) => new()
    {
        Codigo = m.Groups["cod"].Value,
        Desc = m.Groups["desc"].Value.Trim(),
        Und = m.Groups["und"].Value,
        Qtd = Formatacao.Numero(m.Groups["qtd"].Value),
        VUnit = Formatacao.Numero(m.Groups["vu"].Value),
        VTotal = Formatacao.Numero(m.Groups["vt"].Value)
    };

    static void CompletarItem(ItemNota item)
    {
        var d = Regex.Replace((item.Desc ?? "").Trim(), @"\s+", " ");
        var q = QtdRe.Match(d);
        if (q.Success)
        {
            var bruto = q.Groups[1].Value.Replace(',', '.');
            item.Volumes = double.TryParse(bruto, NumberStyles.Float, CultureInfo.InvariantCulture, out var vol) ? vol : null;
            item.TipoVol = q.Groups[2].Value.ToUpperInvariant();
        }
        else
        {
            item.Volumes = null;
            item.TipoVol = "VOL";
        }
        item.Produto = QtdFimRe.Replace(d, "").Trim();
        var em = EmbRe.Match(item.Produto ?? "");
        if (em.Success)
        {
            item.Embalagem = Regex.Replace(em.Groups[1].Value.ToUpperInvariant(), @"\s", "");
            item.Nome = item.Produto![..em.Index].Trim();
        }
        else
        {
            item.Embalagem = "";
            item.Nome = item.Produto ?? "";
        }
        item.Upv = UnidadesPorVolume(string.IsNullOrEmpty(item.Embalagem) ? item.Produto ?? "" : item.Embalagem);
        if (item.Volumes == null && item.Upv > 0) item.Volumes = item.Qtd / item.Upv;
        if (string.IsNullOrEmpty(item.Lote))
        {
            var lt = LoteRe.Match(d);
            item.Lote = lt.Success ? lt.Groups[1].Value : "S/ LOTE";
        }
    }

    static int UnidadesPorVolume(string desc)
    {
        var m = UpvRe.Match(desc.ToUpperInvariant().Replace(" ", ""));
        if (!m.Success) return 0;
        return m.Groups[1].Value.Split('X', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).Aggregate(1, (a, b) => a * b);
    }

    static string? AcharNumero(string full)
    {
        foreach (Match m in Regex.Matches(full, @"N\s*[º°o]\.?\s*([\d.]{3,14})", RegexOptions.IgnoreCase))
        {
            var digitos = Regex.Replace(m.Groups[1].Value, @"\D", "");
            if (digitos.Length is < 3 or > 9) continue;
            var semZero = digitos.TrimStart('0');
            if (semZero.Length >= 3) return semZero;
            if (digitos.Length >= 3) return digitos;
        }
        return null;
    }

    static string? Grupo(string full, string pattern)
    {
        var m = Regex.Match(full, pattern, RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    static string Abaixo(List<LinhaTexto> linhas, int idx, string header, int maxDown = 5)
    {
        if ((uint)idx >= (uint)linhas.Count) return "";
        if (!Localizar(linhas[idx], header, out double hx, out int fim)) return "";
        double nextX = fim + 1 < linhas[idx].Itens.Count ? linhas[idx].Itens[fim + 1].X : 1e9;
        for (int k = 1; k <= maxDown && idx + k < linhas.Count; k++)
        {
            var vals = linhas[idx + k].Itens
                .Where(p => p.X >= hx - 25 && p.X < nextX - 4)
                .Select(p => p.Str.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            if (vals.Count > 0) return string.Join(" ", vals);
        }
        return "";
    }

    static bool Localizar(LinhaTexto linha, string frase, out double x, out int fimPalavra)
    {
        x = 0;
        fimPalavra = -1;
        var hs = linha.Itens;
        if (hs.Count == 0) return false;
        var sb = new StringBuilder();
        var inicios = new int[hs.Count];
        int pos = 0;
        for (int i = 0; i < hs.Count; i++)
        {
            if (i > 0) { sb.Append(' '); pos++; }
            inicios[i] = pos;
            sb.Append(hs[i].Str);
            pos += hs[i].Str.Length;
        }
        int found = sb.ToString().IndexOf(frase, StringComparison.OrdinalIgnoreCase);
        if (found < 0) return false;
        int ini = 0, fim = 0;
        for (int i = 0; i < hs.Count; i++)
        {
            if (inicios[i] <= found) ini = i;
            if (inicios[i] < found + frase.Length) fim = i;
        }
        while (fim + 1 < hs.Count && hs[fim + 1].X - (hs[fim].X + hs[fim].W) <= 14)
            fim++;
        x = hs[ini].X;
        fimPalavra = fim;
        return true;
    }
}

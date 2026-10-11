using Conferencia.Core;

var cargas = ConferenciaCalculo.Montar(ExemploNotas.Carregar(), ParametroProduto.Padrao());
Esperar(cargas.Count == 2, "cargas " + cargas.Count);

var c1 = cargas.Single(c => c.Num == "32764");
var c2 = cargas.Single(c => c.Num == "32784");
Esperar(c1.Avisos.Count == 0, "avisos 32764: " + string.Join(" | ", c1.Avisos));
Esperar(c2.Avisos.Count == 0, "avisos 32784: " + string.Join(" | ", c2.Avisos));

var milho = c1.Linhas.Single(l => l.Codigo == "100001");
Esperar(milho.Qtd == 3456, "qtd 100001 " + milho.Qtd);
Esperar(milho.Vl == 9, "vl 100001 " + milho.Vl);
Esperar(milho.Quebras.Count == 0, "quebras 100001");
Esperar(milho.Fd == 8 && milho.Un == 48, "param 100001");

var zig = c1.Linhas.Single(l => l.Codigo == "100013");
Esperar(zig.Vl == 3 && zig.Quebras.Count == 0, "zig 32764 vl=" + zig.Vl);

var nf = c2.Nfs.Single(x => x.Numero == "131027");
Esperar(nf.Itens.Single(i => i.Codigo == "100009").Nome == "PIPOCA MITOKA SALGADA 25G", "nome duplicado");

var bacon = nf.Quebras.Single(x => x.Codigo == "100002");
Esperar(bacon.Misto && bacon.Completo && bacon.Total == 384, "quebra bacon");

var pip = c2.Linhas.Single(l => l.Codigo == "100009");
Esperar(pip.Qtd == 803, "pip qtd " + pip.Qtd);
Esperar(pip.Vl == 3, "pip vl " + pip.Vl);
Esperar(pip.Quebras.Count == 2 && pip.Quebras.Count(q => q.Misto) == 1, "pip quebras");
Esperar(Formatacao.DecTxt(803, 10, 20) == "4 VL + 3 un", Formatacao.DecTxt(803, 10, 20));

var concha = c2.Linhas.Single(l => l.Codigo == "100012");
Esperar(concha.Vl == 1 && concha.Quebras.Count == 1 && concha.Quebras[0].Misto, "concha");
Esperar(Formatacao.DecTxt(concha.Qtd, concha.Fd, concha.Un) == "2 VL", Formatacao.DecTxt(concha.Qtd, concha.Fd, concha.Un));

var zig2 = c2.Linhas.Single(l => l.Codigo == "100013");
Esperar(zig2.Vl == 0 && zig2.Quebras.Count == 3 && zig2.Quebras.All(q => !q.Misto), "zig frac " + zig2.Quebras.Count);

var trok = c2.Linhas.Single(l => l.Codigo == "100026");
Esperar(trok.Vl == 0 && trok.Quebras.Count == 1 && !trok.Quebras[0].Misto, "trok");
Esperar(Formatacao.DecTxt(48, 8, 48, false) == "1 FD", Formatacao.DecTxt(48, 8, 48, false));

var tsv = Exportacao.Tsv(cargas);
Esperar(tsv.StartsWith("ROMANEIO DE SEPARAÇÃO"), "cabecalho");
Esperar(tsv.Contains("131027") && tsv.Contains("QUEBRA DE LOTE"), "export");

Console.WriteLine("OK " + c1.Nfs.Count + "+" + c2.Nfs.Count + " NFs, " + c1.VlTot + "/" + c2.VlTot + " volumes fechados");

var pdf = PdfDanfe.Criar(PdfDanfe.PaginaAlinhada(), PdfDanfe.PaginaQuebrada());
var lidas = DanfeParser.Ler(pdf);
Esperar(lidas.Notas.Count == 2, "pdf notas " + lidas.Notas.Count + " avisos " + string.Join(" | ", lidas.Avisos));
var n1 = lidas.Notas.Single(n => n.Numero == "130962");
Esperar(n1.Carregamento == "32764", "carreg " + n1.Carregamento);
Esperar((n1.Cliente ?? "").Contains("CAVALCANTE"), "cliente " + n1.Cliente);
Esperar(n1.Itens.Count == 1 && n1.Itens[0].Codigo == "100001" && n1.Itens[0].Qtd == 768, "item alinhado");
Esperar(n1.Itens[0].Lote == "260924", "lote " + n1.Itens[0].Lote);
Esperar(n1.Placa == "QZO4G35", "placa " + n1.Placa);
Esperar(Math.Abs(n1.Valor - 883.80) < 0.01, "valor " + n1.Valor);

var n2 = lidas.Notas.Single(n => n.Numero == "130963");
Esperar(n2.Carregamento == "32784", "carreg2 " + n2.Carregamento);
Esperar(n2.Itens.Count == 1 && n2.Itens[0].Codigo == "100010" && n2.Itens[0].Qtd == 500, "item quebrado qtd " + (n2.Itens.FirstOrDefault()?.Qtd.ToString() ?? "nenhum"));
Esperar(n2.Itens[0].Lote == "261006", "lote2 " + n2.Itens[0].Lote);
Esperar((n2.Itens[0].Embalagem ?? "").Contains("20X25X15G"), "emb " + n2.Itens[0].Embalagem);

var vazio = false;
try { DanfeParser.Ler(PdfDanfe.Criar(PdfDanfe.PaginaVazia())); }
catch (InvalidOperationException ex)
{
    vazio = ex.Message.Contains("não contém texto", StringComparison.OrdinalIgnoreCase);
}
Esperar(vazio, "pdf sem texto deveria avisar");
Console.WriteLine("OK leitura de PDF");

static void Esperar(bool ok, string msg)
{
    if (!ok) throw new InvalidOperationException(msg);
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Conferencia.Core;

namespace ConferenciaCarregamento;

static class Impressao
{
    public static void AbrirPrevia(Window dono, IReadOnlyList<CargaConferencia> cargas, bool detalhe)
    {
        try
        {
            var janela = new PreviaImpressao(cargas, detalhe) { Owner = dono };
            janela.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(dono, ex.Message, "Visualização da impressão", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    internal static FlowDocument CriarDocumento(IReadOnlyList<CargaConferencia> cargas, bool detalhe, Size pagina)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            PageHeight = pagina.Height,
            PageWidth = pagina.Width,
            PagePadding = new Thickness(36),
            ColumnWidth = Math.Max(120, pagina.Width - 72)
        };

        var primeira = true;
        foreach (var c in cargas)
        {
            var titulo = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
            if (!primeira) titulo.BreakPageBefore = true;
            primeira = false;
            titulo.Inlines.Add(new Run("Carregamento " + c.Num) { FontSize = 16, FontWeight = FontWeights.SemiBold });
            doc.Blocks.Add(titulo);

            doc.Blocks.Add(new Paragraph(new Run(
                "Placa " + (c.Placas.Count > 0 ? string.Join(", ", c.Placas) : "—")
                + "    Emissão " + (c.Datas.Count > 0 ? string.Join(", ", c.Datas) : "—")
                + "    NFs/Clientes " + c.Nfs.Count + "/" + c.Clientes
                + "    Vol. fechados " + c.VlTot
                + "    Quebra/Frac. " + c.QuebrasMistas + "/" + c.Fracionados
                + "    Peso " + Formatacao.Fmt(c.Peso) + " kg"
                + "    Valor R$ " + Formatacao.Fmt(c.Valor)))
            { Margin = new Thickness(0, 0, 0, 8), FontSize = 10 });

            foreach (var a in c.Avisos)
                doc.Blocks.Add(new Paragraph(new Run("! " + a)) { Foreground = Brushes.SaddleBrown, Margin = new Thickness(0, 0, 0, 2), FontSize = 10 });

            doc.Blocks.Add(new Paragraph(new Run("ROMANEIO DE SEPARAÇÃO")) { FontWeight = FontWeights.SemiBold, FontSize = 10, Margin = new Thickness(0, 6, 0, 2) });
            doc.Blocks.Add(TabelaRomaneio(c));
            doc.Blocks.Add(new Paragraph(new Run("QUEBRA = volume com sobras de lotes diferentes.    FRAC. = sobra de um lote só."))
            { FontSize = 9, Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 0) });

            if (!detalhe) continue;
            doc.Blocks.Add(new Paragraph(new Run("DETALHE POR NOTA FISCAL / CLIENTE")) { FontWeight = FontWeights.SemiBold, FontSize = 10, Margin = new Thickness(0, 10, 0, 4) });
            foreach (var nf in c.Nfs)
            {
                var meta = string.Join(" – ", new[] { nf.Bairro, nf.Cidade }.Where(s => !string.IsNullOrWhiteSpace(s)));
                doc.Blocks.Add(new Paragraph(new Run(
                    "NF " + nf.Numero + "    " + nf.CodCliente + " · " + nf.Cliente
                    + (meta.Length > 0 ? "    " + meta : "")
                    + "    R$ " + Formatacao.Fmt(nf.Valor)))
                { FontWeight = FontWeights.SemiBold, FontSize = 10, Margin = new Thickness(0, 6, 0, 2) });
                doc.Blocks.Add(TabelaItens(nf));
            }
        }

        return doc;
    }

    internal static void EnviarImpressora(IReadOnlyList<CargaConferencia> cargas, bool detalhe, bool paisagem)
    {
        var dlg = new PrintDialog
        {
            PrintTicket = new System.Printing.PrintTicket
            {
                PageMediaSize = new System.Printing.PageMediaSize(System.Printing.PageMediaSizeName.ISOA4),
                PageOrientation = paisagem
                    ? System.Printing.PageOrientation.Landscape
                    : System.Printing.PageOrientation.Portrait
            }
        };
        if (dlg.ShowDialog() != true) return;
        var area = new Size(
            dlg.PrintableAreaWidth > 1 ? dlg.PrintableAreaWidth : (paisagem ? 1122.52 : 793.70),
            dlg.PrintableAreaHeight > 1 ? dlg.PrintableAreaHeight : (paisagem ? 793.70 : 1122.52));
        var doc = CriarDocumento(cargas, detalhe, area);
        dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Confere");
    }

    static Table TabelaRomaneio(CargaConferencia c)
    {
        var table = NovaTabela(90, 180, 160, 220, 90);
        var grupo = new TableRowGroup();
        grupo.Rows.Add(LinhaCabeca("Código", "Produto", "Volumes fechados", "Quebra / fracionado", "Total"));
        foreach (var p in c.Linhas)
        {
            var total = p.Upv > 0 ? Formatacao.DecTxt(p.Qtd, p.Fd, p.Un) : Formatacao.FmtQ(p.VolNf) + " " + p.TipoNf;
            total += "\n" + Formatacao.FmtQ(p.Qtd) + " un";
            var emb = p.Upv > 0 ? (p.Fd > 1 ? " " + p.Fd + "×" + p.Un : " cx " + p.Un) : "";
            grupo.Rows.Add(Linha(
                p.Codigo,
                p.Nome + emb,
                TextoFechados(p),
                TextoQuebras(p),
                total));
        }
        grupo.Rows.Add(Linha(
            c.Linhas.Count + " produtos · " + Formatacao.FmtQ(c.Qtd) + " un",
            "",
            c.VlTot + " volumes fechados",
            c.QuebrasMistas + " quebra(s) · " + c.Fracionados + " fracionado(s)",
            ""));
        table.RowGroups.Add(grupo);
        return table;
    }

    static Table TabelaItens(NotaFiscal nf)
    {
        var table = NovaTabela(70, 200, 80, 70, 80, 110);
        var grupo = new TableRowGroup();
        grupo.Rows.Add(LinhaCabeca("Código", "Produto", "Lote", "Qtd un", "Vol. NF", "Separar"));
        foreach (var i in nf.Itens)
        {
            grupo.Rows.Add(Linha(
                i.Codigo,
                i.Nome,
                i.Lote ?? "",
                Formatacao.FmtQ(i.Qtd),
                i.Volumes == null ? "?" : Formatacao.FmtQ(i.Volumes.Value) + " " + i.TipoVol,
                i.Upv > 0 ? Formatacao.DecTxt(i.Qtd, i.Fd, i.Un) : "—"));
        }
        table.RowGroups.Add(grupo);
        return table;
    }

    static string TextoFechados(LinhaRomaneio p)
    {
        if (p.Upv <= 0) return Formatacao.FmtQ(p.VolNf) + " " + p.TipoNf + " (NF)";
        var lotes = p.Lotes.Values.Where(l => l.Vl > 0).ToList();
        if (lotes.Count == 0) return "—";
        var und = p.Fd > 1 ? "VL" : "CX";
        return string.Join("   ", lotes.Select(l => l.Lote + "  " + l.Vl + " " + und));
    }

    static string TextoQuebras(LinhaRomaneio p)
    {
        if (p.Quebras.Count == 0) return "—";
        return string.Join("\n", p.Quebras.Select(q =>
        {
            var tag = q.Misto ? "QUEBRA" : "FRAC.";
            var partes = string.Join(" + ", q.Partes.Select(x => x.Lote + " " + Formatacao.DecTxt(x.Qtd, q.Fd, q.Un, false)));
            var fim = q.Misto && q.Completo ? " = 1 VL" : "";
            return tag + " NF " + q.Nf + " " + Formatacao.Curto(q.Cliente) + ": " + partes + fim;
        }));
    }

    static Table NovaTabela(params int[] larguras)
    {
        var table = new Table { CellSpacing = 0, FontSize = 9 };
        foreach (var w in larguras)
            table.Columns.Add(new TableColumn { Width = new GridLength(w) });
        return table;
    }

    static TableRow LinhaCabeca(params string[] textos)
    {
        var row = new TableRow { Background = Brushes.WhiteSmoke };
        foreach (var t in textos) row.Cells.Add(Celula(t, true));
        return row;
    }

    static TableRow Linha(params string[] textos)
    {
        var row = new TableRow();
        foreach (var t in textos) row.Cells.Add(Celula(t, false));
        return row;
    }

    static TableCell Celula(string texto, bool cabeca)
    {
        var p = new Paragraph { Margin = new Thickness(1), FontWeight = cabeca ? FontWeights.SemiBold : FontWeights.Normal, FontSize = cabeca ? 8 : 9 };
        var primeiro = true;
        foreach (var linha in texto.Split('\n'))
        {
            if (!primeiro) p.Inlines.Add(new LineBreak());
            p.Inlines.Add(new Run(linha));
            primeiro = false;
        }
        return new TableCell(p)
        {
            Padding = new Thickness(2, 1, 2, 1),
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 0.4)
        };
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Conferencia.Core;

namespace ConferenciaCarregamento;

sealed class Paleta
{
    public required Brush Bg { get; init; }
    public required Brush Surface { get; init; }
    public required Brush Ink { get; init; }
    public required Brush Muted { get; init; }
    public required Brush Line { get; init; }
    public required Brush Accent { get; init; }
    public required Brush AccentSoft { get; init; }
    public required Brush Warn { get; init; }
    public required Brush WarnSoft { get; init; }
    public required Brush Bad { get; init; }

    public static Paleta Ler(FrameworkElement e)
    {
        Brush R(string k) => (Brush)e.FindResource(k);
        return new Paleta
        {
            Bg = R("Bg"),
            Surface = R("Surface"),
            Ink = R("Ink"),
            Muted = R("Muted"),
            Line = R("Line"),
            Accent = R("Accent"),
            AccentSoft = R("AccentSoft"),
            Warn = R("Warn"),
            WarnSoft = R("WarnSoft"),
            Bad = R("Bad")
        };
    }
}

static class TelaRomaneio
{
    static readonly FontFamily Mono = new("Consolas");

    public static UIElement Cartao(CargaConferencia c, Paleta p, bool mostrarDetalhe, out FrameworkElement detalhe)
    {
        var miolo = new StackPanel();
        miolo.Children.Add(Cabecalho(c, p));
        if (c.Avisos.Count > 0) miolo.Children.Add(Avisos(c, p));
        miolo.Children.Add(Secao("Romaneio de separação", p));
        miolo.Children.Add(Tabela(c, p));
        miolo.Children.Add(Legenda(p));
        detalhe = Detalhe(c, p, mostrarDetalhe);
        miolo.Children.Add(detalhe);

        var borda = new Border
        {
            Margin = new Thickness(0, 28, 0, 0),
            Background = p.Surface,
            BorderBrush = p.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = miolo
        };
        borda.SizeChanged += (_, _) =>
            borda.Clip = new RectangleGeometry(new Rect(0, 0, borda.ActualWidth, borda.ActualHeight), 10, 10);
        return borda;
    }

    static UIElement Cabecalho(CargaConferencia c, Paleta p)
    {
        var faixa = new WrapPanel { Margin = new Thickness(18, 16, 18, 8) };
        faixa.Children.Add(Kv("Carregamento", c.Num, p, 20));
        faixa.Children.Add(Kv("Placa", c.Placas.Count > 0 ? string.Join(", ", c.Placas) : "—", p, 15));
        faixa.Children.Add(Kv("Emissão", c.Datas.Count > 0 ? string.Join(", ", c.Datas) : "—", p, 15));
        faixa.Children.Add(Kv("NFs / Clientes", c.Nfs.Count + " / " + c.Clientes, p, 15));
        faixa.Children.Add(Kv("Vol. fechados", c.VlTot.ToString(), p, 15));
        faixa.Children.Add(Kv("Quebra / Frac.", c.QuebrasMistas + " / " + c.Fracionados, p, 15));
        faixa.Children.Add(Kv("Peso bruto", Formatacao.Fmt(c.Peso) + " kg", p, 15));
        faixa.Children.Add(Kv("Valor", "R$ " + Formatacao.Fmt(c.Valor), p, 15));
        return new Border
        {
            BorderBrush = p.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = faixa
        };
    }

    static StackPanel Kv(string rotulo, string valor, Paleta p, double tamanho)
    {
        var s = new StackPanel { Margin = new Thickness(0, 0, 28, 8) };
        s.Children.Add(new TextBlock
        {
            Text = rotulo.ToUpperInvariant(),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = p.Muted
        });
        s.Children.Add(new TextBlock
        {
            Text = valor,
            FontSize = tamanho,
            FontWeight = tamanho >= 20 ? FontWeights.SemiBold : FontWeights.Normal,
            FontFamily = Mono,
            Foreground = p.Ink,
            Margin = new Thickness(0, 1, 0, 0)
        });
        return s;
    }

    static Border Avisos(CargaConferencia c, Paleta p)
    {
        var box = new StackPanel { Margin = new Thickness(18, 8, 18, 8) };
        foreach (var a in c.Avisos)
            box.Children.Add(new TextBlock
            {
                Text = "⚠ " + a,
                Foreground = p.Warn,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2)
            });
        return new Border { Background = p.WarnSoft, Child = box };
    }

    static TextBlock Secao(string titulo, Paleta p) => new()
    {
        Text = titulo.ToUpperInvariant(),
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Foreground = p.Muted,
        Margin = new Thickness(18, 18, 18, 8)
    };

    static UIElement Tabela(CargaConferencia c, Paleta p)
    {
        var grid = Grade(
            [88, 240, 230, 360, 130, 52],
            p);
        var cab = new[]
        {
            Cabeca("Código", p),
            CabecaProduto(p),
            Cabeca("Volumes fechados por lote", p),
            CabecaQuebra(p),
            Cabeca("Total", p, true),
            Cabeca("Conf.", p, centro: true)
        };
        for (int i = 0; i < cab.Length; i++)
        {
            Grid.SetColumn(cab[i], i);
            grid.Children.Add(cab[i]);
        }

        for (int r = 0; r < c.Linhas.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var linha = c.Linhas[r];
            var hasq = linha.Quebras.Any(q => q.Misto);
            UIElement[] cels =
            [
                Celula(MonoTexto(linha.Codigo, p), p, hasq),
                Celula(NomeProduto(linha, p), p, hasq),
                Celula(Fechados(linha, p), p, hasq),
                Celula(Quebras(linha, p), p, hasq),
                Celula(Total(linha, p), p, hasq),
                Celula(Caixa(p), p, hasq)
            ];
            for (int i = 0; i < cels.Length; i++)
            {
                Grid.SetRow(cels[i], r + 1);
                Grid.SetColumn(cels[i], i);
                grid.Children.Add(cels[i]);
            }
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        int fr = c.Linhas.Count + 1;
        var rodape = new (UIElement El, int Col, int Span)[]
        {
            (Rodape(c.Linhas.Count + " produtos · " + Formatacao.FmtQ(c.Qtd) + " un", p), 0, 2),
            (Rodape(c.VlTot + " volumes fechados", p), 2, 1),
            (Rodape(c.QuebrasMistas + " quebra(s) de lote · " + c.Fracionados + " fracionado(s)", p), 3, 1),
            (Rodape("", p), 4, 2)
        };
        foreach (var (el, col, span) in rodape)
        {
            Grid.SetRow(el, fr);
            Grid.SetColumn(el, col);
            Grid.SetColumnSpan(el, span);
            grid.Children.Add(el);
        }

        return RolagemHorizontal(grid, new Thickness(18, 0, 18, 0));
    }

    static Grid Grade(int[] larguras, Paleta p)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        foreach (var w in larguras)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        return grid;
    }

    static Border Cabeca(string texto, Paleta p, bool direita = false, bool centro = false)
    {
        var tb = new TextBlock
        {
            Text = texto.ToUpperInvariant(),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = p.Muted,
            TextWrapping = TextWrapping.Wrap
        };
        if (direita) tb.TextAlignment = TextAlignment.Right;
        if (centro) tb.TextAlignment = TextAlignment.Center;
        return new Border
        {
            BorderBrush = p.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 6, 8, 6),
            Child = tb
        };
    }

    static Border CabecaProduto(Paleta p)
    {
        var tb = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        tb.Inlines.Add(new System.Windows.Documents.Run("PRODUTO ") { Foreground = p.Muted });
        tb.Inlines.Add(new System.Windows.Documents.Run("(FD×UN)") { Foreground = p.Muted, FontWeight = FontWeights.Normal });
        return new Border
        {
            BorderBrush = p.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 6, 8, 6),
            Child = tb
        };
    }

    static Border CabecaQuebra(Paleta p)
    {
        var tb = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        tb.Inlines.Add(new System.Windows.Documents.Run("QUEBRA DE LOTE / FRACIONADO ") { Foreground = p.Muted });
        tb.Inlines.Add(new System.Windows.Documents.Run("(FARDOS + SOLTAS POR LOTE)") { Foreground = p.Muted, FontWeight = FontWeights.Normal });
        return new Border
        {
            BorderBrush = p.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 6, 8, 6),
            Child = tb
        };
    }

    static Border Celula(UIElement conteudo, Paleta p, bool hasq) => new()
    {
        Background = hasq ? p.WarnSoft : Brushes.Transparent,
        BorderBrush = p.Line,
        BorderThickness = new Thickness(0, 0, 0, 1),
        Padding = new Thickness(8, 6, 8, 6),
        Child = conteudo
    };

    static TextBlock MonoTexto(string texto, Paleta p) => new()
    {
        Text = texto,
        FontFamily = Mono,
        Foreground = p.Ink,
        FontSize = 13,
        VerticalAlignment = VerticalAlignment.Center
    };

    static TextBlock NomeProduto(LinhaRomaneio linha, Paleta p)
    {
        var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = p.Ink };
        tb.Inlines.Add(new System.Windows.Documents.Run(linha.Nome));
        var emb = linha.Upv > 0 ? (linha.Fd > 1 ? "  " + linha.Fd + "×" + linha.Un : "  cx " + linha.Un) : "";
        if (emb.Length > 0)
            tb.Inlines.Add(new System.Windows.Documents.Run(emb) { Foreground = p.Muted, FontFamily = Mono, FontSize = 10.5 });
        return tb;
    }

    static UIElement Fechados(LinhaRomaneio linha, Paleta p)
    {
        if (linha.Upv <= 0)
            return new TextBlock
            {
                Text = Formatacao.FmtQ(linha.VolNf) + " " + linha.TipoNf + " (NF)",
                Foreground = p.Muted,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            };

        var lotes = linha.Lotes.Values.Where(l => l.Vl > 0).ToList();
        if (lotes.Count == 0)
            return new TextBlock { Text = "—", Foreground = p.Muted, FontSize = 13 };

        var wrap = new WrapPanel();
        var und = linha.Fd > 1 ? "VL" : "CX";
        foreach (var l in lotes)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 2) };
            item.Children.Add(Selo(l.Lote, p));
            item.Children.Add(new TextBlock
            {
                Text = l.Vl + " " + und,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = p.Ink,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            });
            wrap.Children.Add(item);
        }
        return wrap;
    }

    static UIElement Quebras(LinhaRomaneio linha, Paleta p)
    {
        if (linha.Quebras.Count == 0)
            return new TextBlock { Text = "—", Foreground = p.Muted, FontSize = 13 };

        var box = new StackPanel();
        foreach (var q in linha.Quebras)
        {
            var faixa = new WrapPanel { Margin = new Thickness(0, 0, 0, 3) };
            faixa.Children.Add(Etiqueta(q.Misto ? "QUEBRA" : "FRAC.", q.Misto, p));
            faixa.Children.Add(Mudo("NF " + q.Nf + " " + Formatacao.Curto(q.Cliente) + ": ", p));
            var primeiro = true;
            foreach (var parte in q.Partes)
            {
                if (!primeiro) faixa.Children.Add(Mudo(" + ", p));
                primeiro = false;
                faixa.Children.Add(Selo(parte.Lote, p));
                faixa.Children.Add(new TextBlock
                {
                    Text = Formatacao.DecTxt(parte.Qtd, q.Fd, q.Un, false),
                    FontWeight = FontWeights.Bold,
                    Foreground = p.Ink,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 0, 0)
                });
            }
            if (q.Misto && q.Completo) faixa.Children.Add(Mudo(" = 1 VL", p));
            box.Children.Add(faixa);
        }
        return box;
    }

    static StackPanel Total(LinhaRomaneio linha, Paleta p)
    {
        var s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        s.Children.Add(new TextBlock
        {
            Text = linha.Upv > 0 ? Formatacao.DecTxt(linha.Qtd, linha.Fd, linha.Un) : Formatacao.FmtQ(linha.VolNf) + " " + linha.TipoNf,
            FontWeight = FontWeights.Bold,
            Foreground = p.Ink,
            FontSize = 13,
            TextAlignment = TextAlignment.Right
        });
        s.Children.Add(new TextBlock
        {
            Text = Formatacao.FmtQ(linha.Qtd) + " un",
            Foreground = p.Muted,
            FontSize = 10.5,
            TextAlignment = TextAlignment.Right
        });
        return s;
    }

    static Border Caixa(Paleta p) => new()
    {
        Width = 16,
        Height = 16,
        BorderBrush = p.Muted,
        BorderThickness = new Thickness(1.5),
        CornerRadius = new CornerRadius(3),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    static TextBlock Rodape(string texto, Paleta p) => new()
    {
        Text = texto,
        FontWeight = FontWeights.SemiBold,
        FontSize = 13,
        Foreground = p.Ink,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(8, 8, 8, 4)
    };

    static WrapPanel Legenda(Paleta p)
    {
        var leg = new WrapPanel { Margin = new Thickness(18, 8, 18, 4) };
        leg.Children.Add(Etiqueta("QUEBRA", true, p));
        leg.Children.Add(Mudo(" volume montado com sobras de lotes diferentes    ", p));
        leg.Children.Add(Etiqueta("FRAC.", false, p));
        leg.Children.Add(Mudo(" sobra de um lote só, sem fechar volume.", p));
        return leg;
    }

    static FrameworkElement Detalhe(CargaConferencia c, Paleta p, bool mostrar)
    {
        var box = new StackPanel
        {
            Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 12)
        };
        box.Children.Add(Secao("Detalhe por nota fiscal / cliente", p));
        var lista = new StackPanel { Margin = new Thickness(18, 0, 18, 6) };
        foreach (var nf in c.Nfs) lista.Children.Add(Nota(nf, p));
        box.Children.Add(lista);
        return box;
    }

    static Border Nota(NotaFiscal nf, Paleta p)
    {
        var miolo = new StackPanel();
        var topo = new DockPanel { Background = p.Bg, LastChildFill = true };
        var valor = new TextBlock
        {
            Text = "R$ " + Formatacao.Fmt(nf.Valor),
            FontFamily = Mono,
            Foreground = p.Ink,
            FontSize = 13,
            Margin = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(valor, Dock.Right);
        topo.Children.Add(valor);
        var esq = new WrapPanel { Margin = new Thickness(10, 5, 10, 5) };
        esq.Children.Add(new TextBlock
        {
            Text = "NF " + nf.Numero,
            FontFamily = Mono,
            FontWeight = FontWeights.Bold,
            Foreground = p.Ink,
            FontSize = 13,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        esq.Children.Add(new TextBlock
        {
            Text = (nf.CodCliente ?? "") + " · " + (nf.Cliente ?? ""),
            FontWeight = FontWeights.SemiBold,
            Foreground = p.Ink,
            FontSize = 13,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        var meta = string.Join(" – ", new[] { nf.Bairro, nf.Cidade }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (meta.Length > 0)
            esq.Children.Add(new TextBlock { Text = meta, Foreground = p.Muted, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        topo.Children.Add(esq);
        miolo.Children.Add(topo);
        miolo.Children.Add(TabelaItens(nf, p));
        if (nf.Quebras.Count > 0) miolo.Children.Add(QuebrasNf(nf, p));

        return new Border
        {
            BorderBrush = p.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 8),
            ClipToBounds = true,
            Child = miolo
        };
    }

    static UIElement TabelaItens(NotaFiscal nf, Paleta p)
    {
        var grid = Grade([88, 280, 120, 80, 100, 140], p);
        string[] titulos = ["Código", "Produto", "Lote", "Qtd un", "Vol. NF", "Separar"];
        bool[] direita = [false, false, false, true, true, true];
        for (int i = 0; i < titulos.Length; i++)
        {
            var h = Cabeca(titulos[i], p, direita[i]);
            Grid.SetColumn(h, i);
            grid.Children.Add(h);
        }
        for (int r = 0; r < nf.Itens.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var i = nf.Itens[r];
            UIElement[] cels =
            [
                Celula(MonoTexto(i.Codigo, p), p, false),
                Celula(new TextBlock { Text = i.Nome, Foreground = p.Ink, FontSize = 12, TextWrapping = TextWrapping.Wrap }, p, false),
                Celula(Selo(i.Lote ?? "S/ LOTE", p), p, false),
                Celula(Direita(Formatacao.FmtQ(i.Qtd), p, false), p, false),
                Celula(Direita(i.Volumes == null ? "?" : Formatacao.FmtQ(i.Volumes.Value) + " " + i.TipoVol, p, true), p, false),
                Celula(Direita(i.Upv > 0 ? Formatacao.DecTxt(i.Qtd, i.Fd, i.Un) : "—", p, false, true), p, false)
            ];
            for (int c = 0; c < cels.Length; c++)
            {
                Grid.SetRow(cels[c], r + 1);
                Grid.SetColumn(cels[c], c);
                grid.Children.Add(cels[c]);
            }
        }
        return RolagemHorizontal(grid, new Thickness(0));
    }

    static ScrollViewer RolagemHorizontal(UIElement conteudo, Thickness margem)
    {
        var rolagem = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = margem,
            Content = conteudo
        };
        rolagem.PreviewMouseWheel += EncaminharRoda;
        return rolagem;
    }

    internal static void EncaminharRoda(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) return;
        if (sender is not DependencyObject origem) return;
        DependencyObject? pai = VisualTreeHelper.GetParent(origem);
        while (pai != null && pai is not ScrollViewer)
            pai = VisualTreeHelper.GetParent(pai);
        if (pai is not UIElement pagina) return;
        e.Handled = true;
        var ev = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = pagina
        };
        pagina.RaiseEvent(ev);
    }

    static TextBlock Direita(string texto, Paleta p, bool mudo, bool negrito = false) => new()
    {
        Text = texto,
        Foreground = mudo ? p.Muted : p.Ink,
        FontWeight = negrito ? FontWeights.Bold : FontWeights.Normal,
        FontSize = 12,
        TextAlignment = TextAlignment.Right,
        FontFamily = Mono
    };

    static Border QuebrasNf(NotaFiscal nf, Paleta p)
    {
        var box = new StackPanel { Margin = new Thickness(10, 6, 10, 6) };
        foreach (var q in nf.Quebras)
        {
            var faixa = new WrapPanel { Margin = new Thickness(0, 0, 0, 3) };
            faixa.Children.Add(Etiqueta(q.Misto ? "QUEBRA" : "FRAC.", q.Misto, p));
            faixa.Children.Add(new TextBlock
            {
                Text = q.Codigo + " " + q.Nome + ": ",
                FontWeight = FontWeights.Bold,
                Foreground = p.Ink,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            var primeiro = true;
            foreach (var parte in q.Partes)
            {
                if (!primeiro) faixa.Children.Add(Mudo(" + ", p));
                primeiro = false;
                faixa.Children.Add(Selo(parte.Lote, p));
                faixa.Children.Add(new TextBlock
                {
                    Text = Formatacao.DecTxt(parte.Qtd, q.Fd, q.Un, false),
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = p.Ink,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 0, 0)
                });
            }
            if (q.Misto && q.Completo) faixa.Children.Add(Mudo(" = 1 VL", p));
            box.Children.Add(faixa);
        }
        return new Border { Background = p.WarnSoft, Child = box };
    }

    static Border Selo(string lote, Paleta p) => new()
    {
        Background = p.AccentSoft,
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(4, 0, 4, 0),
        Margin = new Thickness(0, 1, 2, 1),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = lote,
            Foreground = p.Accent,
            FontFamily = Mono,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11.5
        }
    };

    static Border Etiqueta(string texto, bool quebra, Paleta p) => new()
    {
        Background = quebra ? p.Bad : p.Line,
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(5, 1, 5, 1),
        Margin = new Thickness(0, 1, 4, 1),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = texto,
            Foreground = quebra ? p.Surface : p.Ink,
            FontSize = 9.5,
            FontWeight = FontWeights.Bold
        }
    };

    static TextBlock Mudo(string texto, Paleta p) => new()
    {
        Text = texto,
        Foreground = p.Muted,
        FontSize = 12,
        VerticalAlignment = VerticalAlignment.Center
    };
}

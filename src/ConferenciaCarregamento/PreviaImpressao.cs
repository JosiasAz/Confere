using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Conferencia.Core;

namespace ConferenciaCarregamento;

sealed class PreviaImpressao : Window
{
    readonly IReadOnlyList<CargaConferencia> _cargas;
    readonly bool _detalhe;
    readonly ScrollViewer _rolagem;
    readonly StackPanel _folhas;
    readonly TextBlock _contador;
    readonly Button _retrato;
    readonly Button _paisagem;
    readonly List<DocumentPage> _paginas = [];
    FlowDocument? _documento;
    bool _deitado;
    double _larguraFolha = 794;

    public PreviaImpressao(IReadOnlyList<CargaConferencia> cargas, bool detalhe)
    {
        _cargas = cargas;
        _detalhe = detalhe;
        Title = "Visualização da impressão — Confere";
        Width = 980;
        Height = 760;
        MinWidth = 720;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        SetResourceReference(BackgroundProperty, "Bg");
        SetResourceReference(ForegroundProperty, "Ink");

        _folhas = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        _rolagem = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = new SolidColorBrush(Color.FromRgb(0xD5, 0xDB, 0xD8)),
            Padding = new Thickness(24, 24, 24, 24),
            Content = _folhas
        };
        _rolagem.SizeChanged += (_, _) => AjustarEscala();

        _contador = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 16, 0), FontSize = 13 };
        _contador.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        _retrato = Botao("Retrato", (_, _) => Montar(false));
        _paisagem = Botao("Paisagem", (_, _) => Montar(true));
        var imprimir = Botao("Imprimir", (_, _) => Impressao.EnviarImpressora(_cargas, _detalhe, _deitado));
        var fechar = Botao("Fechar", (_, _) => Close());
        fechar.Margin = new Thickness(0);

        var barra = new Border { Padding = new Thickness(16, 12, 16, 12) };
        barra.SetResourceReference(Border.BackgroundProperty, "Surface");
        barra.SetResourceReference(Border.BorderBrushProperty, "Line");
        barra.BorderThickness = new Thickness(0, 0, 0, 1);
        var linha = new DockPanel();
        var acoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        acoes.Children.Add(_contador);
        acoes.Children.Add(_retrato);
        acoes.Children.Add(_paisagem);
        acoes.Children.Add(imprimir);
        acoes.Children.Add(fechar);
        DockPanel.SetDock(acoes, Dock.Right);
        linha.Children.Add(acoes);
        linha.Children.Add(new TextBlock
        {
            Text = "Visualização da impressão",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        barra.Child = linha;

        var grade = new Grid();
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grade.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_rolagem, 1);
        grade.Children.Add(barra);
        grade.Children.Add(_rolagem);
        Content = grade;

        Montar(false);
    }

    void Montar(bool paisagem)
    {
        _deitado = paisagem;
        var tamanho = paisagem ? new Size(1122.52, 793.70) : new Size(793.70, 1122.52);
        SoltarPaginas();
        _documento = Impressao.CriarDocumento(_cargas, _detalhe, tamanho);
        var paginador = ((IDocumentPaginatorSource)_documento).DocumentPaginator;
        paginador.PageSize = tamanho;
        paginador.ComputePageCount();
        var total = Math.Max(0, paginador.PageCount);
        _contador.Text = total switch
        {
            0 => "Nenhuma página",
            1 => "1 página",
            _ => total + " páginas"
        };
        _folhas.Children.Clear();
        _larguraFolha = tamanho.Width;
        for (int i = 0; i < total; i++)
        {
            var pagina = paginador.GetPage(i);
            _paginas.Add(pagina);
            _folhas.Children.Add(Folha(pagina));
        }
        Marcar(_retrato, !paisagem);
        Marcar(_paisagem, paisagem);
        Dispatcher.BeginInvoke(AjustarEscala);
    }

    static Border Folha(DocumentPage pagina)
    {
        var largura = Math.Max(1, pagina.Size.Width);
        var altura = Math.Max(1, pagina.Size.Height);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(largura),
            (int)Math.Ceiling(altura),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(pagina.Visual);
        bitmap.Freeze();

        return new Border
        {
            Width = largura,
            Height = altura,
            Margin = new Thickness(0, 0, 0, 24),
            Background = Brushes.White,
            Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 1, Opacity = 0.28, Color = Colors.Black },
            Child = new Image { Source = bitmap, Width = largura, Height = altura, Stretch = Stretch.Fill }
        };
    }

    void AjustarEscala()
    {
        if (_rolagem.ViewportWidth <= 1 || _larguraFolha <= 1) return;
        var escala = Math.Max(0.35, Math.Min(1.2, (_rolagem.ViewportWidth - 48) / _larguraFolha));
        _folhas.LayoutTransform = new ScaleTransform(escala, escala);
    }

    void SoltarPaginas()
    {
        foreach (var pagina in _paginas) pagina.Dispose();
        _paginas.Clear();
    }

    void Marcar(Button botao, bool ativo)
    {
        botao.SetResourceReference(BackgroundProperty, ativo ? "Accent" : "Surface");
        botao.SetResourceReference(ForegroundProperty, ativo ? "Bg" : "Ink");
        botao.SetResourceReference(BorderBrushProperty, ativo ? "Accent" : "Line");
    }

    static Button Botao(string texto, RoutedEventHandler clique)
    {
        var botao = new Button
        {
            Content = texto,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand,
            FontSize = 13
        };
        botao.Click += clique;
        return botao;
    }

    protected override void OnClosed(EventArgs e)
    {
        SoltarPaginas();
        base.OnClosed(e);
    }
}

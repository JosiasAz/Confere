using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Conferencia.Core;
using Microsoft.Win32;

namespace ConferenciaCarregamento;

public partial class MainWindow : Window
{
    readonly Dictionary<string, NotaFiscal> _nfs = [];
    readonly List<(string Nome, int Qtd)> _arquivos = [];
    readonly ObservableCollection<ParametroProduto> _params = [];
    readonly List<FrameworkElement> _detalhes = [];
    readonly DispatcherTimer _toast = new() { Interval = TimeSpan.FromSeconds(2.2) };
    List<CargaConferencia> _cargas = [];

    public MainWindow()
    {
        InitializeComponent();
        _toast.Tick += (_, _) =>
        {
            Toast.Visibility = Visibility.Collapsed;
            _toast.Stop();
        };
        foreach (var p in ParametroStore.Carregar()) _params.Add(p);
        GradeParams.ItemsSource = _params;
        MarcarTema();
        Render();
    }

    void Tema_Click(object sender, RoutedEventArgs e)
    {
        var escuro = Theme.Atual != TemaVisual.Escuro;
        Theme.Aplicar(Application.Current, escuro ? TemaVisual.Escuro : TemaVisual.Claro, persistir: true);
        MarcarTema();
        Render();
    }

    void MarcarTema()
    {
        var escuro = Theme.Atual == TemaVisual.Escuro;
        IconeSol.Visibility = escuro ? Visibility.Visible : Visibility.Collapsed;
        IconeLua.Visibility = escuro ? Visibility.Collapsed : Visibility.Visible;
        BtnTema.ToolTip = escuro ? "Tema claro" : "Tema escuro";
    }

    void Render()
    {
        _cargas = ConferenciaCalculo.Montar(_nfs.Values, _params.ToList());
        var tem = _cargas.Count > 0;
        BtnCopiar.IsEnabled = tem;
        BtnCsv.IsEnabled = tem;
        BtnImprimir.IsEnabled = tem;
        BtnLimpar.IsEnabled = tem;

        PainelArquivos.Children.Clear();
        foreach (var f in _arquivos)
            PainelArquivos.Children.Add(Chip(f.Nome + " · " + f.Qtd + " NF"));

        Saida.Children.Clear();
        _detalhes.Clear();
        if (!tem)
        {
            Saida.Children.Add(new TextBlock
            {
                Text = "Nenhuma nota carregada ainda. Solte o PDF da DANFE acima, ou clique em “Ver exemplo” para ver como fica o resultado.",
                Foreground = (Brush)FindResource("Muted"),
                Margin = new Thickness(0, 28, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        var paleta = Paleta.Ler(this);
        var detalhe = ChkDet.IsChecked == true;
        foreach (var c in _cargas)
        {
            var cartao = TelaRomaneio.Cartao(c, paleta, detalhe, out var bloco);
            _detalhes.Add(bloco);
            Saida.Children.Add(cartao);
        }
    }

    Border Chip(string texto) => new()
    {
        Background = (Brush)FindResource("AccentSoft"),
        CornerRadius = new CornerRadius(99),
        Padding = new Thickness(8, 2, 8, 2),
        Margin = new Thickness(0, 10, 6, 0),
        Child = new TextBlock
        {
            Text = texto,
            Foreground = (Brush)FindResource("Accent"),
            FontSize = 12
        }
    };

    void Exemplo_Click(object sender, RoutedEventArgs e)
    {
        var ex = ExemploNotas.Carregar();
        foreach (var n in ex) _nfs[n.Numero] = n;
        if (_arquivos.All(f => f.Nome != "exemplo")) _arquivos.Add(("exemplo", ex.Count));
        Msg.Visibility = Visibility.Collapsed;
        Render();
    }

    void Limpar_Click(object sender, RoutedEventArgs e)
    {
        _nfs.Clear();
        _arquivos.Clear();
        Msg.Visibility = Visibility.Collapsed;
        Render();
    }

    void Detalhe_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var v = ChkDet.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        foreach (var d in _detalhes) d.Visibility = v;
    }

    void Copiar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, Exportacao.Tsv(_cargas)), true);
            MostrarToast("Copiado. Cole no Excel com Ctrl+V.");
        }
        catch
        {
            MostrarToast("Não foi possível copiar.");
        }
    }

    void Csv_Click(object sender, RoutedEventArgs e)
    {
        var nums = string.Join("_", _cargas.Select(c => c.Num));
        var dlg = new SaveFileDialog
        {
            Filter = "Planilha CSV (*.csv)|*.csv",
            FileName = "carregamento_" + nums + ".csv",
            Title = "Baixar planilha"
        };
        if (dlg.ShowDialog(this) != true) return;
        File.WriteAllText(dlg.FileName, Exportacao.Csv(_cargas), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        MostrarToast("Planilha salva.");
    }

    void Imprimir_Click(object sender, RoutedEventArgs e) =>
        Impressao.AbrirPrevia(this, _cargas, ChkDet.IsChecked == true);

    async void Zona_Click(object sender, MouseButtonEventArgs e) => await Escolher();

    async void Zona_Key(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space)) return;
        e.Handled = true;
        await Escolher();
    }

    async Task Escolher()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "DANFE PDF (*.pdf)|*.pdf",
            Multiselect = true,
            Title = "Escolher PDFs de DANFE"
        };
        if (dlg.ShowDialog(this) == true) await Carregar(dlg.FileNames);
    }

    void Janela_DragOver(object sender, DragEventArgs e)
    {
        var ok = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        if (!ok) return;
        var p = e.GetPosition(Zona);
        MarcarZona(p.X >= 0 && p.Y >= 0 && p.X <= Zona.ActualWidth && p.Y <= Zona.ActualHeight);
    }

    void Janela_DragLeave(object sender, DragEventArgs e) => MarcarZona(false);

    async void Janela_Drop(object sender, DragEventArgs e)
    {
        MarcarZona(false);
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] arquivos)
            await Carregar(arquivos);
    }

    void MarcarZona(bool sobre)
    {
        if (sobre)
        {
            Zona.BorderBrush = (Brush)FindResource("Accent");
            Zona.Background = (Brush)FindResource("AccentSoft");
        }
        else
        {
            Zona.ClearValue(Border.BorderBrushProperty);
            Zona.ClearValue(Border.BackgroundProperty);
        }
    }

    async Task Carregar(IEnumerable<string> caminhos)
    {
        var erros = new List<string>();
        var avisos = new List<string>();
        Msg.Text = "Lendo PDFs…";
        Msg.Foreground = (Brush)FindResource("Muted");
        Msg.Visibility = Visibility.Visible;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            foreach (var caminho in caminhos)
            {
                var nome = Path.GetFileName(caminho);
                if (!nome.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    erros.Add(nome + ": não é PDF");
                    continue;
                }
                try
                {
                    var bytes = await File.ReadAllBytesAsync(caminho);
                    var leitura = await Task.Run(() => DanfeParser.Ler(bytes));
                    if (leitura.Notas.Count == 0)
                    {
                        var detalhe = leitura.Avisos.FirstOrDefault() ?? "nenhuma NF com produtos encontrada (o PDF é uma DANFE?)";
                        erros.Add(nome + ": " + detalhe);
                        continue;
                    }
                    foreach (var n in leitura.Notas)
                    {
                        n.Arquivo = nome;
                        _nfs[n.Numero] = n;
                    }
                    _arquivos.Add((nome, leitura.Notas.Count));
                    foreach (var a in leitura.Avisos) avisos.Add(nome + ": " + a);
                }
                catch (Exception ex)
                {
                    erros.Add(nome + ": não foi possível ler (" + ex.Message + ")");
                }
            }
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        var msgs = erros.Concat(avisos).ToList();
        if (msgs.Count > 0)
        {
            Msg.Text = string.Join(Environment.NewLine, msgs);
            Msg.Foreground = (Brush)FindResource(erros.Count > 0 ? "Bad" : "Warn");
            Msg.Visibility = Visibility.Visible;
        }
        else Msg.Visibility = Visibility.Collapsed;
        Render();
    }

    void Grade_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        Dispatcher.BeginInvoke(() =>
        {
            ParametroStore.Salvar(_params);
            Render();
        });
    }

    void Grade_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        TelaRomaneio.EncaminharRoda(sender, e);

    void RemoverParam_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ParametroProduto p) return;
        _params.Remove(p);
        ParametroStore.Salvar(_params);
        Render();
    }

    void AddParam_Click(object sender, RoutedEventArgs e)
    {
        _params.Add(ParametroProduto.Novo("", "", 8, 48));
        ParametroStore.Salvar(_params);
        GradeParams.ScrollIntoView(_params[^1]);
    }

    void ResetParam_Click(object sender, RoutedEventArgs e)
    {
        _params.Clear();
        foreach (var p in ParametroProduto.Padrao()) _params.Add(p);
        ParametroStore.Salvar(_params);
        Render();
    }

    void MostrarToast(string texto)
    {
        ToastTexto.Text = texto;
        Toast.Visibility = Visibility.Visible;
        _toast.Stop();
        _toast.Start();
    }
}

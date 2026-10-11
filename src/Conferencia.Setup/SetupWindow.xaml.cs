using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Conferencia.Setup;

public enum ModoAssistente
{
    Instalar,
    Desinstalar
}

public partial class SetupWindow : Window
{
    readonly ModoAssistente _modo;
    readonly TextBlock[] _etapas;
    int _passo;
    bool _ocupado;
    bool _desinstalando;

    public SetupWindow(ModoAssistente modo)
    {
        _modo = modo;
        InitializeComponent();
        Logo.Source = new BitmapImage(new Uri("pack://application:,,,/assets/logo.jpg"));
        Icon = Logo.Source;
        TextoLicenca.Text = Licenca.Texto;
        TextoPasta.Text = Instalacao.PastaDestino;
        _etapas = [Etapa0, Etapa1, Etapa2, Etapa3, Etapa4];
        if (modo == ModoAssistente.Desinstalar)
        {
            Title = "Desinstalar — Confere";
            TextoMarca.Text = "Remoção do programa";
            TextoRemocao.Text = Instalacao.JaInstalado
                ? "Pasta: " + Instalacao.PastaDestino
                : "Não há uma instalação deste usuário para remover.";
            Mostrar(PaginaRemover);
            BtnVoltar.Visibility = Visibility.Collapsed;
            BtnAvancar.Content = Instalacao.JaInstalado ? "Desinstalar" : "Fechar";
        }
        else
        {
            if (Instalacao.PastaPayload == null)
            {
                PaginaBoasVindas.Children.Add(new TextBlock
                {
                    Margin = new Thickness(0, 16, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A12D2D")!),
                    Text = "A pasta payload não está ao lado deste instalador. Execute o ConferenciaSetup.exe da pasta Instalador."
                });
                BtnAvancar.IsEnabled = false;
            }
            Mostrar(PaginaBoasVindas);
            BtnVoltar.Visibility = Visibility.Collapsed;
        }
    }

    void Mostrar(UIElement pagina)
    {
        PaginaBoasVindas.Visibility = Visibility.Collapsed;
        PaginaTermos.Visibility = Visibility.Collapsed;
        PaginaOpcoes.Visibility = Visibility.Collapsed;
        PaginaProgresso.Visibility = Visibility.Collapsed;
        PaginaFim.Visibility = Visibility.Collapsed;
        PaginaRemover.Visibility = Visibility.Collapsed;
        PaginaRemovido.Visibility = Visibility.Collapsed;
        pagina.Visibility = Visibility.Visible;
    }

    void Aceite_Changed(object sender, RoutedEventArgs e)
    {
        if (PaginaTermos.Visibility == Visibility.Visible)
            BtnAvancar.IsEnabled = ChkAceite.IsChecked == true;
    }

    void Voltar_Click(object sender, RoutedEventArgs e)
    {
        if (_ocupado || _modo == ModoAssistente.Desinstalar) return;
        _passo = Math.Max(0, _passo - 1);
        Ir(_passo);
    }

    async void Avancar_Click(object sender, RoutedEventArgs e)
    {
        if (_modo == ModoAssistente.Desinstalar)
        {
            await AvancarDesinstalacao();
            return;
        }

        if (_passo == 0)
        {
            _passo = 1;
            Ir(1);
            return;
        }
        if (_passo == 1)
        {
            if (ChkAceite.IsChecked != true) return;
            _passo = 2;
            Ir(2);
            return;
        }
        if (_passo == 2)
        {
            await Instalar();
            return;
        }
        if (_passo == 4)
        {
            if (ChkAbrir.IsChecked == true) Instalacao.AbrirPrograma();
            Close();
            return;
        }
        if (_passo == 99) Close();
    }

    void Ir(int passo)
    {
        _passo = passo;
        BtnVoltar.Visibility = passo is > 0 and < 3 ? Visibility.Visible : Visibility.Collapsed;
        BtnAvancar.Visibility = Visibility.Visible;
        if (passo == 0)
        {
            Mostrar(PaginaBoasVindas);
            BtnAvancar.Content = "Avançar";
            BtnAvancar.IsEnabled = Instalacao.PastaPayload != null;
        }
        else if (passo == 1)
        {
            Mostrar(PaginaTermos);
            BtnAvancar.Content = "Avançar";
            BtnAvancar.IsEnabled = ChkAceite.IsChecked == true;
        }
        else if (passo == 2)
        {
            Mostrar(PaginaOpcoes);
            BtnAvancar.Content = "Instalar";
            BtnAvancar.IsEnabled = true;
            TextoJaInstalado.Visibility = Instalacao.JaInstalado ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (passo == 3)
        {
            Mostrar(PaginaProgresso);
            BtnVoltar.Visibility = Visibility.Collapsed;
            BtnAvancar.Visibility = Visibility.Collapsed;
        }
        else
        {
            Mostrar(PaginaFim);
            BtnVoltar.Visibility = Visibility.Collapsed;
            BtnAvancar.Visibility = Visibility.Visible;
            BtnAvancar.Content = "Concluir";
            BtnAvancar.IsEnabled = true;
        }
    }

    async Task Instalar()
    {
        _ocupado = true;
        Ir(3);
        TituloProgresso.Text = "Instalando";
        ResetarEtapas();
        var menuIniciar = ChkMenu.IsChecked == true;
        var areaTrabalho = ChkArea.IsChecked == true;
        var progresso = new Progress<ProgressoInstalacao>(AplicarProgresso);
        try
        {
            await ExecutarNaThreadSta(() => Instalacao.Instalar(menuIniciar, areaTrabalho, progresso));
            await Task.Delay(450);
            _passo = 4;
            _ocupado = false;
            Ir(4);
        }
        catch (Exception ex)
        {
            _ocupado = false;
            StatusProgresso.Text = ex.Message;
            StatusProgresso.Foreground = Brushes.DarkRed;
            BtnAvancar.Visibility = Visibility.Visible;
            BtnAvancar.Content = "Fechar";
            BtnAvancar.IsEnabled = true;
            _passo = 99;
        }
    }

    async Task AvancarDesinstalacao()
    {
        if (!Instalacao.JaInstalado && !_desinstalando)
        {
            Close();
            return;
        }
        if (_passo == 8)
        {
            Close();
            return;
        }
        _ocupado = true;
        _desinstalando = true;
        BtnAvancar.Visibility = Visibility.Collapsed;
        Mostrar(PaginaProgresso);
        TituloProgresso.Text = "Removendo";
        ResetarEtapas();
        Etapa0.Text = "○  Fechando o programa";
        Etapa1.Text = "○  Apagando atalhos";
        Etapa2.Text = "○  Tirando de Aplicativos instalados";
        Etapa3.Text = "○  Removendo arquivos";
        Etapa4.Text = "○  Concluído";
        try
        {
            AplicarProgresso(new ProgressoInstalacao(15, "Fechando o programa…", 0));
            await Task.Delay(350);
            await Task.Run(Instalacao.Desinstalar);
            AplicarProgresso(new ProgressoInstalacao(55, "Apagando atalhos…", 1));
            await Task.Delay(280);
            AplicarProgresso(new ProgressoInstalacao(75, "Tirando de Aplicativos instalados…", 2));
            await Task.Delay(280);
            AplicarProgresso(new ProgressoInstalacao(92, "Removendo arquivos…", 3));
            await Task.Delay(320);
            AplicarProgresso(new ProgressoInstalacao(100, "Remoção concluída", 4));
            await Task.Delay(350);
            Mostrar(PaginaRemovido);
            BtnAvancar.Visibility = Visibility.Visible;
            BtnAvancar.Content = "Fechar";
            BtnAvancar.IsEnabled = true;
            _passo = 8;
            _ocupado = false;
        }
        catch (Exception ex)
        {
            _ocupado = false;
            StatusProgresso.Text = ex.Message;
            BtnAvancar.Visibility = Visibility.Visible;
            BtnAvancar.Content = "Fechar";
            _passo = 8;
        }
    }

    void AplicarProgresso(ProgressoInstalacao p)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, () => AplicarProgresso(p));
            return;
        }

        Barra.Value = p.Percentual;
        StatusProgresso.Text = p.Mensagem;
        for (int i = 0; i < _etapas.Length; i++)
        {
            if (i < p.Etapa)
            {
                _etapas[i].Text = "✓  " + TextoEtapa(i);
                _etapas[i].Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F6B55")!);
                _etapas[i].FontWeight = FontWeights.SemiBold;
            }
            else if (i == p.Etapa)
            {
                _etapas[i].Text = "●  " + TextoEtapa(i);
                _etapas[i].Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18221F")!);
                _etapas[i].FontWeight = FontWeights.SemiBold;
            }
        }
    }

    static Task ExecutarNaThreadSta(Action acao)
    {
        var conclusao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                acao();
                conclusao.SetResult();
            }
            catch (Exception ex)
            {
                conclusao.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Instalacao"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return conclusao.Task;
    }

    string TextoEtapa(int i)
    {
        var bruto = _etapas[i].Text;
        var corte = bruto.IndexOf("  ", StringComparison.Ordinal);
        return corte >= 0 ? bruto[(corte + 2)..] : bruto;
    }

    void ResetarEtapas()
    {
        var cinza = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5D6B66")!);
        for (int i = 0; i < _etapas.Length; i++)
        {
            _etapas[i].Foreground = cinza;
            _etapas[i].FontWeight = FontWeights.Normal;
        }
        if (_modo == ModoAssistente.Instalar)
        {
            Etapa0.Text = "○  Preparando a instalação";
            Etapa1.Text = "○  Copiando o programa";
            Etapa2.Text = "○  Criando atalhos";
            Etapa3.Text = "○  Registrando em Aplicativos instalados";
            Etapa4.Text = "○  Concluído";
        }
        Barra.Value = 0;
        StatusProgresso.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5D6B66")!);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_ocupado) e.Cancel = true;
        base.OnClosing(e);
    }
}

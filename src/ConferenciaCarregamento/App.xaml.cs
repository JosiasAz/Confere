using System.Globalization;
using System.Windows;

namespace ConferenciaCarregamento;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var pt = new CultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = pt;
        CultureInfo.DefaultThreadCurrentUICulture = pt;
        Thread.CurrentThread.CurrentCulture = pt;
        Thread.CurrentThread.CurrentUICulture = pt;

        Theme.Aplicar(this);
        base.OnStartup(e);

        var janela = new MainWindow();
        MainWindow = janela;
        janela.Show();
    }
}

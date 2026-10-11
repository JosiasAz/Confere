using System.IO;
using System.Windows;

namespace Conferencia.Setup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = e.Args;
        var silencioso = args.Any(a => a.Equals("--silencioso", StringComparison.OrdinalIgnoreCase)
            || a.Equals("/S", StringComparison.OrdinalIgnoreCase)
            || a.Equals("/silent", StringComparison.OrdinalIgnoreCase));
        var desinstalar = args.Any(a => a.Equals("--desinstalar", StringComparison.OrdinalIgnoreCase)
            || a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase));
        var instalar = args.Any(a => a.Equals("--instalar", StringComparison.OrdinalIgnoreCase));

        try
        {
            if (desinstalar && silencioso)
            {
                Instalacao.Desinstalar();
                Shutdown(0);
                return;
            }

            if (instalar && silencioso)
            {
                Instalacao.FecharPrograma();
                Instalacao.Instalar(menuIniciar: true, areaTrabalho: false, null);
                Shutdown(0);
                return;
            }
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "ConferenciaSetup.log"), ex.ToString());
            }
            catch
            {
                // o log é só um apoio se a instalação silenciosa falhar
            }
            Shutdown(1);
            return;
        }

        var janela = new SetupWindow(desinstalar ? ModoAssistente.Desinstalar : ModoAssistente.Instalar);
        MainWindow = janela;
        janela.Show();
    }
}

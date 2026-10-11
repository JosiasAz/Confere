using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Conferencia.Setup;

public readonly record struct ProgressoInstalacao(double Percentual, string Mensagem, int Etapa);

public static class Instalacao
{
    public const string Versao = "1.0.0";
    public const string Nome = "Confere";
    const string ChaveRegistro = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ConferenciaCarregamento";

    public static string PastaDestino { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ConferenciaCarregamento");

    public static string PastaParametros { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ConferenciaCarregamento");

    public static string? PastaPayload
    {
        get
        {
            var pastaExe = Path.GetDirectoryName(Environment.ProcessPath);
            if (string.IsNullOrEmpty(pastaExe)) return null;
            var payload = Path.Combine(pastaExe, "payload");
            return File.Exists(Path.Combine(payload, "ConferenciaCarregamento.exe")) ? payload : null;
        }
    }

    public static bool JaInstalado =>
        File.Exists(Path.Combine(PastaDestino, "ConferenciaCarregamento.exe"));

    public static string ExecutavelInstalado =>
        Path.Combine(PastaDestino, "ConferenciaCarregamento.exe");

    public static void FecharPrograma()
    {
        foreach (var processo in Process.GetProcessesByName("ConferenciaCarregamento"))
        {
            try
            {
                if (!processo.CloseMainWindow() || !processo.WaitForExit(2000))
                    processo.Kill(entireProcessTree: true);
                processo.WaitForExit(3000);
            }
            catch
            {
                // o arquivo pode continuar em uso e a cópia avisa
            }
            finally
            {
                processo.Dispose();
            }
        }
    }

    public static void Instalar(bool menuIniciar, bool areaTrabalho, IProgress<ProgressoInstalacao>? progresso)
    {
        var payload = PastaPayload ?? throw new InvalidOperationException("Os arquivos de instalação não foram encontrados ao lado do setup.");
        progresso?.Report(new ProgressoInstalacao(8, "Preparando a instalação…", 0));
        FecharPrograma();

        if (Directory.Exists(PastaDestino))
            Directory.CreateDirectory(PastaDestino);
        else
            Directory.CreateDirectory(PastaDestino);

        var arquivos = Directory.GetFiles(payload, "*", SearchOption.AllDirectories);
        for (int i = 0; i < arquivos.Length; i++)
        {
            var relativo = Path.GetRelativePath(payload, arquivos[i]);
            var destino = Path.Combine(PastaDestino, relativo);
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            File.Copy(arquivos[i], destino, true);
            var pct = 12 + (48.0 * (i + 1) / Math.Max(1, arquivos.Length));
            progresso?.Report(new ProgressoInstalacao(pct, "Copiando " + Path.GetFileName(arquivos[i]), 1));
        }

        var setupOrigem = Environment.ProcessPath ?? throw new InvalidOperationException("Não foi possível localizar o instalador.");
        File.Copy(setupOrigem, Path.Combine(PastaDestino, "ConferenciaSetup.exe"), true);

        progresso?.Report(new ProgressoInstalacao(72, "Criando atalhos…", 2));
        if (menuIniciar) CriarAtalho(PastaMenuIniciar(), ExecutavelInstalado);
        else ApagarArquivo(PastaMenuIniciar());
        if (areaTrabalho) CriarAtalho(PastaAreaTrabalho(), ExecutavelInstalado);
        else ApagarArquivo(PastaAreaTrabalho());

        progresso?.Report(new ProgressoInstalacao(88, "Registrando em Aplicativos instalados…", 3));
        var iconeInstalado = Path.Combine(PastaDestino, "logo.ico");
        Registrar(File.Exists(iconeInstalado) ? iconeInstalado : ExecutavelInstalado);

        progresso?.Report(new ProgressoInstalacao(100, "Instalação concluída", 4));
    }

    public static void Desinstalar()
    {
        FecharPrograma();
        ApagarArquivo(PastaMenuIniciar());
        ApagarArquivo(PastaAreaTrabalho());
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ChaveRegistro, throwOnMissingSubKey: false);
        }
        catch
        {
            // a chave pode já ter sido removida
        }

        if (Directory.Exists(PastaParametros))
        {
            try { Directory.Delete(PastaParametros, true); }
            catch { /* parâmetros em uso ficam para a próxima limpeza */ }
        }

        AgendarRemocao(PastaDestino);
    }

    public static void AbrirPrograma()
    {
        if (!File.Exists(ExecutavelInstalado)) return;
        Process.Start(new ProcessStartInfo(ExecutavelInstalado) { UseShellExecute = true, WorkingDirectory = PastaDestino });
    }

    static void Registrar(string icone)
    {
        using var chave = Registry.CurrentUser.CreateSubKey(ChaveRegistro);
        var setup = Path.Combine(PastaDestino, "ConferenciaSetup.exe");
        long bytes = 0;
        if (Directory.Exists(PastaDestino))
        {
            foreach (var arquivo in Directory.EnumerateFiles(PastaDestino, "*", SearchOption.AllDirectories))
            {
                try { bytes += new FileInfo(arquivo).Length; }
                catch { /* arquivo transitório */ }
            }
        }
        var kb = (int)Math.Min(int.MaxValue, bytes / 1024);
        var iconeFinal = File.Exists(icone) ? icone : ExecutavelInstalado;
        chave.SetValue("DisplayName", Nome);
        chave.SetValue("DisplayVersion", Versao);
        chave.SetValue("Publisher", Nome);
        chave.SetValue("InstallLocation", PastaDestino);
        chave.SetValue("DisplayIcon", iconeFinal);
        chave.SetValue("UninstallString", $"\"{setup}\" --desinstalar");
        chave.SetValue("QuietUninstallString", $"\"{setup}\" --desinstalar --silencioso");
        chave.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        chave.SetValue("EstimatedSize", kb, RegistryValueKind.DWord);
        chave.SetValue("NoModify", 1, RegistryValueKind.DWord);
        chave.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        chave.SetValue("Language", 1046, RegistryValueKind.DWord);
    }

    static void CriarAtalho(string caminho, string alvo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        var tipo = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Não foi possível criar o atalho.");
        dynamic shell = Activator.CreateInstance(tipo)!;
        dynamic atalho = shell.CreateShortcut(caminho);
        atalho.TargetPath = alvo;
        atalho.WorkingDirectory = PastaDestino;
        atalho.WindowStyle = 1;
        atalho.Description = Nome;
        atalho.IconLocation = alvo + ",0";
        atalho.Save();
    }

    static string PastaMenuIniciar() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        "Programs",
        Nome + ".lnk");

    static string PastaAreaTrabalho() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Nome + ".lnk");

    static void ApagarArquivo(string caminho)
    {
        try
        {
            if (File.Exists(caminho)) File.Delete(caminho);
        }
        catch
        {
            // atalho em uso não impede o restante da remoção
        }
    }

    static void AgendarRemocao(string pasta)
    {
        var cheia = Path.GetFullPath(pasta);
        var raiz = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        if (!cheia.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(cheia), "ConferenciaCarregamento", StringComparison.OrdinalIgnoreCase))
            return;
        if (!Directory.Exists(cheia)) return;

        var comando = $"ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{cheia}\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c " + comando,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }
}

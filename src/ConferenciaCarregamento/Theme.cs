using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ConferenciaCarregamento;

enum TemaVisual
{
    Claro,
    Escuro
}

static class Theme
{
    static readonly string Caminho = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ConferenciaCarregamento",
        "tema.txt");

    public static TemaVisual Atual { get; private set; }

    public static void Aplicar(Application app, TemaVisual? escolha = null, bool persistir = false)
    {
        Atual = escolha ?? LerSalvo() ?? (SistemaEscuro() ? TemaVisual.Escuro : TemaVisual.Claro);
        if (persistir) Salvar(Atual);

        var d = app.Resources;
        void C(string chave, string hex) => d[chave] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);

        if (Atual == TemaVisual.Escuro)
        {
            C("Bg", "#121816");
            C("Surface", "#1A2220");
            C("Ink", "#E4ECE9");
            C("Muted", "#9AABA5");
            C("Line", "#2E3A37");
            C("Accent", "#4FC3A1");
            C("AccentSoft", "#183A31");
            C("Warn", "#F0B45A");
            C("WarnSoft", "#3A2C14");
            C("Bad", "#F08A80");
            C("BadSoft", "#3D1F1D");
            C("Ok", "#6FD49C");
            C("OkSoft", "#173326");
        }
        else
        {
            C("Bg", "#F3F5F4");
            C("Surface", "#FFFFFF");
            C("Ink", "#18221F");
            C("Muted", "#5D6B66");
            C("Line", "#D8DFDC");
            C("Accent", "#0F6B55");
            C("AccentSoft", "#E1F0EB");
            C("Warn", "#9A5B00");
            C("WarnSoft", "#FDF1DC");
            C("Bad", "#A12D2D");
            C("BadSoft", "#FBE5E3");
            C("Ok", "#1D7A4A");
            C("OkSoft", "#E2F4EA");
        }
    }

    static TemaVisual? LerSalvo()
    {
        try
        {
            if (!File.Exists(Caminho)) return null;
            return File.ReadAllText(Caminho).Trim().Equals("escuro", StringComparison.OrdinalIgnoreCase)
                ? TemaVisual.Escuro
                : TemaVisual.Claro;
        }
        catch
        {
            return null;
        }
    }

    static void Salvar(TemaVisual tema)
    {
        var pasta = Path.GetDirectoryName(Caminho)!;
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Caminho, tema == TemaVisual.Escuro ? "escuro" : "claro");
    }

    static bool SistemaEscuro()
    {
        try
        {
            var v = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);
            return v is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }
}

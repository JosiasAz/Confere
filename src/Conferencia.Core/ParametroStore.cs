using System.Text.Encodings.Web;
using System.Text.Json;

namespace Conferencia.Core;

public static class ParametroStore
{
    static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Caminho { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ConferenciaCarregamento",
        "params.json");

    public static List<ParametroProduto> Carregar()
    {
        try
        {
            if (File.Exists(Caminho))
            {
                var lista = JsonSerializer.Deserialize<List<ParametroProduto>>(File.ReadAllText(Caminho), Opt);
                if (lista is { Count: > 0 }) return lista;
            }
        }
        catch
        {
            // arquivo corrompido volta ao padrão
        }
        return ParametroProduto.Padrao();
    }

    public static void Salvar(IEnumerable<ParametroProduto> lista)
    {
        var pasta = Path.GetDirectoryName(Caminho)!;
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Caminho, JsonSerializer.Serialize(lista, Opt));
    }
}

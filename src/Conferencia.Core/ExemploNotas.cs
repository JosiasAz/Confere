using System.Reflection;
using System.Text.Json;

namespace Conferencia.Core;

public static class ExemploNotas
{
    static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static List<NotaFiscal> Carregar()
    {
        var asm = typeof(ExemploNotas).Assembly;
        using var s = asm.GetManifestResourceStream("Conferencia.Core.exemplo.json")
            ?? throw new InvalidOperationException("O exemplo embutido não foi encontrado.");
        return JsonSerializer.Deserialize<List<NotaFiscal>>(s, Opt) ?? [];
    }
}

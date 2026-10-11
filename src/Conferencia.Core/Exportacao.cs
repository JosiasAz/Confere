using System.Globalization;
using System.Text;

namespace Conferencia.Core;

public static class Exportacao
{
    public static List<List<object?>> Linhas(IReadOnlyList<CargaConferencia> cargas)
    {
        var outp = new List<List<object?>>
        {
            new() { "ROMANEIO DE SEPARAÇÃO" },
            new() { "Carregamento", "Placa", "Código", "Produto", "FD/vol", "Un/FD", "Tipo", "Lote", "Volumes fechados", "Composição (fardos + soltas)", "Qtd un", "NF", "Cliente" }
        };

        foreach (var c in cargas)
        foreach (var p in c.Linhas)
        {
            var placa = string.Join(" ", c.Placas);
            foreach (var l in p.Lotes.Values.Where(l => l.Vl > 0))
                outp.Add(new() { c.Num, placa, p.Codigo, p.Nome, p.Fd, p.Un, "FECHADO", l.Lote, l.Vl, "", l.Vl * p.Upv, "", "" });
            foreach (var q in p.Quebras)
            {
                outp.Add(new()
                {
                    c.Num, placa, p.Codigo, p.Nome, p.Fd, p.Un,
                    q.Misto ? "QUEBRA DE LOTE" : "FRACIONADO",
                    string.Join(" + ", q.Partes.Select(x => x.Lote)),
                    "",
                    string.Join(" + ", q.Partes.Select(x => ParteTxt(x, q))),
                    q.Total, q.Nf, q.Cliente
                });
            }
        }

        outp.Add([]);
        outp.Add(new() { "DETALHE POR NOTA FISCAL" });
        outp.Add(new() { "Carregamento", "NF", "Cód. cliente", "Cliente", "Bairro", "Código", "Produto", "Lote", "Qtd (un)", "Volume NF", "Tipo vol. NF", "Separar", "Valor item", "Valor nota" });
        foreach (var c in cargas)
        foreach (var nf in c.Nfs)
        foreach (var i in nf.Itens)
        {
            outp.Add(new()
            {
                c.Num, nf.Numero, nf.CodCliente, nf.Cliente, nf.Bairro, i.Codigo, i.Nome, i.Lote,
                i.Qtd, i.Volumes, i.TipoVol, i.Upv > 0 ? Formatacao.DecTxt(i.Qtd, i.Fd, i.Un) : "",
                i.VTotal, nf.Valor
            });
        }
        return outp;
    }

    public static string Tsv(IReadOnlyList<CargaConferencia> cargas) =>
        string.Join("\n", Linhas(cargas).Select(r => string.Join("\t", r.Select(Cel))));

    public static string Csv(IReadOnlyList<CargaConferencia> cargas)
    {
        var sb = new StringBuilder();
        foreach (var r in Linhas(cargas))
            sb.Append(string.Join(";", r.Select(v => Aspas(Cel(v))))).Append("\r\n");
        return sb.ToString();
    }

    static string ParteTxt(ParteLote x, VolumeQuebra c) =>
        $"lote {x.Lote}: {Formatacao.DecTxt(x.Qtd, c.Fd, c.Un, false)}";

    public static string Cel(object? v)
    {
        switch (v)
        {
            case null:
                return "";
            case int i:
                return i.ToString(CultureInfo.InvariantCulture);
            case double d:
                if (double.IsNaN(d) || double.IsInfinity(d)) return "";
                if (Math.Abs(d - Math.Round(d)) < 1e-9)
                    return Math.Round(d).ToString("0", CultureInfo.InvariantCulture);
                return d.ToString("0.########", CultureInfo.InvariantCulture).Replace('.', ',');
            default:
                return v.ToString() ?? "";
        }
    }

    static string Aspas(string s) =>
        s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}

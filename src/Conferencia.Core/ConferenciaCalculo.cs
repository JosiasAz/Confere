using System.Text.RegularExpressions;

namespace Conferencia.Core;

public static class ConferenciaCalculo
{
    readonly record struct Ajuste(int Fd, int Un, string Src);

    public static List<CargaConferencia> Montar(IEnumerable<NotaFiscal> notas, IReadOnlyList<ParametroProduto> parametros)
    {
        var todas = notas.ToList();
        foreach (var nf in todas)
        {
            nf.Itens ??= [];
            nf.Quebras ??= [];
        }
        Normalizar(todas, parametros);
        foreach (var nf in todas) CalcularQuebras(nf);

        var cargas = new Dictionary<string, List<NotaFiscal>>();
        foreach (var nf in todas.OrderBy(n => n.Numero ?? "", Comparer<string>.Create(Formatacao.CompararNumerico)))
        {
            var chave = string.IsNullOrWhiteSpace(nf.Carregamento) ? "SEM CARREG." : nf.Carregamento;
            if (!cargas.TryGetValue(chave, out var lista))
            {
                lista = [];
                cargas[chave] = lista;
            }
            lista.Add(nf);
        }

        return cargas
            .OrderBy(kv => kv.Key, Comparer<string>.Create(Formatacao.CompararNumerico))
            .Select(kv => MontarCarga(kv.Key, kv.Value))
            .ToList();
    }

    static void Normalizar(List<NotaFiscal> nfs, IReadOnlyList<ParametroProduto> parametros)
    {
        foreach (var nf in nfs)
        foreach (var it in nf.Itens)
        {
            if (string.IsNullOrWhiteSpace(it.Nome))
                it.Nome = string.IsNullOrWhiteSpace(it.Produto) ? (it.Desc ?? "") : it.Produto!;
            it.Nome = Regex.Replace(it.Nome, @"\b(\d+(?:[.,]\d+)?\s*G)\s+\1$", "$1", RegexOptions.IgnoreCase);
            it.Codigo ??= "";
            it.Lote = string.IsNullOrWhiteSpace(it.Lote) ? "S/ LOTE" : it.Lote;
            it.TipoVol = string.IsNullOrWhiteSpace(it.TipoVol) ? "VOL" : it.TipoVol;
            var p = Achar(it, parametros);
            if (p is { } x)
            {
                it.Fd = x.Fd;
                it.Un = x.Un;
                it.Upv = x.Fd * x.Un;
                it.ParamSrc = x.Src;
            }
            else
            {
                it.Fd = 0;
                it.Un = 0;
                it.Upv = 0;
                it.ParamSrc = "";
            }
        }

        var refs = new Dictionary<string, ItemNota>();
        foreach (var nf in nfs)
        foreach (var it in nf.Itens)
            if (it.Upv > 0 && !refs.ContainsKey(it.Codigo)) refs[it.Codigo] = it;

        foreach (var nf in nfs)
        foreach (var it in nf.Itens)
        {
            if (it.Upv > 0 || !refs.TryGetValue(it.Codigo, out var r)) continue;
            it.Fd = r.Fd;
            it.Un = r.Un;
            it.Upv = r.Upv;
            it.ParamSrc = r.ParamSrc;
            if (string.IsNullOrEmpty(it.Embalagem)) it.Embalagem = r.Embalagem;
        }
    }

    static Ajuste? Achar(ItemNota it, IReadOnlyList<ParametroProduto> parametros)
    {
        var porCodigo = parametros.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.C) && x.C.Trim() == it.Codigo && x.Fd > 0 && x.Un > 0);
        if (porCodigo != null) return new Ajuste(porCodigo.Fd, porCodigo.Un, "P");

        var d = Formatacao.Norm(it.Nome + " " + (it.Embalagem ?? ""));
        ParametroProduto? best = null;
        int bn = 0;
        foreach (var x in parametros)
        {
            if (x.Fd <= 0 || x.Un <= 0) continue;
            var ts = Formatacao.Norm(x.T).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (ts.Length == 0) continue;
            if (ts.All(t => d.Contains(" " + t + " ", StringComparison.Ordinal)) && ts.Length > bn)
            {
                best = x;
                bn = ts.Length;
            }
        }
        if (best != null) return new Ajuste(best.Fd, best.Un, "P");

        var m = Regex.Match(it.Embalagem ?? "", @"^((?:\d+X)+)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var ns = m.Groups[1].Value.Split('X', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        if (ns.Length > 1) return new Ajuste(ns[0], ns.Skip(1).Aggregate(1, (a, b) => a * b), "NF");
        if (ns.Length == 1) return new Ajuste(1, ns[0], "NF");
        return null;
    }

    static void CalcularQuebras(NotaFiscal nf)
    {
        nf.Quebras.Clear();
        var porCod = new Dictionary<string, List<ItemNota>>();
        foreach (var it in nf.Itens)
        {
            if (!porCod.TryGetValue(it.Codigo, out var lista))
            {
                lista = [];
                porCod[it.Codigo] = lista;
            }
            lista.Add(it);
        }

        foreach (var its in porCod.Values)
        {
            int upv = its[0].Upv, fd = its[0].Fd, un = its[0].Un;
            if (upv <= 0)
            {
                foreach (var it in its) { it.Fechados = null; it.Resto = null; }
                continue;
            }

            var sobras = new List<(string Lote, int Qtd)>();
            foreach (var it in its.OrderBy(x => x.Lote ?? "", StringComparer.CurrentCulture))
            {
                it.Fechados = (int)Math.Floor(it.Qtd / upv + 1e-9);
                it.Resto = (int)Math.Round(it.Qtd - it.Fechados.Value * upv);
                if (it.Resto > 0) sobras.Add((it.Lote ?? "S/ LOTE", it.Resto.Value));
            }

            VolumeQuebra? cx = null;
            foreach (var s in sobras)
            {
                int q = s.Qtd;
                while (q > 0)
                {
                    if (cx == null)
                    {
                        cx = new VolumeQuebra
                        {
                            Codigo = its[0].Codigo,
                            Nome = its[0].Nome,
                            Fd = fd,
                            Un = un,
                            Upv = upv,
                            Nf = nf.Numero,
                            Cliente = nf.Cliente ?? ""
                        };
                        nf.Quebras.Add(cx);
                    }
                    int p = Math.Min(q, upv - cx.Total);
                    var ex = cx.Partes.FirstOrDefault(x => x.Lote == s.Lote);
                    if (ex != null) ex.Qtd += p;
                    else cx.Partes.Add(new ParteLote { Lote = s.Lote, Qtd = p });
                    cx.Total += p;
                    q -= p;
                    if (cx.Total >= upv) cx = null;
                }
            }
        }

        foreach (var c in nf.Quebras)
        {
            c.Completo = c.Total == c.Upv;
            c.Misto = c.Partes.Count > 1;
        }
    }

    static CargaConferencia MontarCarga(string num, List<NotaFiscal> nfs)
    {
        var prods = new Dictionary<string, LinhaRomaneio>();
        foreach (var nf in nfs)
        foreach (var it in nf.Itens)
        {
            if (!prods.TryGetValue(it.Codigo, out var p))
            {
                p = new LinhaRomaneio
                {
                    Codigo = it.Codigo,
                    Nome = it.Nome,
                    Embalagem = it.Embalagem,
                    Fd = it.Fd,
                    Un = it.Un,
                    Upv = it.Upv,
                    Src = it.ParamSrc,
                    TipoNf = it.TipoVol ?? "VOL"
                };
                prods[it.Codigo] = p;
            }
            p.Qtd += it.Qtd;
            p.VolNf += it.Volumes ?? 0;
            if (!p.Lotes.TryGetValue(it.Lote ?? "S/ LOTE", out var lote))
            {
                lote = new LoteFechado { Lote = it.Lote ?? "S/ LOTE" };
                p.Lotes[it.Lote ?? "S/ LOTE"] = lote;
            }
            if (it.Fechados != null) lote.Vl += it.Fechados.Value;
        }

        foreach (var nf in nfs)
        foreach (var q in nf.Quebras)
            if (prods.TryGetValue(q.Codigo, out var p)) p.Quebras.Add(q);

        int vlTot = 0, nQ = 0, nF = 0;
        double qtdTot = 0;
        var avisos = new List<string>();
        var linhas = prods.Values.OrderBy(p => p.Codigo, StringComparer.Ordinal).ToList();
        foreach (var p in linhas)
        {
            p.Vl = p.Lotes.Values.Sum(l => l.Vl);
            vlTot += p.Vl;
            qtdTot += p.Qtd;
            foreach (var q in p.Quebras)
            {
                if (q.Misto) nQ++;
                else nF++;
            }
            if (p.Upv <= 0)
                avisos.Add($"{p.Codigo} {p.Nome}: sem parâmetro de embalagem. Cadastre em “Parâmetros dos produtos”.");
            else if (p.Src == "NF")
            {
                var emb = p.Fd > 1 ? $"{p.Fd} FD × {p.Un} un" : $"{p.Un} un";
                avisos.Add($"{p.Codigo} {p.Nome}: sem parâmetro cadastrado; usando a embalagem da NF ({emb}). Confira em “Parâmetros dos produtos”.");
            }
        }

        var placas = nfs.Select(n => n.Placa).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct().ToList();
        var datas = nfs.Select(n => n.Emissao).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct().ToList();
        var clientes = new HashSet<string>();
        foreach (var nf in nfs)
            clientes.Add(string.IsNullOrEmpty(nf.CodCliente) ? (nf.Cliente ?? "") : nf.CodCliente);

        return new CargaConferencia
        {
            Num = num,
            Nfs = nfs,
            Linhas = linhas,
            Avisos = avisos,
            VlTot = vlTot,
            QuebrasMistas = nQ,
            Fracionados = nF,
            Placas = placas,
            Datas = datas,
            Qtd = qtdTot,
            Valor = nfs.Sum(n => n.Valor),
            Peso = nfs.Sum(n => n.PesoBruto),
            Clientes = clientes.Count
        };
    }
}

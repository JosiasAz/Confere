namespace Conferencia.Core;

public sealed class NotaFiscal
{
    public string Numero { get; set; } = "";
    public string? Cliente { get; set; }
    public string? Cnpj { get; set; }
    public string? Emissao { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public double Valor { get; set; }
    public string? Placa { get; set; }
    public double PesoBruto { get; set; }
    public double PesoLiq { get; set; }
    public string Carregamento { get; set; } = "SEM CARREG.";
    public string? CodCliente { get; set; }
    public string? Fantasia { get; set; }
    public string? Pedido { get; set; }
    public string? Vendedor { get; set; }
    public string? Cobranca { get; set; }
    public string? Arquivo { get; set; }
    public List<ItemNota> Itens { get; set; } = [];
    public List<VolumeQuebra> Quebras { get; set; } = [];
}

public sealed class ItemNota
{
    public string Codigo { get; set; } = "";
    public string Desc { get; set; } = "";
    public string? Und { get; set; }
    public double Qtd { get; set; }
    public double VUnit { get; set; }
    public double VTotal { get; set; }
    public string? Lote { get; set; }
    public string? Fab { get; set; }
    public string? Val { get; set; }
    public double? Volumes { get; set; }
    public string? TipoVol { get; set; }
    public string? Produto { get; set; }
    public string? Embalagem { get; set; }
    public string Nome { get; set; } = "";
    public int Upv { get; set; }
    public int Fd { get; set; }
    public int Un { get; set; }
    public string ParamSrc { get; set; } = "";
    public int? Fechados { get; set; }
    public int? Resto { get; set; }
}

public sealed class ParteLote
{
    public string Lote { get; set; } = "";
    public int Qtd { get; set; }
}

public sealed class VolumeQuebra
{
    public string Codigo { get; set; } = "";
    public string Nome { get; set; } = "";
    public int Fd { get; set; }
    public int Un { get; set; }
    public int Upv { get; set; }
    public List<ParteLote> Partes { get; set; } = [];
    public int Total { get; set; }
    public string Nf { get; set; } = "";
    public string Cliente { get; set; } = "";
    public bool Completo { get; set; }
    public bool Misto { get; set; }
}

public sealed class LoteFechado
{
    public string Lote { get; set; } = "";
    public int Vl { get; set; }
}

public sealed class LinhaRomaneio
{
    public string Codigo { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Embalagem { get; set; }
    public int Fd { get; set; }
    public int Un { get; set; }
    public int Upv { get; set; }
    public string Src { get; set; } = "";
    public string TipoNf { get; set; } = "VOL";
    public double Qtd { get; set; }
    public double VolNf { get; set; }
    public int Vl { get; set; }
    public Dictionary<string, LoteFechado> Lotes { get; set; } = [];
    public List<VolumeQuebra> Quebras { get; set; } = [];
}

public sealed class CargaConferencia
{
    public string Num { get; set; } = "";
    public List<NotaFiscal> Nfs { get; set; } = [];
    public List<LinhaRomaneio> Linhas { get; set; } = [];
    public List<string> Avisos { get; set; } = [];
    public int VlTot { get; set; }
    public int QuebrasMistas { get; set; }
    public int Fracionados { get; set; }
    public List<string> Placas { get; set; } = [];
    public List<string> Datas { get; set; } = [];
    public double Qtd { get; set; }
    public double Valor { get; set; }
    public double Peso { get; set; }
    public int Clientes { get; set; }
}

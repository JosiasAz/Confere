using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Conferencia.Core;

public sealed class ParametroProduto : INotifyPropertyChanged
{
    string _c = "";
    string _t = "";
    int _fd = 1;
    int _un = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    [JsonPropertyName("c")]
    public string C
    {
        get => _c;
        set => Definir(ref _c, (value ?? "").Trim().ToUpperInvariant());
    }

    [JsonPropertyName("t")]
    public string T
    {
        get => _t;
        set => Definir(ref _t, (value ?? "").Trim().ToUpperInvariant());
    }

    [JsonPropertyName("fd")]
    public int Fd
    {
        get => _fd;
        set
        {
            var v = value < 1 ? 1 : value;
            if (_fd == v) return;
            _fd = v;
            On();
            On(nameof(UnVol));
        }
    }

    [JsonPropertyName("un")]
    public int Un
    {
        get => _un;
        set
        {
            var v = value < 1 ? 1 : value;
            if (_un == v) return;
            _un = v;
            On();
            On(nameof(UnVol));
        }
    }

    [JsonIgnore]
    public int UnVol => _fd * _un;

    public static List<ParametroProduto> Padrao() =>
    [
        Novo("100001", "MITTO QUEIJO 35G", 8, 48),
        Novo("100002", "MITTO BACON 35G", 8, 48),
        Novo("100003", "MITTO CHURRASCO 35G", 8, 48),
        Novo("100009", "MITOKA SALGADA 25G", 10, 20),
        Novo("100010", "MITOKA DOCE 15G", 20, 25),
        Novo("100011", "MITTO CONCHA QUEIJO 35G", 8, 48),
        Novo("100012", "MITTO CONCHA PRESUNTO 35G", 8, 48),
        Novo("100013", "MITTO REQUEIJAO 50G", 8, 24),
        Novo("100014", "MITTO CEBOLA SALSA 50G", 8, 24),
        Novo("", "BATATA TROK TRADICIONAL 25G", 1, 40),
        Novo("", "BATATA TROK TRADICIONAL 50G", 1, 24),
        Novo("", "BATATA TROK CHURRASCO 25G", 1, 40),
        Novo("", "BATATA TROK CHURRASCO 50G", 1, 24),
        Novo("", "BATATA TROK CEBOLA SALSA 25G", 1, 40),
        Novo("", "BATATA TROK CEBOLA SALSA 50G", 1, 24),
        Novo("100026", "TROK BACON 35G", 8, 48),
        Novo("", "TROK PICANHA 35G", 8, 48),
        Novo("", "TROK COSTELA LIMAO 35G", 8, 48),
        Novo("", "TRIGO TROK COCKTAIL 50G", 1, 40),
        Novo("", "TROK MILHO MANTEIGA 50G", 1, 40),
    ];

    public static ParametroProduto Novo(string codigo, string texto, int fd, int un) =>
        new() { C = codigo, T = texto, Fd = fd, Un = un };

    void Definir(ref string campo, string valor, [CallerMemberName] string? nome = null)
    {
        if (campo == valor) return;
        campo = valor;
        On(nome);
    }

    void On([CallerMemberName] string? nome = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nome));
}

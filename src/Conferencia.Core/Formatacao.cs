using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Conferencia.Core;

public static class Formatacao
{
    public static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    public static double Numero(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        s = s.Trim();
        if (s.Contains(',')) s = s.Replace(".", "").Replace(',', '.');
        else if (Regex.IsMatch(s, @"^\d{1,3}(\.\d{3})+$")) s = s.Replace(".", "");
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    public static string Fmt(double v, int casas = 2) => v.ToString("N" + casas, Pt);

    public static string FmtQ(double v) => v.ToString("#,##0.##", Pt);

    public static string Norm(string? s)
    {
        var t = (s ?? "").ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(t.Length);
        foreach (var ch in t)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        var u = sb.ToString().Normalize(NormalizationForm.FormC);
        u = u.Replace("TROCK", "TROK", StringComparison.Ordinal);
        u = u.Replace("C/", " ", StringComparison.Ordinal);
        u = Regex.Replace(u, @"['./,\-()]", " ");
        u = Regex.Replace(u, @"(\d+)\s+G\b", "$1G");
        u = Regex.Replace(u, @"\s+", " ").Trim();
        return " " + u + " ";
    }

    public readonly record struct Decomposicao(int Vl, int Fd, int Un);

    public static Decomposicao Dec(double q, int fd, int un)
    {
        int upv = fd * un;
        if (upv <= 0) return new Decomposicao(0, 0, (int)Math.Round(q));
        int vl = (int)Math.Floor(q / upv + 1e-9);
        int r = (int)Math.Round(q - vl * upv);
        int f = fd > 1 ? r / un : 0;
        r = fd > 1 ? r - f * un : r;
        return new Decomposicao(vl, f, r);
    }

    public static string DecTxt(double q, int fd, int un, bool comVl = true)
    {
        var d = Dec(q, fd, un);
        var a = new List<string>();
        var lbl = fd > 1 ? "VL" : "CX";
        if (comVl && d.Vl != 0) a.Add(d.Vl + " " + lbl);
        else if (!comVl && d.Vl != 0 && fd > 1) d = d with { Fd = d.Fd + d.Vl * fd };
        else if (!comVl && d.Vl != 0) d = d with { Un = d.Un + d.Vl * un };
        if (d.Fd != 0) a.Add(d.Fd + " FD");
        if (d.Un != 0) a.Add(d.Un + " un");
        return a.Count == 0 ? "0" : string.Join(" + ", a);
    }

    public static string Curto(string? s)
    {
        s ??= "";
        return s.Length > 24 ? s[..23] + "…" : s;
    }

    public static int CompararNumerico(string? aa, string? bb)
    {
        var a = aa ?? "";
        var b = bb ?? "";
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int i0 = i, j0 = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                int c = CompararTrecho(a, i0, i, b, j0, j);
                if (c != 0) return c;
            }
            else
            {
                int c = a[i].CompareTo(b[j]);
                if (c != 0) return c;
                i++;
                j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }

    static int CompararTrecho(string a, int i, int i2, string b, int j, int j2)
    {
        while (i < i2 - 1 && a[i] == '0') i++;
        while (j < j2 - 1 && b[j] == '0') j++;
        int la = i2 - i, lb = j2 - j;
        if (la != lb) return la.CompareTo(lb);
        return string.CompareOrdinal(a, i, b, j, la);
    }
}

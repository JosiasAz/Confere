using System.Text;

static class PdfDanfe
{
    public static byte[] Criar(params string[] paginas)
    {
        var objetos = new List<string> { "" };
        var fontId = 3 + paginas.Length * 2;
        var kids = string.Join(" ", Enumerable.Range(0, paginas.Length).Select(i => $"{3 + i * 2} 0 R"));
        objetos.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objetos.Add($"<< /Type /Pages /Kids [{kids}] /Count {paginas.Length} >>");
        for (int i = 0; i < paginas.Length; i++)
        {
            var paginaId = 3 + i * 2;
            var conteudoId = paginaId + 1;
            objetos.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents {conteudoId} 0 R /Resources << /Font << /F1 {fontId} 0 R >> >> >>");
            var stream = Encoding.ASCII.GetBytes(paginas[i].Replace("\r\n", "\n"));
            objetos.Add($"<< /Length {stream.Length} >>\nstream\n{Encoding.ASCII.GetString(stream)}\nendstream");
        }
        objetos.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        using var ms = new MemoryStream();
        void W(string s)
        {
            var b = Encoding.ASCII.GetBytes(s);
            ms.Write(b);
        }

        W("%PDF-1.4\n");
        var offsets = new long[objetos.Count];
        for (int i = 1; i < objetos.Count; i++)
        {
            offsets[i] = ms.Position;
            W($"{i} 0 obj\n{objetos[i]}\nendobj\n");
        }
        var xref = ms.Position;
        W($"xref\n0 {objetos.Count}\n");
        W("0000000000 65535 f \n");
        for (int i = 1; i < objetos.Count; i++)
            W(offsets[i].ToString("0000000000") + " 00000 n \n");
        W($"trailer << /Size {objetos.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return ms.ToArray();
    }

    public static string PaginaAlinhada() => """
        BT
        /F1 8 Tf
        1 0 0 1 40 800 Tm (RECEBEMOS DE MITTO ALIMENTOS LTDA) Tj
        1 0 0 1 400 745 Tm (No. 130962) Tj
        1 0 0 1 490 745 Tm (SERIE 1) Tj
        1 0 0 1 40 700 Tm (NOME / RAZAO SOCIAL) Tj
        1 0 0 1 280 700 Tm (CNPJ / CPF) Tj
        1 0 0 1 430 700 Tm (DATA DA EMISSAO) Tj
        1 0 0 1 40 686 Tm (P. A. P. CAVALCANTE LTDA) Tj
        1 0 0 1 280 686 Tm (47.931.730/0001-70) Tj
        1 0 0 1 430 686 Tm (07/10/2026) Tj
        1 0 0 1 40 660 Tm (BAIRRO / DISTRITO) Tj
        1 0 0 1 40 646 Tm (CIDADE DE DEUS) Tj
        1 0 0 1 40 620 Tm (MUNICIPIO) Tj
        1 0 0 1 200 620 Tm (CEP) Tj
        1 0 0 1 40 606 Tm (MANAUS) Tj
        1 0 0 1 40 520 Tm (DADOS DO PRODUTO) Tj
        1 0 0 1 40 500 Tm (100001 SALG. MILHO MITTO'S QUEIJO 35G 8X48X35G QTD. 2 VL) Tj
        1 0 0 1 360 500 Tm (19059090) Tj
        1 0 0 1 420 500 Tm (000) Tj
        1 0 0 1 450 500 Tm (5405) Tj
        1 0 0 1 480 500 Tm (UN) Tj
        1 0 0 1 500 500 Tm (768,000) Tj
        1 0 0 1 545 500 Tm (0,8878) Tj
        1 0 0 1 580 500 Tm (681,84) Tj
        1 0 0 1 40 486 Tm (LT: 260924 FAB: 24/09/2026 VAL: 04/03/2027) Tj
        1 0 0 1 40 430 Tm (CALCULO DO ISSQN) Tj
        1 0 0 1 400 400 Tm (VALOR TOTAL DA NOTA) Tj
        1 0 0 1 520 386 Tm (883,80) Tj
        1 0 0 1 40 360 Tm (PLACA DO VEICULO) Tj
        1 0 0 1 40 346 Tm (QZO4G35) Tj
        1 0 0 1 180 360 Tm (PESO BRUTO) Tj
        1 0 0 1 180 346 Tm (34,580) Tj
        1 0 0 1 280 360 Tm (PESO LIQUIDO) Tj
        1 0 0 1 280 346 Tm (30,120) Tj
        1 0 0 1 40 300 Tm (NUM. CARREG.: 32764  COD. CLIENTE.: 3796) Tj
        ET
        """;

    public static string PaginaQuebrada() => """
        BT
        /F1 8 Tf
        1 0 0 1 40 800 Tm (RECEBEMOS DE MITTO ALIMENTOS LTDA) Tj
        1 0 0 1 400 745 Tm (No. 000.130.963) Tj
        1 0 0 1 40 700 Tm (NOME / RAZAO SOCIAL) Tj
        1 0 0 1 40 686 Tm (G. L. BENIGNO LTDA) Tj
        1 0 0 1 40 520 Tm (DADOS DOS PRODUTOS) Tj
        1 0 0 1 40 500 Tm (100010 PIPOCA MITOKA DOCE 15G) Tj
        1 0 0 1 40 478 Tm (20X25X15G QTD. 1 VL 1905.90.90 060 5405 UN 500,000 1,2500 625,00) Tj
        1 0 0 1 40 464 Tm (LOTE 261006 FAB: 06/10/2026 VAL: 06/04/2027) Tj
        1 0 0 1 40 300 Tm (CARREGAMENTO: 32784) Tj
        1 0 0 1 400 200 Tm (VALOR TOTAL DA NOTA) Tj
        1 0 0 1 520 186 Tm (625,00) Tj
        ET
        """;

    public static string PaginaVazia() => "BT\nET\n";
}

using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace __NAMESPACE__;

public static partial class LocalizedMarkup
{
    /// <summary>
    /// Frase traducida con marcado a media frase (negritas, enlaces, colores) en sus
    /// <c>{0}</c>, <c>{1}</c>…, para no partirla en pedazos sin sentido:
    /// <c>@L[llave].Con(@&lt;b&gt;@folio&lt;/b&gt;)</c>, con la llave <c>"Folio {0} assigned."</c>.
    /// El texto se pinta codificado, igual que cualquier <c>@L[...]</c>.
    /// </summary>
    public static RenderFragment Con(this LocalizedString texto, params RenderFragment[] piezas) => b =>
    {
        var valor = texto.Value;
        var desde = 0;
        foreach (Match m in Marcador().Matches(valor))
        {
            b.AddContent(0, valor[desde..m.Index]);
            var i = int.Parse(m.Groups[1].Value);
            if (i < piezas.Length) b.AddContent(1, piezas[i]);
            else b.AddContent(2, m.Value);
            desde = m.Index + m.Length;
        }
        b.AddContent(3, valor[desde..]);
    };

    [GeneratedRegex(@"\{(\d+)\}")]
    private static partial Regex Marcador();
}

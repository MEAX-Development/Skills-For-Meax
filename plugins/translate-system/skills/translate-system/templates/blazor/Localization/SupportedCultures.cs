using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace __NAMESPACE__;

/// <summary>Idioma disponible en el selector. Los mismos tres que maneja MEAX One.</summary>
public sealed record SupportedCulture(string Code, string NativeName, string ShortLabel, string Flag);

/// <summary>
/// Idiomas del sistema. Solo cambia el idioma de los textos (CurrentUICulture); el formato
/// de fechas y números (CurrentCulture) se queda fijo en <see cref="FormattingCulture"/>.
/// </summary>
public static class SupportedCultures
{
    /// <summary>Idioma cuando el JWT no trae el claim Idioma y no hay elección guardada.</summary>
    public const string DefaultCulture = "es";

    public static readonly IReadOnlyList<SupportedCulture> All =
    [
        new("es", "Español", "ES", "🇲🇽"),
        new("en", "English", "EN", "🇺🇸"),
        new("ja", "日本語", "JA", "🇯🇵"),
    ];

    /// <summary>
    /// Cultura de formato de fechas y números. Se fija al arrancar con la que ya usaba el
    /// proceso (o la de <c>Localization:FormattingCulture</c>), para que cambiar de idioma
    /// nunca cambie cómo se ven los importes ni las fechas.
    /// </summary>
    public static string FormattingCulture { get; internal set; } = "en-US";

    /// <summary>El idioma de la petición o del circuito en curso.</summary>
    public static SupportedCulture Current => Resolve(CultureInfo.CurrentUICulture.Name);

    public static IEnumerable<CultureInfo> AsCultureInfo() => All.Select(c => new CultureInfo(c.Code));

    public static bool IsSupported(string? code) =>
        !string.IsNullOrWhiteSpace(code)
        && All.Any(c => string.Equals(c.Code, Normalize(code), StringComparison.OrdinalIgnoreCase));

    /// <summary>"es-MX" → "es", "JA" → "ja".</summary>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return DefaultCulture;
        var c = code.Trim();
        var dash = c.IndexOfAny(['-', '_']);
        return (dash > 0 ? c[..dash] : c).ToLowerInvariant();
    }

    public static SupportedCulture Resolve(string? code)
    {
        var norm = Normalize(code);
        return All.FirstOrDefault(c => c.Code == norm)
               ?? All.First(c => c.Code == DefaultCulture);
    }
}

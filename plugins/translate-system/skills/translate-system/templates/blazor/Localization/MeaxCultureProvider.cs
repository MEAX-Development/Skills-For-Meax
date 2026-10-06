using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace __NAMESPACE__;

/// <summary>
/// Decide el idioma de cada petición (y por lo tanto del circuito de Blazor):
/// <list type="number">
/// <item>Lo que la persona eligió en el menú de usuario, mientras su idioma en MEAX One siga igual.</item>
/// <item>El claim <c>Idioma</c> del JWT de MEAX One.</item>
/// <item><see cref="SupportedCultures.DefaultCulture"/>.</item>
/// </list>
/// </summary>
/// <remarks>
/// La cookie guarda la elección junto con el idioma que traía el JWT en ese momento. Si
/// después la persona cambia su idioma en MEAX One, el claim ya no coincide y gana el hub:
/// así una elección vieja nunca tapa un cambio nuevo hecho en el hub.
///
/// La cookie va en <c>Path=/</c>: todos los sistemas de MEAX publicados en el mismo
/// servidor comparten la elección.
///
/// Requiere que <c>UseAuthentication</c> corra antes que <c>UseRequestLocalization</c>.
/// </remarks>
public sealed class MeaxCultureProvider : RequestCultureProvider
{
    public const string ClaimType = "Idioma";
    public const string CookieName = ".MEAX.Idioma";

    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var claim = IdiomaDelClaim(httpContext);
        var codigo = Elegido(httpContext, claim) ?? claim;

        return codigo is null
            ? NullProviderCultureResult
            : Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(SupportedCultures.FormattingCulture, codigo));
    }

    /// <summary>Guarda la elección del menú, atada al idioma que trae hoy el JWT.</summary>
    public static void Recordar(HttpContext httpContext, string codigo)
    {
        var valor = $"{SupportedCultures.Normalize(codigo)}|{IdiomaDelClaim(httpContext) ?? ""}";
        httpContext.Response.Cookies.Append(CookieName, valor, new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
        });
    }

    private static string? IdiomaDelClaim(HttpContext httpContext)
    {
        var idioma = httpContext.User.FindFirst(ClaimType)?.Value;
        return SupportedCultures.IsSupported(idioma) ? SupportedCultures.Normalize(idioma) : null;
    }

    private static string? Elegido(HttpContext httpContext, string? claim)
    {
        var cookie = httpContext.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(cookie))
            return null;

        var partes = cookie.Split('|');
        if (!SupportedCultures.IsSupported(partes[0]))
            return null;

        var claimAlElegir = partes.Length > 1 ? partes[1] : "";
        var sigueVigente = claim is null || string.Equals(claim, claimAlElegir, StringComparison.OrdinalIgnoreCase);
        return sigueVigente ? SupportedCultures.Normalize(partes[0]) : null;
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;

namespace __NAMESPACE__;

/// <summary>Conexión del sistema de idiomas (ES / EN / JA) en <c>Program.cs</c>: tres líneas.</summary>
public static class LocalizationSetup
{
    /// <summary>Ruta a la que apunta el selector del menú de usuario.</summary>
    public const string CultureEndpoint = "culture/set";

    public static IServiceCollection AddMeaxLocalization(this IServiceCollection services, IHostEnvironment env, IConfiguration config)
    {
        SupportedCultures.FormattingCulture = FormatoFijo(config["Localization:FormattingCulture"]);

        var store = new JsonTranslationStore(Path.Combine(env.ContentRootPath, "Resources"));
        Loc.Store = store;
        services.AddSingleton(store);

        // Antes de AddLocalization: este usa TryAdd y respeta la fábrica ya registrada.
        services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
        services.AddLocalization();

        services.Configure<RequestLocalizationOptions>(o =>
        {
            var formato = new CultureInfo(SupportedCultures.FormattingCulture);
            o.DefaultRequestCulture = new RequestCulture(formato, new CultureInfo(SupportedCultures.DefaultCulture));
            o.SupportedCultures = [formato];
            o.SupportedUICultures = SupportedCultures.AsCultureInfo().ToList();
            o.FallBackToParentCultures = true;
            o.FallBackToParentUICultures = true;
            o.RequestCultureProviders = [new MeaxCultureProvider()];
        });

        return services;
    }

    /// <summary>Va justo después de <c>UseAuthentication</c>: el idioma sale de un claim.</summary>
    public static IApplicationBuilder UseMeaxLocalization(this IApplicationBuilder app) =>
        app.UseRequestLocalization();

    /// <summary>
    /// <c>GET culture/set?culture=ja&amp;returnUrl=/ruta</c>: guarda la elección y recarga la página
    /// completa, porque el idioma de un circuito de Blazor se fija al abrirlo.
    /// </summary>
    public static IEndpointRouteBuilder MapCultureEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/" + CultureEndpoint, (HttpContext ctx, string? culture, string? returnUrl) =>
        {
            var codigo = SupportedCultures.IsSupported(culture)
                ? SupportedCultures.Normalize(culture)
                : SupportedCultures.DefaultCulture;
            MeaxCultureProvider.Recordar(ctx, codigo);

            // returnUrl ya trae el PathBase (sale de location.pathname); solo se aceptan rutas locales.
            var destino = EsLocal(returnUrl) ? returnUrl! : $"{ctx.Request.PathBase}/";
            return Results.Redirect(destino);
        }).AllowAnonymous();

        return endpoints;
    }

    private static bool EsLocal(string? url) =>
        !string.IsNullOrEmpty(url)
        && url[0] == '/'
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));

    private static string FormatoFijo(string? configurado)
    {
        if (!string.IsNullOrWhiteSpace(configurado))
            return configurado;

        // La que ya usaba el proceso antes de tener idiomas: así nada cambia de formato.
        var actual = CultureInfo.CurrentCulture.Name;
        return string.IsNullOrEmpty(actual) ? "en-US" : actual;
    }
}

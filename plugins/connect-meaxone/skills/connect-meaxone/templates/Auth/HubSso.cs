using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;

namespace __NAMESPACE__;

/// <summary>
/// Entrada por meax.one (patron B de <c>Login/Docs/Satelites.md</c> §4.2). La sesion se
/// abre solo con el <c>meax_token</c> que regresa <c>/auth/sso</c>, que es el que
/// garantiza que la persona tiene fila en <c>meax_system_access</c> para este sistema; la cookie
/// .MEAX.JWT del hub la trae cualquiera con sesion en meax.one.
/// </summary>
public static class HubSso
{
    private const string TokenQuery = "meax_token";
    private const string RoleQuery = "meax_role";

    public static IServiceCollection AddHubSso(this IServiceCollection services, IConfiguration config)
    {
        var hub = HubBaseUrl(config);
        var system = SystemCode(config);
        _ = Secret(config); // Falla al arrancar y no en el primer login.

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                // Nombre unico: todos los satelites comparten el host meax.one.
                o.Cookie.Name = $".{system}.AUTH";
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = CookieSecurePolicy.None; // meax.one es HTTP
                o.ExpireTimeSpan = TimeSpan.FromHours(10);       // un turno y algo, por las capturas largas
                o.SlidingExpiration = false;                     // al vencer se vuelve a preguntar al hub

                o.Events.OnRedirectToLogin = ctx =>
                {
                    // Un fetch o el circuito de Blazor no pueden seguir un SSO: 401 y la
                    // pagina se recarga (la recarga es un GET y pasa por el SSO sola).
                    if (EsApi(ctx.Request))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    // Un POST no se puede repetir tras el SSO: se vuelve al inicio del sistema.
                    var destino = HttpMethods.IsGet(ctx.Request.Method)
                        ? UrlDeRegreso(ctx.Request)
                        : $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}/";
                    ctx.Response.Redirect(
                        $"{hub}/auth/sso?system={Uri.EscapeDataString(system)}&returnUrl={Uri.EscapeDataString(destino)}");
                    return Task.CompletedTask;
                };
            });

        return services;
    }

    /// <summary>Va entre UseAuthentication y UseAuthorization.</summary>
    public static IApplicationBuilder UseHubSso(this IApplicationBuilder app, IConfiguration config)
    {
        var parametros = new TokenValidationParameters
        {
            ValidIssuer = config["Jwt:Issuer"] ?? "meaxHub",
            ValidAudience = config["Jwt:Audience"] ?? "meax-services",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret(config))),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        // Sin esto .NET renombra "sub" y compania a URIs largos.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        return app.Use(async (http, next) =>
        {
            var token = http.Request.Query[TokenQuery].ToString();
            if (token.Length == 0)
            {
                await next();
                return;
            }

            ClaimsPrincipal hubUser;
            try
            {
                hubUser = handler.ValidateToken(token, parametros, out _);
            }
            catch (SecurityTokenExpiredException)
            {
                // Un token viejo del historial: se quita y el SSO emite otro.
                http.Response.Redirect(UrlDeRegreso(http.Request));
                return;
            }
            catch (Exception e) when (e is SecurityTokenException or ArgumentException)
            {
                // Firma, emisor o audiencia equivocados, o un token mal formado. No se
                // reintenta: con el secreto mal configurado seria un ciclo contra el hub.
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await http.Response.WriteAsync("Token de meax.one invalido. Revisa Jwt:Secret.");
                return;
            }

            var identidad = new ClaimsIdentity(
                hubUser.Claims,
                CookieAuthenticationDefaults.AuthenticationScheme,
                nameType: MeaxClaims.DisplayName,
                roleType: MeaxClaims.SystemRole);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));

            // Fuera el token de la barra de direcciones.
            http.Response.Redirect(UrlDeRegreso(http.Request));
        });
    }

    /// <summary>
    /// "Cerrar sesion": cierra la cookie propia y manda al logout del hub, que borra
    /// .MEAX.AUTH y .MEAX.JWT.
    /// </summary>
    public static IEndpointRouteBuilder MapHubSsoLogout(this IEndpointRouteBuilder app, IConfiguration config)
    {
        var hub = HubBaseUrl(config);
        app.MapGet("/salir", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect($"{hub}/Account/Logout");
        }).AllowAnonymous();
        return app;
    }

    public static string HubBaseUrl(IConfiguration config) =>
        (config["Hub:BaseUrl"] ?? "http://meax.one").TrimEnd('/');

    public static string SystemCode(IConfiguration config) =>
        config["Hub:SystemCode"] is { Length: > 0 } code
            ? code
            : throw new InvalidOperationException("Hub:SystemCode no configurado.");

    private static string Secret(IConfiguration config) =>
        config["Jwt:Secret"] is { Length: > 0 } secret
            ? secret
            : throw new InvalidOperationException(
                "Jwt:Secret no configurado. Debe ser el mismo de meaxHub (appsettings.json de Services\\Login).");

    /// <summary>
    /// Esta misma pagina, absoluta y sin los parametros del SSO. Lleva PathBase porque en
    /// IIS el sistema vive bajo /ALIAS: sin el, el hub regresaria a la raiz de meax.one. Path
    /// vacio cuenta como "/" para que empate con la URL registrada con diagonal final.
    /// </summary>
    private static string UrlDeRegreso(HttpRequest r)
    {
        var query = QueryString.Create(r.Query.Where(kv => kv.Key is not (TokenQuery or RoleQuery)));
        var path = r.Path.HasValue ? r.Path.Value : "/";
        return $"{r.Scheme}://{r.Host}{r.PathBase}{path}{query}";
    }

    private static bool EsApi(HttpRequest r) =>
        r.Path.StartsWithSegments("/_blazor")
        || r.Path.StartsWithSegments("/api")
        || r.Headers.XRequestedWith == "XMLHttpRequest";
}

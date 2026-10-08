using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace __NAMESPACE__;

/// <summary>
/// Login de desarrollo para correr el sistema sin meax.one. Doble candado, igual que en los
/// demas satelites: solo se enciende con entorno Development <b>y</b> Auth:DevLogin = true,
/// y esa llave va unicamente en appsettings.Development.json (que no se publica). En IIS
/// el entorno es Production, asi que aunque el archivo se copiara por error no se activa.
/// El usuario falso trae los mismos claims que el hub.
/// </summary>
public static class DevAuth
{
    public const string SchemeName = "DevLogin";

    public static bool Habilitado(IHostEnvironment env, IConfiguration config) =>
        env.IsDevelopment() && config.GetValue<bool>("Auth:DevLogin");

    public static IServiceCollection AddDevAuth(this IServiceCollection services)
    {
        services.AddAuthentication(SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(SchemeName, _ => { });
        return services;
    }

    private sealed class DevAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration config)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var usuario = config.GetSection("Auth:DevUser");
            var claims = usuario.GetChildren()
                .Where(c => !string.IsNullOrEmpty(c.Value))
                .Select(c => new Claim(c.Key, c.Value!))
                .ToList();

            if (claims.Count == 0)
            {
                claims.Add(new Claim(MeaxClaims.PcLoginId, Environment.UserName));
                claims.Add(new Claim(MeaxClaims.DisplayName, Environment.UserName));
            }

            var identidad = new ClaimsIdentity(claims, SchemeName, MeaxClaims.DisplayName, MeaxClaims.SystemRole);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identidad), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

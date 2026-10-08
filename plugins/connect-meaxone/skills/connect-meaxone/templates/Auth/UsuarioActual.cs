using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace __NAMESPACE__;

/// <summary>
/// La persona de la sesion, leida de los claims de MEAX One. Sirve para prellenar
/// "Registrado por" / "Realizado por": en IIS <c>Environment.UserName</c> es la cuenta
/// del App Pool, no quien captura.
/// </summary>
public sealed class UsuarioActual(AuthenticationStateProvider authState)
{
    /// <summary>
    /// Cuenta con la que entro (PcLoginId): para un IDL es su cuenta de AD, para un DL su
    /// nomina. Es el mismo dato que antes daba Environment.UserName en la maquina local.
    /// </summary>
    public async Task<string> LoginAsync()
    {
        var user = (await authState.GetAuthenticationStateAsync()).User;
        return Login(user);
    }

    public static string Login(ClaimsPrincipal user) =>
        user.FindFirstValue(MeaxClaims.PcLoginId) is { Length: > 0 } login
            ? login
            : user.Identity?.Name ?? string.Empty;

    public static string Nombre(ClaimsPrincipal user) =>
        user.FindFirstValue(MeaxClaims.DisplayName) is { Length: > 0 } nombre ? nombre : Login(user);

    public static string Iniciales(string nombre)
    {
        var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length switch
        {
            0 => "?",
            1 => partes[0][..Math.Min(2, partes[0].Length)].ToUpperInvariant(),
            _ => $"{partes[0][0]}{partes[1][0]}".ToUpperInvariant(),
        };
    }
}

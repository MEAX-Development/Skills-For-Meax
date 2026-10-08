namespace __NAMESPACE__;

/// <summary>
/// Nombres de los claims que emite meaxHub (MEAX One). Son contractuales: el hub los
/// escribe con estos nombres exactos y el login de desarrollo los replica, asi que el
/// resto de la aplicacion nunca necesita saber como entro la persona.
/// Ver <c>Login/Docs/JWT.md</c>.
/// </summary>
public static class MeaxClaims
{
    public const string PcLoginId = "PcLoginId";
    public const string DisplayName = "DisplayName";
    public const string Department = "Department";
    public const string Position = "Position";
    public const string Email = "Email";

    /// <summary>La nomina. Es la llave de la persona.</summary>
    public const string EmployeeId = "EmployeeId";

    public const string LoginTipo = "LoginTipo";
    public const string EsIT = "EsIT";

    /// <summary>Rol en este sistema (meax_system_access.role). Solo viaja en el token del SSO.</summary>
    public const string SystemRole = "SystemRole";
}

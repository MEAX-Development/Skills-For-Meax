# Prueba el SSO de un satelite de MEAX One.
#
# Modo local (por omision): el satelite corre en local en modo hub, con App__PathBase=/ALIAS
# y un Jwt__Secret DE PRUEBA. Este script firma tokens con ese mismo secreto de prueba
# (nunca con el de produccion) y revisa todo el viaje:
#   1) sin sesion -> 302 a <hub>/auth/sso?system=CODE&returnUrl=<url con PathBase>
#   2) token con otra firma -> 401 (sin ciclo contra el hub)
#   3) token valido -> 302 a la misma URL sin meax_token + cookie .CODE.AUTH con Path=/ALIAS
#   4) con la cookie -> 200, base href = /ALIAS/, latido con ?system=CODE
#   5) archivo estatico sin sesion -> 200
#   6) token vencido -> 302 a la URL limpia (el SSO emite otro)
#   7) /salir -> 302 al logout del hub
#
# Modo -Publicado: solo pasos 1 y 5 contra el servidor real (no se puede firmar un token).
#
# Uso:
#   .\Probar-Sso.ps1 -Url http://localhost:5199/XYZ -Code XYZ -Secreto prueba-local-0123456789abcdef
#   .\Probar-Sso.ps1 -Url http://meax.one/XYZ -Code XYZ -Publicado [-Estatico js/meax-heartbeat.js]

param(
    [Parameter(Mandatory)] [string]$Url,
    [Parameter(Mandatory)] [string]$Code,
    [string]$Secreto,
    [string]$Ruta = '',
    [string]$Estatico = 'js/meax-heartbeat.js',
    [switch]$Publicado
)

$ErrorActionPreference = 'Stop'
$Url = $Url.TrimEnd('/')
Add-Type -AssemblyName System.Net.Http

function B64U([byte[]]$b) {
    $s = [Convert]::ToBase64String($b).TrimEnd('=')
    $s = $s.Replace([char]43, [char]45)
    $s.Replace([char]47, [char]95)
}

function Jwt([string]$secret, [int]$expMin) {
    $h = B64U ([Text.Encoding]::UTF8.GetBytes('{"alg":"HS256","typ":"JWT"}'))
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $p = [ordered]@{
        sub = 'PRUEBA'; PcLoginId = 'PRUEBA'; DisplayName = 'Prueba SSO'; Department = 'IT'
        EmployeeId = '99999'; LoginTipo = 'IDL'; EsIT = 'false'; SystemRole = 'Admin'; Idioma = 'es'
        iss = 'meaxHub'; aud = 'meax-services'; iat = $now - 600; nbf = $now - 600; exp = $now + 60 * $expMin
    } | ConvertTo-Json -Compress
    $pl = B64U ([Text.Encoding]::UTF8.GetBytes($p))
    $hm = New-Object Security.Cryptography.HMACSHA256 (, [Text.Encoding]::UTF8.GetBytes($secret))
    "$h.$pl." + (B64U ($hm.ComputeHash([Text.Encoding]::UTF8.GetBytes("$h.$pl"))))
}

function NuevoCliente() {
    $h = New-Object System.Net.Http.HttpClientHandler
    $h.AllowAutoRedirect = $false
    $h.CookieContainer = New-Object System.Net.CookieContainer
    $c = New-Object System.Net.Http.HttpClient($h)
    $c.Timeout = [TimeSpan]::FromMinutes(4)
    [pscustomobject]@{ Client = $c; Cookies = $h.CookieContainer }
}

function Req([string]$u, $sesion) {
    if (-not $sesion) { $sesion = NuevoCliente }
    $r = $sesion.Client.GetAsync($u).GetAwaiter().GetResult()
    [pscustomobject]@{
        Status   = [int]$r.StatusCode
        Location = if ($r.Headers.Location) { [uri]::UnescapeDataString($r.Headers.Location.OriginalString) } else { '' }
        Content  = $r.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    }
}

$fallas = 0
function Revisar([string]$nombre, [bool]$ok, [string]$detalle) {
    $marca = if ($ok) { 'OK   ' } else { $script:fallas++; 'FALLA' }
    "$marca $nombre  $detalle"
}

$pagina = "$Url/$Ruta"
$r = Req $pagina
Revisar '1) sin sesion manda al SSO' ($r.Status -eq 302 -and $r.Location -match "/auth/sso\?system=$Code&returnUrl=$([regex]::Escape($pagina))") "$($r.Status) -> $($r.Location)"

$r = Req "$Url/$Estatico"
Revisar '5) estatico sin sesion' ($r.Status -eq 200) "$($r.Status) $Estatico"

if (-not $Publicado) {
    if (-not $Secreto) { throw 'Falta -Secreto (el mismo Jwt__Secret de prueba con el que arrancaste el satelite).' }

    $r = Req "$Url/?meax_token=$(Jwt 'otro-secreto-que-no-es-el-bueno-000000' 5)"
    Revisar '2) token con otra firma' ($r.Status -eq 401) "$($r.Status)"

    $s = NuevoCliente
    $r = Req "$pagina`?meax_token=$(Jwt $Secreto 5)&meax_role=Admin" $s
    $cookie = $s.Cookies.GetCookies([uri]"$Url/") | Where-Object Name -eq ".$Code.AUTH"
    $alias = ([uri]$Url).AbsolutePath
    Revisar '3) token valido abre sesion y limpia la URL' ($r.Status -eq 302 -and $r.Location -eq $pagina -and $cookie) "$($r.Status) -> $($r.Location) | cookie $($cookie.Name) path=$($cookie.Path)"
    Revisar '   cookie con Path del alias' ($cookie -and $cookie.Path -eq $alias) "esperado $alias"

    $r = Req $pagina $s
    $base = ([regex]'<base href="([^"]*)"').Match($r.Content).Groups[1].Value
    Revisar '4) con la cookie entra' ($r.Status -eq 200) "$($r.Status)"
    if ($r.Content -match '<base ') { Revisar '   base href = PathBase' ($base -eq "$alias/") "base href=$base" }
    Revisar '   latido con ?system=' ($r.Content -match "auth/heartbeat\?system=$Code") ''
    $absolutos = [regex]::Matches($r.Content, '(href|src|action)="/(?!/)[^"]*"') | ForEach-Object Value | Where-Object { $_ -notmatch "=`"$([regex]::Escape($alias))/" } | Select-Object -Unique
    Revisar '   sin rutas absolutas a la raiz' (-not $absolutos) ($absolutos -join ' ')

    $r = Req "$pagina`?meax_token=$(Jwt $Secreto -5)"
    Revisar '6) token vencido' ($r.Status -eq 302 -and $r.Location -eq $pagina) "$($r.Status) -> $($r.Location)"

    $r = Req "$Url/salir" $s
    Revisar '7) salir' ($r.Status -eq 302 -and $r.Location -match '/Account/Logout$') "$($r.Status) -> $($r.Location)"
}

if ($fallas) { "RESULTADO: $fallas falla(s)"; exit 1 } else { 'RESULTADO: OK' }

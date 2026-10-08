# Arma el appsettings.json del servidor para un satelite de MEAX One SIN imprimir secretos.
#
# Parte de una base y le pone las secciones Hub y Jwt:
#   - Base: el appsettings.json que hoy tiene el servidor, si es del MISMO sistema (asi se
#     conservan sus cadenas de conexion); si no, el appsettings.json del proyecto.
#   - Jwt:Secret: se copia de otro archivo que ya lo tenga (otro satelite o el hub). Nunca
#     se escribe en pantalla, solo su largo.
#   - Hub: BaseUrl = http://meax.one y SystemCode.
# Al final lista (solo los nombres) las llaves que siguen vacias para que alguien las llene.
#
# Uso:
#   .\Escribir-ConfigServidor.ps1 -Base <appsettings base> -FuenteSecreto <json con Jwt:Secret> `
#       -SystemCode XYZ -Salida C:\temp\xyz-servidor
# La carpeta de salida contiene secretos: borrarla al terminar el deploy.

param(
    [Parameter(Mandatory)] [string]$Base,
    [Parameter(Mandatory)] [string]$FuenteSecreto,
    [Parameter(Mandatory)] [string]$SystemCode,
    [Parameter(Mandatory)] [string]$Salida,
    [string]$HubBaseUrl = 'http://meax.one'
)

$ErrorActionPreference = 'Stop'

# PowerShell 5.1 no acepta comentarios en JSON: se quitan los renglones // completos.
function Leer-Json([string]$ruta) {
    $texto = (Get-Content $ruta -Raw -Encoding UTF8) -split "`r?`n" |
        Where-Object { $_ -notmatch '^\s*//' }
    ($texto -join "`n") | ConvertFrom-Json
}

function Poner([object]$obj, [string]$seccion, [string]$llave, $valor) {
    if (-not $obj.PSObject.Properties[$seccion] -or $obj.$seccion -isnot [pscustomobject]) {
        $obj | Add-Member -NotePropertyName $seccion -NotePropertyValue ([pscustomobject]@{}) -Force
    }
    $obj.$seccion | Add-Member -NotePropertyName $llave -NotePropertyValue $valor -Force
}

function Vacias([object]$obj, [string]$prefijo) {
    foreach ($p in $obj.PSObject.Properties) {
        $n = if ($prefijo) { "${prefijo}:$($p.Name)" } else { $p.Name }
        if ($p.Name -like '//*') { continue }
        if ($p.Value -is [pscustomobject]) { Vacias $p.Value $n }
        elseif ($p.Value -is [string] -and ($p.Value.Length -eq 0 -or $p.Value -match '^<.*>$')) { $n }
    }
}

$config = Leer-Json $Base
$fuente = Leer-Json $FuenteSecreto
$secreto = [string]$fuente.Jwt.Secret
if ($secreto.Length -lt 20) { throw "No se encontro un Jwt:Secret valido en $FuenteSecreto" }

Poner $config 'Hub' 'BaseUrl' $HubBaseUrl.TrimEnd('/')
Poner $config 'Hub' 'SystemCode' $SystemCode
Poner $config 'Jwt' 'Secret' $secreto
Poner $config 'Jwt' 'Issuer' 'meaxHub'
Poner $config 'Jwt' 'Audience' 'meax-services'

New-Item -ItemType Directory -Force $Salida | Out-Null
$destino = Join-Path $Salida 'appsettings.json'
[IO.File]::WriteAllText($destino, ($config | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding $false))

"Listo: $destino (Hub:SystemCode=$SystemCode, Jwt:Secret de $($secreto.Length) caracteres)."
$pendientes = @(Vacias $config '')
if ($pendientes.Count) {
    'Llaves vacias o con <...> que hay que revisar antes de publicar:'
    $pendientes | ForEach-Object { "  - $_" }
}

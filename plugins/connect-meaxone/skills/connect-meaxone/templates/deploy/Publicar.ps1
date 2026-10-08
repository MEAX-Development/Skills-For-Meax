# Publica __PROYECTO__ en IIS de meaxs066 como la aplicacion /__ALIAS__ de meax.one
# (sistema __CODE__ en MEAX One).
#
# Mismo procedimiento que el hub (Login/Docs/Satelites.md §6.3):
#   1. dotnet publish a una carpeta temporal.
#   2. Respaldo completo de lo que hoy esta publicado.
#   3. app_offline.htm para que IIS suelte los binarios.
#   4. robocopy /MIR SIN tocar el appsettings.json del servidor ni App_Data/logs/uploads.
#   5. (Opcional) instala la configuracion del servidor, todavia con la app fuera.
#   6. Se quita app_offline.htm.
#
# El appsettings.json del servidor (Jwt:Secret, cadenas de conexion) vive alla: el deploy
# normal nunca lo sobrescribe. Solo con -ConfigServidor se reemplaza.
# Correr desde PowerShell (Git Bash rompe las rutas \\servidor\...).

param(
    [string]$Destino = '__DESTINO__',
    [string]$Temporal = 'C:\temp',
    [switch]$SinRespaldo,
    # Carpeta con el appsettings.json del servidor (armado con Escribir-ConfigServidor.ps1).
    # Cualquier otro archivo en ella se copia a App_Data (p. ej. credenciales de lectura).
    [string]$ConfigServidor
)

$ErrorActionPreference = 'Stop'
$fecha = Get-Date -Format 'yyyyMMdd-HHmm'
$raiz = Split-Path -Parent $PSScriptRoot
$proyecto = Join-Path $raiz '__CSPROJ_RELATIVO__'
$salida = Join-Path $Temporal "publish-__CODE_LOWER__-$fecha"

if (-not (Test-Path $Destino)) { throw "No existe o no hay acceso a $Destino" }
if ($ConfigServidor) {
    if (-not (Test-Path (Join-Path $ConfigServidor 'appsettings.json'))) { throw "No hay appsettings.json en $ConfigServidor" }
} elseif (-not (Test-Path (Join-Path $Destino 'appsettings.json'))) {
    throw "El servidor no tiene appsettings.json en $Destino. Usa -ConfigServidor."
}

Write-Host "1/6 Publicando $proyecto -> $salida"
dotnet publish $proyecto -c Release -o $salida
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish fallo.' }

# El publish trae el appsettings.json del repo (sin secretos); el del servidor manda.
Remove-Item (Join-Path $salida 'appsettings.json') -Force -ErrorAction SilentlyContinue

if (-not $SinRespaldo) {
    $respaldo = Join-Path $Temporal "respaldo-$(Split-Path $Destino -Leaf)-$fecha"
    Write-Host "2/6 Respaldando $Destino -> $respaldo"
    robocopy $Destino $respaldo /E /NP /NFL /NDL /R:1 /W:2 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy del respaldo fallo con codigo $LASTEXITCODE" }
} else {
    Write-Host '2/6 Respaldo omitido (-SinRespaldo)'
}

Write-Host '3/6 app_offline.htm'
$offline = Join-Path $Destino 'app_offline.htm'
Set-Content -Path $offline -Encoding UTF8 -Value '<!DOCTYPE html><html lang="es"><meta charset="utf-8"><title>Actualizando</title><body style="font-family:Montserrat,sans-serif;padding:40px">El sistema se esta actualizando. Intenta de nuevo en un minuto.</body></html>'
Start-Sleep -Seconds 5

try {
    Write-Host "4/6 Copiando a $Destino"
    robocopy $salida $Destino /MIR /NP /NFL /NDL /R:3 /W:5 /XF appsettings.json app_offline.htm /XD logs App_Data uploads
    if ($LASTEXITCODE -ge 8) { throw "robocopy fallo con codigo $LASTEXITCODE" }

    if ($ConfigServidor) {
        Write-Host '5/6 Instalando la configuracion del servidor'
        Copy-Item (Join-Path $ConfigServidor 'appsettings.json') (Join-Path $Destino 'appsettings.json') -Force
        $extras = Get-ChildItem $ConfigServidor -File | Where-Object Name -ne 'appsettings.json'
        if ($extras) {
            $appData = Join-Path $Destino 'App_Data'
            New-Item -ItemType Directory -Force $appData | Out-Null
            $extras | ForEach-Object { Copy-Item $_.FullName (Join-Path $appData $_.Name) -Force }
        }
    } else {
        Write-Host '5/6 Configuracion del servidor sin cambios'
    }
}
finally {
    Write-Host '6/6 Quitando app_offline.htm'
    Remove-Item $offline -Force -ErrorAction SilentlyContinue
}

Write-Host "Listo. Publicado desde $salida. Probar en http://meax.one/__ALIAS__/"

# Pasa la revisión entera de NubeBar y devuelve la barra como estaba.
#
# ---------- Por qué existe ----------
#
# Correr los diagnósticos a mano son seis órdenes y un baile: parar la barra (porque solo
# puede haber una), lanzar el binario de fase, esperar, matarlo, VOLVER A ARRANCAR LA BARRA.
#
# Ese último paso es el que importa. Olvidarlo deja el escritorio sin barra, y de ahí salió
# el peor accidente de todo el proyecto: sin barra, uno arranca lo primero que encuentra —un
# binario de fase— y ese migra el config a un esquema que la versión instalada ya no sabe
# leer. Pasó. Dos veces.
#
# Así que el `finally` de aquí abajo no es prolijidad: es la lección.
#
# ---------- Uso ----------
#
#   .\revision.ps1                      # los dieciséis diagnósticos (unos 45 min)
#   .\revision.ps1 -Solo                # solo los cuatro de las fases nuevas (~20 min)
#   .\revision.ps1 -Rapido              # solo pruebas y la guarda (1 min)
#   .\revision.ps1 -ConCapturas         # además, las 58 fotos
#
param(
    [switch]$Rapido,
    [switch]$Solo,
    [switch]$ConCapturas,
    [string]$Fase = "dist-nubehypr-d95",
    [string]$Estable = "dist-nuevo"
)

$ErrorActionPreference = "Continue"
$raiz    = $PSScriptRoot
$exeFase = Join-Path $raiz "$Fase\NubeBar.exe"
$exeEst  = Join-Path $raiz "$Estable\NubeBar.exe"
$cfg     = Join-Path $env:APPDATA "NubeBar\config.json"

function Linea($t) { Write-Host $t }
function Huella { if (Test-Path $cfg) { (Get-FileHash $cfg -Algorithm SHA256).Hash } else { "(no existe)" } }

if (-not (Test-Path $exeFase)) { Linea "No encuentro $exeFase. ¿Has publicado?"; exit 2 }
if (-not (Test-Path $exeEst))  { Linea "No encuentro $exeEst (la barra estable)."; exit 2 }

$antes = Huella
Linea "config antes: $($antes.Substring(0,16))"
Linea ""

$fallos = 0

# ---------- 1. Las pruebas de lógica ----------
#
# No necesitan parar nada: `--pruebas` no abre ninguna ventana.
Linea "=== pruebas de lógica ==="
$informe = Join-Path $env:TEMP "nubebar-revision-pruebas.txt"
& $exeFase --pruebas $informe | Out-Null
$veredicto = (Select-String -Path $informe -Pattern "TODO BIEN|MAL de").Line
Linea "  $veredicto"
if ($veredicto -notmatch "TODO BIEN") { $fallos++ }

# ---------- 2. La guarda del binario de fase ----------
#
# Arrancarlo SIN banderas tiene que negarse (código 6) y no tocar el config. Es la
# comprobación que más cara ha salido de no tener.
Linea ""
Linea "=== la guarda: un binario de fase no arranca como barra ==="
$p = Start-Process $exeFase -PassThru -WindowStyle Hidden
$p | Wait-Process -Timeout 30 -ErrorAction SilentlyContinue
$intacto = ($antes -eq (Huella))
Linea "  salida $($p.ExitCode) (se espera 6) · config intacto: $intacto"
if ($p.ExitCode -ne 6 -or -not $intacto) { $fallos++ }

if ($Rapido) {
    Linea ""
    Linea $(if ($fallos -eq 0) { "TODO BIEN (revisión rápida)" } else { "$fallos FALLO(S)" })
    exit $(if ($fallos -eq 0) { 0 } else { 1 })
}

# ---------- 3. Los diagnósticos, con la barra parada ----------
#
# Los cuatro primeros son los de las fases nuevas. Los demás son los de A-D, y están aquí
# por una razón concreta: en una tanda se tocaron `MainWindow.Boton`, la medida de los
# paneles y media docena de ayudantes compartidos, y la única forma de saber que no se
# había roto nada viejo fue correrlos todos a mano. Eso no se recuerda: se escribe.
$cuales = @(
    "--diage0", "--diagmodulos", "--diagpaneles", "--arnesaplicar",
    "--diagvista", "--diaginicio", "--diagbandeja", "--diaghypr", "--diagmotor",
    "--diagespacios", "--diagatajos", "--diagreglas", "--diagprefijos",
    "--diagd6", "--diagd7", "--arnesespacios", "--arneslayout", "--diagrejilla",
    # El único que necesita internet. Va el último a propósito: es el único que puede fallar
    # por algo que no es culpa del código —el wifi caído— y así no arrastra a los demás.
    "--diagclima"
)
if ($Solo) { $cuales = $cuales | Select-Object -First 4 }
if ($ConCapturas) { $cuales += "--capturas" }

Linea ""
Linea "=== diagnósticos (la barra se para y se devuelve al final) ==="
& $exeEst --salir | Out-Null
Start-Sleep -Seconds 3

try {
    foreach ($d in $cuales) {
        $salida = Join-Path $env:TEMP ("nubebar-revision" + ($d -replace '-', '') + ".txt")
        $q = Start-Process $exeFase -ArgumentList $d -PassThru -RedirectStandardOutput $salida -NoNewWindow
        $q | Wait-Process -Timeout 1800 -ErrorAction SilentlyContinue
        $code = $q.ExitCode
        Linea ("  {0,-16} salida {1}   ({2})" -f $d, $code, (Split-Path $salida -Leaf))
        if ($code -ne 0) { $fallos++ }
        Get-Process NubeBar -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
}
finally {
    # ---------- Y la barra, SIEMPRE ----------
    #
    # Pase lo que pase, incluso si algo revienta o interrumpes el script: el escritorio no
    # se queda sin barra.
    Get-Process NubeBar -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-Process $exeEst | Out-Null
    Start-Sleep -Seconds 6
    $vivas = (Get-Process NubeBar -ErrorAction SilentlyContinue).Count
    Linea ""
    Linea "barra devuelta: $vivas proceso(s)"
    if ($vivas -lt 1) { Linea "  ¡OJO! no ha vuelto a arrancar. Lánzala a mano: $exeEst"; $fallos++ }
}

# ---------- 4. Y tu configuración, intacta ----------
$despues = Huella
Linea "config después: $($despues.Substring(0,16))"
if ($antes -ne $despues) {
    Linea "  ¡OJO! TU CONFIG HA CAMBIADO. Ningún diagnóstico debería tocarla."
    Linea "  Respaldos en $(Split-Path $cfg -Parent)"
    $fallos++
} else {
    Linea "  igual que al empezar."
}

Linea ""
Linea $(if ($fallos -eq 0) { "TODO BIEN" } else { "$fallos FALLO(S) — mira los informes en $env:TEMP" })
exit $(if ($fallos -eq 0) { 0 } else { 1 })



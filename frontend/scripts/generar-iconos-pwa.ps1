# =============================================================================
#  generar-iconos-pwa.ps1  -  Iconos del manifest a partir de la marca del repo
# -----------------------------------------------------------------------------
#  POR QUE EXISTE: los iconos de una PWA no se dibujan a mano ni se bajan de
#  ningun lado. Se derivan de `src/assets/brand/italcol-naraanja.png`, que es la
#  misma marca que ya usa el favicon y el apple-touch-icon del index.html. Si la
#  marca cambia, se vuelve a correr este script y los cinco archivos quedan
#  consistentes entre si.
#
#  Genera en src/assets/pwa/:
#    icon-192.png / icon-512.png                  purpose "any"      (fondo claro)
#    icon-maskable-192 / -512.png                 purpose "maskable" (fondo naranja
#                                                 de marca + zona segura del 80%)
#    apple-touch-icon-180.png                     iOS (no soporta maskable)
#
#  Sobre la zona segura: Android recorta el icono maskable con formas distintas
#  segun el launcher (circulo, squircle, gota). La especificacion garantiza solo
#  el 80% central, asi que la marca se dibuja dentro de ese 80% y el resto es
#  fondo solido. Sin eso, en un launcher circular la marca sale mordida.
#
#  Uso (desde frontend/):  powershell -ExecutionPolicy Bypass -File scripts/generar-iconos-pwa.ps1
#  NOTA: ASCII puro a proposito (Windows PowerShell 5.1 malparsea UTF-8 sin BOM).
# =============================================================================
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$raiz    = Split-Path $PSScriptRoot -Parent
$origen  = Join-Path $raiz 'src/assets/brand/italcol-naraanja.png'
$destino = Join-Path $raiz 'src/assets/pwa'

if (-not (Test-Path $origen)) { throw "No existe la marca de origen: $origen" }
if (-not (Test-Path $destino)) { New-Item -ItemType Directory -Path $destino | Out-Null }

# Tokens de marca: los mismos de styles/theme-italfoods.scss
$naranja = [System.Drawing.ColorTranslator]::FromHtml('#F5821F')  # --ital-orange
$crema   = [System.Drawing.ColorTranslator]::FromHtml('#FAF8F5')  # ital-cream

$marca = [System.Drawing.Image]::FromFile($origen)

function New-Icono {
    param(
        [int]$Lado,
        [System.Drawing.Color]$Fondo,
        [double]$Ocupacion,   # fraccion del lado que ocupa la marca
        [string]$Salida
    )

    $bmp = New-Object System.Drawing.Bitmap($Lado, $Lado, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $brocha = New-Object System.Drawing.SolidBrush($Fondo)
        $g.FillRectangle($brocha, 0, 0, $Lado, $Lado)
        $brocha.Dispose()

        # "contain": se respeta la relacion de aspecto, nunca se deforma la marca
        $caja   = $Lado * $Ocupacion
        $escala = [Math]::Min($caja / $marca.Width, $caja / $marca.Height)
        $ancho  = [int][Math]::Round($marca.Width  * $escala)
        $alto   = [int][Math]::Round($marca.Height * $escala)
        $x      = [int][Math]::Round(($Lado - $ancho) / 2)
        $y      = [int][Math]::Round(($Lado - $alto)  / 2)

        $g.DrawImage($marca, $x, $y, $ancho, $alto)
        $bmp.Save((Join-Path $destino $Salida), [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host ("[iconos-pwa] {0}  {1}x{1}" -f $Salida, $Lado) -ForegroundColor Green
    }
    finally {
        $g.Dispose()
        $bmp.Dispose()
    }
}

try {
    # purpose "any": el navegador lo muestra tal cual, sin recortar -> marca al 86%
    New-Icono -Lado 192 -Fondo $crema   -Ocupacion 0.86 -Salida 'icon-192.png'
    New-Icono -Lado 512 -Fondo $crema   -Ocupacion 0.86 -Salida 'icon-512.png'

    # purpose "maskable": solo el 80% central esta garantizado -> marca al 62%
    New-Icono -Lado 192 -Fondo $naranja -Ocupacion 0.62 -Salida 'icon-maskable-192.png'
    New-Icono -Lado 512 -Fondo $naranja -Ocupacion 0.62 -Salida 'icon-maskable-512.png'

    # iOS ignora "maskable" y recorta a squircle con esquinas suaves -> 78%, fondo solido
    New-Icono -Lado 180 -Fondo $crema   -Ocupacion 0.78 -Salida 'apple-touch-icon-180.png'
}
finally {
    $marca.Dispose()
}

Write-Host "[iconos-pwa] Listo -> src/assets/pwa" -ForegroundColor Cyan

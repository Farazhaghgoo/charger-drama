# Installs Charger Drama for the current user (no admin needed), or removes it with -Uninstall.
#   - copies the app to %LOCALAPPDATA%\ChargerDrama
#   - puts a "Charger Drama" ON/OFF shortcut on the Desktop, with hotkey Ctrl+Alt+D
#   - starts it
param([switch]$Uninstall)

$AppId      = 'ChargerDrama'
$dest       = Join-Path $env:LOCALAPPDATA $AppId
$desktopLnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Charger Drama.lnk'
$startupLnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'Charger Drama.lnk'
$conhost    = "$env:SystemRoot\System32\conhost.exe"
$powershell = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
$hotkey     = 'CTRL+ALT+D'

function Get-AppProcesses {
    @(Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" | Where-Object { $_.CommandLine -like '*charger-drama.ps1*' })
}

function Stop-App {
    $asked = $false
    try {
        $s = [System.Threading.EventWaitHandle]::OpenExisting("Local\$AppId.Quit")
        [void]$s.Set(); $s.Dispose(); $asked = $true
    } catch { }
    if ($asked) {
        $deadline = (Get-Date).AddSeconds(8)
        while ((Get-AppProcesses).Count -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
    }
    Get-AppProcesses | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

function New-Shortcut($path, $script, $extraArgs, $hotkey) {
    $wsh = New-Object -ComObject WScript.Shell
    $lnk = $wsh.CreateShortcut($path)
    $lnk.TargetPath       = $conhost
    $lnk.Arguments        = "--headless `"$powershell`" -NoProfile -ExecutionPolicy Bypass -File `"$script`" $extraArgs".Trim()
    $lnk.WorkingDirectory = $dest
    $lnk.IconLocation     = "$dest\ChargerDrama.ico,0"
    $lnk.Description      = 'Charger Drama - turn on/off'
    if ($hotkey) { $lnk.Hotkey = $hotkey }
    $lnk.Save()
}

function New-BoltIcon($path) {
    # a .ico file with PNG images of a yellow lightning bolt
    Add-Type -AssemblyName System.Drawing
    $sizes = 16, 32, 48, 256
    $pngs = foreach ($n in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $n, $n
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = 'AntiAlias'
        $k = $n / 32.0
        $pts = foreach ($p in (19,1),(5,18),(14,18),(10,31),(27,12),(18,12),(23,1)) {
            New-Object System.Drawing.PointF ([float]($p[0] * $k)), ([float]($p[1] * $k))
        }
        $g.FillPolygon([System.Drawing.Brushes]::Gold, [System.Drawing.PointF[]]$pts)
        $g.DrawPolygon((New-Object System.Drawing.Pen ([System.Drawing.Color]::DarkGoldenrod), ([float][Math]::Max(1, 1.5 * $k))), [System.Drawing.PointF[]]$pts)
        $g.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        , $ms.ToArray()
    }
    $out = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $out
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $n = $sizes[$i]; $b = if ($n -ge 256) { 0 } else { $n }
        $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32)
        $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($png in $pngs) { $w.Write([byte[]]$png) }
    $w.Flush()
    [System.IO.File]::WriteAllBytes($path, $out.ToArray())
}

Stop-App

if ($Uninstall) {
    Remove-Item $desktopLnk, $startupLnk -Force -ErrorAction SilentlyContinue
    Remove-Item $dest -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $env:APPDATA $AppId) -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host 'Charger Drama has been removed.'
    return
}

New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'charger-drama.ps1'), (Join-Path $PSScriptRoot 'toggle.ps1') $dest -Force
# copy voice packs (keeps any extra voices you added to the installed copy)
Get-ChildItem (Join-Path $PSScriptRoot 'sounds') -Recurse -File | ForEach-Object {
    $target = Join-Path $dest ($_.FullName.Substring($PSScriptRoot.Length).TrimStart('\'))
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    Copy-Item $_.FullName $target -Force
}
New-BoltIcon (Join-Path $dest 'ChargerDrama.ico')

New-Shortcut $desktopLnk (Join-Path $dest 'toggle.ps1') '' $hotkey
if (Test-Path $startupLnk) {
    # "Start with Windows" was on: point it at the new copy
    New-Shortcut $startupLnk (Join-Path $dest 'charger-drama.ps1') '' $null
}

& $powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dest 'toggle.ps1') -On

Write-Host ''
Write-Host 'Charger Drama is installed and ON.'
Write-Host '  - Desktop shortcut "Charger Drama" turns it on/off (or press Ctrl+Alt+D).'
Write-Host '  - Right-click the yellow bolt in the tray to change voice or start with Windows.'

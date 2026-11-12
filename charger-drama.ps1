# Charger Drama
# Plug the charger in  -> maniacal laughter.
# Pull the charger out -> "Nooooo! No no no no! Please... no!"
#
# Lives in the system tray (yellow lightning bolt). Right-click it to pick a voice, test, or turn it off.
# Voice packs are the folders in .\sounds. Add your own: a folder with laugh*.mp3 and nooo*.mp3
# (also .wav / .m4a / .wma), and optionally hello*.mp3 and bye*.mp3.

param(
    [switch]$Hello,     # say hello when starting (the on/off shortcut uses this)
    [switch]$SelfTest   # silent check of voices and audio files, no tray icon
)

Add-Type -AssemblyName System.Windows.Forms, System.Drawing, System.Speech, PresentationCore, WindowsBase

$AppId        = 'ChargerDrama'
$here         = $PSScriptRoot
$soundsDir    = Join-Path $here 'sounds'
$dataDir      = Join-Path $env:APPDATA $AppId
$settingsFile = Join-Path $dataDir 'settings.json'
$startupLnk   = Join-Path ([Environment]::GetFolderPath('Startup')) 'Charger Drama.lnk'
$robotName    = 'Classic Robot (Windows voice)'
$audioExt     = '.mp3', '.wav', '.m4a', '.wma', '.aac'

# --- one copy at a time; the on/off shortcut sets the quit signal ---------------
$mutex = New-Object System.Threading.Mutex($false, "Local\$AppId")
if (-not $SelfTest) {
    if (-not $mutex.WaitOne(0)) { exit }
    $quitSignal = New-Object System.Threading.EventWaitHandle($false, 'AutoReset', "Local\$AppId.Quit")
}

# --- settings ---------------------------------------------------------------------
function Read-Settings {
    try { Get-Content $settingsFile -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop }
    catch { [pscustomobject]@{ voice = 'Mad Scientist' } }
}
function Save-Settings {
    New-Item -ItemType Directory -Force $dataDir | Out-Null
    $script:settings | ConvertTo-Json | Set-Content $settingsFile -Encoding UTF8
}

# --- voice packs (folders of audio files) -------------------------------------------
function Get-VoicePacks {
    $packs = [ordered]@{}
    if (Test-Path $soundsDir) {
        foreach ($dir in Get-ChildItem $soundsDir -Directory | Sort-Object Name) {
            $files = @(Get-ChildItem $dir.FullName -File | Where-Object { $audioExt -contains $_.Extension.ToLower() })
            $pack = @{}
            foreach ($kind in 'laugh', 'nooo', 'hello', 'bye') {
                $pack[$kind] = @($files | Where-Object { $_.BaseName -like "$kind*" } | ForEach-Object FullName)
            }
            if ($pack.laugh.Count -or $pack.nooo.Count) { $packs[$dir.Name] = $pack }
        }
    }
    $packs
}

# --- built-in Windows voice, used by "Classic Robot" and as a fallback -------------
$voice = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voice.Volume = 100
$english = $voice.GetInstalledVoices() | Where-Object { $_.Enabled -and $_.VoiceInfo.Culture.Name -like 'en-*' } | Select-Object -First 1
if ($english) { $voice.SelectVoice($english.VoiceInfo.Name) }
$lang = $voice.Voice.Culture.Name

function New-Ssml([string]$body) {
    "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='$lang'>$body</speak>"
}

$robot = @{
    laugh = @(
        (New-Ssml "<prosody rate='fast' pitch='+10%'>Mwa ha ha ha ha!</prosody><prosody rate='x-fast' pitch='+35%'>Ha ha ha ha ha ha ha!</prosody><prosody rate='x-fast' pitch='+60%'>HA HA HA HA HA HA!</prosody><break time='150ms'/><prosody rate='x-fast' pitch='+90%'>hee hee hee hee hee!</prosody><break time='250ms'/><prosody rate='slow' pitch='-15%'>Yesss. Sweet, sweet electricity.</prosody>"),
        (New-Ssml "<prosody rate='x-fast' pitch='+50%'>Ha ha ha ha ha ha ha ha ha!</prosody><prosody rate='fast' pitch='-10%'>Mwa ha ha ha!</prosody><prosody rate='x-fast' pitch='+80%'>Ah ha ha ha ha ha ha!</prosody><break time='200ms'/><prosody rate='medium' pitch='+20%'>I am charging! I am unstoppable!</prosody>")
    )
    nooo = @(
        (New-Ssml "<prosody rate='x-slow' pitch='-10%' volume='x-loud'>Noooooo!</prosody><break time='150ms'/><prosody rate='fast' pitch='+25%'>No no no no no!</prosody><break time='200ms'/><prosody rate='slow' pitch='+45%'>Please... no!</prosody>"),
        (New-Ssml "<prosody rate='fast' pitch='+30%'>Wait wait wait!</prosody><prosody rate='x-slow' pitch='-15%' volume='x-loud'>Noooooo!</prosody><prosody rate='fast' pitch='+35%'>No no no no!</prosody><prosody rate='slow' pitch='+55%'>Please... no... it's so cold.</prosody>")
    )
    hello = @((New-Ssml "<prosody pitch='+10%'>Charger Drama is awake.</prosody>"))
    bye   = @((New-Ssml "<prosody rate='slow' pitch='-10%'>Fine. I will be quiet.</prosody>"))
}

# --- self test (silent) ------------------------------------------------------------
if ($SelfTest) {
    $packs = Get-VoicePacks
    $failed = 0
    foreach ($name in $packs.Keys) {
        $pack = $packs[$name]
        $secs = @()
        foreach ($file in @($pack.laugh + $pack.nooo + $pack.hello + $pack.bye)) {
            $p = New-Object System.Windows.Media.MediaPlayer
            $p.Volume = 0
            $p.Open([uri]$file)
            $sw = [Diagnostics.Stopwatch]::StartNew()
            while (-not $p.NaturalDuration.HasTimeSpan -and $sw.ElapsedMilliseconds -lt 5000) {
                [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 20
            }
            if ($p.NaturalDuration.HasTimeSpan) { $secs += $p.NaturalDuration.TimeSpan.TotalSeconds } else { $failed++; "  CAN'T OPEN: $file" }
            $p.Close()
        }
        "{0,-16} laugh x{1}  nooo x{2}  hello x{3}  bye x{4}   clips {5:N1}-{6:N1}s" -f $name, $pack.laugh.Count, $pack.nooo.Count, $pack.hello.Count, $pack.bye.Count, ($secs | Measure-Object -Minimum).Minimum, ($secs | Measure-Object -Maximum).Maximum
    }
    # prove playback actually runs (muted)
    $p = New-Object System.Windows.Media.MediaPlayer
    $p.Volume = 0
    $p.Open([uri]$packs[0].laugh[0]); $p.Play()
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt 1500) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 20 }
    "muted playback advanced to {0:N2}s" -f $p.Position.TotalSeconds
    $p.Close()
    # robot voice markup renders
    $tmp = Join-Path ([IO.Path]::GetTempPath()) 'charger-drama-selftest.wav'
    foreach ($ssml in $robot.Values | ForEach-Object { $_ }) { $voice.SetOutputToWaveFile($tmp); $voice.SpeakSsml($ssml); $voice.SetOutputToNull() }
    Remove-Item $tmp -ErrorAction SilentlyContinue
    "robot voice OK ($($voice.Voice.Name))"
    "files that failed: $failed"
    "charger right now: " + [System.Windows.Forms.SystemInformation]::PowerStatus.PowerLineStatus
    exit
}

$script:settings = Read-Settings
$script:packs = Get-VoicePacks
if ($script:settings.voice -ne $robotName -and -not $script:packs.Contains($script:settings.voice)) {
    $script:settings.voice = if ($script:packs.Count) { @($script:packs.Keys)[0] } else { $robotName }
}

# --- playback --------------------------------------------------------------------
$player = New-Object System.Windows.Media.MediaPlayer
$player.Volume = 1
$script:playing = $false
$player.add_MediaEnded({ $script:playing = $false })
$player.add_MediaFailed({ $script:playing = $false })

function Stop-Sound {
    $voice.SpeakAsyncCancelAll()
    $player.Stop()
    $script:playing = $false
}

function Invoke-Sound([string]$kind) {
    Stop-Sound
    $pack  = $script:packs[$script:settings.voice]
    $files = if ($pack) { @($pack[$kind]) } else { @() }
    if ($files.Count) {
        $player.Open([uri]($files | Get-Random))
        $player.Play()
        $script:playing = $true
    } else {
        $voice.SpeakSsmlAsync(($robot[$kind] | Get-Random)) | Out-Null
    }
}

function Test-SoundBusy { $script:playing -or $voice.State -eq 'Speaking' }

# --- tray icon (little yellow lightning bolt) --------------------------------------
$bmp = New-Object System.Drawing.Bitmap 32, 32
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$bolt = [System.Drawing.Point[]]@(
    (New-Object System.Drawing.Point 19, 1),
    (New-Object System.Drawing.Point 5, 18),
    (New-Object System.Drawing.Point 14, 18),
    (New-Object System.Drawing.Point 10, 31),
    (New-Object System.Drawing.Point 27, 12),
    (New-Object System.Drawing.Point 18, 12),
    (New-Object System.Drawing.Point 23, 1)
)
$g.FillPolygon([System.Drawing.Brushes]::Gold, $bolt)
$g.DrawPolygon((New-Object System.Drawing.Pen ([System.Drawing.Color]::DarkGoldenrod), 1.5), $bolt)
$g.Dispose()

$tray = New-Object System.Windows.Forms.NotifyIcon
$tray.Icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$tray.Visible = $true

$menu = New-Object System.Windows.Forms.ContextMenuStrip
$statusItem = New-Object System.Windows.Forms.ToolStripMenuItem 'Charger Drama'
$statusItem.Enabled = $false
$menu.Items.Add($statusItem) | Out-Null
$menu.Items.Add('-') | Out-Null
$menu.Items.Add('Test: laugh', $null, { Invoke-Sound 'laugh' }) | Out-Null
$menu.Items.Add('Test: nooo', $null, { Invoke-Sound 'nooo' }) | Out-Null

$voiceMenu = New-Object System.Windows.Forms.ToolStripMenuItem 'Voice'
$menu.Items.Add($voiceMenu) | Out-Null

function Select-Voice([string]$name) {
    $script:settings.voice = $name
    Save-Settings
    foreach ($i in $voiceMenu.DropDownItems) {
        if ($i -is [System.Windows.Forms.ToolStripMenuItem]) { $i.Checked = ($i.Text -eq $name) }
    }
    Update-Status
    Invoke-Sound 'laugh'   # preview the new voice
}

function Build-VoiceMenu {
    $script:packs = Get-VoicePacks
    $voiceMenu.DropDownItems.Clear()
    foreach ($name in @($script:packs.Keys) + $robotName) {
        $item = New-Object System.Windows.Forms.ToolStripMenuItem $name
        $item.Checked = ($name -eq $script:settings.voice)
        $item.add_Click({ param($s, $e) Select-Voice $s.Text })
        $voiceMenu.DropDownItems.Add($item) | Out-Null
    }
    $voiceMenu.DropDownItems.Add('-') | Out-Null
    $voiceMenu.DropDownItems.Add('Open sounds folder (add your own voice)', $null, {
        New-Item -ItemType Directory -Force $soundsDir | Out-Null
        Start-Process explorer.exe "`"$soundsDir`""
    }) | Out-Null
    $voiceMenu.DropDownItems.Add('Reload voices', $null, { Build-VoiceMenu }) | Out-Null
}
Build-VoiceMenu

function New-StartupShortcut {
    $wsh = New-Object -ComObject WScript.Shell
    $lnk = $wsh.CreateShortcut($startupLnk)
    $lnk.TargetPath       = "$env:SystemRoot\System32\conhost.exe"
    $lnk.Arguments        = "--headless `"$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe`" -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    $lnk.WorkingDirectory = $here
    $lnk.Description      = 'Charger Drama'
    $lnk.Save()
}

$autoItem = New-Object System.Windows.Forms.ToolStripMenuItem 'Start with Windows'
$autoItem.Checked = Test-Path $startupLnk
$autoItem.add_Click({
    if (Test-Path $startupLnk) { Remove-Item $startupLnk -Force } else { New-StartupShortcut }
    $autoItem.Checked = Test-Path $startupLnk
})
$menu.Items.Add($autoItem) | Out-Null
$menu.Items.Add('-') | Out-Null
$menu.Items.Add('Turn off  (Ctrl+Alt+D)', $null, { Request-Quit }) | Out-Null
$tray.ContextMenuStrip = $menu

function Update-Status {
    $plugged = $script:last -eq 'Online'
    $statusItem.Text = if ($plugged) { 'Plugged in - very happy' } else { 'On battery - very sad' }
    $text = "Charger Drama - $($script:settings.voice) - " + $(if ($plugged) { 'plugged in' } else { 'on battery' })
    $tray.Text = $text.Substring(0, [Math]::Min(63, $text.Length))
}

# --- turning off -------------------------------------------------------------------
$script:quitting = $false
function Request-Quit {
    if ($script:quitting) { return }
    $script:quitting  = $true
    $script:quitStart = Get-Date
    Invoke-Sound 'bye'
}
function Close-App {
    $timer.Stop()
    Stop-Sound
    $tray.Visible = $false
    $tray.Dispose()
    [System.Windows.Forms.Application]::Exit()
}

# --- watch the charger -------------------------------------------------------------
$script:last = [System.Windows.Forms.SystemInformation]::PowerStatus.PowerLineStatus
$script:sayHello = [bool]$Hello
Update-Status

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 250
$timer.add_Tick({
    try {
        if ($quitSignal.WaitOne(0)) { Request-Quit }
        if ($script:quitting) {
            $elapsed = ((Get-Date) - $script:quitStart).TotalSeconds
            if (($elapsed -gt 0.8 -and -not (Test-SoundBusy)) -or $elapsed -gt 8) { Close-App }
            return
        }
        if ($script:sayHello) { $script:sayHello = $false; Invoke-Sound 'hello' }

        $now = [System.Windows.Forms.SystemInformation]::PowerStatus.PowerLineStatus
        if ($now -eq 'Unknown' -or $now -eq $script:last) { return }
        $script:last = $now
        Update-Status
        if ($now -eq 'Online') { Invoke-Sound 'laugh' } else { Invoke-Sound 'nooo' }
    } catch { }
})
$timer.Start()

if ($Hello) {
    $tray.ShowBalloonTip(3000, 'Charger Drama is ON', 'Plug or unplug your charger. Ctrl+Alt+D turns it off. Right-click the bolt to change voice.', 'Info')
}

[System.Windows.Forms.Application]::Run()

$mutex.ReleaseMutex()

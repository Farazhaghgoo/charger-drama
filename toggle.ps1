# Turns Charger Drama ON if it's off, and OFF if it's on.
# The desktop shortcut (and Ctrl+Alt+D) runs this.
param([switch]$On, [switch]$Off)

$AppId = 'ChargerDrama'
$app   = Join-Path $PSScriptRoot 'charger-drama.ps1'

function Test-Running {
    try { [System.Threading.Mutex]::OpenExisting("Local\$AppId").Dispose(); $true } catch { $false }
}

if (Test-Running) {
    if ($On) { return }
    try {
        # ask it to say goodbye and close
        $signal = [System.Threading.EventWaitHandle]::OpenExisting("Local\$AppId.Quit")
        [void]$signal.Set()
        $signal.Dispose()
    } catch {
        # an older copy without the goodbye signal: just stop it
        Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
            Where-Object { $_.CommandLine -like '*charger-drama.ps1*' } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
    }
}
elseif (-not $Off) {
    # conhost --headless = no console window flashing up
    Start-Process "$env:SystemRoot\System32\conhost.exe" -ArgumentList "--headless `"$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe`" -NoProfile -ExecutionPolicy Bypass -File `"$app`" -Hello"
}

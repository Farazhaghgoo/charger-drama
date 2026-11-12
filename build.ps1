# Builds dist\ChargerDrama.exe with the C# compiler that ships with Windows (.NET Framework 4.8).
# Nothing to install. The voice packs in .\sounds are embedded in the exe.
#
#   .\build.ps1            build
#   .\build.ps1 -Test      build, then run the silent self-test (results in dist\selftest\selftest.txt)
param([switch]$Test)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$fw   = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path "$fw\csc.exe")) { $fw = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319" }
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$exe  = Join-Path $dist 'ChargerDrama.exe'

$rsp = @(
    '/nologo', '/target:winexe', '/optimize+', '/platform:anycpu',
    "/out:`"$exe`"",
    "/win32manifest:`"$root\assets\app.manifest`"",
    "/r:`"$fw\WPF\PresentationCore.dll`"",
    "/r:`"$fw\WPF\WindowsBase.dll`"",
    "/r:`"$fw\WPF\System.Speech.dll`"",
    "/r:`"$fw\System.Xaml.dll`"",
    "/r:`"$fw\System.Web.Extensions.dll`"",
    '/r:System.Management.dll', '/r:Microsoft.CSharp.dll'
)
if (Test-Path "$root\assets\ChargerDrama.ico") { $rsp += "/win32icon:`"$root\assets\ChargerDrama.ico`"" }
Get-ChildItem "$root\sounds" -Recurse -File | Where-Object { $_.Extension -in '.mp3', '.wav', '.json' } | ForEach-Object {
    $name = 'sounds/' + $_.FullName.Substring((Join-Path $root 'sounds').Length + 1).Replace('\', '/')
    $rsp += "/resource:`"$($_.FullName)`",`"$name`""
}
Get-ChildItem "$root\src" -Filter *.cs | ForEach-Object { $rsp += "`"$($_.FullName)`"" }
$rspFile = Join-Path $dist 'build.rsp'
Set-Content $rspFile $rsp -Encoding UTF8

$output = & "$fw\csc.exe" "@$rspFile" 2>&1 | Where-Object { $_ -notmatch 'only supports language versions up to C# 5|aka\.ms/|This compiler is provided' -and "$_".Trim() }
$code = $LASTEXITCODE
$output | ForEach-Object { "$_" }
if ($code -ne 0) { throw "Build failed." }
"Built $exe  ({0:N1} MB)" -f ((Get-Item $exe).Length / 1MB)

if ($Test) {
    $out = Join-Path $dist 'selftest'
    $p = Start-Process $exe -ArgumentList '--selftest', "`"$out`"" -Wait -PassThru
    Get-Content (Join-Path $out 'selftest.txt')
    if ($p.ExitCode -ne 0) { throw "Self-test failed." }
}
